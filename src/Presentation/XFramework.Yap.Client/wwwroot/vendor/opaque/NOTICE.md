# OPAQUE browser dependency

- Package: `@serenity-kit/opaque` 1.1.0 (MIT).
- Source: https://github.com/serenity-kit/opaque
- npm archive integrity: `sha512-Y6v/+hRMn0MdMEk5+/ArM0vPIiFfFEbdTZc7oAx+cWyvGODGezAQ/sMjXBVogf7NsS9z9EV0Ve5paZCVULuedw==`
- `opaque.mjs` is the unmodified npm `esm/index.js`, with WASM embedded by upstream.
- Server adapter: `src/Libraries/XFramework.Opaque.Native`, pinned opaque-ke 4.0.1.
- Protocol: RFC 9807, ristretto255 / SHA-512 / TripleDH. Client key stretching is explicitly `memory-constrained` (Argon2id, 64 MiB, 3 iterations, parallelism 4).
- Upstream audit: https://www.opentech.fund/security-safety-audits/opaque-security-audit/ (2023). The audit predates this release and does not certify the XFramework integration.

To update: download a specific npm version, verify npm integrity, retain its license, compare upstream changes and run native/browser interoperability and authentication/recovery regression tests. Never load this dependency from a CDN at runtime.
