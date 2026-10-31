## Design delivered for review — not implementation approval
- [x] Specify manual UNC/drive setup, same-context probe isolation, typed errors, stale replies and uncertain connection outcomes.
- [x] Separate share readability from vendor Project registration and database verification.
- [x] Research exact TOML packages, dependencies, alternatives and pending round-trip tests; recommend Tomlyn 2.10.1 for review only.
- [x] Specify portable slots, local bindings, selected grants, fingerprints, revocation, impact and generation commits.
- [x] Deliver native FigCraft forms, result states, grant/import/revoke previews, components and visual acceptance contract.
- [x] Specify isolated VM topology and 16 pending scenarios; index the eight-section explanation in DocFX.
- [x] Document the selected VMware Host-only runbook: concrete adapter/IP steps, standard accounts, share/NTFS matrix, scoped guest firewall, session reset, G0–G5 gates and evidence.

## Review gates — pending
- [ ] User reviews operations, security boundaries, data model and native FigCraft design.
- [x] User approves Tomlyn 2.10.1 on 2026-10-28; installation is explicitly deferred.
- [ ] Obtain go-ahead to install/implement; review generator use and any additional dependency beyond the approved package scope.
- [x] User selects VMware Host-only and dedicated test accounts; requests configuration instructions first, without environment execution.
- [x] User authorizes existing-environment inspection and G0/G1 execution on 2026-10-28, with pre-change snapshot and no impact on other VMs; product dependencies/WPF remain deferred.
- [ ] Obtain product implementation go-ahead separately from lab-environment setup.

## G0/G1 execution — passed; original VMX protection exception retained
- [x] Inspect VMware 26.0.1, existing Windows VM, free space and current VMnets without changing shared network definitions.
- [x] Create independent full clone LD-PROJ-VM from the sole Windows VM; keep original powered off.
- [x] Create powered-off pre-change snapshot UID1, LD-Proj-Before-G0-G1-20261028.
- [x] Change only the clone adapter to Host-only; preserve global DHCP/NAT configuration hashes and record the original VMX rewrite/hash exception during VMware cloning.
- [x] Complete user-approved native credential handoff into local restricted DPAPI storage; verify encrypted clone open and running Tools without passwords on CLI.
- [x] Resolve actual guest account ZHANGSH1 and updated user-selected credential; native API login and administrator-token inspection pass. Earlier failures remain recorded; no offline reset or policy weakening.
- [x] Configure and inspect clone 192.168.244.10/24, no gateway/DNS/external IPv4 route, hostname LD-PROJ-VM; G0 passes.
- [x] Create standard Users-only reader/denied accounts, fake directories, explicit SMB/NTFS and host-only-source TCP445 firewall; ten real SMB checks across four LUIDs pass G1.
- [x] Inspect only lab sessions, avoid forcibly closing cached open handles, shut down clone normally, retain before UID1 and Ready UID2 snapshots with hashes. All VMs end powered off.
- [x] Preserve source/global guard results and original VMX hash exception; retain temporary C# tool source and redacted JSON/JSONL evidence. This does not approve or implement product G2–G5.

## Read-only licence clarification — completed
- [x] Record host E3D entries and clone Licensing System 4.1 inventory, bounded scan and separate service/port results; no vendor checkout, installation or licence changes. Clone shut down normally after inspection.
- [x] Shorten the eight-section review; keep detailed contracts in existing OpenSpec and preserve anchors, pseudocode and evidence.

## Remote connection / portable profiles / grants implementation — pending (not MD-01 core)
- [ ] Implement concrete C# scripts and neutral platform capabilities; retain explicit frontend/input/coordinator calls.
- [ ] Implement versioned generation persistence, JSON migration, secret-safe export/import and local credential grants.
- [x] Existing architecture rules cover current layers (latest Addin batch64 tests); future remote/TOML/grant adapters still require their own rules and tests.
- [ ] Implement approved FigCraft controls using explicit WPF templates; compare actual rendering at 100/125/150% DPI.
- [ ] Run TL-01–08 library checks, VM-01–16, fault injection, grant-race and secret-exclusion tests with List<Record> and redacted logs.
- [ ] Validate actual AVEVA Project registration/login/licence with an authorized real case; generic SMB does not satisfy this gate.

Checked items certify only their stated scope: review assets, separately authorized G0/G1 environment evidence, or read-only inventory. The remote/portable/grant product workflows remain pending. Later isolated native E3D Model startup and AVEVA201 checkout/release passed; see LC-06, not a new configurable-VM regression.


## 4546 and real E3D investigation — completed with a failed checkout
- [x] With Clash TUN active, record VMnet1 selected route and copied NAT-only firewall scope; use only temporary clone rules for the host-only source.
- [x] Confirm RULES never listens on4546, waits for RMS licence details then stops with an initialisation error; preserve uncertainty about the deeper RMS cause.
- [x] Obtain protected project input and explicit one-test command-line exposure approval after the initial automatic execution rejection; do not apply this exception to product design.
- [x] Run existing host E3D3.1 MAR/SYSTEM/ALL_NO_MDS/DESIGN against only clone4545@192.168.244.10: real des.exe reports AVEVA201 checkout failure, RULES4546 error10060. Monitor exit0 does not make the test pass.
- [x] Remove temporary firewall rules, consume/delete protected project secret, preserve existing user sessions and shut down only the clone; retain evidence and original protection exception.
- [x] Resolve the RMS licence-detail failure by read-only vendor diagnostics: hostname-only selector0008 no longer matches LD-PROJ-VM;22 RMS event86 records include ARMS_ID/AVEVA201, with pre-rename RULES active history. No repair performed.


## Earlier read-only clone identity diagnosis — confirmed; no repair in that run
- [x] Run installed vendor echoid -d, then use a scratch working-directory echoid.dat selecting0x8; do not change installed configuration.
- [x] Compare composite lock codes using local AVEVA ALS4.1 guide6.6–6.8; publish hashes and field whitelist, not licence strings.
- [x] Correlate RMS event86 with pre-rename clone RULES activation10:35 and post-rename failures11:25; document the missed licence-binding check in G0/G1 naming changes.
- [x] Preserve failed read-only inventory attempts caused by the exclusively locked RMS timestamp; do not unlock, restart service or edit it.
- [x] Record current static licence/tool hashes, preserve original VM and snapshots, shut down only the clone; no firewall opening, application login, dependency or WPF changes in this run.
- [x] User separately authorizes snapshot-first restoration of clone hostname toZHANGSH and retest. This does not authorize licence reissue/rebinding or time adjustment.


## LC-05 hostname restoration and retest — licence passed; full app startup failed
- [x] Create powered-off pre-restore snapshotUID3 before any rename; preserve UID1/2.
- [x] Capture baseline, restore only Windows hostname toZHANGSH, reboot normally; keep VM display name, Host-only, accounts, share and ACL.
- [x] Verify unchanged licence hash, matching vendor hostname fingerprint and no current-boot event86; local and host4546 connect with temporary scoped licence rules.
- [x] Verify10/10 separate-LUID SMB cases with original test passwords and newZHANGSH account prefix.
- [x] Use user-specified default SYSTEM credential for one real E3D3.1 attempt; RMS issues AVEVA201 at13:38:33 and releases it13:38:38. Record subsequent application communications-file FNF7 separately; no full login claim.
- [x] Record rejected optional second working-directory test; no bypass or second login. Remove temporary rules, delete temporary project secret, keep user sessions, shut down clone and preserve original VM/global guards.
- [x] User authorizes necessary SYSTEM-default tests/command-line exposure; investigate FNF through controlled startup comparisons. Native handles/no standard-stream redirection enters MAR Model in both console visibility modes; product G2–G5 remains pending.


## LC-06 and DV-01 — native startup verified; developer scaffold default off
- [x] Compare original LaunchService.Build and current WindowsProcessHost, cwd/init/temp; observe owned communications-file metadata without reading contents.
- [x] Preserve A/B failures and C/D real Model startup, live lease, normal exit/release; retain Toolkit Model Unknown limitation and transient observations.
- [x] Respect user sleep/re-login and later explicit exit/shutdown; no extra reboot/login or PML model modification.
- [x] Split canonical Host/Guest sources into dev/RemoteProjectLab; preserve frozen historical artifacts and private credentials separately.
- [x] Add explicit machine/user-bound local enable switch; product solution/initialization/publishing does not reference it. No Debug/environment/VM detection auto-enable and no new UI.
- [x] Default disabled checks stop before runtime lookup and VIX host connect;6 activation checks and48 architecture tests pass with existing tools/packages. Private current VIX entry also gated; historical backup retained.
- [x] Index developer-only details separately; normal remote presentation has no VM/simulation badge, while developer evidence retains actual source and errors.
## DV-02 Documentation-only call chain and reading theme
- [x] Expand the developer index to eight sections with product/scaffold separation, source links/line ranges and existing controller/runner failure returns.
- [x] State that enable/disable writes local state only; ordinary remote product behavior has no dependency on the lab switch.
- [x] Add code-authored flowchart/sequence diagrams and a small Yuzuha-inspired DocFX modern reading override; no dependencies, product/UI code or lab execution.
- [x] Validate strict OpenSpec, warning-free DocFX, two rendered SVG diagrams, source links and light/dark readability in the current711px browser viewport without page overflow; save Git Diff.
## MD-01 / DV-03 model-driven core — implemented; remaining scope explicit
- [x] Record MD-01, DV-03 and UX-03 in an eight-section review distinguishing existing code from proposed changes.
- [x] User confirms product package plus independent development tool; use one WorkspaceHost and shared backend assemblies with hash checks.
- [x] Migrate duplicate frontend fields/flow to one edited model, backend rules and immutable persisted execution snapshots; preserve Record/Log evidence and credential-reference-only storage.
- [x] Configure VM/licence parameters through LabSettings and the common LaunchModel with explicit target confirmation and backend identity guards; no implicit VM startup or portable activation.
- [x] Record the CAF-style composable feature boundary without claiming a concrete CAF plugin adapter.
- [x] Inspect the actual original release and three empty-error deployment results; archive integrity and isolated5228-file deployment pass; real target and original source untouched, historical root cause not recovered.
- [x] Final regression:47 unit,52 architecture,4 UI and9 integration tests;6 activation and8 fixture-target checks. Both packages have identical shared DLLs; DocFX/OpenSpec validated. No VM or vendor process started; Git Diff saved.
- [x] Implement the latest requested DocFX revision aligned with the portfolio; preserve navigation/hierarchy/diagram review and the prior visual rejection. User visual acceptance remains pending.
## AD-01 XML Addin registration
- [x] Add one shared catalog and explicit host factory composition; no manifest means no feature construction or business command entry.
- [x] Ship one canonical login declaration in both hosts and the VM declaration only in the developer package; no new parser dependency.
- [x] Make Framework VM tools delegate registration checks to the same developer Addin host while retaining local authorization.
- [x] Add manifest/DTD/duplicate/disabled factory tests;64 architecture tests pass.
- [x] Packaged binary-present/manifest-absent behavior passes9 checks, normal UI regression passes4 tests; update DocFX and save Git Diff. No VM action or local authorization change.

## DOC-01 Documentation consistency and role-based entries — 2026-10-29
- [x] Separate implemented, proposed and historical scope; retain dated evidence and distinguish document review from execution.
- [x] Correct stale verification/task/README statements without changing historical results or product code.
- [x] Add eight-section user/developer/demo navigation backed by one feature fact page and source/evidence links.
- [x] Compare with the observed original LaunchDesk error chain; state qualitative traceability gains without invented timing/compression metrics.
- [x] Strict OpenSpec and DocFX pass (0 warnings/errors);22 metadata pages and3 eight-section entries checked, rendered links valid; five local HTTP entries return200. Save full Git Diff; no VM or product execution.

## DOC-02 Project-manager portfolio — 2026-10-30
- [x] Prepare eight-section narrative, scope/evidence links and one shared content/palette source.
- [x] Create eight editable native FigCraft boards and inspect cover/architecture exports.
- [x] Build independent HTML deck with navigation, contents, source chain and clearly illustrative local interactions; no product UI change.
- [x] Browser interactions and390px eight-slide overflow checks pass;8 native frames/165 editable layers,56 links valid, no console errors observed. Strict OpenSpec and warning-free DocFX pass; save previews and Git Diff. Print rendering/native fullscreen state are not independently asserted; see evidence.

## DOC-03 Design-instance consistency correction — 2026-10-30
- [x] Retain prior independent design as historical evidence; remove the second layout template from the HTML generator.
- [x] Capture eight stable1600x900 HTML pages with DOM/line/geometry data, fixing overflow and font fallback; import475 editable native layers and six bound variables.
- [x] Export native variable CSS plus11 node mappings for heading typography and cover geometry; returned browser properties pass checks.
- [x] Provide per-page native/browser screenshots, raw native CSS/readback, source archives, SHA256 and bounded pixel diagnostics without invented equivalence claims.
- [x] Comparison selector/overlay slider and actual1600x900 images verified;390px eight-slide no-overflow and core interactions pass. Strict OpenSpec/DocFX pass, source links and cumulative Git Diff retained. Visual acceptance remains with the user.

## UX-04 DocFX reading theme — 2026-10-31
- [x] Implement the shared native palette, branded reading shell, desktop file/section navigation, metadata labels and local search without changing product UI.
- [x] Check title filtering, LaunchModel search, desktop and390px navigation, table containment and light/dark diagram labels; preserve real screenshots.
- [x] Import the actual DocFX page into a separate native snapshot through the shared DOM adapter; read back palette/title CSS with source hashes.
- [x] Final checks pass:4 desktop/3 narrow page observations without overflow,2 rendered diagrams,128 built links, native palette/title readback, strict OpenSpec and0-warning DocFX. Evidence and cumulative Git Diff retained; visual acceptance remains with the user.
