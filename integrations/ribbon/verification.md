# Local integration verification · 2026-10-31

Executed solely from integrations/ribbon/source, with its freshly restored lockfile dependencies and locally compiled C# executables. Original sibling source and installed AVEVA directories remained read-only.

| Check | Result |
|---|---|
| npm ci --no-audit --no-fund | PASS, 34 locked packages restored |
| npm run build (C# backend + TypeScript + Vite production bundle) | PASS |
| node scripts/build-desktop.mjs | PASS, WPF executable and resources |
| node scripts/check-features.mjs | PASS |
| npm test, after deep-path correction | PASS: 34 tests, 0 failures, 16.54 s |
| npm run desktop:verify | PASS: import/edit/export/deploy/undo/multidocument/table/window smoke; layout seed/restore/docked all passed |
| Tracked candidate output scan | No bin/obj/node_modules/exe/dll/pdb candidates |

The restricted Windows container disables HttpListener (PlatformNotSupportedException). Initial HTTP tests failed there; the normal Windows execution rerun used fixture-only directories and passed after the deep-checkout atomic filename correction documented in feature-handoff.md. Dependency download needed network-enabled execution. npm emitted install-script approval notices, but required native packages, builds and tests completed without changing script approval settings.

This verifies source integration in the current checkout. Fresh-clone proof and root Manager-to-local-backend operation validation belong to the parent integration handoff. No actual AVEVA UIC load, CAF host, database migration or visual acceptance is claimed. Default runtime workspace lives outside the checkout; users can select UIC_DATA_DIR explicitly.
