# Known Issues

## KI-001: Integration test `Register_Then_Login_Works` fails with 500
**Discovered:** 2026-10-20
**Status:** Open (pre-existing, not introduced by Audio System work)
**Severity:** Low (test-only; unit tests pass; register works in production manually)
**Verification:** Confirmed failing on `main` HEAD before Audio System changes
(verified via `git stash push` on 2026-10-20).

**Symptoms:** `POST /api/auth/register` returns 500 in `WebApplicationFactory<Program>`.

**Suspected causes:** Test environment lacks overrides for SMTP, seed, or DB path.

**Action:** Defer. Investigate separately.

## KI-002: NuGet security warnings
- `AutoMapper 13.0.1` (CVE GHSA-rvv3-g6hj-g44x, high)
- `MailKit 4.7.1.1` (CVE GHSA-9j88-vvj5-vhgr, moderate)

**Action:** Upgrade in a dedicated maintenance task. Not blocking.