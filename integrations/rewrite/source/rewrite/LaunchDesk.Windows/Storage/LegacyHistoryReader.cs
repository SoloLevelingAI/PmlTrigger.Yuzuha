namespace LaunchDesk.Windows;

// 仅只读兼容；V1回执缺少V3准入和不可变计划证据。
internal static class LegacyHistoryReader
{
    internal static IEnumerable<DeploymentReceipt> Read(ToolDefinition tool)
    {
        if (string.IsNullOrWhiteSpace(tool.LegacyRecordsDirectory)) yield break;
        var key = CanonicalDigest.Text(Path.GetFullPath(tool.TargetDirectory).TrimEnd('\\').ToUpperInvariant());
        var roots = new[]
        {
            Path.Combine(tool.LegacyRecordsDirectory, "Updates", key),
            Path.Combine(tool.TargetDirectory, ".seagend-manager")
        };
        var seen = new HashSet<Guid>();
        var found = false;
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            using var pin = WindowsPaths.PinPath(root);
            foreach (var directory in Directory.EnumerateDirectories(pin.Path.Path).Take(10000))
            {
                var file = Path.Combine(directory, "receipt.json");
                if (!File.Exists(file)) continue;
                using var input = PackageReader.SourceStream(file);
                if (input.Length > 32 * 1024 * 1024) throw new InvalidDataException("Legacy receipt exceeds limit.");
                using var document = JsonDocument.Parse(input);
                var data = document.RootElement;
                var id = Guid.TryParse(Text(data, "Id"), out var parsed)
                    ? parsed
                    : new Guid(Convert.FromHexString(CanonicalDigest.Text(file)[..32]));
                if (!seen.Add(id)) continue;
                found = true;
                var started = DateTimeOffset.TryParse(Text(data, "Started"), out var time)
                    ? time
                    : File.GetLastWriteTimeUtc(file);
                var status = Text(data, "Status");
                yield return new DeploymentReceipt(new OperationId(id), tool.Id, "legacy", tool.TargetDirectory,
                    "legacy", "LegacyReadOnly", [], started, null,
                    "旧版记录：" + status + "。原备份保留，请使用旧版核验或回滚；新版本不推断旧执行权。", PlanId: Guid.Empty);
            }
        }

        if (!found)
            yield return new DeploymentReceipt(
                new OperationId(
                    new Guid(Convert.FromHexString(CanonicalDigest.Text(tool.Id + "/legacy-missing")[..32]))), tool.Id,
                "legacy", tool.TargetDirectory, "legacy", "LegacyUnavailable", [], DateTimeOffset.MinValue, null,
                "旧更新记录路径已保留，但未找到可读记录：" + tool.LegacyRecordsDirectory);
    }

    private static string Text(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? "" : "";
        return "";
    }
}