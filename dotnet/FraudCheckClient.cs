using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FraudCheck.Client;

/// <summary>Settings for a <see cref="FraudCheckClient"/>.</summary>
public sealed class FraudCheckOptions
{
    /// <summary>Your API key. Required. Server-side only — never ship one in browser or mobile code.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>The API root. Only change this to point at a private deployment.</summary>
    public Uri BaseAddress { get; set; } = new Uri("https://fraudcheckapi.com");

    /// <summary>
    /// How long to wait for a response. Screening is meant to be fast, and it usually sits in a checkout or
    /// signup path — better to fail open quickly than to hold a customer's request open.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Sign every request (HMAC-SHA256). Turn this on only for keys created with signing required — the server
    /// rejects an unsigned request for such a key, and ignores signatures on keys that don't require them.
    /// On the DI path use <c>AddFraudCheck</c>, which wires the handler for you.
    /// </summary>
    public bool SignRequests { get; set; }
}

/// <summary>
/// Typed client for the FraudCheck API.
///
/// <para>
/// Remember what this returns: <b>checks, not verdicts</b>. There is no <c>is_fraud</c> field. The score is
/// advisory and you own the decision — which also means you own the false positives, so prefer reacting to
/// specific <see cref="ScreenResult.Reasons"/> over a blanket score threshold.
/// </para>
///
/// <para>
/// Thread-safe and intended to be long-lived. In a DI app, use <c>AddFraudCheck()</c> so the underlying
/// <see cref="HttpClient"/> is pooled properly; constructing one per request will exhaust sockets.
/// </para>
/// </summary>
public sealed class FraudCheckClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Create a client that manages its own <see cref="HttpClient"/>. Keep it for the app's lifetime.</summary>
    public FraudCheckClient(string apiKey)
        : this(new FraudCheckOptions { ApiKey = apiKey }) { }

    /// <summary>Create a client from options, managing its own <see cref="HttpClient"/>.</summary>
    public FraudCheckClient(FraudCheckOptions options)
        : this(CreateHttpClient(options), options, ownsHttpClient: true) { }

    /// <summary>
    /// Create a client over an <see cref="HttpClient"/> you own — the DI path, where the factory handles
    /// pooling, and the place to plug in your own retry/resilience handlers.
    /// </summary>
    public FraudCheckClient(HttpClient httpClient, FraudCheckOptions options)
        : this(httpClient, options, ownsHttpClient: false) { }

    private FraudCheckClient(HttpClient httpClient, FraudCheckOptions options, bool ownsHttpClient)
    {
        if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ArgumentException("An API key is required. Create one in your FraudCheck dashboard.", nameof(options));

        _http = httpClient;
        _ownsHttpClient = ownsHttpClient;
        Configure(_http, options);
    }

    private static HttpClient CreateHttpClient(FraudCheckOptions options) =>
        options.SignRequests
            ? new HttpClient(new RequestSigningHandler(options.ApiKey) { InnerHandler = new HttpClientHandler() })
            : new HttpClient();

    private static void Configure(HttpClient http, FraudCheckOptions options)
    {
        // Only set what isn't already set: on the DI path the caller may have configured these deliberately.
        if (http.BaseAddress == null)
            http.BaseAddress = options.BaseAddress;
        if (!http.DefaultRequestHeaders.Contains("X-Api-Key"))
            http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        if (options.Timeout > TimeSpan.Zero)
            http.Timeout = options.Timeout;
    }

    /// <summary>
    /// Screen any combination of inputs in one call. Supply at least one of the request's fields.
    /// </summary>
    /// <exception cref="ArgumentException">Every field on <paramref name="request"/> is empty.</exception>
    /// <exception cref="FraudCheckException">The API rejected the request or failed.</exception>
    public Task<ScreenResult> ScreenAsync(ScreenRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Ip) && string.IsNullOrWhiteSpace(request.Email) &&
            string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.ShippingCountry) &&
            string.IsNullOrWhiteSpace(request.Name))
        {
            // Fail here rather than spend a request to be told `missing_input`.
            throw new ArgumentException(
                "Supply at least one of Ip, Email, Phone, ShippingCountry or Name.", nameof(request));
        }

        return PostAsync<ScreenResult>("/v1/screen", request, cancellationToken);
    }

    /// <summary>Screen a single set of values without building a <see cref="ScreenRequest"/> yourself.</summary>
    public Task<ScreenResult> ScreenAsync(
        string? ip = null,
        string? email = null,
        string? phone = null,
        string? shippingCountry = null,
        string? name = null,
        bool rdns = false,
        CancellationToken cancellationToken = default)
        => ScreenAsync(new ScreenRequest
        {
            Ip = ip,
            Email = email,
            Phone = phone,
            ShippingCountry = shippingCountry,
            Name = name,
            Rdns = rdns,
        }, cancellationToken);

    /// <summary>
    /// Screen up to 100 records in one call (plan-gated — throws <see cref="FraudCheckException"/> with code
    /// <c>batch_not_available</c> when the plan doesn't include batch). Results align with the submitted
    /// items by index; a bad item fails alone inside the response, never the whole batch.
    /// </summary>
    public Task<BatchScreenResult> ScreenBatchAsync(IReadOnlyList<ScreenRequest> items, CancellationToken cancellationToken = default)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("Supply 1-100 items.", nameof(items));
        if (items.Count > 100)
            throw new ArgumentException("The batch limit is 100 items.", nameof(items));
        return PostAsync<BatchScreenResult>("/v1/screen/batch", new BatchScreenBody { Items = items }, cancellationToken);
    }

    /// <summary>IP checks on their own. <paramref name="rdns"/> opts in to the reverse-DNS fields
    /// (adds a live DNS lookup to the call).</summary>
    public Task<IpChecks> CheckIpAsync(string ip, bool rdns = false, CancellationToken cancellationToken = default)
    {
        Require(ip, nameof(ip));
        return GetAsync<IpChecks>(
            "/v1/ip/" + Uri.EscapeDataString(ip) + (rdns ? "?rdns=true" : string.Empty), cancellationToken);
    }

    /// <summary>Email checks on their own.</summary>
    public Task<EmailChecks> CheckEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        Require(email, nameof(email));
        return GetAsync<EmailChecks>("/v1/email/" + Uri.EscapeDataString(email), cancellationToken);
    }

    /// <summary>
    /// Phone checks on their own. Pass E.164 (<c>+14155552671</c>), or a national number plus
    /// <paramref name="region"/> to parse it against.
    /// </summary>
    public Task<PhoneChecks> CheckPhoneAsync(string phone, string? region = null, CancellationToken cancellationToken = default)
    {
        Require(phone, nameof(phone));
        // EscapeDataString handles the leading '+', which would otherwise be read as a space.
        var path = "/v1/phone/" + Uri.EscapeDataString(phone);
        if (!string.IsNullOrWhiteSpace(region))
            path += "?region=" + Uri.EscapeDataString(region!);
        return GetAsync<PhoneChecks>(path, cancellationToken);
    }

    /// <summary>
    /// Full geolocation for an IP. Location data, not a risk check — non-routable addresses come back with
    /// <see cref="GeoResult.Bogon"/> set rather than an error.
    /// </summary>
    public Task<GeoResult> GeolocateAsync(string ip, CancellationToken cancellationToken = default)
    {
        Require(ip, nameof(ip));
        return GetAsync<GeoResult>("/v1/geo/" + Uri.EscapeDataString(ip), cancellationToken);
    }

    /// <summary>
    /// Sanctions name screening for a person's or company's name — checked against listed parties' primary
    /// names and known aliases. Matching is normalised (case/punctuation/word order ignored) but not fuzzy.
    /// A CHECK, not a compliance determination: a match is review-worthy (names collide), a non-match is not
    /// clearance.
    /// </summary>
    public Task<NameChecks> CheckNameAsync(string name, CancellationToken cancellationToken = default)
    {
        Require(name, nameof(name));
        return GetAsync<NameChecks>("/v1/name/" + Uri.EscapeDataString(name), cancellationToken);
    }

    /// <summary>
    /// The live reason-code catalog: every code the scorer can emit, its current weight, and its meaning.
    /// Fetch once and cache (it's metered like any call) — ideal for building review UIs without hardcoding
    /// explanations that drift.
    /// </summary>
    public Task<IReadOnlyList<ReasonDetail>> GetReasonsAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<ReasonDetail>>("/v1/reasons", cancellationToken);

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", name);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using (var response = await _http.GetAsync(path, ct).ConfigureAwait(false))
            return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, Json);
        using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
        using (var response = await _http.PostAsync(path, content, ct).ConfigureAwait(false))
            return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    /// <summary>Turn a response into either a result or the most specific exception we can offer.</summary>
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var payload = await ReadStringAsync(response).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw BuildException(response, payload);

        var result = JsonSerializer.Deserialize<T>(payload, Json);
        if (result == null)
        {
            throw new FraudCheckException(ErrorCodes.InternalError,
                "The API returned an empty response body.", response.StatusCode);
        }
        return result;
    }

    private static Exception BuildException(HttpResponseMessage response, string payload)
    {
        // /v1 always answers with {code,message} — but a proxy or gateway in front of us might not, so never
        // let a parse failure mask the real status.
        string code = ErrorCodes.InternalError;
        string message = "The API returned " + (int)response.StatusCode + ".";
        try
        {
            var error = JsonSerializer.Deserialize<ApiError>(payload, Json);
            if (error != null)
            {
                if (!string.IsNullOrEmpty(error.Code)) code = error.Code!;
                if (!string.IsNullOrEmpty(error.Message)) message = error.Message!;
            }
        }
        catch (JsonException)
        {
            if (!string.IsNullOrWhiteSpace(payload))
                message += " " + Truncate(payload, 200);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new FraudCheckAuthenticationException(code, message, response.StatusCode);

        if (response.StatusCode == (HttpStatusCode)429)
        {
            TimeSpan? retryAfter = null;
            var ra = response.Headers.RetryAfter;
            if (ra != null)
            {
                if (ra.Delta.HasValue) retryAfter = ra.Delta;
                else if (ra.Date.HasValue)
                {
                    var wait = ra.Date.Value - DateTimeOffset.UtcNow;
                    if (wait > TimeSpan.Zero) retryAfter = wait;
                }
            }
            return new FraudCheckRateLimitException(code, message, response.StatusCode, retryAfter);
        }

        return new FraudCheckException(code, message, response.StatusCode);
    }

    private static Task<string> ReadStringAsync(HttpResponseMessage response)
        => response.Content.ReadAsStringAsync();

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";

    /// <summary>Disposes the underlying <see cref="HttpClient"/> only when this client created it.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
}
