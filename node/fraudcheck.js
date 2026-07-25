/**
 * FraudCheck API — official Node.js client.
 *
 * Screen an IP, email, phone and shipping country in one call and get per-check results, a composite advisory
 * score, and stable reason codes.
 *
 * Checks, not verdicts. There is no `is_fraud` field, and there never will be — you get the checks and make the
 * decision. Results are derived from publicly available databases and provided as-is: treat them as input to
 * your judgement, not as fact. Prefer reacting to specific `reasons` codes over thresholding the score — a
 * datacenter IP is often just a VPN, whereas a sanctioned country is a compliance question.
 *
 * Zero dependencies: uses the global `fetch` built into Node 18+. Works in CommonJS and ESM.
 *
 *   const { FraudCheckClient, ReasonCodes } = require("./fraudcheck");
 *   const client = new FraudCheckClient("fck_live_your_key_here");
 *   const result = await client.screen({ ip: "8.8.8.8", email: "buyer@example.com", shippingCountry: "US" });
 *   if (result.country?.shipping_country_sanctioned) holdForReview(order);
 */

"use strict";

const crypto = require("crypto");

const DEFAULT_BASE_URL = "https://fraudcheckapi.com";
const DEFAULT_TIMEOUT_MS = 10_000; // screening sits in checkout paths; fail open fast rather than hang

/**
 * Thrown when the API returns an error. Branch on `code` (see ErrorCodes), not the message or the raw status —
 * codes are contractual, messages are prose.
 */
class FraudCheckError extends Error {
  constructor(code, message, status) {
    super(`${code} (${status}): ${message}`);
    this.name = "FraudCheckError";
    this.code = code;
    this.apiMessage = message;
    this.status = status;
  }
  /** True when retrying later could plausibly succeed (rate limits, server errors). */
  get isTransient() {
    return this.code === ErrorCodes.RATE_LIMITED || this.code === ErrorCodes.INTERNAL_ERROR || this.status >= 500;
  }
}

/** Your key is missing, wrong or revoked (HTTP 401). Retrying never helps — fix the configuration. */
class FraudCheckAuthError extends FraudCheckError {
  constructor(code, message, status) {
    super(code, message, status);
    this.name = "FraudCheckAuthError";
  }
}

/** A limit was hit (HTTP 429). `code` says which; `retryAfter` (seconds) is set when the server gave one. */
class FraudCheckRateLimitError extends FraudCheckError {
  constructor(code, message, status, retryAfter) {
    super(code, message, status);
    this.name = "FraudCheckRateLimitError";
    this.retryAfter = retryAfter;
  }
}

class FraudCheckClient {
  /**
   * @param {string} apiKey  Your API key. Server-side only; never ship one in browser code.
   * @param {{ baseUrl?: string, timeoutMs?: number }} [options]
   */
  constructor(apiKey, options = {}) {
    if (!apiKey || !String(apiKey).trim()) {
      throw new Error("An API key is required. Create one in your FraudCheck dashboard.");
    }
    this._apiKey = String(apiKey).trim();
    this._baseUrl = (options.baseUrl || DEFAULT_BASE_URL).replace(/\/+$/, "");
    this._timeoutMs = options.timeoutMs || DEFAULT_TIMEOUT_MS;
  }

  // -- endpoints ---------------------------------------------------------------------------------------

  /**
   * Screen any combination of inputs in one call. Supply at least one of ip/email/phone/shippingCountry/name.
   * Returns the full result object: score, per-check blocks, and reasons. `rdns: true` opts in to
   * reverse-DNS fields on the ip block (adds a live DNS lookup to the call).
   */
  async screen({ ip, email, phone, shippingCountry, name, rdns } = {}) {
    const body = {};
    if (ip != null) body.ip = ip;
    if (email != null) body.email = email;
    if (phone != null) body.phone = phone;
    if (shippingCountry != null) body.shipping_country = shippingCountry;
    if (name != null) body.name = name;
    if (Object.keys(body).length === 0) {
      // Fail here rather than spend a request to be told missing_input.
      throw new Error("Supply at least one of ip, email, phone, shippingCountry or name.");
    }
    if (rdns) body.rdns = true;
    return this._request("POST", "/v1/screen", body);
  }

  /**
   * IP checks on their own: country, ASN, Tor/datacenter/cloud, proxy likelihood, and a location block.
   * `rdns: true` opts in to reverse-DNS fields (adds a live DNS lookup to the call).
   */
  async checkIp(ip, { rdns } = {}) {
    requireValue(ip, "ip");
    return this._request("GET", "/v1/ip/" + encodeURIComponent(ip) + (rdns ? "?rdns=true" : ""));
  }

  /** Email checks: syntax, MX, disposable, role, freemail, TLD risk. */
  async checkEmail(email) {
    requireValue(email, "email");
    return this._request("GET", "/v1/email/" + encodeURIComponent(email));
  }

  /** Phone checks. Pass E.164 (`+14155552671`), or a national number plus `region` to parse against. */
  async checkPhone(phone, region) {
    requireValue(phone, "phone");
    // encodeURIComponent encodes the leading '+' as %2B; a raw '+' in a path would be misread as a space.
    let path = "/v1/phone/" + encodeURIComponent(phone);
    if (region) path += "?region=" + encodeURIComponent(region);
    return this._request("GET", path);
  }

  /**
   * Full geolocation for an IP. Location data, not a risk check — non-routable addresses come back with
   * `bogon: true` rather than an error.
   */
  async geolocate(ip) {
    requireValue(ip, "ip");
    return this._request("GET", "/v1/geo/" + encodeURIComponent(ip));
  }

  /**
   * Sanctions name screening for a person or company name (primary listed names + known aliases; normalised, not fuzzy). A check, not a compliance
   * determination — a match is review-worthy (names collide); a non-match is not clearance.
   */
  async checkName(name) {
    requireValue(name, "name");
    return this._request("GET", "/v1/name/" + encodeURIComponent(name));
  }

  /**
   * The live reason-code catalog: every code with its current weight and meaning. Fetch once and cache
   * (it's metered like any call) — ideal for review UIs that should never hardcode explanations.
   */
  async reasons() {
    return this._request("GET", "/v1/reasons");
  }

  /**
   * Screen up to 100 records in one call (plan-gated: rejects with code `batch_not_available` when the plan
   * doesn't include batch). `items` entries take { ip, email, phone, shippingCountry }. Results align by
   * index; a bad item fails alone inside the response, never the whole batch.
   */
  async screenBatch(items) {
    if (!Array.isArray(items) || items.length === 0 || items.length > 100) {
      throw new Error("Supply 1-100 items.");
    }
    const mapped = items.map((i) => {
      const item = {};
      if (i.ip != null) item.ip = i.ip;
      if (i.email != null) item.email = i.email;
      if (i.phone != null) item.phone = i.phone;
      if (i.shippingCountry != null) item.shipping_country = i.shippingCountry;
      return item;
    });
    return this._request("POST", "/v1/screen/batch", { items: mapped });
  }

  // -- transport ---------------------------------------------------------------------------------------

  async _request(method, path, body) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this._timeoutMs);
    let response;
    try {
      response = await fetch(this._baseUrl + path, {
        method,
        headers: {
          "X-Api-Key": this._apiKey,
          Accept: "application/json",
          ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
        signal: controller.signal,
      });
    } finally {
      clearTimeout(timer);
    }

    const text = await response.text();
    if (!response.ok) {
      throw toError(response, text);
    }
    return text ? JSON.parse(text) : {};
  }
}

function requireValue(value, name) {
  if (value == null || String(value).trim() === "") {
    throw new Error(`${name} is required.`);
  }
}

function toError(response, text) {
  const status = response.status;
  let code = ErrorCodes.INTERNAL_ERROR;
  let message = `The API returned ${status}.`;
  try {
    const payload = JSON.parse(text);
    if (payload.code) code = payload.code;
    if (payload.message) message = payload.message;
  } catch {
    // A proxy/gateway may not speak our error shape — keep the status regardless.
  }

  if (status === 401) return new FraudCheckAuthError(code, message, status);
  if (status === 429) {
    let retryAfter = null;
    const raw = response.headers.get("retry-after");
    if (raw && /^\d+$/.test(raw.trim())) retryAfter = Number(raw.trim());
    return new FraudCheckRateLimitError(code, message, status, retryAfter);
  }
  return new FraudCheckError(code, message, status);
}

/**
 * The reason codes the API can return. Plain string constants on purpose: new codes can appear at any time and
 * an unknown value must not break your code.
 */
const ReasonCodes = Object.freeze({
  // Email
  DISPOSABLE_EMAIL: "DISPOSABLE_EMAIL",
  EMAIL_SYNTAX_INVALID: "EMAIL_SYNTAX_INVALID",
  EMAIL_NO_MX: "EMAIL_NO_MX",
  HIGH_RISK_TLD: "HIGH_RISK_TLD",
  MEDIUM_RISK_TLD: "MEDIUM_RISK_TLD",
  ROLE_ACCOUNT: "ROLE_ACCOUNT",
  EMAIL_AUTH_MISSING: "EMAIL_AUTH_MISSING",
  // Phone
  PHONE_INVALID: "PHONE_INVALID",
  PHONE_VOIP: "PHONE_VOIP",
  PHONE_PREMIUM_RATE: "PHONE_PREMIUM_RATE",
  REPORTED_SCAM_PHONE: "REPORTED_SCAM_PHONE",
  // Name
  SANCTIONED_NAME: "SANCTIONED_NAME",
  SANCTIONED_NAME_PARTIAL: "SANCTIONED_NAME_PARTIAL",
  // Country
  SANCTIONED_COUNTRY: "SANCTIONED_COUNTRY",
  FATF_HIGH_RISK: "FATF_HIGH_RISK",
  FATF_MONITORED: "FATF_MONITORED",
  COUNTRY_MISMATCH: "COUNTRY_MISMATCH",
  COUNTRY_MISMATCH_VPN: "COUNTRY_MISMATCH_VPN",
  // IP
  SANCTIONED_IP: "SANCTIONED_IP",
  KNOWN_BAD_NETWORK: "KNOWN_BAD_NETWORK",
  TOR_EXIT_NODE: "TOR_EXIT_NODE",
  DATACENTER_IP: "DATACENTER_IP",
  VPN_IP: "VPN_IP",
  CLOUD_HOSTING_IP: "CLOUD_HOSTING_IP",
});

/** Stable `code` values on an error response. Switch on these, not the message. */
const ErrorCodes = Object.freeze({
  INVALID_REQUEST: "invalid_request",
  MISSING_INPUT: "missing_input",
  INVALID_IP: "invalid_ip",
  INVALID_KEY: "invalid_key",
  INVALID_SIGNATURE: "invalid_signature",
  INSUFFICIENT_SCOPE: "insufficient_scope",
  BATCH_NOT_AVAILABLE: "batch_not_available",
  IP_NOT_ALLOWED: "ip_not_allowed",
  NOT_FOUND: "not_found",
  METHOD_NOT_ALLOWED: "method_not_allowed",
  RATE_LIMITED: "rate_limited",
  QUOTA_EXCEEDED: "quota_exceeded",
  SPEND_CAP_REACHED: "spend_cap_reached",
  INTERNAL_ERROR: "internal_error",
});

/**
 * Verify outbound webhook deliveries from FraudCheck. Each POST is signed with the endpoint's `whsec_`
 * secret; verify it before trusting the payload — anyone can POST JSON at a public URL. Pass the RAW request
 * body (Buffer or string, exactly as received — do not re-serialize):
 *
 *   const { Webhooks } = require("./fraudcheck");
 *   const ok = Webhooks.verify({
 *     secret: MY_ENDPOINT_SECRET,
 *     timestamp: req.headers["x-fraudcheck-timestamp"],
 *     body: rawBody,
 *     signature: req.headers["x-fraudcheck-signature"],
 *   });
 *   if (!ok) return res.status(400).end();
 */
const Webhooks = Object.freeze({
  EVENT_HEADER: "X-FraudCheck-Event",
  DELIVERY_HEADER: "X-FraudCheck-Delivery",
  TIMESTAMP_HEADER: "X-FraudCheck-Timestamp",
  SIGNATURE_HEADER: "X-FraudCheck-Signature",

  /** The expected `v1=…` signature value for a (timestamp, body) pair. */
  sign(secret, timestamp, body) {
    const mac = crypto.createHmac("sha256", secret);
    mac.update(`${timestamp}.`);
    mac.update(body);
    return "v1=" + mac.digest("hex");
  },

  /**
   * True when the signature matches and (if toleranceSeconds > 0) the timestamp is recent. Constant-time
   * compare, so a mismatch leaks nothing about how close it was.
   */
  verify({ secret, timestamp, body, signature, toleranceSeconds = 300 }) {
    if (!signature) return false;
    if (toleranceSeconds > 0) {
      const ts = Number(timestamp);
      if (!Number.isFinite(ts) || Math.abs(Date.now() / 1000 - ts) > toleranceSeconds) return false;
    }
    const expected = Buffer.from(this.sign(secret, timestamp, body), "utf8");
    const provided = Buffer.from(signature, "utf8");
    return expected.length === provided.length && crypto.timingSafeEqual(expected, provided);
  },
});

module.exports = {
  FraudCheckClient,
  FraudCheckError,
  FraudCheckAuthError,
  FraudCheckRateLimitError,
  ReasonCodes,
  ErrorCodes,
  Webhooks,
};
