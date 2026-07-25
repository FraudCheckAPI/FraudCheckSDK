#if NET8_0_OR_GREATER
using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FraudCheck.Client;

/// <summary>
/// DI wiring for <see cref="FraudCheckClient"/>. net8.0 only — netstandard2.0 consumers construct the client
/// directly rather than drag Microsoft.Extensions.* into a .NET Framework app that may not want it.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="FraudCheckClient"/> with a pooled <see cref="HttpClient"/> from
    /// <c>IHttpClientFactory</c> — the handler rotation matters for a client that lives as long as your app.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddFraudCheck(o => o.ApiKey = builder.Configuration["FraudCheck:ApiKey"]!);
    /// </code>
    /// Then inject <c>FraudCheckClient</c> anywhere. Add resilience with the usual builder, e.g.
    /// <c>.AddStandardResilienceHandler()</c>.
    /// </example>
    public static IHttpClientBuilder AddFraudCheck(this IServiceCollection services, Action<FraudCheckOptions> configure)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var options = new FraudCheckOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            // Fail at startup with a clear reason, rather than on the first screening call in production.
            throw new InvalidOperationException(
                "FraudCheck: no API key configured. Set FraudCheckOptions.ApiKey — create a key in your dashboard.");
        }

        var builder = services.AddHttpClient<FraudCheckClient>(http =>
            {
                http.BaseAddress = options.BaseAddress;
                http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                if (options.Timeout > TimeSpan.Zero)
                    http.Timeout = options.Timeout;
            })
            // The typed-client factory hands us the configured HttpClient; the ctor won't re-set what's set.
            .AddTypedClient(http => new FraudCheckClient(http, options));

        if (options.SignRequests)
        {
            // As a handler, signing sits inside the retry chain — every attempt re-signs with a fresh
            // timestamp, which a stale signature on a retry would otherwise fail.
            builder.AddHttpMessageHandler(() => new RequestSigningHandler(options.ApiKey));
        }

        return builder;
    }

    /// <summary>Register with just a key, using the defaults for everything else.</summary>
    public static IHttpClientBuilder AddFraudCheck(this IServiceCollection services, string apiKey)
        => services.AddFraudCheck(o => o.ApiKey = apiKey);
}
#endif
