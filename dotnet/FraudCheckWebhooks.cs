using System;
using System.Security.Cryptography;
using System.Text;

namespace FraudCheck.Client;

/// <summary>
/// Verify outbound webhook deliveries from FraudCheck. Each POST is signed with the endpoint's <c>whsec_</c>
/// secret; verify it before trusting the payload — anyone can POST JSON at a public URL.
///
/// <para>
/// Pass the RAW request body exactly as received (do not deserialize-then-reserialize — that changes the
/// bytes and the signature won't match). Read the headers by their constant names below.
/// </para>
///
/// <example>
/// <code>
/// var ok = FraudCheckWebhooks.Verify(
///     secret: mySecret,
///     timestamp: Request.Headers[FraudCheckWebhooks.TimestampHeader],
///     body: rawBody,
///     signature: Request.Headers[FraudCheckWebhooks.SignatureHeader]);
/// if (!ok) return BadRequest();
/// </code>
/// </example>
/// </summary>
public static class FraudCheckWebhooks
{
    /// <summary>Header naming the event type, e.g. <c>quota.warning</c>.</summary>
    public const string EventHeader = "X-FraudCheck-Event";
    /// <summary>Header carrying the unique delivery id (use it to deduplicate retries).</summary>
    public const string DeliveryHeader = "X-FraudCheck-Delivery";
    /// <summary>Header carrying the unix-seconds timestamp the delivery was signed at.</summary>
    public const string TimestampHeader = "X-FraudCheck-Timestamp";
    /// <summary>Header carrying the <c>v1=…</c> signature.</summary>
    public const string SignatureHeader = "X-FraudCheck-Signature";

    /// <summary>The expected <c>v1=…</c> signature value for a (timestamp, body) pair.</summary>
    public static string Sign(string secret, string timestamp, string body)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + body));
            var sb = new StringBuilder("v1=", 3 + hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// True when <paramref name="signature"/> matches and (if <paramref name="toleranceSeconds"/> &gt; 0) the
    /// timestamp is recent. Uses a fixed-time comparison, so a mismatch reveals nothing about how close it was.
    /// </summary>
    public static bool Verify(string secret, string? timestamp, string body, string? signature,
        int toleranceSeconds = 300)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        if (toleranceSeconds > 0)
        {
            if (!long.TryParse(timestamp, out var ts))
                return false;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - ts) > toleranceSeconds)
                return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(secret, timestamp!, body));
        var provided = Encoding.UTF8.GetBytes(signature!);
        return FixedTimeEquals(expected, provided);
    }

    // Constant-time for equal-length inputs. Hand-rolled rather than CryptographicOperations.FixedTimeEquals
    // because that type isn't available on the netstandard2.0 target. The expected length is fixed (v1= + 64
    // hex chars), so returning early on a length mismatch leaks nothing sensitive.
    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
