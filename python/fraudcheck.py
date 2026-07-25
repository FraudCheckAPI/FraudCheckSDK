"""
FraudCheck API — official Python client.

Screen an IP, email, phone and shipping country in one call and get per-check results, a composite advisory
score, and stable reason codes.

Checks, not verdicts. There is no ``is_fraud`` field, and there never will be — you get the checks and make the
decision. Results are derived from publicly available databases and provided as-is: treat them as input to your
judgement, not as fact. Prefer reacting to specific ``reasons`` codes over thresholding the score — a datacenter
IP is often just a VPN, whereas a sanctioned country is a compliance question.

Zero dependencies: uses only the standard library, so you can drop this file into any project. Requires
Python 3.8+.

    from fraudcheck import FraudCheckClient, ReasonCodes

    client = FraudCheckClient("fck_live_your_key_here")
    result = client.screen(ip="8.8.8.8", email="buyer@example.com", shipping_country="US")
    print(result["score"], result["reasons"])
    if result.get("country", {}).get("shipping_country_sanctioned"):
        hold_for_review(order)
"""

from __future__ import annotations

import hashlib
import hmac
import json
import time
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Dict, List, Optional

__all__ = [
    "FraudCheckClient",
    "FraudCheckError",
    "FraudCheckAuthError",
    "FraudCheckRateLimitError",
    "ReasonCodes",
    "ErrorCodes",
]

_DEFAULT_BASE_URL = "https://fraudcheckapi.com"
_DEFAULT_TIMEOUT = 10.0  # seconds — screening sits in checkout paths; fail open fast rather than hang


class FraudCheckError(Exception):
    """
    Raised when the API returns an error. Branch on ``code`` (see :class:`ErrorCodes`), not the message or the
    raw status — codes are contractual, messages are prose.
    """

    def __init__(self, code: str, message: str, status: int):
        super().__init__(f"{code} ({status}): {message}")
        self.code = code
        self.message = message
        self.status = status

    @property
    def is_transient(self) -> bool:
        """True when retrying later could plausibly succeed (rate limits, server errors)."""
        return self.code in (ErrorCodes.RATE_LIMITED, ErrorCodes.INTERNAL_ERROR) or self.status >= 500


class FraudCheckAuthError(FraudCheckError):
    """Your key is missing, wrong or revoked (HTTP 401). Retrying never helps — fix the configuration."""


class FraudCheckRateLimitError(FraudCheckError):
    """
    A limit was hit (HTTP 429): the per-second rate, the monthly quota, or the overage spend cap — ``code``
    says which. ``retry_after`` is the server's ``Retry-After`` in seconds when it gave one.
    """

    def __init__(self, code: str, message: str, status: int, retry_after: Optional[float]):
        super().__init__(code, message, status)
        self.retry_after = retry_after


class FraudCheckClient:
    """
    Thread-safe client for the FraudCheck API. Create one and keep it — it holds no per-request state.

    :param api_key: your API key. Server-side only; never ship one in browser or mobile code.
    :param base_url: the API root. Only change this to point at a private deployment.
    :param timeout: per-request timeout in seconds (default 10).
    """

    def __init__(
        self,
        api_key: str,
        base_url: str = _DEFAULT_BASE_URL,
        timeout: float = _DEFAULT_TIMEOUT,
    ):
        if not api_key or not api_key.strip():
            raise ValueError("An API key is required. Create one in your FraudCheck dashboard.")
        self._api_key = api_key.strip()
        self._base_url = base_url.rstrip("/")
        self._timeout = timeout

    # -- endpoints -----------------------------------------------------------------------------------------

    def screen(
        self,
        ip: Optional[str] = None,
        email: Optional[str] = None,
        phone: Optional[str] = None,
        shipping_country: Optional[str] = None,
        name: Optional[str] = None,
        rdns: bool = False,
    ) -> Dict[str, Any]:
        """
        Screen any combination of inputs in one call. Supply at least one.

        Returns the full result dict: ``score``, per-check blocks (``ip``/``email``/``phone``/``country``/
        ``name``), and ``reasons``. ``rdns=True`` opts in to reverse-DNS fields on the ip block (adds a
        live DNS lookup to the call).
        """
        body = {
            k: v
            for k, v in (
                ("ip", ip),
                ("email", email),
                ("phone", phone),
                ("shipping_country", shipping_country),
                ("name", name),
            )
            if v is not None
        }
        if not body:
            # Fail here rather than spend a request to be told missing_input.
            raise ValueError("Supply at least one of ip, email, phone, shipping_country or name.")
        if rdns:
            body["rdns"] = True
        return self._request("POST", "/v1/screen", body=body)

    def check_ip(self, ip: str, rdns: bool = False) -> Dict[str, Any]:
        """
        IP checks on their own: country, ASN, Tor/datacenter/cloud, proxy likelihood, and a location block.
        ``rdns=True`` opts in to reverse-DNS fields (adds a live DNS lookup to the call).
        """
        self._require(ip, "ip")
        return self._request(
            "GET", "/v1/ip/" + urllib.parse.quote(ip, safe="") + ("?rdns=true" if rdns else ""))

    def check_email(self, email: str) -> Dict[str, Any]:
        """Email checks: syntax, MX, disposable, role, freemail, TLD risk."""
        self._require(email, "email")
        return self._request("GET", "/v1/email/" + urllib.parse.quote(email, safe=""))

    def check_phone(self, phone: str, region: Optional[str] = None) -> Dict[str, Any]:
        """
        Phone checks. Pass E.164 (``+14155552671``), or a national number plus ``region`` to parse against.
        """
        self._require(phone, "phone")
        # quote() with safe="" encodes the leading '+' as %2B, which a raw '+' in a path would misread as space.
        path = "/v1/phone/" + urllib.parse.quote(phone, safe="")
        if region:
            path += "?region=" + urllib.parse.quote(region, safe="")
        return self._request("GET", path)

    def geolocate(self, ip: str) -> Dict[str, Any]:
        """
        Full geolocation for an IP. Location data, not a risk check — non-routable addresses come back with
        ``bogon: true`` rather than an error.
        """
        self._require(ip, "ip")
        return self._request("GET", "/v1/geo/" + urllib.parse.quote(ip, safe=""))

    def check_name(self, name: str) -> Dict[str, Any]:
        """
        Sanctions name screening for a person or company name (primary listed names + known aliases; normalised, not fuzzy). A check, not a compliance
        determination — a match is review-worthy (names collide); a non-match is not clearance.
        """
        self._require(name, "name")
        return self._request("GET", "/v1/name/" + urllib.parse.quote(name, safe=""))

    def reasons(self) -> Any:
        """
        The live reason-code catalog: every code with its current weight and meaning. Fetch once and cache
        (it's metered like any call) — ideal for review UIs that should never hardcode explanations.
        """
        return self._request("GET", "/v1/reasons")

    def screen_batch(self, items):
        """
        Screen up to 100 records in one call (plan-gated: raises with code ``batch_not_available``
        when your plan doesn't include batch). ``items`` is a list of dicts with any of
        ``ip``/``email``/``phone``/``shipping_country``. Results align by index; a bad item fails
        alone inside the response, never the whole batch.
        """
        if not items or len(items) > 100:
            raise ValueError("Supply 1-100 items.")
        return self._request("POST", "/v1/screen/batch", {"items": items})

    # -- transport -----------------------------------------------------------------------------------------

    @staticmethod
    def _require(value: str, name: str) -> None:
        if not value or not str(value).strip():
            raise ValueError(f"{name} is required.")

    def _request(self, method: str, path: str, body: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        data = json.dumps(body).encode("utf-8") if body is not None else None
        req = urllib.request.Request(self._base_url + path, data=data, method=method)
        req.add_header("X-Api-Key", self._api_key)
        req.add_header("Accept", "application/json")
        if data is not None:
            req.add_header("Content-Type", "application/json")

        try:
            with urllib.request.urlopen(req, timeout=self._timeout) as resp:
                return json.loads(resp.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            raise self._to_error(exc) from None

    @staticmethod
    def _to_error(exc: "urllib.error.HTTPError") -> FraudCheckError:
        status = exc.code
        code = ErrorCodes.INTERNAL_ERROR
        message = f"The API returned {status}."
        try:
            payload = json.loads(exc.read().decode("utf-8"))
            code = payload.get("code") or code
            message = payload.get("message") or message
        except Exception:  # a proxy/gateway may not speak our error shape — keep the status regardless
            pass

        if status == 401:
            return FraudCheckAuthError(code, message, status)
        if status == 429:
            retry_after = None
            raw = exc.headers.get("Retry-After") if exc.headers else None
            if raw and raw.strip().isdigit():
                retry_after = float(raw.strip())
            return FraudCheckRateLimitError(code, message, status, retry_after)
        return FraudCheckError(code, message, status)


class ReasonCodes:
    """
    The reason codes the API can return. Plain string constants (not an enum) on purpose: new codes can appear
    at any time and an unknown value must not break your code.
    """

    # Email
    DISPOSABLE_EMAIL = "DISPOSABLE_EMAIL"
    EMAIL_SYNTAX_INVALID = "EMAIL_SYNTAX_INVALID"
    EMAIL_NO_MX = "EMAIL_NO_MX"
    HIGH_RISK_TLD = "HIGH_RISK_TLD"
    MEDIUM_RISK_TLD = "MEDIUM_RISK_TLD"
    ROLE_ACCOUNT = "ROLE_ACCOUNT"
    EMAIL_AUTH_MISSING = "EMAIL_AUTH_MISSING"
    # Phone
    PHONE_INVALID = "PHONE_INVALID"
    PHONE_VOIP = "PHONE_VOIP"
    PHONE_PREMIUM_RATE = "PHONE_PREMIUM_RATE"
    REPORTED_SCAM_PHONE = "REPORTED_SCAM_PHONE"
    # Name
    SANCTIONED_NAME = "SANCTIONED_NAME"
    SANCTIONED_NAME_PARTIAL = "SANCTIONED_NAME_PARTIAL"
    # Country
    SANCTIONED_COUNTRY = "SANCTIONED_COUNTRY"
    FATF_HIGH_RISK = "FATF_HIGH_RISK"
    FATF_MONITORED = "FATF_MONITORED"
    COUNTRY_MISMATCH = "COUNTRY_MISMATCH"
    COUNTRY_MISMATCH_VPN = "COUNTRY_MISMATCH_VPN"
    # IP
    SANCTIONED_IP = "SANCTIONED_IP"
    KNOWN_BAD_NETWORK = "KNOWN_BAD_NETWORK"
    TOR_EXIT_NODE = "TOR_EXIT_NODE"
    DATACENTER_IP = "DATACENTER_IP"
    VPN_IP = "VPN_IP"
    CLOUD_HOSTING_IP = "CLOUD_HOSTING_IP"


class ErrorCodes:
    """Stable ``code`` values on an error response. Switch on these, not the message."""

    INVALID_REQUEST = "invalid_request"
    MISSING_INPUT = "missing_input"
    INVALID_IP = "invalid_ip"
    INVALID_KEY = "invalid_key"
    INVALID_SIGNATURE = "invalid_signature"
    INSUFFICIENT_SCOPE = "insufficient_scope"
    BATCH_NOT_AVAILABLE = "batch_not_available"
    IP_NOT_ALLOWED = "ip_not_allowed"
    NOT_FOUND = "not_found"
    METHOD_NOT_ALLOWED = "method_not_allowed"
    RATE_LIMITED = "rate_limited"
    QUOTA_EXCEEDED = "quota_exceeded"
    SPEND_CAP_REACHED = "spend_cap_reached"
    INTERNAL_ERROR = "internal_error"


class Webhooks:
    """
    Verify outbound webhook deliveries from FraudCheck. Each POST is signed with the endpoint's ``whsec_``
    secret; verify it before trusting the payload — anyone can POST JSON at a public URL.

    Read the headers off the incoming request and pass the RAW request body (bytes or str, exactly as
    received — do not re-serialize):

        from fraudcheck import Webhooks
        ok = Webhooks.verify(
            secret=MY_ENDPOINT_SECRET,
            timestamp=request.headers["X-FraudCheck-Timestamp"],
            body=raw_body,
            signature=request.headers["X-FraudCheck-Signature"],
        )
        if not ok:
            return 400
    """

    EVENT_HEADER = "X-FraudCheck-Event"
    DELIVERY_HEADER = "X-FraudCheck-Delivery"
    TIMESTAMP_HEADER = "X-FraudCheck-Timestamp"
    SIGNATURE_HEADER = "X-FraudCheck-Signature"

    @staticmethod
    def sign(secret: str, timestamp: "int | str", body: "str | bytes") -> str:
        """The expected ``v1=…`` signature value for a (timestamp, body) pair."""
        raw = body.encode("utf-8") if isinstance(body, str) else body
        mac = hmac.new(secret.encode("utf-8"), f"{timestamp}.".encode("utf-8") + raw, hashlib.sha256)
        return "v1=" + mac.hexdigest()

    @staticmethod
    def verify(
        secret: str,
        timestamp: "int | str",
        body: "str | bytes",
        signature: str,
        tolerance_seconds: int = 300,
    ) -> bool:
        """
        True when ``signature`` matches and (if ``tolerance_seconds`` > 0) the timestamp is recent. Uses a
        constant-time compare, so a mismatch leaks nothing about how close it was.
        """
        if not signature:
            return False
        if tolerance_seconds > 0:
            try:
                if abs(time.time() - int(timestamp)) > tolerance_seconds:
                    return False
            except (TypeError, ValueError):
                return False
        return hmac.compare_digest(Webhooks.sign(secret, timestamp, body), signature)
