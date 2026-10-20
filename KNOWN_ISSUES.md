# Known Issues

## KI-001: Integration test `Register_Then_Login_Works` fails with 500

**Discovered:** 2026-10-20
**Status:** Open (pre-existing, not introduced by Audio System work)
**Severity:** Low (test-only; unit tests pass; manual API verification pending)
**Verification:** Confirmed failing on `main` HEAD before any Audio System changes
(verified via `git stash push` on 2026-10-20).

**Symptoms:**
`POST /api/auth/register` returns 500 InternalServerError in
`WebApplicationFactory<Program>` test environment.

**Suspected causes (unconfirmed):**
- `WebApplicationFactory<Program>` uses production `Program.cs` configuration,
  possibly missing Testing-environment overrides.
- Possible exception during `DbSeeder.SeedAsync` when run in test environment
  (e.g., duplicate admin seed, missing SMTP config).
- Possible conflict with the shared `library.db` file.

**Action:** Defer. Investigate separately as a standalone task after Audio System
is documented. Do not modify or delete the test.

## KI-002: NuGet security warnings (AutoMapper 13.0.1, MailKit 4.7.1.1)

**Discovered:** 2026-10-20
**Severity:** Medium (build-time warning only, not breaking)
**Affected packages:**
- `AutoMapper 13.0.1` (CVE GHSA-rvv3-g6hj-g44x, high)
- `MailKit 4.7.1.1` (CVE GHSA-9j88-vvj5-vhgr, moderate)

**Action:** Upgrade in a dedicated maintenance task. Do not upgrade as part of
Audio System work (avoids introducing regressions while feature work is ongoing).