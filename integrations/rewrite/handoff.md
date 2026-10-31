# Rewrite integration handoff · 2026-10-31

## Imported closure

310 original files with original/integrated SHA-256 in `manifest.json`: all rewrite source projects and tests, legacy Core/Update test closure, original specification catalog/BRD/PRD/SRS/FRD/SDD/SDS, native FigCraft design and 01–17 exports, architecture contracts and portfolio sources/data/static previews. Original namespaces, operation/requirement IDs and original source layout remain intact. No original repository was modified.

PMLTWIN remains product authority. These historical coordinators, JSON stores, launch rules, UI and Worker are migration sources; importing the snapshot does not register a second active backend or claim the planned features are completed. Source owns no CAF integration or modern Database DLL.

## Deliberate transformations

- Bootstrap/Rewrite.Tests/Update.Tests accept optional `PmlTwinSevenZipExecutable`; no executable/vendor binary is copied. Default ZIP/7z scenario tests ZIP plus missing-codec rejection; full codec and Update regression are optional category `Optional7Zip`.
- `ExistingScenarioTests` uses a unique short system temporary path for deep Windows fixtures and archives JSON results under the source artifacts directory. The extra integration nesting otherwise exceeds Windows path limits. No test assertions are removed.
- Upstream disabled NuGet audit. Integration enables it. Offline commands downgrade only unavailable-service diagnostic NU1900 to warning; known vulnerability diagnostics remain active. The network-restricted run cannot certify vulnerability audit success.
- Architecture portfolio retains all referenced report/input sources and data. It uses original PNG previews plus the native LikeC4 model source instead of the absent generated LikeC4 site. Historical status labels are preserved. No additional third-party JS bundle is vendored.

## Actual verification

.NET SDK 10.0.302, Windows, Release. Restore from existing NuGet cache succeeds with NU1900 availability warnings; complete rewrite solution build succeeds with 0 errors. Default architecture/contract/behavior suite: **71 passed, 0 failed, 0 skipped**. This includes the existing Core executable suite (32 internal cases). Node source/script contract tests: **10 passed**. Manifest 310 original/final hash pairs and portal local asset closure pass. `Optional7Zip` was not run; Aspire AppHost is preserved as a development experiment outside the core solution and was not restored/built.

Sandbox testhost initially could not establish IPC. Running the same isolated test command with elevated sandbox permissions resolved it. The first elevated run exposed fixture path-length errors, corrected as above. A subsequent run had one transient legacy child-process handoff timing failure; the final complete run passed 71/71. Preserve this limitation if a fresh-clone run reproduces it rather than filtering the assertion.

Commands are in README.md. For automated total assembly use restore/build with `-m:1 -nr:false -p:WarningsNotAsErrors=NU1900`, then `dotnet test .../LaunchDesk.Architecture.Tests.csproj -c Release --no-build --filter 'Category!=Optional7Zip'`. Fresh-clone validation and push remain root-owner work. No source commit or push performed by this integration agent.

## Source/license limits

No repository-wide license file was found in Archon; do not infer an open-source license from PMLTWIN's root license. Existing DocFX license and codec notices are retained. Original machine roots appear only in provenance/historical evidence and do not participate in build/runtime lookup. Native design was byte-copied and not opened or edited. No separate Archon marketing portfolio was found; the preserved architecture portal is historical architecture evidence, while Next's product portfolio is delivered by its own integration.
## Review follow-up: configuration and Worker isolation

The AR18 fixture now runs the original authenticated Worker host inside the currently built test executable, so Debug fresh clones never need an existing Release Worker. The test assembly is explicitly registered as an internal friend and references Worker in the approved dependency graph. `PrivilegedWorkerHost` accepts its registry directory only through trusted constructor assembly; the production executable entry continues to use the original user registry and never reads the test environment variable. No protocol/request field can select the protection root. Named-pipe current-user restriction, expected PID/start time, nonce, admitted journal ownership, frozen request digest, machine identity and target lease checks remain unchanged. The fixture still verifies rejection of an unsupported operation and a replay.

Debug solution build and all 71 default tests pass after this change. An external read-only check compared the real user ControlV3 file before and after the full suite: identical SHA-256 `5608c276b8e1dda0309323c5134f378c249d95ab3d55e0983acdbbac54221fbb`.

Earlier AR18 runs had registered three test workspaces (six exact Roots including their `.control` children) in the user's ControlV3 file. After identifying them from this integration's fixture IDs, the repair backed up original complete bytes and SHA-256 into ignored local `artifacts/integration/rewrite-control-registry-repair/`, held `registration.lock`, rechecked the unchanged file and empty Active list, and atomically removed only those six exact entries while retaining the ACL. Readback verified every other field and all 37 remaining Roots unchanged. Repaired SHA-256: `33b121a15c811ec798d9d74ebed8f339511b46123be6edcff1b08ea993551050`. No original Archon/user registration or data directory was deleted. Full user registry backups are local only and must never be committed. Only these non-secret repair counts/hashes are published.

Final staging closure omits six original `.idea` user IDE-state files and three historical generated `TestResults/*.trx` files. Their original paths and hashes remain in manifest `omissions`; they are not source requirements or integration evidence. The final imported source/asset count is 310. Current test output stays ignored and is not included in the source manifest.

## Security follow-up: strict online audit

Fresh-clone online audit correctly blocked the inherited transitive Microsoft.OpenApi 2.0.0 with NU1903. Microsoft's [official advisory](https://github.com/microsoft/OpenAPI.NET/security/advisories/GHSA-v5pm-xwqc-g5wc) identifies 2.7.5 as the patched 2.x version (3.x has a separate 3.5.4 fix). DevHost now directly pins **Microsoft.OpenApi 2.7.5**, preserving its major version and the existing Microsoft.AspNetCore.OpenApi/Scalar integration. Upstream source remains untouched.

Strict online restore of the complete solution passes with no audit diagnostic, suppression or audit disablement. Debug full-solution build passes with **0 warnings, 0 errors**; all **71 default tests pass**. A real isolated DevHost loopback smoke test generates OpenAPI **3.1.1** including `/health` and `/api/operations`, and `/scalar/v1` returns HTTP **200**. The created diagnostic host used a unique temporary data directory and was stopped afterward. This supersedes the earlier offline NU1900 availability limitation for this verified package closure. Normal README commands now show strict restore/build, with no warning downgrade flag.
