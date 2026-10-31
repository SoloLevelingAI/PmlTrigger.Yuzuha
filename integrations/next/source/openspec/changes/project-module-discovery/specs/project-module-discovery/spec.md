## ADDED Requirements

### Requirement: PM-01 Respect intentionally shared project roots
The system SHALL preserve configured Project paths even when their product-version directory differs from the application version. It SHALL report the source rather than replacing E3D2.1 with E3D3.1.
#### Scenario: Shared E3D2.1 projects
- **WHEN** E3D3.1 initialization declares projects_dir pointing at E3D2.1
- **THEN** discovery inspects that declared location and does not rewrite it

### Requirement: PM-02 Inspect project initialization evidence
The system SHALL use C# to read bounded evars.bat, evars.init, launch.init and referenced configuration files and inspect bounded child directories. It SHALL identify Project codes from declarations such as APS000 rather than treating the display folder or APS000ID as the code. It SHALL NOT execute batch/init files.
#### Scenario: Referenced project declaration
- **WHEN** launch.init includes evars.init, which includes custom_evars.bat and a project evars file
- **THEN** a resolved existing project directory is returned with source file and line
#### Scenario: Dynamic or cyclic configuration
- **WHEN** unsupported expansion, control flow, encoding, limits or include cycles occur
- **THEN** discovery reports a diagnostic and does not claim a fully evaluated runtime environment

### Requirement: PM-03 Distinguish module declarations and hints
The system SHALL return Module candidates with source file and line where available. Explicit assignments, launch-dispatch names and executable hints SHALL remain distinguishable. A candidate SHALL NOT be described as proof of licence, permissions or successful login.
#### Scenario: Launch dispatch and executable clues
- **WHEN** launch.bat names Paragon and the installation contains adm.exe
- **THEN** PARAGON is marked LaunchDispatch and ADMIN is marked ExecutableHint

### Requirement: PM-04 Preserve editable choices
The system SHALL preserve explicit presets and manual edits. It SHALL prefill an empty value only from one distinct eligible declaration and SHALL keep multiple results as candidates for manual selection.
#### Scenario: Several available projects
- **WHEN** ABC and XYZ both have existing declared directories
- **THEN** both are shown and no first project is silently selected
#### Scenario: User preset
- **WHEN** a Project or Module preset differs from discovered candidates
- **THEN** the preset remains editable and the candidates remain visible

### Requirement: PM-05 Report actual installation discovery coverage
The system SHALL describe installation discovery as configured-directory scanning while no Registry adapter exists. Marine/AM, E3D and PDMS directory recognition SHALL NOT imply product login compatibility. Administration SHALL NOT be silently equated with Marine/AM.
#### Scenario: PDMS fixture and local E3D inventory
- **WHEN** a PDMS mon.exe is found in a configured directory
- **THEN** it is labelled PDMS, while absent real PDMS installations remain explicitly unverified
