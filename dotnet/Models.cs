using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FraudCheck.Client;

/// <summary>
/// What to screen. Every field is optional, but send at least one — an empty request is rejected with
/// <c>missing_input</c>.
/// </summary>
public sealed class ScreenRequest
{
    /// <summary>The visitor's IPv4 or IPv6 address.</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>The email address to check.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// The phone number. E.164 (<c>+14155552671</c>) is safest; anything else is parsed against
    /// <see cref="ShippingCountry"/>.
    /// </summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    /// <summary>ISO 3166-1 country code — alpha-2 (<c>US</c>), alpha-3 (<c>USA</c>), or numeric
    /// (<c>840</c>) are all accepted; responses echo the alpha-2 form. See <see cref="Countries"/> for
    /// named constants. Codes only — full country names are not accepted.</summary>
    [JsonPropertyName("shipping_country")]
    public string? ShippingCountry { get; set; }

    /// <summary>A person or company name to screen against sanctions lists. A check, not a verdict.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Opt in to reverse-DNS fields on the ip checks (<c>reverse_dns*</c>). Adds a live DNS lookup to the
    /// call, so it is off by default.
    /// </summary>
    [JsonPropertyName("rdns")]
    public bool Rdns { get; set; }
}

/// <summary>
/// The result of a screen: each check that ran, plus a composite advisory score and the reason codes behind it.
///
/// There is no <c>is_fraud</c> field, deliberately and permanently. FraudCheck returns checks; the decision is
/// yours. Prefer switching on <see cref="Reasons"/> over thresholding <see cref="Score"/> — a datacenter IP is
/// often just a VPN, whereas a sanctioned country is a compliance question.
/// </summary>
public sealed class ScreenResult
{
    /// <summary>Composite advisory score, 0 (clear) to 100 (high). Advisory only — never a verdict.</summary>
    [JsonPropertyName("score")]
    public int Score { get; set; }

    /// <summary>IP checks, when an IP was supplied.</summary>
    [JsonPropertyName("ip")]
    public IpChecks? Ip { get; set; }

    /// <summary>Email checks, when an email was supplied.</summary>
    [JsonPropertyName("email")]
    public EmailChecks? Email { get; set; }

    /// <summary>Phone checks, when a phone was supplied.</summary>
    [JsonPropertyName("phone")]
    public PhoneChecks? Phone { get; set; }

    /// <summary>Country checks, when a shipping country was supplied.</summary>
    [JsonPropertyName("country")]
    public CountryChecks? Country { get; set; }

    /// <summary>Sanctions name-screening result, when a name was supplied.</summary>
    [JsonPropertyName("name")]
    public NameChecks? Name { get; set; }

    /// <summary>
    /// Stable codes for everything that contributed to the score, e.g. <c>DISPOSABLE_EMAIL</c>.
    /// Safe to switch on; see <see cref="ReasonCodes"/> for the published set.
    /// </summary>
    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; set; } = new List<string>();

    /// <summary>
    /// One entry per reason code: the weight it added as applied to THIS response, plus a human-readable
    /// explanation. Branch on <see cref="ReasonDetail.Code"/> — the message is prose and may be reworded.
    /// Null when nothing contributed.
    /// </summary>
    [JsonPropertyName("reason_details")]
    public IReadOnlyList<ReasonDetail>? ReasonDetails { get; set; }

    /// <summary>Set only when a contributing dataset is temporarily serving older data.</summary>
    [JsonPropertyName("data_age_warning")]
    public string? DataAgeWarning { get; set; }

    /// <summary>
    /// True when <paramref name="code"/> is among <see cref="Reasons"/>. Convenience for the common
    /// <c>if (result.HasReason(ReasonCodes.SanctionedCountry))</c> shape.
    /// </summary>
    public bool HasReason(string code)
    {
        if (Reasons == null) return false;
        for (var i = 0; i < Reasons.Count; i++)
            if (string.Equals(Reasons[i], code, System.StringComparison.Ordinal))
                return true;
        return false;
    }
}

/// <summary>Request body for <c>POST /v1/screen/batch</c>.</summary>
public sealed class BatchScreenBody
{
    [JsonPropertyName("items")]
    public IReadOnlyList<ScreenRequest> Items { get; set; } = new List<ScreenRequest>();
}

/// <summary>One batch item's outcome: exactly one of <see cref="Result"/>/<see cref="Error"/> is set.</summary>
public sealed class BatchItemResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("result")]
    public ScreenResult? Result { get; set; }

    [JsonPropertyName("error")]
    public BatchItemError? Error { get; set; }
}

/// <summary>An item-level error inside a successful batch response. Branch on <see cref="Code"/>.</summary>
public sealed class BatchItemError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;
}

/// <summary>Response of <c>POST /v1/screen/batch</c> — results align with the submitted items by index.</summary>
public sealed class BatchScreenResult
{
    [JsonPropertyName("results")]
    public IReadOnlyList<BatchItemResult> Results { get; set; } = new List<BatchItemResult>();
}

/// <summary>One scored reason, explained. Also the row shape of <c>GET /v1/reasons</c> (the live catalog).</summary>
public sealed class ReasonDetail
{
    /// <summary>The stable machine code — the thing to branch on.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    /// <summary>What this code adds to the score (weights are tunable server-side).</summary>
    [JsonPropertyName("weight")]
    public int Weight { get; set; }

    /// <summary>Human-readable explanation. Prose — may be reworded; never parse it.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;
}

/// <summary>IP checks. Also returned on its own by <c>GET /v1/ip/{ip}</c>.</summary>
public sealed class IpChecks
{
    /// <summary>
    /// The address that was actually screened. The ip input accepts a proxy chain (X-Forwarded-For style);
    /// the API resolves the real client and this says which entry it picked.
    /// </summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>ISO alpha-2 country the IP is registered in.</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>English name for <see cref="Country"/> (ISO 3166 decode). Null when unknown.</summary>
    [JsonPropertyName("country_name")]
    public string? CountryName { get; set; }

    /// <summary>ISO 3166-1 alpha-3 form of <see cref="Country"/> (e.g. "USA"). Null when unknown.</summary>
    [JsonPropertyName("country_code3")]
    public string? CountryCode3 { get; set; }

    /// <summary>Autonomous System Number of the network operator.</summary>
    [JsonPropertyName("asn")]
    public long? Asn { get; set; }

    /// <summary>The IP is a known Tor exit node.</summary>
    [JsonPropertyName("is_tor")]
    public bool IsTor { get; set; }

    /// <summary>The IP belongs to a datacenter/hosting range rather than a consumer ISP.</summary>
    [JsonPropertyName("is_datacenter")]
    public bool IsDatacenter { get; set; }

    /// <summary>The address is a known CDN edge/proxy (Cloudflare, Fastly, CloudFront). Seeing this on every
    /// order means the integration forwards the CDN's IP instead of the shopper's.</summary>
    [JsonPropertyName("is_cdn_edge")]
    public bool IsCdnEdge { get; set; }

    /// <summary>The IP belongs to a known cloud provider's range.</summary>
    [JsonPropertyName("is_cloud")]
    public bool IsCloud { get; set; }

    /// <summary>The IP is a known commercial VPN exit — deliberate origin masking, not itself fraud.</summary>
    [JsonPropertyName("is_vpn")]
    public bool IsVpn { get; set; }

    /// <summary>The IP is on a network flagged as hostile — hijacked or criminal-controlled infrastructure.
    /// A strong risk signal.</summary>
    [JsonPropertyName("is_known_bad_network")]
    public bool IsKnownBadNetwork { get; set; }

    /// <summary>
    /// Derived 0–100 corroboration (Tor / known-VPN / datacenter / cloud). A hint, not a fact.
    /// </summary>
    [JsonPropertyName("proxy_likelihood")]
    public int ProxyLikelihood { get; set; }

    /// <summary>Nested location. Null for private/reserved addresses.</summary>
    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    /// <summary>
    /// Outcome of the opt-in reverse-DNS lookup — present only when the request asked for it (rdns=true).
    /// <c>"found"</c> | <c>"none"</c> (the address definitively has no PTR record) | <c>"error"</c> (DNS
    /// didn't answer in time; every other field on this object is unaffected).
    /// </summary>
    [JsonPropertyName("reverse_dns_status")]
    public string? ReverseDnsStatus { get; set; }

    /// <summary>The address's PTR hostname, when one exists.</summary>
    [JsonPropertyName("reverse_dns")]
    public string? ReverseDns { get; set; }

    /// <summary>Forward-confirmed reverse DNS: the PTR hostname resolves back to this same IP. A mismatch
    /// (false) is a classic spoofing/misconfiguration tell. Null when it couldn't be checked.</summary>
    [JsonPropertyName("reverse_dns_valid")]
    public bool? ReverseDnsValid { get; set; }

    /// <summary>Registrable domain of the PTR hostname. Null when not derivable.</summary>
    [JsonPropertyName("reverse_dns_domain")]
    public string? ReverseDnsDomain { get; set; }

    /// <summary>The PTR hostname's domain is on FraudCheck's bad-domain lists. Rare, but decisive.</summary>
    [JsonPropertyName("reverse_dns_domain_flagged")]
    public bool? ReverseDnsDomainFlagged { get; set; }
}

/// <summary>City-level location. Every field is nullable — coverage varies by IP.</summary>
public sealed class GeoLocation
{
    /// <summary>First-level subdivision (state/province).</summary>
    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }

    /// <summary>Approximate latitude. City-level — never a precise address.</summary>
    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    /// <summary>Approximate longitude. City-level — never a precise address.</summary>
    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    /// <summary>IANA timezone, e.g. <c>America/Los_Angeles</c>.</summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
}

/// <summary>Full geolocation for an IP. This is location data, not a risk check — no score is involved.</summary>
public sealed class GeoResult
{
    /// <summary>The address you asked about, echoed back.</summary>
    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "";

    /// <summary>IP version: 4 or 6.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }

    /// <summary>Routing scope: <c>public</c>, <c>private</c>, <c>loopback</c>, <c>link_local</c>, <c>cgnat</c>, …</summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "public";

    /// <summary>True for non-routable/reserved addresses, which can't be located.</summary>
    [JsonPropertyName("bogon")]
    public bool Bogon { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("country_name")]
    public string? CountryName { get; set; }

    /// <summary>ISO 3166-1 alpha-3 form of <see cref="CountryCode"/> (e.g. "USA"). Null when unknown.</summary>
    [JsonPropertyName("country_code3")]
    public string? CountryCode3 { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    /// <summary>Second-level subdivision (county/district), where known.</summary>
    [JsonPropertyName("region2")]
    public string? Region2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    [JsonPropertyName("asn")]
    public long? Asn { get; set; }

    /// <summary>Network operator's name.</summary>
    [JsonPropertyName("org")]
    public string? Org { get; set; }
}

/// <summary>Email checks. Also returned on its own by <c>GET /v1/email/{email}</c>.</summary>
public sealed class EmailChecks
{
    /// <summary>The address parses as a valid email address. When false, the other checks are skipped.</summary>
    [JsonPropertyName("syntax_valid")]
    public bool SyntaxValid { get; set; }

    /// <summary>The domain publishes an MX record, so it can receive mail.</summary>
    [JsonPropertyName("domain_has_mx")]
    public bool DomainHasMx { get; set; }

    /// <summary>
    /// Whether the domain exists in DNS at all. false = DNS definitively says the name does not exist
    /// (a typo like gmail.con, or a fabricated domain) — stronger than merely having no mail server.
    /// Null when DNS gave no definitive answer.
    /// </summary>
    [JsonPropertyName("domain_exists")]
    public bool? DomainExists { get; set; }

    /// <summary>The domain publishes an SPF record (root TXT starting <c>v=spf1</c>). Presence only.</summary>
    [JsonPropertyName("has_spf")]
    public bool HasSpf { get; set; }

    /// <summary>The domain publishes a DMARC record (<c>_dmarc</c> TXT starting <c>v=DMARC1</c>). Presence only.</summary>
    [JsonPropertyName("has_dmarc")]
    public bool HasDmarc { get; set; }

    /// <summary>The domain is a known throwaway/temporary inbox provider.</summary>
    [JsonPropertyName("is_disposable")]
    public bool IsDisposable { get; set; }

    /// <summary>A shared mailbox (info@, admin@, support@) rather than a person.</summary>
    [JsonPropertyName("is_role_account")]
    public bool IsRoleAccount { get; set; }

    /// <summary>
    /// A consumer mail provider (gmail.com, outlook.com…). Informational only — freemail is not itself
    /// suspicious and contributes nothing to the score.
    /// </summary>
    [JsonPropertyName("is_freemail")]
    public bool IsFreemail { get; set; }

    /// <summary>The top-level domain exists in the current TLD registry.</summary>
    [JsonPropertyName("tld_valid")]
    public bool TldValid { get; set; }

    /// <summary><c>low</c> | <c>medium</c> | <c>high</c> — how often that TLD is abused for throwaway signups.</summary>
    [JsonPropertyName("tld_risk")]
    public string TldRisk { get; set; } = "low";

    /// <summary>The domain part, normalised to lower case.</summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; set; }
}

/// <summary>Phone checks. Also returned on its own by <c>GET /v1/phone/{e164}</c>.</summary>
public sealed class PhoneChecks
{
    /// <summary>The number is valid for its region.</summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    /// <summary>ISO alpha-2 region the number belongs to.</summary>
    [JsonPropertyName("region")]
    public string? Region { get; set; }

    /// <summary>
    /// <c>mobile</c> | <c>fixed</c> | <c>fixed_or_mobile</c> | <c>voip</c> | <c>toll_free</c> |
    /// <c>premium_rate</c> | <c>unknown</c>.
    /// </summary>
    [JsonPropertyName("line_type")]
    public string LineType { get; set; } = "unknown";

    /// <summary>The number normalised to E.164 — store this form.</summary>
    [JsonPropertyName("e164")]
    public string? E164 { get; set; }

    /// <summary>Human-readable country name for the region.</summary>
    [JsonPropertyName("country_name")]
    public string? CountryName { get; set; }

    /// <summary>
    /// Original carrier for the number range, where the metadata knows it. Always null for US numbers —
    /// number portability means no reliable US carrier data exists.
    /// </summary>
    [JsonPropertyName("carrier")]
    public string? Carrier { get; set; }

    /// <summary>Geographic description of the number range, e.g. "San Francisco, CA".</summary>
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    /// <summary>
    /// The number appears in recent consumer scam/unwanted-call complaints. A WEAK signal — caller IDs are
    /// frequently spoofed, so the owner may be a victim — hence a low score weight. US/NANP numbers only.
    /// </summary>
    [JsonPropertyName("reported_scam")]
    public bool ReportedScam { get; set; }
}

/// <summary>Country checks. Returned when a shipping country was supplied.</summary>
public sealed class CountryChecks
{
    /// <summary>The country you passed, echoed back.</summary>
    [JsonPropertyName("shipping_country")]
    public string? ShippingCountry { get; set; }

    /// <summary>English name for <see cref="ShippingCountry"/> (ISO 3166 decode). Null when unknown.</summary>
    [JsonPropertyName("shipping_country_name")]
    public string? ShippingCountryName { get; set; }

    /// <summary>The supplied code is a recognized ISO 3166 country ("UK" is accepted as an alias of GB).
    /// When false the sanctions/risk fields are defaults and the code is excluded from the mismatch check —
    /// an unrecognized code is almost always a typo on the caller's side.</summary>
    [JsonPropertyName("shipping_country_valid")]
    public bool ShippingCountryValid { get; set; }

    /// <summary>The country appears on a sanctions list.</summary>
    [JsonPropertyName("shipping_country_sanctioned")]
    public bool ShippingCountrySanctioned { get; set; }

    /// <summary><c>standard</c> | <c>monitored</c> | <c>high_risk</c>, derived from the FATF lists.</summary>
    [JsonPropertyName("risk_tier")]
    public string RiskTier { get; set; } = "standard";

    /// <summary>The phone, IP and shipping countries disagree in a way worth flagging.</summary>
    [JsonPropertyName("country_mismatch")]
    public bool CountryMismatch { get; set; }
}

/// <summary>
/// Sanctions name-screening result. Also returned on its own by <c>GET /v1/name/{name}</c>. A CHECK, not a
/// compliance determination: <see cref="SanctionsMatch"/> means the name lines up with a sanctions-list entry
/// and warrants review — names collide, so it is not proof of identity, and a non-match is not clearance.
/// </summary>
public sealed class NameChecks
{
    /// <summary>The name you screened, echoed back.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The name matches a listed party's primary name or a known alias. Review-worthy, not a verdict.</summary>
    [JsonPropertyName("sanctions_match")]
    public bool SanctionsMatch { get; set; }

    /// <summary><c>none</c> | <c>exact</c> | <c>reordered</c> | <c>contains</c> (whole-word partial overlap
    /// with a listed multi-word company name — weaker, scored lower) — how the BEST match was made.</summary>
    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = "none";

    /// <summary>The best-matching listed name. Null when no match.</summary>
    [JsonPropertyName("matched_name")]
    public string? MatchedName { get; set; }

    /// <summary><c>individual</c> (person) | <c>entity</c> (company). Null when no match.</summary>
    [JsonPropertyName("matched_type")]
    public string? MatchedType { get; set; }

    /// <summary>The sanctions program(s) the best-matching entry falls under. Null when no match.</summary>
    [JsonPropertyName("program")]
    public string? Program { get; set; }

    /// <summary>
    /// Every distinct listed party the name lined up with, best first (exact, then reordered, then contains),
    /// capped at 10. The top-level fields mirror the first entry. Null when there is no match.
    /// </summary>
    [JsonPropertyName("matches")]
    public IReadOnlyList<NameMatchEntry>? Matches { get; set; }
}

/// <summary>One listed party a screened name lined up with. See <see cref="NameChecks"/> for semantics.</summary>
public sealed class NameMatchEntry
{
    /// <summary><c>exact</c> | <c>reordered</c> | <c>contains</c>.</summary>
    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = default!;

    /// <summary>The listed name (or alias) that matched.</summary>
    [JsonPropertyName("matched_name")]
    public string? MatchedName { get; set; }

    /// <summary><c>individual</c> (person) | <c>entity</c> (company).</summary>
    [JsonPropertyName("matched_type")]
    public string? MatchedType { get; set; }

    /// <summary>The sanctions program(s) this entry falls under.</summary>
    [JsonPropertyName("program")]
    public string? Program { get; set; }
}

/// <summary>
/// The reason codes the API can return. Constants rather than an enum on purpose: new codes can appear at any
/// time, and an unknown value must not break your deserialization or your switch.
/// </summary>
public static class ReasonCodes
{
    // Email
    public const string DisposableEmail = "DISPOSABLE_EMAIL";
    public const string EmailSyntaxInvalid = "EMAIL_SYNTAX_INVALID";
    public const string EmailNoMx = "EMAIL_NO_MX";
    public const string HighRiskTld = "HIGH_RISK_TLD";
    public const string MediumRiskTld = "MEDIUM_RISK_TLD";
    public const string RoleAccount = "ROLE_ACCOUNT";
    /// <summary>The domain has MX but publishes neither SPF nor DMARC — its email isn't set up correctly.</summary>
    public const string EmailAuthMissing = "EMAIL_AUTH_MISSING";

    // Phone
    public const string PhoneInvalid = "PHONE_INVALID";
    public const string PhoneVoip = "PHONE_VOIP";
    public const string PhonePremiumRate = "PHONE_PREMIUM_RATE";
    /// <summary>The number appears in recent consumer scam-call complaints. Weak — caller IDs are spoofed.</summary>
    public const string ReportedScamPhone = "REPORTED_SCAM_PHONE";

    // Name
    /// <summary>The screened name matches a sanctions-list entry. Review-worthy, not a verdict.</summary>
    public const string SanctionedName = "SANCTIONED_NAME";
    /// <summary>The company name only PARTIALLY overlaps listed company name(s) (whole words). Weaker than SANCTIONED_NAME; emitted instead of it.</summary>
    public const string SanctionedNamePartial = "SANCTIONED_NAME_PARTIAL";

    // Country
    public const string SanctionedCountry = "SANCTIONED_COUNTRY";
    public const string FatfHighRisk = "FATF_HIGH_RISK";
    public const string FatfMonitored = "FATF_MONITORED";
    public const string CountryMismatch = "COUNTRY_MISMATCH";
    public const string CountryMismatchVpn = "COUNTRY_MISMATCH_VPN";

    // IP
    /// <summary>The IP address is located in a comprehensively sanctioned country.</summary>
    public const string SanctionedIp = "SANCTIONED_IP";
    /// <summary>The IP is on a network flagged as hostile (hijacked/criminal-controlled infrastructure).</summary>
    public const string KnownBadNetwork = "KNOWN_BAD_NETWORK";
    public const string TorExitNode = "TOR_EXIT_NODE";
    public const string DatacenterIp = "DATACENTER_IP";
    /// <summary>Known commercial VPN exit. Emitted instead of DATACENTER_IP when both apply.</summary>
    public const string VpnIp = "VPN_IP";
    public const string CloudHostingIp = "CLOUD_HOSTING_IP";
}

/// <summary>
/// The stable <c>code</c> values on an error response. Switch on these, not on the message — messages get
/// reworded, codes don't.
/// </summary>
public static class ErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string MissingInput = "missing_input";
    public const string InvalidIp = "invalid_ip";
    public const string InvalidKey = "invalid_key";
    /// <summary>The key requires HMAC signing and the signature was missing or wrong.</summary>
    public const string InvalidSignature = "invalid_signature";
    /// <summary>The credential is valid but lacks the scope this endpoint needs.</summary>
    public const string InsufficientScope = "insufficient_scope";
    public const string BatchNotAvailable = "batch_not_available";
    public const string IpNotAllowed = "ip_not_allowed";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string RateLimited = "rate_limited";
    public const string QuotaExceeded = "quota_exceeded";
    public const string SpendCapReached = "spend_cap_reached";
    public const string InternalError = "internal_error";
}
