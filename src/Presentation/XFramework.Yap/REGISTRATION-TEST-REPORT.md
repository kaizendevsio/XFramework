# Registration and mobile input follow-up

Implemented September 11, 2026 after the initial Yap deployment.

- Login now links to `/register`; registration provides display name, username, password, confirmation, and a return-to-login link.
- The browser posts to Yap with antiforgery protection. Yap calls the generated IdentityServer registration wrapper with its configured tenant. IdentityServer authenticates the service and applies an explicitly configured workspace/member-role policy.
- Identity, credential, and role creation are atomic. The password uses the existing BCrypt policy. An unavailable workspace, disabled/elevated role, missing scope, or different tenant is rejected. Concurrent duplicate usernames leave exactly one complete account.
- Mobile/coarse-pointer input and textarea text is 16px; the viewport does not disable user zoom.

Validation before deployment:

- 39 Yap tests passed, including actual HTTP form/antiforgery validation, password mismatch, API error mapping, and rejection of browser-supplied workspace/role overrides.
- 11 focused PostgreSQL/Bolt integration tests passed, including 9 registration cases, wrapper coverage, and production composition. Registration uses a separately authenticated Yap transport identity. A registered account successfully authenticated afterward.
- 13 service-identity/Compose contract tests passed.
- Isolated Chrome fixture at 390×844 and 1440×1000: registration form submits to the success/sign-in state; mobile login, all four registration fields, conversation search, and message composer have computed 16px text. No horizontal page overflow was found. Desktop and mobile screenshots are in ignored `artifacts/yap/registration-*.png`.

The browser form used fixture wrappers; the PostgreSQL/Bolt tests exercised the real account-creation backend separately. Native iOS Safari was not available on this Windows host, so physical-device focus/keyboard behavior has not been directly observed. The existing zoomable viewport is preserved.
