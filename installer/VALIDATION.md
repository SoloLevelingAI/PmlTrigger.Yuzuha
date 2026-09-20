# Windows Setup v0.3.3 validation — 2026-09-20

The maintainer reported successful installation testing of preview11 and
requested publication. v0.3.3 retains that installation logic, updating release
identifiers and documentation. This is a maintainer-reported result, not a claim
of independent testing of every AVEVA/client/UAC combination. The historical
preview notes below retain their original test boundaries.

Preview11 updates instructions, not environment-editing behavior: pre-preview
MCP/Skill choice, verified client/account paths, no repeated questions for explicit
choices, native installation-scope documentation, and English auto-plan/mode
guidance. Delivered installation guide copies must be byte-identical. Agent
behavior and VM/UAC/client loading are not proven by documentation or packaging
checks. The standard Skill validator requires PyYAML, unavailable in this build
environment; instructions and references are reviewed separately.

Preview10: program-generated AVEVA plans via plan-auto; product-root EVARS
precedence; explicit aveva/none integration mode; empty default integration
requests rejected; environment checksum verification and separate integration
summary in results. Native Inno adds an explicit installation-scope page.

Executed NET48 tests: automatic E3D+PDMS plan without environment-file writes,
empty-default refusal, explicit none acceptance, conflicting mode refusal,
unknown/ambiguous/incomplete/no-candidate refusal, root versus PMLLIB/tools
copy precedence, result summary distinctions, and existing supplied EVARS
copy/encoding/SET/rollback regression coverage. Live VM installation and
Workbuddy behavioral tests remain to be performed by the maintainer.

Preview9 adds an Agent archive entry, a dedicated installation Skill, and a
complete bundle SHA256 inventory. The redundant startup information dialog is
removed; account/path guidance is on the directory page. Administrator startup
requirement remains enabled. VM/UAC and live Agent/client loading are not
claimed by archive or plan-only checks.

Authored and tested by OpenAI Codex at the maintainer's request. Existing 0.3.2 Host and unchanged NET10 Native AOT payload. Official Inno Setup 6.7.3 compiler; generated EXE is unsigned.

## Preview8 verification boundary

Executed: NET48 regression suite including the supplied EVARS copies, native
discovery interchange fields/Unicode sanitization, and absence of a WinForms
assembly reference. Inno 6.7.3 compiles the native page script.

All configuration pages are native Inno controls in NativePages.iss.
The WinForms window and its project references have been removed. The helper
is headless; native pages submit JSON to the existing plan/approval workflow.

Preview8 requires administrator privileges at startup and updates PMLLIB from
the maintainer's D-drive source, excluding the portable bootstrap (see
PENDING-SOURCE-REVIEW.md). This version requires fresh VM/UAC and AVEVA tests;
the install/update/uninstall results below belong to earlier previews and
must not be interpreted as elevated Preview8 end-to-end validation.

## Historical executed coverage

- Preview7 generates unquoted SET assignments as requested by the maintainer. Regression asserts no double quotes in the generated assignment lines; original vendor-file quotes remain untouched. The controlled CMD test includes spaces in paths and still verifies final search lists.

- Preview6 supersedes the earlier in-place assignment strategy. The maintainer's supplied PDMS evars.bat (SHA256 5E03EF10FA943B29790D470F5611384A1EF426D7987164BD515136B5DC8C3E22) and E3D evars.init (6426D9C2DE19901974BFB2AAF1FF5F1319335A2C939B8BE8CE30CEB42E14EBB9) are tested as local copies, never executed or distributed. All original bytes are retained and the three-line integration block is added after vendor processing/custom calls. Tests check repeated planning, install/update/uninstall and original-file preservation. A separate synthetic SET/FOR/ECHO-only CMD test verifies normalization, final search lists and the unchanged derived reports path; no supplied script or external CALL is executed. AVEVA/VM launch validation remains separate.

- Preview5 replaces whole-file syntax gating with focused assignment edits. Tests preserve the reported unrelated IF EXIST line and x86 path, check three-line changes, omit unnecessary YuzuhaFramework, refuse ambiguous target assignments, and retain encoding/idempotence/rollback coverage. Historical control-flow rejection claims below describe earlier previews; unrelated commands are now preserved, not executed. A repository-owned SVG icon is rendered to seven ICO sizes and used by Setup, helper and shortcuts.

- Preview4 regression uses EVARS.INIT under Program Files (x86), including the reported aveva_design_plots assignment. Discovery matches EVARS case-insensitively, BAT fallback refuses a neighboring EVARS.INIT, and ordinary path parentheses no longer fail validation. Compound commands, control flow, redirection and continuation remain refused. Scan-limit notices are no longer labelled access errors.

- Preview3 adds product/version recommendation tests (Plant/PDMS, Marine, E3D, unknown version refusal), INIT preference, and UTF-8/GBK/UTF-16 byte-preservation plus repeat-edit tests. Normal UI removes redundant confirmation/encoding columns; optional AI integration moves to its own tab.

- NET48 helper and regression tests: JSON merge, unrelated data, idempotence, conflicts, disabled/duplicate entries, atomic replacement, rollback and concurrent-edit refusal.
- Actual compiled EXE in unique temporary directories with synthetic PDMS BAT, E3D INIT, MCP JSON and Skill targets. No live AVEVA configurations changed.
- Preview without deployment; target edits after preview cause refusal before installation.
- Chinese silent install and English silent update; two environments, UTF-8 BOM preservation, MCP/Skill, registry location and actual success reports.
- Windows uninstaller restores originals byte-for-byte and retains synthetic user data.
- Unsupported control flow, non-batch files and unconfirmed targets refused.
- Additional regression covers registry locate, wrong approval hash refusal, subsequent client attach without reinstall, and uninstall restoration.
- AOT hashes verified; no PS1, vendor reference assemblies or local databases in payload.
- Read-only registry discovery found local E3D 2.1, E3D 3.1 and Plant 12.1 candidates; launcher correctness is not established by discovery.

## Corrected during development

JSON-null prior state broke fresh installation; fixed. Inno post-install exceptions could report exit zero; explicit failure code added. Shortcuts now respect /NOICONS. Bilingual test error output now has explicit decoding.

## Still requires VM acceptance

Local Windows tests are not tests on the user's VM or real AVEVA launch chain. Full bilingual interactive pages, keyboard/DPI, OS variants, restricted permissions and interruption recovery need further review. Earlier welcome layout was inspected; silent language selection is not visual localization verification.

Complex INIT/BAT, Codex TOML auto-registration, and legacy PS1/preview1 migration are unsupported. AI clients do not automatically scan the registry. Recovery is not a power-loss transaction; interrupted updates may leave uninstall metadata/support files to inspect. Multi-file uninstall failure under permission changes is not acceptance-complete. Backups and reports are retained.

No production AVEVA configuration/model or remote GitHub release was changed.
