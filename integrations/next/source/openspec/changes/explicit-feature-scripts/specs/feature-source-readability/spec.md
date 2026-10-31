## ADDED Requirements

### Requirement: Concrete C# feature reading path
The system SHALL present login launch ordering in LoginLaunchCoordinator and product-specific behavior in named concrete C# scripts without an additional workflow engine or an interface for every script.
#### Scenario: Inspect Monitor arguments
- **WHEN** a maintainer opens StartMonitor
- **THEN** the eight ordered arguments and initialization-file requirement are directly visible
#### Scenario: Inspect application discovery
- **WHEN** a maintainer opens FindInstalledAveva
- **THEN** search roots, limits, product matches and editable default assignments are directly visible

### Requirement: Neutral logistics boundary
Infrastructure contracts and Platform implementations SHALL NOT depend on feature types. Concrete feature scripts SHALL translate feature inputs into generic process, file, network and storage operations.
#### Scenario: Feature-aware infrastructure regression
- **WHEN** a logistics type references LaunchRequest
- **THEN** ARC-07 rejects the compiled dependency using the same rule as the production check
#### Scenario: Licence policy inspection
- **WHEN** a maintainer opens CheckAvevaLicence
- **THEN** local RMS, remote RMS and optional RULES choices are visible, while probes only accept service names or host/port

### Requirement: Behavior preservation
The refactor SHALL preserve UI, saved JSON compatibility, immutable snapshots, saved-content validation, secret exclusion, attempt admission, pre-create evidence gating and post-create uncertainty handling.
#### Scenario: Existing safe launch flow
- **WHEN** the user discovers, edits, saves and starts a safe fixture
- **THEN** the same UI operations produce creation feedback and inspectable records
#### Scenario: Missing initialization file
- **WHEN** the Monitor script receives a missing initialization file
- **THEN** the process host is not called
