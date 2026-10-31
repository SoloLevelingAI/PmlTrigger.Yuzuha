## ADDED Requirements

### Requirement: RP-01 Diagnose remote Project access before launch
The system SHALL distinguish ConnectionNotConfigured, PathUnavailable, AccessDenied and ProbeTimedOut before a remote Project is passed to a vendor process. A failed connectivity check SHALL NOT be labelled database corruption.
#### Scenario: Probe with the launch identity
- **WHEN** a remote preflight is requested
- **THEN** The system SHALL first try read-only access in the same Windows user/token context as the future vendor process. Missing connection metadata alone SHALL NOT invalidate an already readable share. A bounded C# read-only worker SHALL enforce an eight-second deadline; a UI Task.Run timeout alone SHALL NOT be treated as cancellation of OS I/O. Replies SHALL carry OperationId and configuration Revision. Cleanup failure SHALL be explicit.
#### Scenario: Remote share not connected
- **WHEN** a configured Project points to an inaccessible remote share
- **THEN** a typed actionable connection result is shown without claiming database damage
#### Scenario: Old request returns after editing
- **WHEN** a probe returns after the user changes the path or starts another request
- **THEN** its mismatched OperationId or Revision cannot update current UI state or permit launch
#### Scenario: Probe exceeds deadline
- **WHEN** the read-only worker exceeds eight seconds
- **THEN** the system stops waiting, terminates only its own worker or reports CleanupPending, and prevents this launch

### Requirement: RP-02 Offer explicit manual connection configuration
The system SHALL offer manual server/share/path configuration, recheck and cancel actions when remote setup is missing. Network credentials SHALL remain local and separate from AVEVA login credentials. Applying a network connection SHALL require explicit user confirmation.
#### Scenario: Confirm a draft or reconcile a side effect
- **WHEN** the user saves, confirms, cancels or times out a manual connection
- **THEN** Saving a draft SHALL NOT establish a connection. The default SHALL be a drive-less UNC session without CONNECT_UPDATE_PROFILE. Drive mapping SHALL require an explicit free drive and separate impact confirmation. Error 1219 SHALL NOT cause automatic disconnection, elevation or address-alias retries. Connection timeout/cancellation after a side-effecting request SHALL produce ConnectionOutcomeUnknown until reconciled, rather than claiming no connection occurred.
#### Scenario: User opens manual setup
- **WHEN** ConnectionNotConfigured is shown
- **THEN** the user can review connection parameters, configure them manually or cancel without changing system connections
#### Scenario: Drive already used
- **WHEN** an explicitly selected drive is occupied
- **THEN** DriveInUse is shown without replacing the existing mapping
#### Scenario: Connection completion is unknown
- **WHEN** a connection request times out or is cancelled after dispatch
- **THEN** the UI offers session reconciliation and does not automatically connect again or disconnect

### Requirement: RP-03 Gate startup on known remote access failures
The system SHALL preserve the login profile and record the redacted preflight result while preventing vendor process creation after a known failed remote check. Passing the probe SHALL NOT claim database compatibility or integrity.
#### Scenario: Review vendor registration separately
- **WHEN** a readable Project is prepared for vendor launch
- **THEN** Launch SHALL use a fresh preflight and check Project registration independently: a mismatched init/evars declaration SHALL produce ProjectRegistrationMismatch. An uncertain static result SHALL require review of the actual source, bound to initialization fingerprint, path and Revision. The system SHALL NOT rewrite vendor initialization files or assume environment injection overrides them. The first phase SHALL NOT introduce a managed vendor-init wrapper.
#### Scenario: Permission denied
- **WHEN** the Project path check returns AccessDenied
- **THEN** no vendor process starts and the saved profile remains available for correction
#### Scenario: Readable share but different registered Project
- **WHEN** the directory is readable but the same Project code resolves elsewhere in the selected initialization
- **THEN** startup is blocked until the user corrects the selection or configuration; share readiness does not certify registration
#### Scenario: Reviewed initialization changes
- **WHEN** the path, configuration Revision or initialization fingerprint changes after manual review
- **THEN** the previous confirmation is invalidated and review must occur again

### Requirement: PF-01 Support readable portable configuration
The system SHALL support versioned human-readable non-secret profile persistence with TOML using an explicitly approved serializer, subject to migration review and implementation authorization. Frontend code SHALL NOT own file serialization.
#### Scenario: Preserve source and adapter boundaries
- **WHEN** an approved TOML adapter edits or migrates a document
- **THEN** The proposed adapter SHALL use a format-neutral document contract; only Platform adapters SHALL reference serialization packages. Exact library/version approval SHALL precede installation. Existing TOML edits SHALL preserve explanatory comments and unaffected formatting through minimal syntax-tree changes. Unknown schema/keys SHALL be preserved read-only or rejected for saving, never silently dropped. JSON migration SHALL require preview, backup and retention of the original.
#### Scenario: Publish a consistent generation
- **WHEN** configuration and local binding metadata are committed
- **THEN** Configuration and local binding metadata SHALL be committed as an immutable generation with a single atomic current-pointer replacement under a user-scope single-writer lock and Revision comparison. Readers SHALL pin one generation. This SHALL NOT be described as an atomic transaction across files, Windows credentials and SMB.
#### Scenario: Storage implementation changes
- **WHEN** an approved TOML storage implementation is introduced
- **THEN** the same application input and coordinator contracts remain usable and existing JSON data has an explicit migration path
#### Scenario: Saving loses unsupported information
- **WHEN** the implementation cannot preserve unknown keys or existing comments
- **THEN** it refuses to overwrite the original and shows a diagnostic instead of silently reducing meaning
#### Scenario: Generation publication fails
- **WHEN** writing or switching to a new generation fails
- **THEN** the previous published generation remains authoritative and partial files are not exposed as committed state

### Requirement: PF-02 Preview configuration export and import
The system SHALL export/import non-secret application, Project, Module, login-item and portable credential-slot relationships with schema versioning. Import SHALL preview path changes and collisions before modifying existing records.
#### Scenario: Validate and publish portable data
- **WHEN** the user previews and confirms an import or export
- **THEN** Import SHALL bound size, validate strict decoding, syntax, schema, references and forbidden fields before confirmation. ID collisions SHALL default to copy, not overwrite. Export SHALL explicitly allow portable fields rather than package a local generation directory. Imported affected items SHALL start unbound, including same-machine imports; unrelated local items SHALL remain unchanged.
#### Scenario: Import on another machine
- **WHEN** profiles contain machine-specific executable or Project paths
- **THEN** the user can review replacements and conflicts before saving
#### Scenario: Invalid or secret-bearing input
- **WHEN** an import has malformed syntax, unknown schema, dangling references or prohibited secret/binding fields
- **THEN** it is rejected before publication with a safe location diagnostic and no partial import

### Requirement: CR-01 Keep secrets local and out of portable artifacts
Passwords and credential blobs SHALL remain in the local credential store and SHALL NOT appear in TOML, exports, logs, snapshots or ordinary profile objects. Portable artifacts MAY contain logical slots but SHALL NOT transfer a usable local secret binding.
#### Scenario: Export stored-password profiles
- **WHEN** a profile uses a local stored credential
- **THEN** export contains no password, encrypted credential blob or usable local binding

### Requirement: CR-02 Reuse credentials by reference
Several login profiles SHALL be able to reference one local secret through an explicit binding instead of storing password copies. Shared use SHALL NOT require identical Module or Project values.
#### Scenario: Separate portable needs from local secrets
- **WHEN** the user selects a credential for a slot
- **THEN** Portable CredentialSlot SHALL describe need and Kind only. LocalCredentialBinding and CredentialGrant SHALL remain local metadata. Network-share and AVEVA-login kinds SHALL NOT implicitly substitute for each other. Credential selection SHALL show app-owned safe metadata, not enumerate arbitrary secrets into the UI.
#### Scenario: One Project with several Modules
- **WHEN** the user authorizes DESIGN, DRAFT and ADMIN login items to use one credential
- **THEN** all reference the same local secret and no password copy is added to those profiles
#### Scenario: Unselected item shares the portable slot
- **WHEN** selected profiles are rebound while an unchecked profile has the same SlotId
- **THEN** only selected profile grants point to the selected BindingId; the unchecked grant retains its existing binding, because SlotId is not a global authorization or password-switch instruction

### Requirement: CR-03 Confirm selected bulk grants
The system SHALL display credential identity without revealing its secret and list target Project, Module, account and login item before one explicit confirmation. Cross-Project use SHALL require selected targets and SHALL NOT be inferred from names or guessed password equality.
#### Scenario: Bind the preview to exact target revisions
- **WHEN** a bulk grant is submitted or retried
- **THEN** Confirmation SHALL bind exact ProfileIds, revisions, kind and a security fingerprint covering application, Project path, Module, account and binding version. Security-relevant edits SHALL invalidate prior grants. Grant retries with the same OperationId and identical content SHALL return the prior receipt; conflicting content SHALL be rejected. Intent and redacted List<Record>/log evidence SHALL distinguish pre-commit failure from committed-but-evidence-pending outcomes.
#### Scenario: Cross-Project selection
- **WHEN** the user selects two items in different Projects and leaves a third unchecked
- **THEN** only the two selected items receive grants after confirmation
#### Scenario: Target changes after preview
- **WHEN** a selected profile changes Project path, Module or account before commit
- **THEN** the old preview cannot grant access and a refreshed explicit confirmation is required

### Requirement: CR-04 Preview generated login items and restrict authorization
The system SHALL offer discovery-based login-item generation with editable preview. Bulk authorization SHALL apply only to reviewed items; new discoveries SHALL NOT silently inherit access.
#### Scenario: Newly discovered Module
- **WHEN** a new Module appears after a previous bulk grant
- **THEN** its generated item remains unapproved until explicitly reviewed

### Requirement: CR-05 Revoke and show credential-change impact
The system SHALL allow revoking one profile grant without deleting the shared secret or other grants. Updating or deleting a shared credential SHALL show affected profiles before confirmation and record only redacted metadata.
#### Scenario: Serialize revocation and stage credential updates
- **WHEN** a grant is revoked or its shared secret is changed
- **THEN** Revocation SHALL gate future process creation under the same user-scope serialization as the final launch grant check; it SHALL NOT terminate running processes or claim to recall already delivered secrets. Updating SHALL stage a new local secret entry before publishing new binding metadata. Failure SHALL retain the old binding and identify only newly created unreferenced entries as CleanupPending; it SHALL NOT delete other credentials.
#### Scenario: Revoke one Module
- **WHEN** access is revoked for DRAFT
- **THEN** DESIGN can retain its existing grant and no global credential deletion is implied
#### Scenario: Secret staged but metadata fails
- **WHEN** a new secret entry is written but generation publication fails
- **THEN** old bindings remain valid and only the new unreferenced entry is eligible for scoped cleanup

### Requirement: CR-06 Rebind imported slots locally
Imported credential slots SHALL start unbound on another machine/user and SHALL require explicit local credential selection. Matching labels SHALL NOT automatically grant access to a local secret.
#### Scenario: Same label on another computer
- **WHEN** an imported slot label matches a local credential display name
- **THEN** the system still asks for explicit binding and authorization before use
#### Scenario: Same-machine import spoofs an existing ID
- **WHEN** imported content reuses a local slot/profile ID
- **THEN** imported affected items gain no usable local binding or grant merely from that ID

### Requirement: CR-07 Describe actual local security boundaries
The system SHALL describe current-user/local-machine storage and application grant limitations accurately. It SHALL NOT claim per-module OS isolation or protection against arbitrary malicious same-user processes merely because Credential Manager is used.
#### Scenario: User reviews a shared credential
- **WHEN** shared-use authorization is presented
- **THEN** the UI explains the affected login items and local-user scope without claiming Windows has created a per-profile security boundary

### Requirement: VM-01 Plan layered remote validation
The review SHALL provide isolated Windows VM topology, setup/reset steps, fault cases and redacted evidence fields before execution. Generic SMB tests SHALL remain distinct from actual AVEVA registration, database locking, licence and login validation. Test tooling SHALL use C# where available; discovered initialization files SHALL NOT be executed for discovery.
#### Scenario: No real remote Project is available
- **WHEN** the reviewer has only fake directories and test accounts
- **THEN** VM tests may verify networking and application failure handling, while real AVEVA behavior remains explicitly unverified
#### Scenario: VMware or a loopback share is selected
- **WHEN** the user proposes VMware Host-only networking or a 127.* UNC share
- **THEN** VMware Windows SMB may provide the separate-server test layer; loopback SHALL remain optional local smoke coverage and SHALL NOT count as cross-machine validation. Existing Windows-session authentication SHALL NOT be labelled anonymous, and testing SHALL NOT automatically relax guest, anonymous-access or SMB-signing policies on the host

### Requirement: VM-02 Provide the selected VMware environment runbook
The review SHALL specify VMware Host-only networking, dedicated Windows accounts, SMB and NTFS permissions, ordered acceptance gates and cleanup. Writing the runbook SHALL NOT count as configuring or validating the environment.
#### Scenario: Configure the selected isolated network
- **WHEN** an operator later executes the runbook
- **THEN** a conflict-free dedicated Host-only VMnet, host adapter and static guest address SHALL be recorded; other VMnets and host physical-network/firewall settings SHALL remain unchanged. Guest SMB access SHALL be scoped to the host test address without enabling SMB1, Guest or weakening signing policies
#### Scenario: Establish permission controls
- **WHEN** dedicated reader and denied standard accounts are prepared
- **THEN** both share and NTFS permissions SHALL be specified. Both accounts can read the fake APS directory, only reader can read the Denied marker, and neither can write. Extra inherited group permissions SHALL be checked rather than assuming removal of one user entry proves denial
#### Scenario: Accept the environment before the product
- **WHEN** the test operator evaluates results
- **THEN** G0 network isolation and G1 Windows account/permission baselines SHALL precede future product gates G2 through G4 and separate visual/real-AVEVA gate G5. Existing sessions SHALL be identified before each credential case. Every gate SHALL retain actual evidence or an explicit unexecuted/blocked status; an OS share success SHALL NOT certify product behavior
#### Scenario: Restore a reproducible baseline
- **WHEN** a scenario ends or a VM snapshot is restored
- **THEN** client sessions, handles and local credentials SHALL be reconciled separately from VM state; only registered test resources may be cleaned. Snapshot IDs, exact targets and redacted results SHALL be recorded without global connection deletion or credential-store clearing
#### Scenario: Access or environment protection checks fail
- **WHEN** encrypted VM or guest administrator access is rejected, guest network data mismatches the Host-only subnet, or a protected original file hash changes
- **THEN** the execution record SHALL report the actual native error, pending gate and protection exception rather than claim G0/G1 passed. User-selected test credential changes SHALL remain limited to the test clone; no offline authentication bypass, shared-network reset, security-policy weakening or secret-bearing log/CLI argument is permitted

### Requirement: UX-01 Design frontend in FigCraft first
New frontend workflows SHALL have editable native FigCraft frames, reusable controls, state previews and explicit operation labels before WPF implementation. Prototype navigation SHALL NOT count as completed backend behavior.
#### Scenario: Reviewing a connection screen
- **WHEN** the reviewer opens the design
- **THEN** draft saving, confirmed connection, failure recovery, grants, import and revocation can be assessed without installing dependencies or changing system connections

### Requirement: UX-02 Verify design-to-render fidelity
WPF implementation SHALL follow reviewed tokens and explicit control templates rather than default styles. Actual screenshots at 100%, 125% and 150% DPI SHALL verify layout, long paths, focus/error/disabled states and keyboard navigation against the FigCraft baseline. Architecture checks SHALL keep UI validation/input separate from coordinator execution and Platform serialization/system calls.
#### Scenario: Default WPF styling differs from design
- **WHEN** actual controls differ in appearance or state behavior from the approved design
- **THEN** delivery remains unaccepted until templates are corrected and actual render evidence is reviewed; design PNGs alone do not pass this gate


### Requirement: LC-01 Separate client installation, licence discovery and checkout
The system SHALL distinguish an installed E3D client, a licensing service, a discovered licence file and a successful feature checkout. A running service or reachable port SHALL NOT be presented as proof of a valid feature licence.
#### Scenario: Inspect the isolated clone
- **WHEN** the operator investigates whether the test VM contains E3D or licences
- **THEN** report the bounded inventory scope, installed licensing components, service state and endpoint results separately; do not install E3D, modify licences or start the original VM as part of this read-only investigation
#### Scenario: A copied server has a different identity
- **WHEN** a cloned VM contains licensing components or files
- **THEN** preserve the distinction between copied installation evidence and a valid checkout; record actual vendor checkout as unverified until explicitly exercised with an authorized real case

### Requirement: LC-02 Diagnose the selected test endpoint without touching the original
The investigation SHALL separate listener state, RULES initialisation, RMS feature queries, firewall source scopes and host routing with TUN active. It SHALL retain evidence and uncertainty rather than guessing a deeper licensing cause.
#### Scenario: RULES reports running without a listener
- **WHEN** TCP4546 fails on the clone loopback and RULES logs an RMS licence-detail initialisation failure
- **THEN** record the missing listener and subsequent service shutdown separately from host-side firewall failures; a running service SHALL NOT satisfy readiness
#### Scenario: Host-only source differs from copied firewall scope
- **WHEN** the old rule permits only NAT host192.168.173.1
- **THEN** a lab-only rule may allow host192.168.244.1 to clone192.168.244.10 on TCP4545/4546 and UDP4545 for the explicitly authorized test, with removal and verification afterward; do not alter the original VM, global network definitions or Clash configuration

### Requirement: LC-03 Verify a real application attempt with explicit evidence limits
The test SHALL use an existing host E3D installation with only the test VM as its process-scoped licence target, preserve existing sessions, and distinguish entry startup, project login and confirmed feature checkout. Failed or incomplete evidence SHALL remain explicit.
#### Scenario: Protected input and method approval
- **WHEN** a project password is required
- **THEN** obtain it only by a local protected handoff, exclude it from recorded output and configuration, and delete temporary protected storage after use; command-line exposure requires the user's explicit one-test method approval, which does not authorize that method in the product
#### Scenario: Finish the application test
- **WHEN** the attempt completes or reaches its observation limit
- **THEN** preserve timestamps, process IDs, sanitized vendor results and server-side evidence, terminate only owned safe test processes, remove temporary firewall rules and shut down the clone normally; no original-VM start, dependency installation or WPF implementation is authorized


### Requirement: LC-04 Read-only machine identity and licence binding diagnosis
The investigation SHALL compare vendor-produced fingerprints and declared licence criteria using the same selector, correlate machine-identity changes with vendor service records, and preserve the original VM. Diagnosis SHALL NOT implicitly authorize identity restoration, licence editing/rebinding or time adjustment.
#### Scenario: Compare a hostname-bound cloned installation
- **WHEN** installed echoid produces a fingerprint using selector0x8 and the rejected licence records declare selector0008
- **THEN** compare the composite codes using the vendor guide, retain event86 and pre/post-change timelines, and report the mismatch without substituting the unrelated default381E fingerprint
#### Scenario: Collect diagnostics without altering the target
- **WHEN** a temporary selector is needed or a running RMS timestamp is locked
- **THEN** place echoid.dat only in a separate lab working directory; do not change installed diagnostic configuration, unlock files, restart services, alter licence data or open firewall ports; publish field-whitelisted/hash evidence rather than raw licence strings
#### Scenario: Plan future identity changes
- **WHEN** a lab clone is to be renamed or otherwise assigned new identity
- **THEN** inspect licence binding before the change and record any resulting limitation separately from SMB G0/G1 acceptance; any repair requires a separate reviewed task


### Requirement: LC-05 Snapshot-first hostname restoration and layered retest
The authorized restoration SHALL snapshot the powered-off clone before renaming Windows to ZHANGSH, preserve Host-only networking, test accounts and shares, and separate real licence checkout from complete application startup. It SHALL NOT edit licence files/bindings or adjust time, nor start the original VM.
#### Scenario: Restore only the authorized machine name
- **WHEN** the pre-restore snapshot and baseline are verified
- **THEN** rename only the fixed clone, reboot normally, compare account SIDs, network/routes and share/NTFS ACL, verify the unchanged licence hash and matching vendor fingerprint, and retain rollback evidence
#### Scenario: A licence is issued but the application exits
- **WHEN** RMS records an AVEVA201 issue and release while E3D reports communications-file FNF7
- **THEN** mark licence checkout passed and full application startup/project login unconfirmed; preserve both results, never infer success from Monitor exit0 or retry automatically
#### Scenario: Finish this test without expanding credential approval
- **WHEN** the one authorized application test ends
- **THEN** remove temporary scoped licence rules, delete its temporary protected secret, preserve user sessions and shut down only the clone; an additional password-command-line attempt needs its own authorization and SHALL NOT bypass automatic rejection


### Requirement: LC-06 Preserve the vendor native launch channel during FNF diagnosis
The investigation SHALL compare saved cwd, initialization and effective TEMP/TMP before changing launch behavior. It SHALL retain standard-handle/stream behavior as part of the vendor startup contract, exclude passwords and communications-file contents from evidence, and require real project-window/runtime/licence evidence for success.
#### Scenario: A generated communication file still produces FNF7
- **WHEN** a redirected experimental launcher fails although the communications file is generated
- **THEN** retain metadata and compare native launch behavior rather than rewriting init, globally changing TEMP, relaxing ACL or declaring database damage
#### Scenario: Native startup succeeds
- **WHEN** no-redirection/native-handle tests enter MAR/ALL_NO_MDS/Model with both CreateNoWindow values
- **THEN** record the experimental standard-stream/handle mode as the failing boundary, preserve the known-good vendor contract and retain any SDK module-reporting limitation without claiming every channel detail was isolated

### Requirement: DV-01 Explicit local developer scaffold with ordinary remote presentation
VM-based remote simulation SHALL remain separate retained development scaffolding, excluded from product projects, initialization and publishing. Normal remote-user presentation SHALL NOT add VM/simulation flags or controls. Only an explicit local developer opt-in bound to machine and user SHALL permit supported scaffold actions; source and evidence SHALL remain truthful for maintainers.
#### Scenario: Product or developer tool starts without opt-in
- **WHEN** product initialization, Debug mode, environment variables or VMware detection occur without a matching local activation
- **THEN** no lab is loaded or enabled, no simulation UI appears, and supported host tools reject before checking runtime paths or connecting VIX
#### Scenario: Explicit developer activation
- **WHEN** an operator runs enable --local on a development computer
- **THEN** store a local-only machine/user-bound activation without starting a VM; a later explicit action still specifies the private runtime, and a copied, malformed, disabled or mismatched activation cannot enable another context
#### Scenario: Keep source and audience boundaries
- **WHEN** lab code or documentation is maintained
- **THEN** keep current Host/Guest sources under dev, frozen run snapshots under artifacts and secrets/binaries outside Git; normal product wording remains remote-project/service wording while developer records preserve VM source, failure and verification facts

### Requirement: DV-02 Traceable scaffold-only activation and readable call chain
The local switch SHALL control only development scaffolding, never ordinary remote product behavior. The developer index SHALL explain the existing call chain, source entries and failure returns within eight sections, with code-authored diagrams. Documentation theme changes SHALL NOT change product UI or introduce a VM startup requirement.
#### Scenario: Use ordinary remote behavior with the lab disabled
- **WHEN** the activation file is absent, false, corrupt or machine/user-mismatched
- **THEN** the product SHALL NOT read the lab switch, load LabVix or require VMware; ordinary remote outcomes depend on actual network, permissions, paths and services, with proposed product features still marked unimplemented
#### Scenario: Trace explicit local activation and dispatch
- **WHEN** a maintainer follows the index
- **THEN** show LabControl.Main -> SetLocal or RequireEnabled -> explicit runtime runner -> LabVix.RequireEnabled -> configured target verification -> action/result, with a source link and named method for each stage; enable/disable only writes local state and does not start/stop a VM or cancel an active run
#### Scenario: Distinguish current return contracts
- **WHEN** a supported lab command fails
- **THEN** document status returning0 for both enabled/disabled, controller-disabled stderr/78, invalid arguments/path/process failures generally stderr/2, and the runner's JSON stage/error with exit1; preserve exception-specific controller78, child exit-code propagation and direct guest-program exit2 rather than inventing a uniform error code
#### Scenario: Handle unknown completion honestly
- **WHEN** the VIX wait exceeds90 seconds
- **THEN** describe the outcome as unknown and prohibit automatic side-effect retries; handle cleanup SHALL NOT be described as guaranteed rollback or shutdown
#### Scenario: Maintain diagrams without duplicated implementation
- **WHEN** building DocFX with the existing modern template
- **THEN** render the Markdown flowchart and sequenceDiagram using bundled Mermaid without a CDN or new package, apply a small Yuzuha-inspired reading-theme override, keep readable text/tables as the contract, and verify source links and responsive light/dark presentation without running lab tools or a VM
### Requirement: MD-01 Model editing and snapshot-only backend execution
The frontend SHALL edit one authoritative model through two-way presentation binding and model-file persistence, without maintaining business rules or a duplicate field copy. Execution SHALL durably capture an immutable, secret-free input snapshot before side effects; the backend SHALL accept that snapshot, validate it and extract parameters for concrete scripts.
#### Scenario: Edit and execute different revisions
- **WHEN** a user executes revisionN and continues editing
- **THEN** execution uses only the frozen revisionN, later edits affect only a later attempt, and results/records correlate by AttemptId and Revision without overwriting the newer draft
#### Scenario: Reject without hidden defaults or side effects
- **WHEN** input is invalid, the file conflicts, or snapshot/required Record persistence fails
- **THEN** preserve editable input and actionable errors, never convert invalid ports silently to0, and reject execution before external side effects; rejected business inputs retain their snapshot where persistence succeeded
#### Scenario: Keep the frontend thin
- **WHEN** discovery, validation, credential authorization, connection policy or startup decisions are required
- **THEN** the backend owns the rules and returns results/patches; the binding adapter handles only presentation notification, editing and errors, and neither model nor backend depends on WPF controls or ViewModels

### Requirement: DV-03 Configurable development target without product activation dependency
Development VM and licence service parameters SHALL be model-configurable rather than fixed in active scripts. Target identity checks and local opt-in SHALL remain backend preconditions for lab actions only. Licence configurability SHALL NOT authorize key, binding or clock changes. The user-approved product package and separate test-tool package SHALL share a single backend implementation; frozen historical experiments remain evidence only.
#### Scenario: Configure a different test target
- **WHEN** a developer edits VM/VIX paths, expected identity, service address, share, licence ports or credential references
- **THEN** save the model, require explicit target confirmation and backend identity validation before actions, never follow an unchecked path automatically, and never embed passwords or transferable activation in model files
#### Scenario: Product and test-tool packaging review
- **WHEN** comparing separate distribution with a hidden Settings entry
- **THEN** deliver one product package plus an optional independent test tool sharing model/backend code, keep VM operations out of product startup, and treat any future developer Settings page as a model editor rather than isolation or business logic

### Requirement: FM-01 Composable feature boundary with one implementation
The login feature SHALL expose one host composition/lifetime boundary used by product UI and developer tooling. Removing the developer VM feature SHALL NOT remove ordinary login capability. Distribution SHALL verify identical shared backend assemblies rather than fork source. This boundary SHALL NOT be presented as verified compatibility with an unspecified CAF plugin API.
#### Scenario: Build both distributions
- **WHEN** packaging from a single source revision
- **THEN** compare Domain/Application/Platform/Bootstrap DLL SHA256 in both packages, reject divergence, exclude LabControl/LabVix/VMware dependencies from the product, and publish the feature inventory and package manifest
#### Scenario: Preserve concrete scripts without duplicate normal startup paths
- **WHEN** retaining FNF experiments for audit
- **THEN** keep them in non-shipping History and immutable artifacts, while normal product and developer model execution call the same coordinator and concrete scripts

### Requirement: DG-01 Diagnose deployment failures without inventing missing evidence
Diagnosis of original LaunchDesk tool deployment SHALL distinguish the actual running release/data directory, lost error feedback and the underlying copy failure. It SHALL preserve real tool files and original source; successful isolated copying SHALL NOT prove the actual target was writable or updated.
#### Scenario: Historical result has no cause
- **WHEN** Success=false, Error is empty and Receipt/log evidence is absent
- **THEN** report missing failure evidence, preserve operation IDs and timestamps, identify the Message-only/empty-error path, and do not assert a particular lock, permission or archive fault without evidence
#### Scenario: Check the same package safely
- **WHEN** checking archive integrity and copying with the same release engine into a new isolated target
- **THEN** retain the successful isolated result separately, do not write the real PMLLIB target, and state that no actual SeaGend update occurred
#### Scenario: Ordinary remote use
- **WHEN** the lab package is absent or local activation is disabled
- **THEN** ordinary remote behavior SHALL NOT depend on either, and configuring a remote endpoint SHALL NOT start a VM automatically

### Requirement: UX-03 Reopen DocFX theme visual acceptance
The theme SHALL remain pending user visual acceptance. Documentation build success SHALL NOT imply fidelity to the requested Yuzuha navigation, hierarchy or reading experience. New application frontend changes SHALL retain the FigCraft-first review boundary.
#### Scenario: Review the next theme revision
- **WHEN** proposing a revised DocFX theme
- **THEN** compare navigation regions, reading width, hierarchy and diagram presentation against Yuzuha, preserve content traceability and obtain visual review rather than declaring acceptance from fonts/colors or a successful build
### Requirement: AD-01 Manifest-driven feature registration independent of code presence
Hosts SHALL register and present only features declared by enabled XML Addins. Assembly presence SHALL NOT imply activation. Product and developer hosts SHALL reuse one AddinCatalog and backend implementation. Runtime activation SHALL use the manifest, not a second features inventory. VM operations SHALL additionally require the existing local opt-in and target checks.
#### Scenario: Code exists but the declaration is absent or false
- **WHEN** the host starts with the feature binary present but no matching enabled Addin
- **THEN** do not invoke its factory or create its feature window/workspace; developer model commands remain absent from help and direct invocation is rejected before model IO
#### Scenario: Compose the shipped login feature
- **WHEN** login.launch.addin.xml declares id=login.launch, enabled=true and contractVersion=1
- **THEN** compose the existing login UI and shared backend unchanged; with no login feature the current single-feature product exits normally without creating a workspace
#### Scenario: Reject invalid declarations
- **WHEN** XML is malformed, repeats an id, contains unsupported attributes/contract versions, DTDs or external entities
- **THEN** fail closed with the manifest filename and reason, without executing code, accessing an external entity or resolving declarations by last-file-wins
#### Scenario: Gate VM code without a second parser
- **WHEN** a developer invokes LabControl or LabVix
- **THEN** check local authorization and call the shared Addin host before VIX operations; absent/false VM Addin or missing host rejects the operation and does not start a VM
#### Scenario: Change manifests or distribute packages
- **WHEN** an operator edits/removes/disables a declaration or packages both hosts
- **THEN** document startup-time composition rather than hot unload, retain one source declaration copied into appropriate packages, remove the old features.json authority, and keep full CAF/dynamic third-party assembly loading outside the current implementation

### Requirement: DOC-01 Consistent delivery state and single-source manuals
Documentation SHALL distinguish implemented, proposed and historical scope, tie claims to dated evidence, and distinguish review dates from verification dates. User/developer/demo entries SHALL reuse one eight-section feature fact page rather than duplicate behavioral specifications.
#### Scenario: Read a manual or demo entry
- **WHEN** a reader chooses user, developer or core demo navigation
- **THEN** provide ordered preparation/reading steps, canonical feature anchors and source/evidence entries, and do not advertise proposed remote/TOML/grant controls as shipped
#### Scenario: Reconcile older evidence
- **WHEN** older discovery or licence experiment results remain available
- **THEN** label them as historical batches, preserve their outcomes, avoid summing overlapping test counts, and do not claim current VM or vendor availability from past success
#### Scenario: Compare original LaunchDesk understandability
- **WHEN** assessing the new documentation and metadata
- **THEN** show concrete improvements in feature lookup and failure traceability against inspected original evidence, separate documentation from UI claims, and leave novice/expert timing or compression gains unquantified without measurement

### Requirement: DOC-02 FigCraft and HTML engineering portfolio
The project SHALL provide an editable FigCraft narrative and a local HTML presentation from a project-manager perspective. It SHALL explain customer value, usability, execution logic, design tradeoffs, maintainability, evidence and delivery scope in at most eight narrative sections, referencing canonical feature facts rather than creating a second specification.
#### Scenario: Explore the portfolio
- **WHEN** a reader opens the HTML presentation
- **THEN** support accessible slide navigation, a contents view, source/evidence links, responsive reading and print styling, without autoplay or external runtime dependencies
#### Scenario: Demonstrate internal logic
- **WHEN** editing the illustrative Project, capturing its illustrative snapshot, selecting call-chain steps or toggling the illustrative Addin
- **THEN** explain the selected state using local presentation memory only, label it as a demonstration and never mutate product files, obtain credentials, create a target process or invoke VMware
#### Scenario: Review claims and editable design
- **WHEN** reviewing technology, usability and roadmap claims
- **THEN** distinguish implemented, proposed and historical scope, retain source/evidence navigation and overlapping-test caveats, avoid invented impact metrics, and deliver native editable FigCraft frames with inspected exported previews

### Requirement: DOC-03 Observable HTML to FigCraft to CSS round trip
Portfolio design SHALL originate from the actual rendered HTML at a declared viewport, preserving observed geometry, wrapping and computed styles as editable native layers. It SHALL return explicitly supported native variables/properties to CSS, identify the node-to-selector mapping and verify the returned result in the browser. Independent slide templates sharing only narrative or palette SHALL NOT be called design-instance consistency.
#### Scenario: Import and return styles
- **WHEN** revising the portfolio design
- **THEN** capture the real1600x900 DOM and screenshots, import native FigCraft layers, export six bound palette variables with FigCraft itself, and read back supported typography and cover geometry to generated CSS with native node IDs
#### Scenario: Verify visual evidence
- **WHEN** reviewing the returned HTML
- **THEN** provide same-size per-page side-by-side and overlay evidence, SHA256 input/native/output records, and computed-property checks; do not turn RGB error into an invented similarity percentage or pixel-perfect claim
#### Scenario: Edit beyond the current adapter
- **WHEN** an edit changes unsupported layout, effects, DOM structure or behavior
- **THEN** extend and verify the mapping or mark it unsupported, retain authored responsive/behavior code, and never claim that the changed design has been fully synchronized from a palette-only export

### Requirement: UX-04 DocFX reading style aligned with the portfolio
DocFX SHALL share the portfolio's native-exported palette and visual language while retaining documentation reading density, source/evidence links and existing feature-state semantics. The actual template SHALL provide navigable desktop/mobile reading surfaces; a standalone portfolio stylesheet alone SHALL NOT satisfy this requirement.
#### Scenario: Read and find documents
- **WHEN** opening manuals, the source index or developer diagrams
- **THEN** provide desktop file navigation and section navigation, mobile file/navigation controls, accessible local search/filtering and contained wide tables/diagrams without page overflow
#### Scenario: Trace style and behavior
- **WHEN** reviewing the new theme
- **THEN** retain actual browser screenshots, the DOM-derived editable DocFX snapshot, shared native variable export, bounded desktop-title CSS readback and file hashes; do not claim full automatic layout roundtrip or user visual acceptance from a successful build
#### Scenario: Preserve scope
- **WHEN** synchronizing the theme
- **THEN** keep product/WPF behavior and visuals, original/Rewrite repositories, VM state and dependency installation outside the change; preserve proposed/historical/implemented documentation distinctions
