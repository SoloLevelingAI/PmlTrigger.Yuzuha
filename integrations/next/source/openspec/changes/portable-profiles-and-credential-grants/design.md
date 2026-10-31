# Design review — not implemented

The authoritative eight-section explanation is [remote-portable review](xref:remote-portable-review). It answers BRD/PRD/SRS/FRD/SDD/SDS with concrete operations, frontend/backend pseudocode and failure reasons. This file indexes engineering decisions rather than repeating customer instructions.

| Decision | Review location | Implementation boundary |
|---|---|---|
| Same-user C# read-only worker, 8-second deadline, stale-reply rejection | Eight-section review §⑤–⑦ | No Task.Run-only cancellation claim; side-effecting connection timeout is OutcomeUnknown |
| Explicit UNC connection; optional confirmed free drive | Review §③⑥⑦ | No auto-disconnect, persistent reconnect, elevation or ACL changes |
| Share access, Project registration and real AVEVA validation separated | Review §③⑥⑧ | Init fingerprint/path/Revision invalidate manual confirmation; no vendor-file rewrite |
| Portable slot vs local binding and selected grants | Review §④⑥ | Kind and security fingerprints; no name-based or imported-ID authorization |
| Immutable generation and current-pointer publication | Review §④⑥ | Lock/Revision; local metadata excluded from export; not a cross-system transaction |
| TOML syntax-tree edits, JSON backup/migration | [Exact package research](xref:toml-selection) | Tomlyn 2.10.1 approved on 2026-10-28; installation explicitly deferred and round-trip tests pending |
| Native FigCraft first; explicit WPF templates and DPI evidence | [Design contract](xref:remote-portable-figcraft) | No WPF implementation in this review |
| Selected VMware Host-only environment | [Eight-section runbook](xref:remote-project-lab) | Static VMnet/IP, two standard accounts, share/NTFS matrix, G0–G5 gates; G0/G1 environment and ten OS checks passed; 16 product cases remain pending |

## Code entry points and planned changes

Current LoginLaunchCoordinator, feature Scripts and Infrastructure/Contracts remain unchanged at fee897f. Planned concrete scripts: CheckProjectAccess, ConnectProjectShare, CheckProjectRegistration, GrantLoginCredential, RevokeLoginCredential, ImportProfiles, ExportProfiles. These are proposed names, not existing source files.

The ViewModel validates/edit-displays; an ordinary input class carries frozen values and revisions; the backend coordinator explicitly calls concrete scripts. Neutral ports provide file probing, connections, documents, local secrets and records without knowledge of APS/DESIGN. Only Platform adapters reference TOML/OS APIs. Replace/adapt the existing IJsonDocumentStore with a format-neutral contract during approved implementation. ArchUnitNET rules must verify package/layer boundaries; existing architecture tests do not automatically cover these future classes.

## Approval and evidence

The [requirement delta](specs/portable-profiles-and-credential-grants/spec.md) is normative; the [task list](tasks.md) separates delivered review assets from pending approval, implementation and execution. Existing 99 tests belong to fee897f and do not verify this proposal. CRED_PERSIST_LOCAL_MACHINE means same Windows user/local machine persistence, not a per-module OS security boundary. Separately authorized G0/G1 lab setup and read-only licensing inventory have now been recorded. No dependency installation or product code change was made; product remote workflows and actual AVEVA checkout remain pending.


## Latest vendor test

Host E3D3.1 attempted AVEVA201 checkout against only the isolated clone and failed with RULES4546 socket10060; entry exit0 is not success. RULES logs an RMS licence-detail initialisation failure and stops. Meta TUN stayed enabled; VMnet1 was the selected route. Temporary clone-only firewall rules were removed. See [test evidence](xref:licence-checkout-test); the follow-up read-only diagnosis confirms hostname-only binding mismatch after the G0/G1 clone rename, and no product dependency or WPF change follows from this test.


Machine-identity planning must include licence binding before renaming a clone. Vendor selector0008 is Host Name; changed MAC/UUID are not the selected criteria for the22 rejected licence records. Keep the observed failure until a separate repair decision. Read-only evidence must not become an implicit permission to restore identity or rehost licences.


## Latest authorized restoration

User authorized a powered-off pre-restore snapshot followed by hostname-only restoration toZHANGSH. Licence file/hash and all Host-only/account/share invariants remain;10 SMB cases pass. Vendor hostname fingerprint matches, RULES4546 works, and RMS issues/releases AVEVA201. E3D then fails with communications-file FNF7, so complete application startup is not accepted. An optional second working-directory test was automatically rejected for additional command-line password exposure and was not executed. Temporary licence rules are removed; future remote tests need reviewed scoped access. Original VM remains off.


## Latest FNF startup and developer isolation decision

The original native LaunchDesk startup reaches MAR/ALL_NO_MDS/Model. Controlled cwd-only and quoting-only corrections still fail with redirected stdio; native standard-handle launches succeed with CreateNoWindow false and true. Preserve the vendor channel contract, use process/runtime/lease records instead of replacing its streams for logging. LC-06 proves existing vendor startup, not unimplemented product remote flows.

VM-based simulation is retained only as dev/RemoteProjectLab scaffolding, independent of production projects/bootstrap. Explicit enable --local binds machine fingerprint and user SID; normal initialization, Debug mode, environment variables or VMware discovery never enable it. Product remote endpoints use ordinary labels without a VM/simulation flag. Developer evidence stays truthful and accessible through a separate maintenance index. The switch is an accidental-use guard, not an OS security boundary.
## DV-02 Developer documentation boundary and diagrams

The switch authorizes only the independent dev executable graph. Product composition never queries it, so ordinary remote behavior has no activation dependency. The eight-section developer index maps existing source locations to activation, dispatch, exact exit/error channels and fixed-clone operations; disabling is neither cancellation nor shutdown. VIX timeout remains unknown completion, not permission to retry. Product features still awaiting implementation remain marked proposed.

DocFX uses default/modern plus a small reading override inspired by the local Yuzuha template, without copying its product-specific shell. Markdown Mermaid flowchart/sequenceDiagram are the sole diagram sources; built-in rendering needs no new dependency/CDN. Source tables remain readable without diagrams. This documentation-only change does not alter product code, WPF, VM power state or activation state.

## MD-01 / DV-03 / FM-01 implementation update

The later user-approved implementation shares a single LaunchModel/LaunchWorkspace and WorkspaceHost composition between the unchanged product UI and independent developer CLI. The product and development distributions compare identical backend DLL hashes; VM code ships only with development. LabSettings replaces active fixed target paths, shared LaunchModel owns licence parameters, and old experimental launchers remain non-shipping History. This implements the model/snapshot and package core, not the remote-connection/TOML/new-UI proposal above. CAF host API adaptation, actual VM parameter regression and theme visual acceptance remain pending.
