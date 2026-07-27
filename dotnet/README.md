# FraudCheck.Client

Official .NET client for the [FraudCheck API](https://fraudcheckapi.com). Screen an IP, email, phone and
shipping country in one call and get per-check results, a composite advisory score, and stable reason codes.

## Supported runtimes

| Your app targets | Works | Asset you get |
|---|---|---|
| .NET 8, 9, 10 — and 11+ when it ships | ✅ | `lib/net8.0` |
| .NET Core 2.0 – 3.1, .NET 5–7 | ✅ | `lib/netstandard2.0` |
| .NET Framework 4.6.1+ | ✅ | `lib/netstandard2.0` |
| Xamarin, Unity, Mono | ✅ | `lib/netstandard2.0` |

Two targets cover everything, because .NET's framework compatibility is forward-only: a `net8.0` library is a
valid dependency for **any later .NET**, and NuGet automatically picks the best `lib/` folder for your target.
So a .NET 10 or .NET 11 app resolves the `net8.0` build and runs it on your runtime — you don't need a
`net10.0`-specific package, and there won't be one.

We target net8.0 rather than the newest release on purpose: it's the oldest LTS still in support, which
maximises reach without costing anyone on a newer runtime anything.

## Checks, not verdicts

There is no `is_fraud` field, and there never will be. FraudCheck tells you what it found; **you** decide what
to do about it. Results are derived from publicly available databases and provided as-is — treat them as input
to your judgement, not as fact.

In practice: prefer reacting to specific reason codes over thresholding the score. `SANCTIONED_COUNTRY` is a
compliance question. `DATACENTER_IP` on its own is often just someone on a VPN.

## Install

```bash
dotnet add package FraudCheck.Client
```

## Quick start

```csharp
using FraudCheck.Client;

using var client = new FraudCheckClient("fck_live_your_key_here");

var result = await client.ScreenAsync(
    ip: "8.8.8.8",
    email: "buyer@example.com",
    phone: "+14155552671",
    shippingCountry: "US");

Console.WriteLine(result.Score);                       // 53
Console.WriteLine(string.Join(", ", result.Reasons));  // DISPOSABLE_EMAIL, DATACENTER_IP

if (result.HasReason(ReasonCodes.SanctionedCountry))
    HoldForManualReview(order);
```

## Dependency injection (net8.0)

```csharp
builder.Services.AddFraudCheck(o => o.ApiKey = builder.Configuration["FraudCheck:ApiKey"]!);
```

Then inject `FraudCheckClient` anywhere. This routes through `IHttpClientFactory`, so handlers are pooled and
rotated — important for a client that lives as long as your app. Add retries with the usual builder:

```csharp
builder.Services.AddFraudCheck(o => o.ApiKey = key).AddStandardResilienceHandler();
```

Don't `new` up a client per request: you'll exhaust sockets. One long-lived instance, or DI.

## Individual checks

```csharp
IpChecks    ip    = await client.CheckIpAsync("8.8.8.8");
EmailChecks email = await client.CheckEmailAsync("buyer@example.com");
PhoneChecks phone = await client.CheckPhoneAsync("+14155552671");
PhoneChecks nat   = await client.CheckPhoneAsync("4155552671", region: "US");
GeoResult   geo   = await client.GeolocateAsync("8.8.8.8");   // location data, not a risk check
```

## Errors

Everything the API rejects throws a `FraudCheckException` carrying the stable `Code`. Switch on that, never on
the message — messages get reworded, codes don't.

```csharp
try
{
    var result = await client.ScreenAsync(ip: order.Ip, email: order.Email);
}
catch (FraudCheckAuthenticationException)
{
    // Key is wrong, missing or revoked. Retrying won't help — fix the configuration.
}
catch (FraudCheckRateLimitException ex)
{
    // ex.Code is rate_limited, quota_exceeded or spend_cap_reached.
    // ex.RetryAfter is set when the server said how long to wait.
}
catch (FraudCheckException ex) when (ex.IsTransient)
{
    // Rate limits and server errors — worth a retry.
}
```

### Fail open, not closed

Screening usually sits in a checkout or signup path. If FraudCheck is slow or down, the safe default for most
businesses is to let the customer through and review later, rather than block real revenue:

```csharp
ScreenResult? result = null;
try { result = await client.ScreenAsync(ip: ip, email: email); }
catch (FraudCheckException) { /* log it; proceed without a score */ }
```

The default timeout is 10 seconds for this reason. Tune it with `FraudCheckOptions.Timeout`.

## Reason codes

`ReasonCodes` has constants for every published code. They're constants rather than an enum on purpose: new
codes can appear at any time, and an unknown value must never break your deserialization or your switch.

| Code | Weight | Fires when |
|---|---|---|
| `SANCTIONED_COUNTRY` | 40 | Shipping country is on a sanctions list |
| `DISPOSABLE_EMAIL` | 35 | Email domain is a known throwaway inbox |
| `EMAIL_SYNTAX_INVALID` | 30 | Not a valid email address |
| `TOR_EXIT_NODE` | 30 | IP is a known Tor exit node |
| `EMAIL_NO_MX` | 25 | Domain can't receive mail |
| `FATF_HIGH_RISK` | 25 | Country is on the FATF high-risk list |
| `PHONE_INVALID` | 20 | Number isn't valid for its region |
| `PHONE_PREMIUM_RATE` | 20 | Premium-rate number |
| `DATACENTER_IP` | 18 | IP is a datacenter/hosting range |
| `HIGH_RISK_TLD` | 15 | TLD commonly abused for throwaway signups |
| `PHONE_VOIP` | 15 | VoIP number — cheap to obtain and discard |
| `COUNTRY_MISMATCH` | 12 | Phone, IP and shipping countries disagree |
| `CLOUD_HOSTING_IP` | 10 | IP belongs to a cloud provider |
| `FATF_MONITORED` | 10 | Country under increased FATF monitoring |
| `ROLE_ACCOUNT` | 10 | Shared mailbox (info@, admin@) not a person |
| `MEDIUM_RISK_TLD` | 6 | TLD sees elevated abuse |

Weights are additive and clamped to 0–100. Nothing is hidden — you can always explain a score, or recompute
your own from `Reasons` and ignore ours.

## Your own weighting (`ScoringPolicy`)

Our score is the same for every customer, which means it can't know your business. Throwaway email addresses
may be the whole problem for a digital-goods seller, while a B2B supplier sees corporate VPNs all day and
cares far more about a sanctions hit. `ScoringPolicy` re-scores a result using weights you choose.

It runs entirely on your side — no API call, no settings stored with us, and you can unit-test a policy with
no network.

```csharp
var policy = new ScoringPolicy("checkout-v3")
{
    Weights =
    {
        [ReasonCodes.DisposableEmail] = 45,   // hurts us badly
        [ReasonCodes.DatacenterIp]    = 5,    // half our customers are on a VPN
        [ReasonCodes.SanctionedName]  = 100,  // always look at this one
    },
    Bands = { [40] = "review", [80] = "decline" },
};

var result = await client.ScreenAsync(ip: "9.9.9.9", email: "user@mailinator.com");
var assessment = result.Assess(policy);

assessment.Score;       // 50   — yours; result.Score is still ours, untouched
assessment.Band;        // "review"
assessment.Explain();   // "checkout-v3 scored 50 (review): DISPOSABLE_EMAIL +45, DATACENTER_IP +5"
```

Store `Explain()` next to whatever you decided. When you're contesting a chargeback or answering an auditor
months later, a sentence naming the policy and every contributing signal is evidence in a way a bare number
isn't — which is also why a policy must be named: retune the weights and `checkout-v3` becomes `checkout-v4`,
so an old record stays reproducible.

**Codes you haven't weighted.** New reason codes ship over time. By default an unlisted code keeps the weight
we applied, so a policy written today still reacts to a signal added next year; `assessment.UsedFallbackWeight`
tells you it happened, which makes a good alert for "something new is firing on our traffic." Set
`UnknownCodes = UnknownCodeBehavior.Ignore` if you'd rather only your own codes ever move the number — just
know that's a decision to stop noticing new signals, not a default to accept quietly.

`Score` is not clamped to 0–100: with your own weights, an unbounded total keeps sorting a review queue
worst-first meaningful. And there's deliberately no `ShouldBlock()` — bands are labels you name, for the same
reason the API has no `is_fraud` field. The decision stays yours.

## Verifying webhooks

If you configure webhook endpoints in your dashboard, FraudCheck POSTs signed event notifications to your
server. Verify the signature before trusting the payload — `FraudCheckWebhooks` does it for you. Pass the
**raw** request body (don't deserialize then re-serialize — that changes the bytes):

```csharp
var ok = FraudCheckWebhooks.Verify(
    secret: myEndpointSecret,
    timestamp: Request.Headers[FraudCheckWebhooks.TimestampHeader],
    body: rawBody,
    signature: Request.Headers[FraudCheckWebhooks.SignatureHeader]);
if (!ok) return BadRequest();
```

Verification is fixed-time and rejects deliveries older than 5 minutes by default. Retries are byte-identical,
so use `X-FraudCheck-Delivery` to deduplicate.

## Keys

Keys belong to your **account**, not to you personally, so they keep working when someone leaves the team.
Treat one like a password: server-side only, never in browser or mobile code. Secrets are shown once at
creation; a revoked key never comes back.

## Links

- Full API reference: your dashboard → **Docs & code**
- Support: support@fraudcheckapi.com

© F7 Software Inc. MIT licensed.
