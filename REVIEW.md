# REVIEW — multi-party review of the open-source tree

This tree is a **sanitized export mirror** of the private source at
`I:\Ds-Harness\202608\portable-build\source`. It exists for review only —
never edit here; always edit the private canonical tree and re-export.

## How to review (per reviewer)

1. Clone / open this directory (git history: `git log`).
2. Write findings to `reviews/reviewer-<name>.md` — one file per reviewer so
   comments stay independent. Suggested sections:
   - ✅ 结论 (approve / conditional / reject)
   - 🔴 阻断项 (must fix before publish)
   - 🟡 建议项 (should fix / consider)
   - ❓ 问题 (questions for the author)
3. Sign off in `README.md` status checklist after resolution.

## Checklist

### R1. Sensitive information sweep — 🔴
- [ ] No `wxid_*` / personal OneDrive paths anywhere (config, docs, comments)
- [ ] No credentials / API keys / session data
- [ ] No machine-specific absolute paths in committed files

### R2. AVEVA redistribution — 🔴
- [ ] No `PMLNet.dll`, `Aveva.*`, `Infragistics.*`, `ForeignLanguage.dll`
      committed (check `bin/`, `lib/`, history)
- [ ] README states AVEVA runtime is a prerequisite, not bundled

### R3. Third-party licenses — 🟡
- [ ] `NOTICE.md` inventory is complete and accurate
- [ ] License files for permissive deps referenced (MIT/BSD) where applicable

### R4. Ownership of PlantHost.Rpc — ✅
- [x] Project-owned; bundled dependency and license inventory are documented
      in `THIRD-PARTY.md`.

### R5. BfsCache / SQLite patch — ✅
- [x] SQLite implementation excluded; only the optional `IBfsCache` contract
      remains.

### R6. csproj HintPath parameterization — ✅
- [x] Both host projects use `$(AvevaInstallDir)` through
      `src/build/AvevaSdk.props`; see `docs/build-configuration.md`.

### R7. Example data anonymization — 🟡
- [ ] DBREFs / real element names (`/ZONE-CIVIL-AREA03`, `/100-FW-202`, …) in
      docs and skill examples replaced with generic placeholders

### R8. Build reproducibility — 🟡
- [ ] `NuGet.config` restore works offline (packages-offline) or with network
- [ ] Build commands documented in README

### R9. PML release readiness — 🔴
- [ ] Dynamic DBREF/global-variable/attribute inputs are strictly validated
- [ ] Examples are removed from the default Addin and `pml.index`
- [ ] Empty Spiral example is implemented or removed
- [ ] Object-graph traversal reports duplicates/truncation safely
- [ ] Runtime discovery supports explicit portable configuration
- [ ] Net48 and Net35 claims are backed by real AVEVA integration evidence

Detailed bilingual findings:

- `docs/pml-open-source-review.zh-CN.md`
- `docs/pml-open-source-review.en.md`

## Review status

| Reviewer | Date | Verdict | File |
|---|---|---|---|
| (open) | | | `reviews/reviewer-1.md` |
