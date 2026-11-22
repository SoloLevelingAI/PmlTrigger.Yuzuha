---
name: yuzuha-setup
description: Install or update the extracted Phayton Windows program using a reviewed installation plan.
---

# Yuzuha Windows Setup

Read [references/INSTALL.md](references/INSTALL.md) completely before tool use.
The build places the matching installation guide there. Resolve the bundle
root as two levels above this skill directory; it contains `setup`, `skill`,
`.setup-payload.json`, `BUNDLE-SHA256SUMS.txt`, and one Setup EXE.

1. Verify all paths and SHA256 entries in `BUNDLE-SHA256SUMS.txt` before
   executing the helper or EXE. Reject absolute/traversal paths. A bundled
   hash detects corruption, not publisher authenticity; use the user's trusted
   download and separately supplied archive hash when available.
2. Read the current version section of `INSTALL.md` and `VALIDATION.md`. Use
   `locate <new-json>` to find existing Phayton programs. Confirm the Windows
   account and installation directory. Keep `PmlTrigger` in the folder name.
3. Create a request with the actual directory, `integrationMode: "none"`,
   an empty `environments` array, and empty `mcpJson` and `skillTarget`.
   Run `plan` with `.setup-payload.json` and the supplied `skill` argument.
   Present the Markdown program preview and wait for approval before execution.
4. Execute the same EXE with the approved `/PLAN`, `/PLANHASH`, and matching
   `/DIR`. Use the documented silent flags only after approval. UAC cannot be
   bypassed: an unelevated agent must request elevation through its supported
   process launcher or ask the user to launch it. Wait for exit and inspect
   the matching `result.json`/`result.md` for the program result.
5. Use PmlTwin to deploy the installed PMLLIB and PMLUI source folders. Review the two deployment destinations
   and confirm the EVARS switch separately after both resources verify.

On failure, record the error and available reports; do not blindly retry,
delete ownership markers, overwrite conflicts, or run vendor EVARS scripts.
Do not launch AVEVA, rehash PML, rebuild runtimes, or index knowledge as an
installation side effect. Preserve user data and D-drive development files.

Report the program installation, any separately approved deployments or EVARS
changes, and the scope of actual client verification.
