using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Mcp;

/// <summary>本机登记的 PML 源码身份、标量签名与独立调用权限；不是 AVEVA 已加载版本的证明。</summary>
public sealed class PmlFunctionRegistration
{
    /// <summary>不带 !! 的全局函数名称。</summary>
    public string Name { get; set; } = "";
    /// <summary>用户选择的本机 pmlfnc 源文件。</summary>
    public string SourceFile { get; set; } = "";
    /// <summary>登记时的完整源文件 SHA256。</summary>
    public string SourceSha256 { get; set; } = "";
    /// <summary>按声明顺序记录 STRING、REAL 或 BOOLEAN 参数。</summary>
    public string[] ParameterTypes { get; set; } = [];
    /// <summary>声明的返回类型；空串表示未声明。</summary>
    public string ReturnType { get; set; } = "";
    /// <summary>是否明确允许经注册调用入口调用；登记和重新登记默认关闭。</summary>
    public bool AllowInvoke { get; set; }
}

/// <summary>函数登记文件；与人工信任列表分开存储。</summary>
internal sealed class PmlFunctionRegistryFile
{
    /// <summary>当前文件格式版本。</summary>
    public int Schema { get; set; } = 1;
    /// <summary>所有函数及其权限。</summary>
    public List<PmlFunctionRegistration> Functions { get; set; } = [];
}

/// <summary>使用编译期 JSON 元数据保存函数登记，支持 Native AOT。</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PmlFunctionRegistryFile))]
[JsonSerializable(typeof(PmlFunctionRegistration))]
[JsonSerializable(typeof(List<PmlFunctionRegistration>))]
internal partial class PmlFunctionRegistryJsonContext : JsonSerializerContext { }

/// <summary>登记真实源码、显式授予权限并在调用前核对身份和参数；不加载或修改原生 PMLLIB。</summary>
public sealed class PmlFunctionRegistry
{
    private readonly string stateFile;
    /// <summary>使用本机独立状态目录保存注册表。</summary>
    public PmlFunctionRegistry() : this(Environment.GetEnvironmentVariable("YUZUHA_FUNCTION_REGISTRY") ??
        Path.Combine(Environment.GetEnvironmentVariable("YUZUHA_STATE_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"YuzuhaToolkit"),"functions.json")) { }
    /// <summary>选择明确的本机注册表文件，用于隔离开发与测试。</summary>
    public PmlFunctionRegistry(string stateFile) { this.stateFile=Path.GetFullPath(stateFile); }

    /// <summary>列出实际保存的函数；无法解析或不支持的文件格式会失败，不默默覆盖。</summary>
    public List<PmlFunctionRegistration> List() { using var held=Lock();return Load().Functions; }

    /// <summary>从指定 pmlfnc 解析唯一的全局函数声明并登记源码哈希；重登记撤销原调用许可。</summary>
    public PmlFunctionRegistration Register(string sourceFile,string functionName)
    {
        var name=Name(functionName);var file=Path.GetFullPath(sourceFile);
        if(!file.EndsWith(".pmlfnc",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("A .pmlfnc source file is required.",nameof(sourceFile));
        var bytes=ReadSource(file);var source=System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var matches=Regex.Matches(source,@"^[ \t]*define[ \t]+function[ \t]+!!([A-Za-z][A-Za-z0-9_]*)[ \t]*\(([^\r\n)]*)\)[ \t]*(?:is[ \t]+([A-Za-z][A-Za-z0-9_]*))?[ \t]*(?:--[^\r\n]*)?\r?$",RegexOptions.IgnoreCase|RegexOptions.Multiline,TimeSpan.FromSeconds(1))
            .Cast<Match>().Where(m=>m.Groups[1].Value.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(matches.Length!=1)throw new InvalidDataException("Source must contain one matching, supported function declaration.");
        var match=matches[0];var parameters=new List<string>();var parameterNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(match.Groups[2].Value.Trim().Length>0)foreach(var part in match.Groups[2].Value.Split(','))
        {
            var parameter=Regex.Match(part,@"^\s*!([A-Za-z][A-Za-z0-9_]*)\s+is\s+(STRING|REAL|BOOLEAN)\s*$",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
            if(!parameter.Success||!parameterNames.Add(parameter.Groups[1].Value))throw new InvalidDataException("Only unique STRING, REAL and BOOLEAN scalar parameters are supported in phase 1.");
            parameters.Add(parameter.Groups[2].Value.ToUpperInvariant());
        }
        var entry=new PmlFunctionRegistration{Name=match.Groups[1].Value,SourceFile=file,SourceSha256=Hash(bytes),ParameterTypes=parameters.ToArray(),ReturnType=match.Groups[3].Value.ToUpperInvariant(),AllowInvoke=false};
        using var held=Lock();var state=Load();state.Functions.RemoveAll(f=>f.Name.Equals(name,StringComparison.OrdinalIgnoreCase));state.Functions.Add(entry);Save(state);return entry;
    }

    /// <summary>显式授予或撤销已登记函数的调用许可；授予前重新核验源文件。</summary>
    public PmlFunctionRegistration SetPermission(string functionName,bool allowInvoke)
    {
        using var held=Lock();var state=Load();var entry=Find(state,Name(functionName));
        if(allowInvoke)VerifySource(entry);entry.AllowInvoke=allowInvoke;Save(state);return entry;
    }

    /// <summary>在任何 RPC 前核验许可、源码和参数，返回使用既有生成器编码的唯一调用文本。</summary>
    public string PrepareCall(string functionName,List<DynamicParameterInput> parameters)
    {
        using var held=Lock();var entry=Find(Load(),Name(functionName));
        if(!entry.AllowInvoke)throw new UnauthorizedAccessException("PML_FUNCTION_INVOKE_DENIED: grant permission for this source revision first.");
        VerifySource(entry);
        if(parameters is null||parameters.Count!=entry.ParameterTypes.Length)throw new ArgumentException("Parameter count does not match the registered signature.",nameof(parameters));
        var values=new DynamicParameter[parameters.Count];
        for(var i=0;i<parameters.Count;i++)
        {
            var input=parameters[i]??throw new ArgumentException("Null parameter.",nameof(parameters));
            var type=(input.Type??"").ToLowerInvariant() switch {"str" or "string"=>"STRING","bool" or "boolean"=>"BOOLEAN","double" or "real" or "number"=>"REAL",_=>""};
            if(type!=entry.ParameterTypes[i])throw new ArgumentException("Parameter type does not match the registered signature.",nameof(parameters));
            object? value=input.Value;
            if(value is JsonElement json)value=json.ValueKind switch {JsonValueKind.String=>json.GetString(),JsonValueKind.True=>true,JsonValueKind.False=>false,JsonValueKind.Number=>json.GetRawText(),_=>throw new ArgumentException("Scalar JSON value required.")};
            if(value is string text&&text.IndexOfAny(['\r','\n'])>=0)throw new ArgumentException("Multiline scalar strings are not supported.");
            values[i]=new DynamicParameter(input.Type!,value);
        }
        return PmlMethodSignatureGenerator.Build(entry.Name,values);
    }

    private FileStream Lock(){Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);return new FileStream(stateFile+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
    private PmlFunctionRegistryFile Load()
    {
        if(!File.Exists(stateFile))return new();
        var state=JsonSerializer.Deserialize(File.ReadAllText(stateFile),PmlFunctionRegistryJsonContext.Default.PmlFunctionRegistryFile)??throw new InvalidDataException("Empty function registry.");
        if(state.Schema!=1||state.Functions is null||state.Functions.Any(f=>f is null||f.ParameterTypes is null)||state.Functions.Select(f=>Name(f.Name)).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=state.Functions.Count)throw new InvalidDataException("Invalid function registry schema or duplicate identities.");
        return state;
    }
    private void Save(PmlFunctionRegistryFile state)
    {
        var temporary=stateFile+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,JsonSerializer.Serialize(state,PmlFunctionRegistryJsonContext.Default.PmlFunctionRegistryFile));File.Move(temporary,stateFile,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private static PmlFunctionRegistration Find(PmlFunctionRegistryFile state,string name)=>state.Functions.SingleOrDefault(f=>f.Name.Equals(name,StringComparison.OrdinalIgnoreCase))??throw new KeyNotFoundException("PML_FUNCTION_NOT_REGISTERED: "+name);
    private static string Name(string name)
    {
        if(string.IsNullOrEmpty(name)||!Regex.IsMatch(name,@"\A[A-Za-z][A-Za-z0-9_]{0,127}\z",RegexOptions.None,TimeSpan.FromSeconds(1)))throw new ArgumentException("Use a plain PML global function name without !! or parentheses.",nameof(name));
        return name;
    }
    private static byte[] ReadSource(string file){if(new FileInfo(file).Length>1024*1024)throw new InvalidDataException("Function source exceeds 1 MiB.");return File.ReadAllBytes(file);}
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void VerifySource(PmlFunctionRegistration entry){if(Hash(ReadSource(entry.SourceFile))!=entry.SourceSha256)throw new InvalidDataException("PML_FUNCTION_SOURCE_CHANGED: register again and explicitly grant fresh permission.");}
}
