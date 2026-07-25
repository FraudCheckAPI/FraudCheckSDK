using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FraudCheck.Client;

/// <summary>
/// Signs outgoing requests for keys that require it, so you never implement the scheme yourself.
///
/// <para>
/// It's a <see cref="DelegatingHandler"/> rather than code in the client so it composes: put it under a retry
/// policy and each attempt gets a fresh timestamp and signature, which is what you want — a retried request
/// carrying a stale timestamp would fail the ±5 minute window.
/// </para>
///
/// <para>The canonical string, if you ever need to reproduce it:</para>
/// <code>{unix-seconds}.{METHOD}.{path+query}.{hex(sha256(body))}</code>
/// <para>HMAC-SHA256 it with <c>hex(sha256(your-api-key))</c> and send:</para>
/// <code>
/// X-FraudCheck-Timestamp: {unix-seconds}
/// X-FraudCheck-Signature: v1={hex}
/// </code>
/// </summary>
public sealed class RequestSigningHandler : DelegatingHandler
{
    private readonly string _signingKey;

    /// <param name="apiKey">Your API key. The HMAC key is derived from it — the key itself is never sent as the secret.</param>
    public RequestSigningHandler(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An API key is required to sign requests.", nameof(apiKey));

        // The server stores sha256(secret) and signs with that, so both sides derive the same value without
        // the server ever holding the secret itself.
        using (var sha = SHA256.Create())
            _signingKey = Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(apiKey)));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var body = request.Content is null
            ? Array.Empty<byte>()
            : await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        string bodyHash;
        using (var sha = SHA256.Create())
            bodyHash = Hex(sha.ComputeHash(body));
        var path = request.RequestUri is null ? "" : request.RequestUri.PathAndQuery;
        var canonical = $"{timestamp}.{request.Method.Method.ToUpperInvariant()}.{path}.{bodyHash}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingKey));
        var signature = Hex(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));

        request.Headers.Remove("X-FraudCheck-Timestamp");
        request.Headers.Remove("X-FraudCheck-Signature");
        request.Headers.Add("X-FraudCheck-Timestamp", timestamp.ToString());
        request.Headers.Add("X-FraudCheck-Signature", "v1=" + signature);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string Hex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
