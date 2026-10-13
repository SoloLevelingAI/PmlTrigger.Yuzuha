# Addin startup investigation, 2026-10-13

An E3D 2.1 Model session stalled during ordinary startup. The installed
`YuzuhaAddin` constructor executed `PML REHASH ALL` before creating the RPC and
executor objects. Rebuilding the configured PML libraries emitted index-write
access errors and a large volume of duplicate-file warnings.

The main UI was initializing its message panel:

```text
WindowManagerImpl.LoadWindowLayout
  -> MessageAddin.InitialiseForDisplay
  -> SystemControl.AppendMessage
  -> TextBoxBase.AppendText
```

The observed RVM transfer helper was inactive at the stalled startup sample.
The investigation did not attribute this stall to a closed or missing Godot
viewer.

## Repair and observations

| Stage | Observed pending system-message entries | Outcome |
| --- | ---: | --- |
| Original startup constructor and duplicate index cache | 465,270 | Startup stalled while displaying messages |
| Constructor full rehash removed | 155,102 | Existing duplicate cache still stalled startup |
| Full rehash removed and local duplicate index records reconciled | 0 after initialization | Model session responsive |

The final zero is a measurement of the pending buffer after initialization,
not a count of every message generated during startup.

After this recovery, one manually requested `PML REHASH ALL` regenerated the
original duplicate index. A subsequently started Draw session was unresponsive
with 155,106 pending messages, including duplicate-file warnings, even though
the repaired addin remained installed. Restoring the reviewed index again and
starting a second project's Design/Model session produced a normally displayed,
responsive window with no pending startup messages. This reproduces the separate
cache-maintenance issue and confirms that the constructor change alone does not
clean existing physical library duplicates.

The local index recovery removed 19,335 ignored duplicate entries and verified
that all 5,287 first-match name lookups still resolved to the same source paths.
PML and resource files were retained, and the E3D message panel remained enabled.
This recovery was specific to the inspected local cache; the source patch does
not implement a general customer-library cleanup operation.

The source change removes the constructor's unconditional rehash and keeps the
`YuzuhaRpc`, `YuzuhaExcuter` and version initialization calls. The repository's
existing `PMLLIB/pml.index` contains those object entries. Rehash remains an
explicit maintenance step after PML files or search paths change.

## Duplicate source identified

A later filesystem and source audit identified two successful installation
transactions from the SeaGend/PML tool manager created on the same day. Its
transaction storage was inside the active PML library target:

```text
<active-library>/.seagend-manager/<transaction>/backup/...
<active-library>/.seagend-manager/<transaction>/stage/...
```

Both rollback backups and extracted staging copies remained on disk and were
included in the actual PML index. They accounted for 19,234 indexed records.
Filtering these transaction paths from the diagnostic index retained all 5,287
distinct names and reduced duplicate records from 19,335 to 101. Some backup
paths also preceded their production counterparts in the recorded search order.

This establishes the major source of duplicates in this incident. The addin's
automatic full rehash triggered and amplified the issue; the manager's choice
of transaction location populated the active search tree with extra copies.

The manager needs separate transaction storage outside every active PML search
root, with existing receipts and rollback backups migrated together. Staging
cleanup should also be verified. Keeping the transaction folder under a name
starting with a dot did not exclude it from the observed PML scan. The Yuzuha
constructor patch remains a startup improvement; this PR does not modify the
manager or relocate its live transaction files.

## Verification scope

- Live verification used the installed 0.3.2 package and E3D 2.1 / NET48. The
  constructor in main at commit `04617bf` contained the same full-rehash call.
- After both local repairs, the Model window displayed normally, the VIEW
  ribbon tab switched, and expanding the model tree displayed project nodes.
- Built-in session selection and connection-status checks returned
  `Connected=true` and `TargetVerified=true`. The host reported module `Unknown`;
  that result does not independently validate the module identifier.
- No model edits or RVM export were part of this startup validation.
- AM/PDMS NET35 and other E3D versions require their own restart verification.

Raw process dumps, screenshots, engineering data, machine paths, saved login
arguments and local PML library contents are not included in this record.

## Follow-up reminder

- [ ] Include the constructor repair in the next release's Setup and Agent packages.
- [ ] Verify that installation/update provides a usable package index before startup.
- [ ] Provide a reviewed, backed-up recovery procedure for sites with duplicate caches.
- [ ] Move the tool manager's transaction storage outside active PML roots and migrate existing rollback history safely.
- [ ] Restart-test AM/PDMS NET35 and the remaining supported E3D profiles.
- [ ] Confirm that a routine restart does not invoke a full PML library rehash.

Updates can replace the locally repaired addin. A later full rehash can also
reintroduce duplicate entries from a site's physical library copies. Retain
the release fix and the separate cache-maintenance follow-up when closing this
work item.
