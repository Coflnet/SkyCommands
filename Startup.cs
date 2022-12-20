using System;
using System.IO;
using System.Net;
using System.Reflection;
using AspNetCoreRateLimit;
using AspNetCoreRateLimit.Redis;
using Coflnet.Payments.Client.Api;
using Coflnet.Sky.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Coflnet.Sky.Commands.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Prometheus;
using StackExchange.Redis;
using System.Diagnostics;

namespace SkyCommands
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var redisCon = Configuration["REDIS_HOST"];
            services.AddControllers().AddNewtonsoftJson();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SkyCommands", Version = "v1" });
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
            services.AddJaeger(Configuration, 0.0005, 60);
            services.AddScoped<PricesService>();
            services.AddSingleton<AuctionService>();
            services.AddDbContext<HypixelContext>();
            var paymentsUrl = Configuration["PAYMENTS_BASE_URL"] ?? "http://" + Configuration["PAYMENTS_HOST"];
            services.AddSingleton<ProductsApi>(sp =>
            {
                return new ProductsApi(paymentsUrl);
            });
            services.AddSingleton<UserApi>(sp =>
            {
                return new UserApi(paymentsUrl);
            });
            services.AddSingleton<TopUpApi>(sp =>
            {
                return new TopUpApi(paymentsUrl);
            });


            services.AddSwaggerGenNewtonsoftSupport();
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisCon;
                options.InstanceName = "SampleInstance";
            });
            services.AddResponseCaching();
            var redisOptions = ConfigurationOptions.Parse(Configuration["RATE_LIMITER_REDIS_HOST"]);
            services.AddSingleton<IConnectionMultiplexer>(provider => ConnectionMultiplexer.Connect(redisOptions));

            // Rate limiting 
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
            services.AddRedisRateLimiting();
            services.AddSingleton<IIpPolicyStore, DistributedCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, DistributedCacheRateLimitCounterStore>();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            services.AddCoflService();
            services.AddHostedService<FlipperService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkyCommands v1");
                c.RoutePrefix = "api";
            });

            app.UseRouting();

            app.UseAuthorization();

            app.UseResponseCaching();
            app.UseIpRateLimiting();

            app.Use(async (context, next) =>
            {
                context.Response.GetTypedHeaders().CacheControl =
                    new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromSeconds(10)
                    };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
                    new string[] { "Accept-Encoding" };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.AccessControlAllowOrigin] =
                    new string[] { "*" };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.AccessControlAllowHeaders] =
                    new string[] { "*" };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.AccessControlAllowMethods] =
                    new string[] { "*" };
                context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Allow] =
                    new string[] { "OPTIONS, GET, POST" };

                await next();
            });

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; ;
                    context.Response.ContentType = "text/json";

                    var exceptionHandlerPathFeature =
                        context.Features.Get<IExceptionHandlerPathFeature>();

                    if (exceptionHandlerPathFeature?.Error is CoflnetException ex)
                    {
                        await context.Response.WriteAsync(
                                        JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));
                    }
                    else
                    {
                        var spanProvider = DiHandler.GetService<ActivitySource>();
                        using var span = spanProvider.StartActivity("error");
                        span.AddTag("message", exceptionHandlerPathFeature?.Error?.Message);
                        span.AddTag("stackTrace", exceptionHandlerPathFeature?.Error?.StackTrace);
                        var traceId = System.Net.Dns.GetHostName().Replace("commands", "").Trim('-') + "." + span.TraceId;
                        await context.Response.WriteAsync(
                            JsonConvert.SerializeObject(new
                            {
                                Slug = "internal_error",
                                Message = "An unexpected internal error occured. Please check that your request is valid. If it is please report he error and include the Trace " + span.TraceId,
                                Trace = traceId
                            }));
                    }
                });
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapMetrics();
                endpoints.MapControllers();
            });
        }
    }
}
