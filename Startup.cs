using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
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
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Prometheus;
using StackExchange.Redis;
using Coflnet.Sky.Commands;

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
            services.AddJaeger(0.001,60);
            services.AddScoped<PricesService>();
            services.AddSingleton<AuctionService>();
            services.AddDbContext<HypixelContext>();
            services.AddSingleton<ProductsApi>(sp =>
            {
                return new ProductsApi("http://" + SimplerConfig.Config.Instance["PAYMENTS_HOST"]);
            });
            services.AddSingleton<UserApi>(sp =>
            {
                return new UserApi("http://" + SimplerConfig.Config.Instance["PAYMENTS_HOST"]);
            });
            services.AddSingleton<TopUpApi>(sp =>
            {
                return new TopUpApi("http://" + SimplerConfig.Config.Instance["PAYMENTS_HOST"]);
            });
            services.AddSingleton<PremiumService>();


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
                        using var span = OpenTracing.Util.GlobalTracer.Instance.BuildSpan("error").StartActive();
                        span.Span.Log(exceptionHandlerPathFeature?.Error?.Message);
                        span.Span.Log(exceptionHandlerPathFeature?.Error?.StackTrace);
                        var traceId = System.Net.Dns.GetHostName().Replace("commands", "").Trim('-') + "." + span.Span.Context.TraceId;
                        await context.Response.WriteAsync(
                            JsonConvert.SerializeObject(new
                            {
                                Slug = "internal_error",
                                Message = "An unexpected internal error occured. Please check that your request is valid. If it is please report he error and include the Trace.",
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
