# Ribbon Feature handoff · 2026-10-31

Feature identities: ribbon authoring/history and UIC deployment. Source is the local unversioned UicRibbonDesigner snapshot, preserved under source/; the manifest pins every copied file. Business implementation remains apps/backend/Features, with HTTP v1 as the process boundary. Web and WPF use HTTP; TS services remain compatibility/reference fixtures rather than a second PMLTWIN production coordinator.

Manager's existing RibbonHttpAdapter registrations map ribbon.* to api/v1/* and retain original 403/409/423/error bodies. Start-Ribbon.ps1 supplies this repository's backend; no external service checkout is needed. Existing Manager write authorization remains separate from service validation and UIC deployment preview. There is no automatic replay after an uncertain write.

Definitions/references, opaque imported XML, stable keys, snapshots, incremental UI undo, original bytes, file preview/apply and registration preview/apply are retained. Configuration persistence is distinct from deployment. Tests target temporary fixture installations; actual AVEVA UIC loading, visual approval, CAF and Database migration remain unverified.

Native FigCraft files are copied without editing; the 0.12 portfolio and its HTML/screenshots/review assets remain historical design evidence. Do not describe those pictures as connected production UI. All relative web/contracts/design paths remain within source/. build-desktop.ps1 now delegates to the authoritative local node builder, fixing its missing contracts and framework references. Generated desktop artifacts are ignored.

Root orchestration can call node source/scripts/build-backend.mjs (absolute path supported), then Start-Ribbon.ps1 with explicit -DataDirectory and -Port. Web build/test commands must run with source/ as current directory. Self-contained fresh-clone verification is performed by the integrating parent task, after these files are committed.

Deep-checkout regression: original atomic temp names appended another GUID to the complete destination filename and exceeded Windows limits after integration. Data.Atomic now uses a unique GUID sibling name in the same directory, preserving CreateNew and MoveFileEx atomic replacement. net48 runtime configuration/longPathAware manifest are shipped by both project and node compiler. This does not promise arbitrary path length for all legacy Windows/AVEVA APIs.


## Long workspace paths follow-up · 2026-10-31

The r4 fresh clone passed the then-469 integrated tests but its final Manager-to-Ribbon chain failed at workspace.save. The evidence remains in `.portable-checks/20261031-r4/pmltwin/artifacts/portable-validation/ribbon-chain/backend.stderr.log`: Win32 MoveFileEx failed inside Data.Atomic. The earlier shorter sibling temporary filename did not resolve native path length handling.

Root cause: .NET AppContext long-path switches and an application manifest do not make an unprefixed Win32 P/Invoke independent of the machine-wide long-path policy. Data.NativePath canonicalizes absolute paths and maps drive/UNC paths to the Windows extended-path form. Atomic rename uses explicit MoveFileExW with those paths; workspace internal storage root uses the same form so managed directory creation/read/history enumeration also work at deep locations. Logical document IDs, imported/deployment DTO paths, file validation, same-directory atomic replacement and retry/cleanup semantics remain unchanged. No registry policy was modified.

A new HTTP regression deliberately uses a workspace root longer than 320 characters, saves two versions, restarts the backend, opens the latest document, restores the first version, verifies three versions and no temporary leftovers. It failed before correction and passes afterward. Full Ribbon tests now pass 35/35. A separate Manager CLI chain passed with a 223-character workspace and 321-character version file path, deeper than the failed r4 chain. Logs remain in root artifacts/ribbon-longpath-before.log, ribbon-longpath-after.log, ribbon-longpath-full-tests.log and ribbon-longpath-manager-chain.log; chain result is under artifacts/long-path-chain/.

Coverage is workspace history persistence/recovery and the native atomic rename boundary. This is not a claim of unlimited path support in all AVEVA installation APIs or actual UIC loading acceptance. UNC conversion is implemented but this machine's network-share permissions were not part of the test.
