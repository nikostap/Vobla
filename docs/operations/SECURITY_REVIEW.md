# Security review — M13

- Identity uses one-time codes with expiry and attempt limits; sessions are server-side revocable.
- Administrative and financial pages have explicit role authorization; denied access returns 403.
- Login endpoints use a fixed-window rate limiter. CSP, anti-framing, MIME-sniffing, referrer and permissions headers are emitted globally.
- Razor antiforgery protects state-changing forms. Payment operations additionally use a database-unique idempotency key.
- Uploads have extension/MIME/size allowlists and chat files are served only after authorization.
- Exact listing coordinates and private contact data remain separate from public projections.
- Secrets in `compose.yaml` are development-only. Production must use a secret store, HTTPS-only cookies, external email delivery, key rotation and dependency scanning.
- Direct and transitive NuGet packages plus the npm test toolchain are checked by a fail-closed CI vulnerability gate; the current audit reports no known vulnerabilities.

Residual risks: demo OTP is visible only in Development; Production cannot start until a real `IOneTimeCodeService` adapter is registered. Scripts and styles are restricted to same-origin external files without `unsafe-inline`; map coordinates use bounded CSS utility classes instead of style attributes. Maintenance jobs are durable and use multi-instance-safe PostgreSQL locking, but production still needs external monitoring and independent security testing.

Legal surfaces: `/Privacy`, `/Terms`, `/Cookies` and `/Rules` publish pre-release descriptions of the current data, session and moderation behavior and are linked globally. They deliberately do not claim final legal approval; operator identity, contacts, jurisdiction-specific retention/deletion terms, age limits and incident/law-enforcement procedures remain release blockers.

Realtime lifecycle: authenticated hub methods retain conversation membership checks. Database work uses the socket cancellation token; only cancellation proven to originate from `ConnectionAborted` is treated as a normal disconnect. Other authorization, validation and persistence failures still propagate. Presence counters remove zero entries atomically to avoid unbounded per-user process state without introducing a reconnect race.

Error disclosure: handled failures return a localized HTTP 500 page with private cache policy. The only diagnostic value exposed to the user is the request correlation UUID also returned in `X-Correlation-ID`; exception messages, stack traces, Activity identifiers and environment-switching instructions are not rendered.

Personal data portability: authenticated users can download a versioned JSON export of account-linked profile, content, conversation, recommendation, engagement and sandbox financial records. Queries are read-only projections. Identity secrets, OTP material, normalized identity internals and attachment storage keys are explicitly excluded; exports use attachment disposition and private `no-store` caching. Account erasure and operator-handled incident requests still require an approved legal/retention process.

Erasure requests: users can create or cancel an audited durable request. A PostgreSQL partial unique index prevents duplicate pending requests across replicas. Only explicitly authorized privacy operators can record an Approved or Rejected decision with a mandatory reason. No handler physically deletes account data: retention/legal holds, shared-conversation anonymization and financial record requirements must be approved before a separate erasure executor is introduced.

Privileged bootstrap: the first Owner is provisioned only by `--bootstrap-owner`, with email/display name and a minimum 32-character capability supplied through the configuration secret provider rather than command-line arguments. A PostgreSQL advisory transaction lock serializes attempts; the same identity is idempotent, a different second Owner is rejected, and the successful operation creates an immutable audit event. Production web startup fails while the bootstrap token remains configured, so the capability must be removed immediately after provisioning.

Export abuse controls: personal JSON generation is limited to five requests per authenticated user per 15-minute window before any export query runs. Counters are atomic PostgreSQL buckets shared by all replicas and store only a SHA-256 key; exceeding the limit returns `429` with retry guidance. If the shared limiter store is unavailable, export fails closed with `503`, while the lightweight account-data page and erasure request workflow remain available.

Erasure concurrency: cancellation and operator resolution are conditional `Pending` updates. Exactly one transition can win across replicas; stale or competing requests receive `409` and cannot overwrite the final state. The state transition and its audit event commit in one PostgreSQL transaction, preserving an evidence trail even under concurrent operator actions.

OTP controls: request partitions are isolated by remote IP and action, verification is limited both per IP and per email/code, rejected requests return `429` with retry guidance, and rapid fake-provider resend reuses the current code. Per-IP/action counters are shared by all replicas through atomic PostgreSQL minute buckets whose keys contain only SHA-256 output; the in-process limiter remains a second defensive layer. Challenges are PostgreSQL-backed and multi-replica safe: only a salted SHA-256 hash is used for verification, while expiry, attempts and resend state are serialized transactionally. Durable hourly jobs remove expired challenges and throttle buckets. The Development fake adapter keeps a debug code solely to render the local demo; Production rejects Fake at startup. Automatic demo administrator elevation is Development-only and causes Production configuration validation to fail when configured.

Proxy trust: forwarded IP and scheme are processed before rate limiting, but only one symmetric hop from an explicit `KnownProxies` allowlist is accepted. Forwarded-header processing is disabled when the allowlist is empty. Production refuses to start without a valid proxy IP; a browser smoke confirms that an untrusted client cannot spoof its IP or HTTPS scheme.

Transport limits: requests larger than the route budget are rejected with `413` before form parsing. Generic endpoints receive 1 MiB, chat upload 65 MiB, listing wizard 105 MiB; multipart value/key counts and lengths, request headers, request line and header timeout are bounded. Per-file MIME/extension/count limits remain enforced by the upload handlers.

Upload content: accepted JPEG/PNG/WebP/PDF files must match their magic bytes; plain text must be strict UTF-8 without NUL/control payloads. Storage names are generated by the server and chat downloads require conversation membership. Production still needs an asynchronous antivirus/content-disarm provider before enabling arbitrary user attachments at public scale.

Upload durability: filesystem writes use same-directory temp files followed by atomic rename. Chat message/attachment metadata and listing media metadata commit transactionally; files created before a failed DB commit are removed by compensating cleanup. A future object-storage adapter must preserve the same staging/commit/cleanup contract.

Health isolation: `/health/live` checks only process liveness and remains independent of external dependencies. `/health/ready` checks PostgreSQL with a bounded timeout and is the endpoint for load-balancer readiness. The automated outage drill requires live `200`, ready `503` during database loss, and full recovery after PostgreSQL returns.

Cookie and session lifecycle: Production authentication and antiforgery cookies use `__Host-` names, `Secure=Always`, HttpOnly and root path; auth is SameSite Lax and antiforgery is SameSite Strict. Development keeps HTTP-compatible names while browser tests verify the remaining flags. Sliding authentication cannot exceed the server-side absolute 30-day session age; invalid, revoked or expired principals are rejected and signed out. Daily retention removes expired and old revoked session records.

Private response caching and cross-origin isolation: every authenticated response and all sensitive account, messaging, administration and personal-data paths emit `Cache-Control: no-store, no-cache`, `Pragma: no-cache` and an expired timestamp. Anonymous public pages retain normal cache semantics. CSP anti-framing is reinforced with `X-Frame-Options: DENY`; COOP and CORP are `same-origin`. Browser tests cover both public and private cache behavior.
