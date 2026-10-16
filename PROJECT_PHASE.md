# Project Phase: API Validation and Regression Testing

## Copilot Review Contributions

Copilot's code review highlighted three issues addressed in this phase:

- User fields had basic required/email checks but no length limits. Validation now trims inputs, limits names to 100 characters and emails to 254, and rejects email display-name syntax.
- Unhandled endpoint exceptions are logged by `ExceptionHandlingMiddleware`, which returns HTTP 500 as `{"error":"Internal server error."}` without exposing internal details. Request-abort cancellation is rethrown.
- `GET /api/users` sorted and returned the full collection. It now accepts `skip` and `take`, defaults to 100 results, caps page size at 100, and uses a deterministic name/ID ordering.
- The middleware did not record normal request/response activity. An outer audit middleware now logs method, path, status, elapsed time, and trace ID for each request, including handled failures.
- Middleware hot-path work was reduced by using timestamp-based elapsed-time measurement, caching the expected API-key bytes at startup, and ordering exception handling before status-code formatting.
- Middleware registration now places exception handling first, bearer authentication before request logging, and logging after authentication. Rejected requests are audited without reaching endpoints.

Audit logs intentionally omit request/response bodies, headers, and query strings to avoid recording credentials or personal data.

Copilot also identified that storage is process-local. That remains a production concern. Pagination bounds response size, but the in-memory list still needs a collection snapshot and sort.

## Bearer Token Access

The `/api/users` endpoints require `Authorization: Bearer <token>`. Configure the shared token through `Authentication:ApiKey` (for example, the `Authentication__ApiKey` environment variable); startup fails when it is missing. This is an opaque shared token, not a JWT: it has no issuer, expiration, or per-user identity. Keep it secret and rotate it when exposed.

## Verification

- `dotnet build`: passed.
- POST invalid payloads (blank name, display-name email, name over 100 characters, malformed JSON): returned 400.
- POST valid user: created successfully and returned an ID.
- GET existing ID: returned 200; missing and malformed IDs: returned 404.
- PUT invalid payload: returned 400; PUT missing ID: returned 404; PUT existing user: updated successfully.
- DELETE existing ID: returned 204 and subsequent GET returned 404; DELETE missing ID: returned 404.
- GET pagination: default list returned all two test users; `skip=1&take=1` returned the next sorted user; `take=0&skip=-1` was clamped to the first result.
- Authentication: missing, malformed, incorrect, and legacy `X-API-Key` credentials returned 401 with a Bearer challenge; the configured Bearer token returned 200.
- Middleware regression: bearer authentication was verified alongside the request logger and standardized error responses.

Middleware regression tests exercised missing, malformed, and invalid bearer credentials (401), a valid token (200), validation errors (400), missing resources (404), and an intentional exception (500). The temporary Development-only exception route was removed after testing. Audit logs recorded the matching final statuses and trace IDs; exception details were logged server-side while the response remained generic JSON.
The middleware changes were functionally verified, not load-benchmarked; no throughput improvement is claimed.