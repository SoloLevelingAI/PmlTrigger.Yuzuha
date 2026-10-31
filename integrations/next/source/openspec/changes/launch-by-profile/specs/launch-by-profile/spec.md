## ADDED Requirements
### Requirement: Editable local discovery
The system SHALL discover installed AVEVA monitor entries through bounded read-only C# scans and present every candidate with editable values and provenance.
#### Scenario: Multiple installed monitors
- **WHEN** two E3D monitors and one Marine monitor exist in configured roots
- **THEN** all three candidates are returned and Marine mon.exe uses Monitor protocol
#### Scenario: Rescan after manual editing
- **WHEN** the user changes a prefilled value and rescans
- **THEN** the current editor value is preserved
### Requirement: Evidence before side effects
The system SHALL freeze each submission and persist required evidence before process creation.
#### Scenario: Evidence failure
- **WHEN** creation-intent evidence fails
- **THEN** no process is created
#### Scenario: Uncertain creation
- **WHEN** a process may have been created but confirmation fails
- **THEN** the system requires manual verification and does not retry
### Requirement: Independent implementation
The system SHALL keep product assemblies independent of old product source and tests.
#### Scenario: Boundary regression
- **WHEN** a forbidden dependency is introduced
- **THEN** architecture tests identify the violated rule

### Requirement: Explicit information and execution boundaries
The system SHALL keep input models and requests free of ViewModel, WPF notification and command dependencies. ViewModel SHALL only adapt the same LaunchModel for editing and presentation, displaying validation owned by the backend. LaunchWorkspace SHALL persist draft changes and freeze/persist execution input before calling the shared coordinator, as refined by MD-01.
#### Scenario: User edits a field
- **WHEN** an input binding changes a displayed field
- **THEN** the authoritative model, backend-owned validation display and draft file update without starting a process or calling the execution coordinator
#### Scenario: Caller mutates its input after submission
- **WHEN** the caller changes a submitted input collection
- **THEN** the submitted request and recorded snapshot retain the original values

### Requirement: Saved configuration fidelity
The compatibility saved-profile ExecuteAsync API SHALL reject missing, stale or edited unsaved configurations, excluding per-attempt identity and dictionary ordering. The product and developer model editors SHALL instead execute their durably captured draft through ExecuteSnapshotAsync without requiring a separate named-profile save; draft generation conflicts SHALL still reject execution.
#### Scenario: Modified executable before saving
- **WHEN** the compatibility saved-profile API receives an executable changed without another successful save
- **THEN** the coordinator rejects the request and creates no process
#### Scenario: Execute the edited model
- **WHEN** the user edits the same model and clicks Execute
- **THEN** save the draft and complete secret-free input snapshot, validate and execute that snapshot with the shared coordinator, independent of a separate named-profile Save click

### Requirement: Inspectable persisted history
The system SHALL keep detached List<Record> snapshots and a single NLog JSONL evidence stream. Normal reopening SHALL restore valid historical records and continue sequence numbering. Passwords SHALL remain outside ordinary inputs, saved profiles and snapshots.
#### Scenario: Reopen the application
- **WHEN** an existing evidence stream is reopened and a record is appended
- **THEN** earlier valid records remain readable and the new sequence exceeds their maximum
#### Scenario: Truncated final record
- **WHEN** a malformed tail is encountered
- **THEN** the original bytes are preserved, damage is detectable, and later valid records remain readable

### Requirement: Reusable architecture guards
The system SHALL enforce ARC-01 through ARC-06 with ArchUnitNET and executable forbidden-dependency fixtures. Only the explicit Desktop.Bootstrap namespace SHALL reference concrete Platform types from Desktop. Information DTOs SHALL not depend on INotifyPropertyChanged or ICommand.
#### Scenario: A new Desktop service bypasses the composition root
- **WHEN** Desktop.Services references a concrete Platform type
- **THEN** the same rule used for production architecture rejects the dependency

### Requirement: Reviewable visual and documentation evidence
The feature SHALL preserve editable FigCraft planning, inspected previews and actual UI verification separately. DocFX SHALL index role-oriented eight-part feature documentation and these OpenSpec scenarios without duplicating their authoritative text.
#### Scenario: A new maintainer locates the feature
- **WHEN** the maintainer opens the document index
- **THEN** the frontend, backend, specification, design and verification entry points can be found directly
