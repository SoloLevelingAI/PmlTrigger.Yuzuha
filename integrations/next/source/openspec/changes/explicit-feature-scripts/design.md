# Design

Authoritative explanation: [feature sections ⑤–⑦](xref:launch-feature).

Concrete scripts have no step interfaces or registration DSL. Their names expose
what the product does. LoginLaunchCoordinator states their execution order;
Execution/ holds admission bookkeeping and record serialization.

Infrastructure.Contracts accepts neutral information: paths, ports, service names,
JSON values, process commands and secret handles. It cannot depend on Features.
Platform implementations also cannot depend on Features. ILaunchService and
IApplicationDiscovery are explicitly feature APIs and live beside their inputs.

SavedProfiles owns validation and optimistic revisions. JsonDocumentStore only
reads/replaces JSON. CheckAvevaLicence chooses RMS/RULES policy; probes only
measure TCP or service state. StartMonitor/StartMarineBatch choose argument order
and child environment; WindowsProcessHost only executes the supplied OS command.

The split adds concrete files but shortens the path from a functional question
to its implementation. Existing tests remain the behavior baseline; the new rule
must reject a real fixture referencing LaunchRequest from logistics.
