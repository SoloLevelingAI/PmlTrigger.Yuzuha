using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>只读迁移预览的显式注册；不复用旧导入apply的写入语义。</summary>
public static class MigrationPreviewOperations
{
    /// <summary>提供唯一只读入口；由产品装配层显式加入目录。</summary>
    public static IEnumerable<OperationRegistration> Registrations()
    {
        using var schema = JsonDocument.Parse("""
            {"type":"object","additionalProperties":false,"properties":{"format":{"type":"string","enum":["rewrite","next","ribbon"]},"source":{"type":"string","minLength":1}},"required":["format","source"]}
            """);
        yield return new(new("migration.preview", 1, "Read bounded source material without activation or storage switching.", schema.RootElement.Clone()),
            new DelegateOperationHandler((operation, input, _, token) =>
            {
                var report = MigrationPreview.Preview(input.GetProperty("format").GetString()!, input.GetProperty("source").GetString()!, token);
                var data = JsonSerializer.SerializeToElement(report, MigrationPreviewJsonContext.Default.MigrationPreviewReport);
                return Task.FromResult(report.Complete ? OperationResult.Ok(operation, data) :
                    OperationResult.Fail(operation, 2, "MIGRATION_PREVIEW_INCOMPLETE", "来源预览不完整；请检查诊断后重新选择，不可激活或切换。", data));
            }));
    }
}
