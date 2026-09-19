# Session UI recording

The existing NET35/NET48 PmlCommandMethod exposes:

```pml
!recordId = !!YuzuhaRpcHost.BeginLog(!record)
-- Execute the original application callback here.
!ok = !!YuzuhaRpcHost.EndLog(!record, !recordId)
```

The callback runs once. The returned event ID correlates nested operations safely. BeginLog persists a Before checkpoint; EndLog appends After and the complete caller-supplied array. Check the returned ID/boolean and `GetLastLogError()`; RPC dispatch success alone does not mean Log succeeded. `Log(!record)` remains a single-snapshot alternative. `GetLastLogPath()` returns the current session file.

Files are stored under `%LOCALAPPDATA%\YuzuhaToolkit\Records\<session-id>\session.jsonl` and `images\*.jpg`. One process/assembly lifetime shares a writer, even if the host object is recreated. Each JSONL line has sessionId, recordId, sequence and phase. Before records survive an interrupted operation; correlate completed events by recordId. AI analysis should use the latest phase for each event and report incomplete events rather than assume success.

Capture covers the desktop-clipped bounding rectangle of visible host-process top-level windows, including the main interface and floating forms. Hidden/minimized/cloaked windows are excluded. Occluding applications and gaps inside the rectangle are visible. JPEG quality is 80; original resolution is retained. Consecutive byte-identical JPEGs reuse the previous filename. Images are referenced with paths relative to the session directory.

PML ARRAY maps to Hashtable. Supported values are strings, numbers, booleans, null and nested Hashtables with numeric indexes. **PMLNetAny/form/database objects are not serialized automatically.** Convert them to appropriate scalar values in PML first. CONTAINER controls are pictured but their child values are not automatically inspected. The full record is a snapshot supplied by PML; the DLL does not read gadget state or wait for asynchronous redraw.

The FormUse prototype preserves the user's callback/return behavior and known issues. See the [E3D live validation report](validation/2026-09-19-e3d.md) for passed tests, recorded problems and coverage limits. No local screenshots, project records or vendor reference binaries are included in this source change.
