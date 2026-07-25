using System;
using System.Net;
using System.Text.Json.Serialization;

namespace FraudCheck.Client;

/// <summary>The API's error body. Every /v1 failure returns this shape, on every status code.</summary>
internal sealed class ApiError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Thrown when the API returns an error. Branch on <see cref="Code"/> (see <see cref="ErrorCodes"/>) rather
/// than on the message or the raw status — codes are contractual, messages are prose.
/// </summary>
public class FraudCheckException : Exception
{
    /// <summary>The stable machine code, e.g. <c>quota_exceeded</c>. See <see cref="ErrorCodes"/>.</summary>
    public string Code { get; }

    /// <summary>The HTTP status that carried this error.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// True when retrying the same request later could plausibly succeed: rate limits, quota/spend caps that
    /// reset, and server errors. False for anything you must fix first (a bad key, a malformed address).
    /// </summary>
    public bool IsTransient =>
        Code == ErrorCodes.RateLimited ||
        Code == ErrorCodes.InternalError ||
        StatusCode >= HttpStatusCode.InternalServerError;

    public FraudCheckException(string code, string message, HttpStatusCode statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public override string ToString() => $"{Code} ({(int)StatusCode}): {Message}";
}

/// <summary>
/// Your key is wrong, missing or revoked (HTTP 401). Split out because it's the one error that always means
/// "fix your configuration" — retrying will never help.
/// </summary>
public sealed class FraudCheckAuthenticationException : FraudCheckException
{
    public FraudCheckAuthenticationException(string code, string message, HttpStatusCode statusCode)
        : base(code, message, statusCode) { }
}

/// <summary>
/// You've hit a limit (HTTP 429): the per-second rate, the monthly quota, or your overage spend cap —
/// <see cref="FraudCheckException.Code"/> says which.
/// </summary>
public sealed class FraudCheckRateLimitException : FraudCheckException
{
    /// <summary>
    /// How long the server asked you to wait, from its <c>Retry-After</c> header. Null when it didn't say —
    /// quota and spend-cap rejections generally don't, since the answer is "next month" or "raise the cap".
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    public FraudCheckRateLimitException(string code, string message, HttpStatusCode statusCode, TimeSpan? retryAfter)
        : base(code, message, statusCode)
    {
        RetryAfter = retryAfter;
    }
}
