using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Mcp;

/// <summary>把共享操作映射到现有会话桥、生成器和结构化返回协议。</summary>
public sealed class YuzuhaOperationBackend
{
    private readonly YuzuhaRpcBridge _bridge;
    private readonly PmlFunctionTrustStore _trust;

    /// <summary>把共享操作映射到现有会话桥、生成器和结构化返回协议。</summary>
    public YuzuhaOperationBackend(
        YuzuhaRpcBridge bridge,
        PmlFunctionTrustStore trust)
    {
        _bridge = bridge;
        _trust = trust;
    }

    /// <summary>序列化只读会话发现结果，不连接目标。</summary>
    public string ListAvevaSessions()
    {
        return JsonSerializer.Serialize(
            _bridge.DiscoverSessions(),
            YuzuhaJsonContext.Default.AvevaSessionListResponse);
    }

    /// <summary>重新核验当前桥的目标身份并返回状态。</summary>
    public async Task<string> GetConnectionStatus(
        CancellationToken cancellationToken)
    {
        var status = await _bridge.GetConnectionStatusAsync(cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Serialize(
            status,
            YuzuhaJsonContext.Default.YuzuhaConnectionStatus);
    }

    /// <summary>按有序参数生成 PML 调用文本，拒绝非有限数值和不支持的类型。</summary>
    public string GeneratePmlCall(
        string methodName,
        List<DynamicParameterInput>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return PmlMethodSignatureGenerator.Build(
                methodName);

        var values =
            new DynamicParameter[parameters.Count];

        for (var index = 0; index < parameters.Count; index++)
        {
            var input = parameters[index]
                        ?? throw new ArgumentException(
                            "Parameter at index " + index + " is null.",
                            nameof(parameters));

            var value = NormalizeJsonValue(input.Value);
            if (input.Type is "double" or "real" or "number")
            {
                if (!double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                    throw new FormatException("Numeric parameters must be finite numbers.");
            }
            values[index] = new DynamicParameter(input.Type, value!);
        }

        return PmlMethodSignatureGenerator.Build(methodName, values);
    }

    /// <summary>经现有已核验会话桥执行一次命令，保留 Host 结果和信任提醒。</summary>
    public async Task<string> RunPmlCommand(
        string pmlCommand,
        CancellationToken cancellationToken)
    {
        try
        {
            var response =
                await _bridge.RunPmlCommandAsync(
                        pmlCommand,
                        cancellationToken)
                    .ConfigureAwait(false);

            response.FunctionTrustWarning =
                _trust.GetUntrustedWarning(pmlCommand);

            return JsonSerializer.Serialize(
                response,
                YuzuhaJsonContext.Default.RunPmlCommandResponse);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception.Data["YuzuhaDispatched"] is false)
        {
            throw new PmlDispatchPreflightException(exception);
        }
        catch (Exception exception)
        {
            return TransportFailure(pmlCommand, exception);
        }
    }

    /// <summary>执行指定表达式并读取结构化数组，分别计算完整统计和显示筛选。</summary>
    public async Task<string> RunPmlCommandList(
        string pmlCommand,
        string globalVar,
        bool deleteGlobalVar,
        bool includeEmpty = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response =
                await _bridge.RunPmlCommandListAsync(
                        pmlCommand,
                        globalVar,
                        deleteGlobalVar,
                        cancellationToken)
                    .ConfigureAwait(false);

            List<PmlListItem> items = new();
            List<string> unparsed = new();

            if (response.ResultList != null)
                foreach (var raw in response.ResultList)
                    if (TryParsePmlListItem(raw, out var item))
                        items.Add(item!);
                    else
                        unparsed.Add(raw);

            // L2: aggregate over the full set (before includeEmpty filtering).
            PmlListSummary summary = new()
            {
                Total = items.Count,
                Unset = items.Count(i => i.Empty && i.Type != "STRING" && i.Type != "ARRAY"),
                Blank = items.Count(i => i.Empty && i.Type == "STRING"),
                Zero = items.Count(i => !i.Empty && i.Type == "REAL" && IsZeroValue(i.Value)),
                EmptyArray = items.Count(i => i.Empty && i.Type == "ARRAY"),
                HasValue = items.Count(i => !i.Empty),
                ByType = items
                    .GroupBy(i => i.Type)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            // L3: optional filtering of empty items.
            var shown = includeEmpty
                ? items
                : items.Where(i => !i.Empty).ToList();

            PmlListToolResponse toolResponse = new()
            {
                Success = response.Success,
                Code = response.Code,
                ErrorMessage = response.ErrorMessage,
                PmlCommand = response.PmlCommand,
                Summary = summary,
                Count = shown.Count,
                Items = shown,
                IncludeEmpty = includeEmpty,
                UnparsedCount = unparsed.Count,
                Unparsed = unparsed,
                RequestId = response.RequestId,
                ServerRuntime = response.ServerRuntime,
                ServerTimeUtc = response.ServerTimeUtc,
                FunctionTrustWarning = _trust.GetUntrustedWarning(pmlCommand)
            };

            return JsonSerializer.Serialize(
                toolResponse,
                YuzuhaJsonContext.Default.PmlListToolResponse);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception.Data["YuzuhaDispatched"] is false)
        {
            throw new PmlDispatchPreflightException(exception);
        }
        catch (Exception exception)
        {
            return TransportFailure(pmlCommand, exception);
        }
    }

    /// <summary>
    ///     Formats a transport-level failure. The leading "PML RPC failed:"
    ///     marker stays stable for downstream parsing; the trailing triage
    ///     note keeps agents from treating a dead pipe or an unloaded host as
    ///     evidence that the user's PML function itself is wrong.
    /// </summary>
    private static string TransportFailure(
        string pmlCommand,
        Exception exception)
    {
        return "PML RPC failed: " + exception.Message +
               " (transport/connectivity failure — this does not prove the " +
               "PML function is wrong. Check get_connection_status or " +
               "list_aveva_sessions, confirm with the user whether the host " +
               "is loaded, and never retry automatically.)";
    }

    /// <summary>
    ///     Parses one ResultList item from the PML string format
    ///     "&lt;STRING&gt; '[depth] path &lt;TYPE&gt; value'" into a PmlListItem.
    ///     Path may be empty (element-level rows). Applies L1 normalization:
    ///     STRING values are unquoted; Unset / blank / empty-array values become
    ///     Value=null with Empty=true.
    /// </summary>
    private static bool TryParsePmlListItem(string raw, out PmlListItem? parsed)
    {
        parsed = null;
        if (string.IsNullOrEmpty(raw)) return false;

        var text = raw;
        const string wrapperPrefix = "<STRING> '";
        if (text.StartsWith(wrapperPrefix, StringComparison.Ordinal) &&
            text.EndsWith("'", StringComparison.Ordinal))
            text = text.Substring(
                wrapperPrefix.Length,
                text.Length - wrapperPrefix.Length - 1);

        var match = Regex.Match(
            text,
            @"^\[(\d+)\]\s*(.*?)\s*<([A-Za-z0-9_]+)>\s*(.*)$");

        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var depth)) return false;

        var type = match.Groups[3].Value;
        var rawValue = match.Groups[4].Value.Trim();

        PmlListItem item = new()
        {
            Depth = depth,
            Path = match.Groups[2].Value.Trim(),
            Type = type,
            Value = rawValue,
            Empty = false
        };

        // L1: normalize empty / unset / blank values.
        if (type == "STRING")
        {
            var unquoted = UnquotePmlString(rawValue);
            if (string.IsNullOrWhiteSpace(unquoted))
            {
                item.Value = null;
                item.Empty = true;
            }
            else
            {
                item.Value = unquoted;
            }
        }
        else if (rawValue.Equals("Unset", StringComparison.OrdinalIgnoreCase))
        {
            item.Value = null;
            item.Empty = true;
        }
        else if (type == "ARRAY" &&
                 Regex.IsMatch(rawValue, @"^0\s+Elements?$", RegexOptions.IgnoreCase))
        {
            item.Value = null;
            item.Empty = true;
        }

        parsed = item;
        return true;
    }

    /// <summary>
    ///     Strips one surrounding PML single-quote pair from a string display.
    /// </summary>
    private static string UnquotePmlString(string value)
    {
        if (value.Length >= 2 &&
            value[0] == '\'' &&
            value[value.Length - 1] == '\'')
            return value.Substring(1, value.Length - 2);

        return value;
    }

    /// <summary>
    ///     True when a REAL display like "0", "0mm", or "0pascal" is numerically
    ///     zero (unit suffix ignored).
    /// </summary>
    private static bool IsZeroValue(string? value)
    {
        if (value == null) return false;

        var match = Regex.Match(value, @"^-?\d+(\.\d+)?");
        if (!match.Success) return false;

        return double.TryParse(
            match.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number) && number == 0d;
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement element) return value;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                return element.GetRawText();
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                throw new FormatException(
                    "Parameter value must be a JSON string, boolean, " +
                    "number, or null.");
        }
    }
}
