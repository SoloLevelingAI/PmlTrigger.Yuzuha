using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Mcp;

/// <summary>在显式选择并核验的 AVEVA Host 上只读查询原生窗体和控件。</summary>
[McpServerToolType]
public sealed class PmlFormTools(YuzuhaRpcBridge bridge)
{
    /// <summary>读取 FMSYS 已加载或已显示窗体，保留原生顺序与续读索引。</summary>
    [McpServerTool, Description("List native loaded forms via FMSYS.FORMS(), or displayed forms via FMSYS.SHOWNFORMS() when shownOnly=true. Reuses the PMLFORMS query path. Requires explicitly selected verified session and FormQuery Host v1. Does not show, load, hide, kill, refresh or decorate any form. startIndex>=1, maxItems 1..1000 scanned slots, maxCharacters 1..262144 returned UTF-16 string characters excluding JSON. Follow NextIndex; calls are not a frozen snapshot. No automatic retry.")]
    public Task<string> ListPmlForms(bool shownOnly = true, int startIndex = 1, int maxItems = 100, int maxCharacters = 32768, CancellationToken cancellationToken = default) =>
        Query(new() { Kind = shownOnly ? "shown-forms" : "forms", StartIndex = startIndex, MaxItems = maxItems, MaxCharacters = maxCharacters }, cancellationToken);

    /// <summary>按原生属性窗口列出指定已加载窗体的 GADGET，排除普通成员与 MENU。</summary>
    [McpServerTool, Description("List GADGET members of one already loaded form using the native PMLFORMS attributes/attribute/objectType path. formName is a plain name without !!, 1..64 ASCII letters/digits/underscore starting with a letter. Does not load or change the form. maxItems limits scanned attribute slots, including non-gadgets; an empty page may still have NextIndex. Type comes from native gadget.Type(). Requires selected verified session and FormQuery Host v1. startIndex>=1, maxItems 1..1000, maxCharacters 1..262144 returned UTF-16 characters excluding JSON. Cross-call state may change; no automatic retry.")]
    public Task<string> ListPmlGadgets(string formName, int startIndex = 1, int maxItems = 100, int maxCharacters = 32768, CancellationToken cancellationToken = default) =>
        Query(new() { Kind = "gadgets", FormName = formName, StartIndex = startIndex, MaxItems = maxItems, MaxCharacters = maxCharacters }, cancellationToken);

    /// <summary>读取指定 GADGET 类型及固定元数据，属性读取失败明确标为 unavailable。</summary>
    [McpServerTool, Description("Read one native gadget's Type and fixed metadata: shown, active, visible, tooltip and callback text. formName and gadgetName are separate plain identifiers without !! or dots, 1..64 ASCII letters/digits/underscore starting with a letter. Does not read val, invoke callbacks, load forms or traverse hosted WPF/CONTAINER internals. Missing/non-GADGET fails; unsupported metadata has Status=unavailable and an Error, never inferred false. Requires selected verified session and FormQuery Host v1. maxCharacters 1..262144 returned UTF-16 string characters excluding JSON; oversized items fail. No automatic retry.")]
    public Task<string> GetPmlGadget(string formName, string gadgetName, int maxCharacters = 32768, CancellationToken cancellationToken = default) =>
        Query(new() { Kind = "gadget", FormName = formName, GadgetName = gadgetName, StartIndex = 1, MaxItems = 1, MaxCharacters = maxCharacters }, cancellationToken);

    private async Task<string> Query(PmlFormQueryRequest request, CancellationToken token)
    {
        try
        {
            var response = await bridge.QueryPmlFormsAsync(request, token).ConfigureAwait(false);
            return JsonSerializer.Serialize(response, YuzuhaJsonContext.Default.RunPmlCommandResponse);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException ||
            error is InvalidOperationException && error.Message.StartsWith("E3D_TARGET_", StringComparison.Ordinal))
        { throw new McpException(error.Message); }
    }
}
