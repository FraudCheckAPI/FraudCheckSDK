<?php

/**
 * FraudCheck API — official PHP client.
 *
 * Screen an IP, email, phone and shipping country in one call and get per-check results, a composite advisory
 * score, and stable reason codes.
 *
 * Checks, not verdicts. There is no `is_fraud` field, and there never will be — you get the checks and make the
 * decision. Results are derived from publicly available databases and provided as-is: treat them as input to
 * your judgement, not as fact. Prefer reacting to specific reason codes over thresholding the score — a
 * datacenter IP is often just a VPN, whereas a sanctioned country is a compliance question.
 *
 * Zero dependencies: uses the cURL extension that ships with PHP. Requires PHP 7.4+.
 *
 *   require "FraudCheck.php";
 *   use FraudCheck\FraudCheckClient;
 *   use FraudCheck\ReasonCodes;
 *
 *   $client = new FraudCheckClient("fck_live_your_key_here");
 *   $result = $client->screen(["ip" => "8.8.8.8", "email" => "buyer@example.com", "shipping_country" => "US"]);
 *   if (!empty($result["country"]["shipping_country_sanctioned"])) { holdForReview($order); }
 */

declare(strict_types=1);

namespace FraudCheck;

use Exception;

/**
 * Thrown when the API returns an error. Branch on getCode() (see ErrorCodes), not the message or the raw
 * status — codes are contractual, messages are prose.
 */
class FraudCheckException extends Exception
{
    private string $errorCode;
    private int $status;

    public function __construct(string $errorCode, string $message, int $status)
    {
        parent::__construct(sprintf("%s (%d): %s", $errorCode, $status, $message));
        $this->errorCode = $errorCode;
        $this->status = $status;
    }

    /** The stable machine code, e.g. "quota_exceeded". */
    public function errorCode(): string
    {
        return $this->errorCode;
    }

    public function status(): int
    {
        return $this->status;
    }

    /** True when retrying later could plausibly succeed (rate limits, server errors). */
    public function isTransient(): bool
    {
        return $this->errorCode === ErrorCodes::RATE_LIMITED
            || $this->errorCode === ErrorCodes::INTERNAL_ERROR
            || $this->status >= 500;
    }
}

/** Your key is missing, wrong or revoked (HTTP 401). Retrying never helps — fix the configuration. */
class FraudCheckAuthException extends FraudCheckException
{
}

/** A limit was hit (HTTP 429). errorCode() says which; retryAfter() is set when the server gave one. */
class FraudCheckRateLimitException extends FraudCheckException
{
    private ?int $retryAfter;

    public function __construct(string $errorCode, string $message, int $status, ?int $retryAfter)
    {
        parent::__construct($errorCode, $message, $status);
        $this->retryAfter = $retryAfter;
    }

    /** Seconds the server asked you to wait, or null when it didn't say. */
    public function retryAfter(): ?int
    {
        return $this->retryAfter;
    }
}

class FraudCheckClient
{
    private const DEFAULT_BASE_URL = "https://fraudcheckapi.com";
    private const DEFAULT_TIMEOUT = 10; // screening sits in checkout paths; fail open fast rather than hang

    private string $apiKey;
    private string $baseUrl;
    private int $timeout;

    /**
     * @param string $apiKey  Your API key. Server-side only; never ship one in browser or mobile code.
     * @param array  $options { base_url?: string, timeout?: int }
     */
    public function __construct(string $apiKey, array $options = [])
    {
        $apiKey = trim($apiKey);
        if ($apiKey === "") {
            throw new \InvalidArgumentException("An API key is required. Create one in your FraudCheck dashboard.");
        }
        $this->apiKey = $apiKey;
        $this->baseUrl = rtrim($options["base_url"] ?? self::DEFAULT_BASE_URL, "/");
        $this->timeout = $options["timeout"] ?? self::DEFAULT_TIMEOUT;
    }

    // -- endpoints -------------------------------------------------------------------------------------

    /**
     * Screen any combination of inputs in one call. Supply at least one of ip/email/phone/shipping_country/name.
     * Returns the full result array: score, per-check blocks, and reasons. Set "rdns" => true to opt in to
     * reverse-DNS fields on the ip block (adds a live DNS lookup to the call).
     *
     * @param array $inputs { ip?, email?, phone?, shipping_country?, name?, rdns? }
     */
    public function screen(array $inputs): array
    {
        $body = [];
        foreach (["ip", "email", "phone", "shipping_country", "name"] as $field) {
            if (isset($inputs[$field]) && $inputs[$field] !== "") {
                $body[$field] = $inputs[$field];
            }
        }
        if (count($body) === 0) {
            // Fail here rather than spend a request to be told missing_input.
            throw new \InvalidArgumentException("Supply at least one of ip, email, phone, shipping_country or name.");
        }
        if (!empty($inputs["rdns"])) {
            $body["rdns"] = true;
        }
        return $this->request("POST", "/v1/screen", $body);
    }

    /**
     * IP checks on their own: country, ASN, Tor/datacenter/cloud, proxy likelihood, and a location block.
     * $rdns = true opts in to reverse-DNS fields (adds a live DNS lookup to the call).
     */
    public function checkIp(string $ip, bool $rdns = false): array
    {
        $this->requireValue($ip, "ip");
        return $this->request("GET", "/v1/ip/" . rawurlencode($ip) . ($rdns ? "?rdns=true" : ""));
    }

    /** Email checks: syntax, MX, disposable, role, freemail, TLD risk. */
    public function checkEmail(string $email): array
    {
        $this->requireValue($email, "email");
        return $this->request("GET", "/v1/email/" . rawurlencode($email));
    }

    /** Phone checks. Pass E.164 ("+14155552671"), or a national number plus $region to parse against. */
    public function checkPhone(string $phone, ?string $region = null): array
    {
        $this->requireValue($phone, "phone");
        // rawurlencode encodes the leading '+' as %2B; a raw '+' in a path would be misread as a space.
        $path = "/v1/phone/" . rawurlencode($phone);
        if ($region !== null && $region !== "") {
            $path .= "?region=" . rawurlencode($region);
        }
        return $this->request("GET", $path);
    }

    /**
     * Full geolocation for an IP. Location data, not a risk check — non-routable addresses come back with
     * "bogon" => true rather than an error.
     */
    public function geolocate(string $ip): array
    {
        $this->requireValue($ip, "ip");
        return $this->request("GET", "/v1/geo/" . rawurlencode($ip));
    }

    /**
     * Sanctions name screening for a person or company name (primary listed names + known aliases; normalised, not fuzzy). A check, not a compliance
     * determination — a match is review-worthy (names collide); a non-match is not clearance.
     */
    public function checkName(string $name): array
    {
        $this->requireValue($name, "name");
        return $this->request("GET", "/v1/name/" . rawurlencode($name));
    }

    /**
     * The live reason-code catalog: every code with its current weight and meaning. Fetch once and cache
     * (it's metered like any call) — ideal for review UIs that should never hardcode explanations.
     */
    public function reasons(): array
    {
        return $this->request("GET", "/v1/reasons");
    }

    /**
     * Screen up to 100 records in one call (plan-gated: throws with code batch_not_available when the plan
     * doesn't include batch). Each item is an array with any of ip/email/phone/shipping_country. Results
     * align by index; a bad item fails alone inside the response, never the whole batch.
     */
    public function screenBatch(array $items): array
    {
        if (count($items) === 0 || count($items) > 100) {
            throw new \InvalidArgumentException("Supply 1-100 items.");
        }
        return $this->request("POST", "/v1/screen/batch", ["items" => array_values($items)]);
    }

    // -- transport -------------------------------------------------------------------------------------

    private function requireValue(string $value, string $name): void
    {
        if (trim($value) === "") {
            throw new \InvalidArgumentException("$name is required.");
        }
    }

    private function request(string $method, string $path, ?array $body = null): array
    {
        $ch = curl_init($this->baseUrl . $path);
        $headers = ["X-Api-Key: " . $this->apiKey, "Accept: application/json"];

        curl_setopt($ch, CURLOPT_CUSTOMREQUEST, $method);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_TIMEOUT, $this->timeout);
        // Capture the Retry-After header for 429s.
        $retryAfter = null;
        curl_setopt($ch, CURLOPT_HEADERFUNCTION, function ($ch, $line) use (&$retryAfter) {
            if (stripos($line, "retry-after:") === 0) {
                $val = trim(substr($line, strlen("retry-after:")));
                if (ctype_digit($val)) {
                    $retryAfter = (int) $val;
                }
            }
            return strlen($line);
        });

        if ($body !== null) {
            $headers[] = "Content-Type: application/json";
            curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($body));
        }
        curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);

        $responseBody = curl_exec($ch);
        if ($responseBody === false) {
            $err = curl_error($ch);
            curl_close($ch);
            // Transport failure (timeout, DNS, connection). Model it as a transient server-side error.
            throw new FraudCheckException(ErrorCodes::INTERNAL_ERROR, "Request failed: $err", 0);
        }
        $status = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($status >= 200 && $status < 300) {
            return $responseBody === "" ? [] : (json_decode($responseBody, true) ?? []);
        }
        throw self::toError($status, (string) $responseBody, $retryAfter);
    }

    private static function toError(int $status, string $body, ?int $retryAfter): FraudCheckException
    {
        $code = ErrorCodes::INTERNAL_ERROR;
        $message = "The API returned $status.";
        $payload = json_decode($body, true);
        if (is_array($payload)) {
            // A proxy/gateway may not speak our error shape — keep the status regardless.
            if (!empty($payload["code"])) {
                $code = $payload["code"];
            }
            if (!empty($payload["message"])) {
                $message = $payload["message"];
            }
        }

        if ($status === 401) {
            return new FraudCheckAuthException($code, $message, $status);
        }
        if ($status === 429) {
            return new FraudCheckRateLimitException($code, $message, $status, $retryAfter);
        }
        return new FraudCheckException($code, $message, $status);
    }
}

/**
 * The reason codes the API can return. Plain string constants on purpose: new codes can appear at any time and
 * an unknown value must not break your code.
 */
class ReasonCodes
{
    // Email
    public const DISPOSABLE_EMAIL = "DISPOSABLE_EMAIL";
    public const EMAIL_SYNTAX_INVALID = "EMAIL_SYNTAX_INVALID";
    public const EMAIL_NO_MX = "EMAIL_NO_MX";
    public const HIGH_RISK_TLD = "HIGH_RISK_TLD";
    public const MEDIUM_RISK_TLD = "MEDIUM_RISK_TLD";
    public const ROLE_ACCOUNT = "ROLE_ACCOUNT";
    public const EMAIL_AUTH_MISSING = "EMAIL_AUTH_MISSING";
    // Phone
    public const PHONE_INVALID = "PHONE_INVALID";
    public const PHONE_VOIP = "PHONE_VOIP";
    public const PHONE_PREMIUM_RATE = "PHONE_PREMIUM_RATE";
    public const REPORTED_SCAM_PHONE = "REPORTED_SCAM_PHONE";
    // Name
    public const SANCTIONED_NAME = "SANCTIONED_NAME";
    public const SANCTIONED_NAME_PARTIAL = "SANCTIONED_NAME_PARTIAL";
    // Country
    public const SANCTIONED_COUNTRY = "SANCTIONED_COUNTRY";
    public const FATF_HIGH_RISK = "FATF_HIGH_RISK";
    public const FATF_MONITORED = "FATF_MONITORED";
    public const COUNTRY_MISMATCH = "COUNTRY_MISMATCH";
    public const COUNTRY_MISMATCH_VPN = "COUNTRY_MISMATCH_VPN";
    // IP
    public const SANCTIONED_IP = "SANCTIONED_IP";
    public const KNOWN_BAD_NETWORK = "KNOWN_BAD_NETWORK";
    public const VPN_IP = "VPN_IP";
    public const TOR_EXIT_NODE = "TOR_EXIT_NODE";
    public const DATACENTER_IP = "DATACENTER_IP";
    public const CLOUD_HOSTING_IP = "CLOUD_HOSTING_IP";
}

/** Stable "code" values on an error response. Switch on these, not the message. */
class ErrorCodes
{
    public const INVALID_REQUEST = "invalid_request";
    public const MISSING_INPUT = "missing_input";
    public const INVALID_IP = "invalid_ip";
    public const INVALID_KEY = "invalid_key";
    public const INVALID_SIGNATURE = "invalid_signature";
    public const INSUFFICIENT_SCOPE = "insufficient_scope";
    public const BATCH_NOT_AVAILABLE = "batch_not_available";
    public const IP_NOT_ALLOWED = "ip_not_allowed";
    public const NOT_FOUND = "not_found";
    public const METHOD_NOT_ALLOWED = "method_not_allowed";
    public const RATE_LIMITED = "rate_limited";
    public const QUOTA_EXCEEDED = "quota_exceeded";
    public const SPEND_CAP_REACHED = "spend_cap_reached";
    public const INTERNAL_ERROR = "internal_error";
}

/**
 * Verify outbound webhook deliveries from FraudCheck. Each POST is signed with the endpoint's whsec_ secret;
 * verify it before trusting the payload — anyone can POST JSON at a public URL. Pass the RAW request body
 * exactly as received (file_get_contents('php://input')), not a re-encoded array:
 *
 *   $ok = FraudCheck\Webhooks::verify(
 *       $mySecret,
 *       $_SERVER['HTTP_X_FRAUDCHECK_TIMESTAMP'],
 *       file_get_contents('php://input'),
 *       $_SERVER['HTTP_X_FRAUDCHECK_SIGNATURE']
 *   );
 *   if (!$ok) { http_response_code(400); exit; }
 */
class Webhooks
{
    public const EVENT_HEADER = "X-FraudCheck-Event";
    public const DELIVERY_HEADER = "X-FraudCheck-Delivery";
    public const TIMESTAMP_HEADER = "X-FraudCheck-Timestamp";
    public const SIGNATURE_HEADER = "X-FraudCheck-Signature";

    /** The expected "v1=…" signature value for a (timestamp, body) pair. */
    public static function sign(string $secret, string $timestamp, string $body): string
    {
        return "v1=" . hash_hmac("sha256", $timestamp . "." . $body, $secret);
    }

    /**
     * True when the signature matches and (if $toleranceSeconds > 0) the timestamp is recent. Uses
     * hash_equals for a constant-time compare.
     */
    public static function verify(
        string $secret,
        string $timestamp,
        string $body,
        string $signature,
        int $toleranceSeconds = 300
    ): bool {
        if ($signature === "") {
            return false;
        }
        if ($toleranceSeconds > 0) {
            if (!is_numeric($timestamp) || abs(time() - (int) $timestamp) > $toleranceSeconds) {
                return false;
            }
        }
        return hash_equals(self::sign($secret, $timestamp, $body), $signature);
    }
}
