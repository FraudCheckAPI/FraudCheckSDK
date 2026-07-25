# FraudCheck API — Official SDKs

Client libraries for the [FraudCheck API](https://fraudcheckapi.com) — fraud checks for an IP, email,
phone, shipping country, and person/company name in one fast call, with an advisory 0–100 score and
stable reason codes.

Each client mirrors the [`/openapi/v1.json`](https://fraudcheckapi.com/openapi/v1.json) contract. All of
them share the same shape: create a client with your API key, call an endpoint, and get the response back
as your language's native data. Errors carry the stable machine `code` so you branch on that, not on the
message or the status.

| Language | Where | Runtime | Install |
|---|---|---|---|
| .NET | [`dotnet/`](dotnet/) | .NET Framework 4.6.1+ / .NET 8+ | [`dotnet add package FraudCheck.Client`](https://www.nuget.org/packages/FraudCheck.Client) |
| Python | [`python/fraudcheck.py`](python/fraudcheck.py) | Python 3.8+ | drop the file in — stdlib only |
| Node.js | [`node/fraudcheck.js`](node/fraudcheck.js) | Node 18+ | drop the file in — uses global `fetch` |
| PHP | [`php/FraudCheck.php`](php/FraudCheck.php) | PHP 7.4+ | drop the file in — uses the cURL extension |

The .NET client is the most fully featured (typed models, DI registration, request signing). The Python,
Node and PHP clients are single-file, zero-dependency drop-ins — the fastest path to a working integration
in those languages. No SDK is required at all: the API is plain REST, and your dashboard's **Docs & code**
page has copy-paste examples in 8 languages.

Get an API key at [fraudcheckapi.com](https://fraudcheckapi.com) — the free tier includes the full check set.

## Checks, not verdicts

Every client returns checks, never a verdict. There is no `is_fraud` field. The score is advisory and you
own the decision — which means you own the false positives, so prefer reacting to specific reason codes over
a blanket score threshold. Results are derived from publicly available databases and provided as-is: input
to your judgement, not fact.

## Fail open

Screening usually sits in a checkout or signup path. If FraudCheck is slow or down, the safe default for
most businesses is to let the customer through and review later, rather than block real revenue. Every
client defaults to a 10-second timeout for this reason. Catch the error, log it, and proceed without a score.

## Quick look

**Python**
```python
from fraudcheck import FraudCheckClient, ReasonCodes

client = FraudCheckClient("fck_live_your_key")
r = client.screen(ip="8.8.8.8", email="buyer@example.com", shipping_country="US")
if ReasonCodes.SANCTIONED_COUNTRY in r["reasons"]:
    hold_for_review(order)
```

**Node.js**
```js
const { FraudCheckClient, ReasonCodes } = require("./fraudcheck");

const client = new FraudCheckClient("fck_live_your_key");
const r = await client.screen({ ip: "8.8.8.8", email: "buyer@example.com", shippingCountry: "US" });
if (r.reasons.includes(ReasonCodes.SANCTIONED_COUNTRY)) holdForReview(order);
```

**PHP**
```php
use FraudCheck\FraudCheckClient;
use FraudCheck\ReasonCodes;

$client = new FraudCheckClient("fck_live_your_key");
$r = $client->screen(["ip" => "8.8.8.8", "email" => "buyer@example.com", "shipping_country" => "US"]);
if (in_array(ReasonCodes::SANCTIONED_COUNTRY, $r["reasons"], true)) { holdForReview($order); }
```

**.NET**
```csharp
using FraudCheck.Client;

using var client = new FraudCheckClient("fck_live_your_key");
var result = await client.ScreenAsync(ip: "8.8.8.8", email: "buyer@example.com",
                                      shippingCountry: Countries.UnitedStates);
if (result.Reasons.Contains(ReasonCodes.SanctionedCountry)) HoldForReview(order);
```

## Other languages — Go, Rust, Java, Ruby, …

The API publishes a complete OpenAPI 3 spec at
[`/openapi/v1.json`](https://fraudcheckapi.com/openapi/v1.json), so you can generate a typed client for any
language with [OpenAPI Generator](https://openapi-generator.tech):

**Go**
```bash
docker run --rm -v "${PWD}:/local" openapitools/openapi-generator-cli generate \
  -i https://fraudcheckapi.com/openapi/v1.json -g go -o /local/fraudcheck-go
```

**Rust**
```bash
docker run --rm -v "${PWD}:/local" openapitools/openapi-generator-cli generate \
  -i https://fraudcheckapi.com/openapi/v1.json -g rust -o /local/fraudcheck-rust
```

(If you prefer npm over Docker: `npx @openapitools/openapi-generator-cli generate -i … -g go -o …`.
Run `openapi-generator-cli list` to see all supported generators — 50+ languages.)

Two things to configure in any generated client:

1. **Authentication** — send your key on every request as the `X-Api-Key` header (or
   `Authorization: Bearer <key>`). Each generator has a way to set default headers; see its README.
2. **Timeout** — set ~10 seconds and fail open (see above). Generated clients often default to no timeout.

The response fields are additive-only — we add fields but never rename or remove them — so a generated
client keeps working across API updates; regenerate whenever you want the newest fields typed.

## Errors

Each client raises/throws a typed error carrying the stable `code`, with distinct auth (401) and rate-limit
(429) types, and an `is_transient` flag telling you whether a retry could help. See each file's header for
the full list and per-language examples.

## Webhooks

If you configure webhook endpoints in your dashboard, FraudCheck POSTs signed event notifications (quota
warnings, key changes, spend-cap hits) to your server. **Verify the signature before trusting the payload** —
each client ships a helper so you don't implement HMAC yourself. Pass the raw request body exactly as received:

```python
from fraudcheck import Webhooks
ok = Webhooks.verify(secret=MY_SECRET, timestamp=hdr["X-FraudCheck-Timestamp"],
                     body=raw_body, signature=hdr["X-FraudCheck-Signature"])
```

Verification is constant-time and rejects deliveries older than 5 minutes by default. Retries are
byte-identical, so use the `X-FraudCheck-Delivery` id to deduplicate.

## Keys

Keys belong to your **account**, not to you personally, so they keep working when someone leaves the team.
Treat one like a password: server-side only, never in browser or mobile code. Secrets are shown once at
creation; a revoked key never comes back. Full details — scopes, rotation, request signing — are in your
dashboard under **Docs & code**.

## License

[MIT](LICENSE) — © F7 Software, Inc.
