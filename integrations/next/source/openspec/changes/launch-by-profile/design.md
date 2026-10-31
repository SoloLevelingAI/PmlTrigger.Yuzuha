# Design decisions

The authoritative explanation, including frontend/backend pseudocode and trade-offs,
is [the eight-part feature](xref:launch-feature).
This change records normative requirements and acceptance scenarios, not a second
copy of the implementation narrative.

- Input information, ViewModel and backend coordinator have separate duties;
  explicit UI interaction submits immutable information to application ports.
- Reusable boundaries are executable ArchUnitNET rules; architecture tests do
  not claim to verify XAML, runtime side effects or visual quality.
- C# discovery supports this machine's real installations and editable defaults.
  Product-specific discovery is allowed; a general plugin framework is not required.
- List<Record> and NLog JSONL provide inspectable snapshots; evidence gates process
  creation. Restart history is supported; durable deduplication and crash recovery
  of running processes are outside this version.
- FigCraft native planning and actual WPF evidence are separately preserved.
  The original UI resources supply the visual baseline without original code dependencies.

Approved direct versions live in Directory.Packages.props; resolved dependencies
and rationale are indexed by [dependencies](xref:dependencies).
This version uses NLog, CommunityToolkit.Mvvm, NUnit, its adapter and test SDK,
coverlet.collector, FlaUI.UIA3, ArchUnitNET and its NUnit adapter. New packages
or additional implementation languages require review; selection is not automatic approval.

Actual test results and remaining vendor-environment limitations are recorded in
[verification](xref:verification) and RELEASE-V1.md.
