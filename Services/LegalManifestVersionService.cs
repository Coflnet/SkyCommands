using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Coflnet.Sky.Commands.Services;

public sealed class LegalManifestVersionService : IHostedService
{
    private readonly IHttpClientFactory clients;
    private readonly IConfiguration configuration;
    private readonly IHostEnvironment environment;

    public string TermsVersion { get; private set; }

    public LegalManifestVersionService(
        IHttpClientFactory clients,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        this.clients = clients;
        this.configuration = configuration;
        this.environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var manifestUri = new Uri(
            configuration["LEGAL_MANIFEST_URL"]
            ?? "https://coflnet.com/legal/manifest.json");
        if (!environment.IsDevelopment()
            && (manifestUri.Scheme != Uri.UriSchemeHttps
                || !manifestUri.Host.Equals(
                    "coflnet.com",
                    StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "LEGAL_MANIFEST_URL must use the Coflnet HTTPS origin.");

        var client = clients.CreateClient(
            nameof(LegalManifestVersionService));
        var manifest = JsonSerializer.Deserialize<Manifest>(
            await client.GetByteArrayAsync(
                manifestUri,
                cancellationToken),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw new InvalidOperationException(
                "The legal manifest is empty.");
        if (manifest.SchemaVersion != 1
            || !Uri.TryCreate(
                manifest.Source,
                UriKind.Absolute,
                out var source)
            || (!environment.IsDevelopment()
                && source != new Uri("https://coflnet.com/"))
            || !manifest.Documents.TryGetValue(
                "terms",
                out var terms)
            || string.IsNullOrWhiteSpace(terms.AcceptanceHash)
            || terms.Locales.Count != 2
            || !terms.Locales.ContainsKey("en")
            || !terms.Locales.ContainsKey("de"))
            throw new InvalidOperationException(
                "The legal manifest or Terms entry is invalid.");

        foreach (var document in manifest.Documents.Values)
            foreach (var locale in document.Locales.Values)
            {
                if (!Uri.TryCreate(
                        locale.Url,
                        UriKind.Absolute,
                        out var uri)
                    || uri.Scheme != source.Scheme
                    || uri.Host != source.Host
                    || locale.Sha256?.Length != 64)
                    throw new InvalidOperationException(
                        "A legal document location is invalid.");
                var content = await client.GetByteArrayAsync(
                    uri,
                    cancellationToken);
                if (!Sha256(content).Equals(
                    locale.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Legal document hash mismatch for {uri}.");
            }

        var canonical = Encoding.UTF8.GetBytes(
            $"version={terms.Version}\nen={terms.Locales["en"].Sha256}\nde={terms.Locales["de"].Sha256}\n");
        if (!Sha256(canonical).Equals(
            terms.AcceptanceHash,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The Terms acceptance hash is invalid.");
        TermsVersion = terms.Version;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value))
            .ToLowerInvariant();

    private sealed class Manifest
    {
        public int SchemaVersion { get; set; }
        public string Source { get; set; }
        public Dictionary<string, Document> Documents { get; set; } = [];
    }

    private sealed class Document
    {
        public string Version { get; set; }
        public string AcceptanceHash { get; set; }
        public Dictionary<string, Locale> Locales { get; set; } = [];
    }

    private sealed class Locale
    {
        public string Url { get; set; }
        public string Sha256 { get; set; }
    }
}
