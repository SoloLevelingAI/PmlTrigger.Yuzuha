using System.Text.Json.Nodes;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    private interface IMigrationWorkspaceCodec
    {
        MigrationWorkspace Read(JsonObject document);
        JsonObject Write(MigrationWorkspace workspace);
    }

    private sealed class WorkspaceV1Codec : IMigrationWorkspaceCodec
    {
        public MigrationWorkspace Read(JsonObject document) => new(
            IndexRecords(document, "Applications", CopyApplication),
            IndexRecords(document, "Licenses", CopyLicense),
            IndexRecords(document, "Tools", CopyTool),
            IndexRecords(document, "Profiles", CopyProfile),
            ReadSearchRoots(document),
            (bool?)document["NotifyOnDisconnect"] ?? true);

        public JsonObject Write(MigrationWorkspace workspace) => new()
        {
            ["SchemaVersion"] = 1,
            ["Applications"] = CopyRecords(workspace.Applications),
            ["Licenses"] = CopyRecords(workspace.Licenses),
            ["Tools"] = CopyRecords(workspace.Tools),
            ["Profiles"] = CopyRecords(workspace.Profiles),
            ["SearchRoots"] = workspace.SearchRoots.DeepClone(),
            ["NotifyOnDisconnect"] = workspace.NotifyOnDisconnect
        };

        private static JsonArray CopyRecords(Dictionary<string, JsonObject> records) =>
            new(records.Values.Select(record => record.DeepClone()).ToArray());
    }
}
