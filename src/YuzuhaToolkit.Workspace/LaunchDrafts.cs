using System.Text.Json;

namespace YuzuhaToolkit.Workspace;

/// <summary>创建免填安装路径的启动草稿，并只读带入 LaunchDesk 的非密码配置。</summary>
public static class LaunchDrafts
{
    /// <summary>按用户选定入口填入本机可确认的路径；不保存或启动程序。</summary>
    public static LaunchProfile FromApplication(string executable)
    {
        executable = Path.GetFullPath(executable);
        var folder = Path.GetDirectoryName(executable)!;
        var batch = Path.GetExtension(executable).Equals(".bat",StringComparison.OrdinalIgnoreCase) || Path.GetExtension(executable).Equals(".cmd",StringComparison.OrdinalIgnoreCase);
        var legacy = folder.Contains("PDMS",StringComparison.OrdinalIgnoreCase);
        var marine = batch && !legacy && !Path.GetFileNameWithoutExtension(executable).Equals("pdms", StringComparison.OrdinalIgnoreCase);
        var profile = new LaunchProfile { Executable=executable, Kind=batch?"MarineBatch":"Monitor",
            Product=marine?"AM":legacy||batch?"PDMS":"E3D", WorkingDirectory=folder, Module=marine?"Outfitting":"Design" };
        if(!batch && File.Exists(Path.Combine(folder,"launch.init"))) profile.Initialization=Path.Combine(folder,"launch.init");
        var version = System.Text.RegularExpressions.Regex.Match(folder,@"\d+\.\d+(?:\.SP\d+)?",System.Text.RegularExpressions.RegexOptions.IgnoreCase).Value;
        profile.Version = version;
        var userDirectory = batch ? Path.Combine(Directory.GetParent(folder)?.FullName??folder,"Data"+version,"pdmsuser")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),"AVEVA","USERDATA");
        if(Directory.Exists(userDirectory)) profile.WorkingDirectory=userDirectory;
        return profile;
    }

    /// <summary>省略配置名称时，用项目、模块和安装目录生成可辨识名称。</summary>
    public static string SuggestName(LaunchProfile profile) => string.Join(" · ",new[] { profile.Project,profile.Module,
        Path.GetFileName(Path.GetDirectoryName(profile.Executable)) }.Where(s=>!string.IsNullOrWhiteSpace(s)));

    /// <summary>解析选定的 LaunchDesk workspace.json，返回未保存草稿；不读取密码、凭据引用或工具环境。</summary>
    public static LaunchProfile[] ReadLaunchDesk(string file)
    {
        var info = new FileInfo(file);
        if(info.Length>2*1024*1024) throw new InvalidDataException("LaunchDesk 配置超过 2 MiB，请检查所选文件。");
        var bytes = File.ReadAllBytes(file);
        using var document=JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? 3 : 0));
        var root=document.RootElement;
        if(!root.TryGetProperty("Applications",out var applications) || applications.ValueKind!=JsonValueKind.Array ||
           !root.TryGetProperty("Profiles",out var profiles) || profiles.ValueKind!=JsonValueKind.Array ||
           applications.GetArrayLength()>2000 || profiles.GetArrayLength()>2000)
            throw new InvalidDataException("请选择 LaunchDesk Data 目录中的 workspace.json。");
        static string Text(JsonElement value,string key) => value.TryGetProperty(key,out var field) && field.ValueKind==JsonValueKind.String ? field.GetString()! : "";
        var apps = new Dictionary<string,JsonElement>(StringComparer.Ordinal);
        foreach(var app in applications.EnumerateArray())
        {
            var id=Text(app,"Id");
            if(id.Length==0 || !apps.TryAdd(id,app)) throw new InvalidDataException("LaunchDesk 应用标识为空或重复。");
        }
        var result = new List<LaunchProfile>();
        foreach(var item in profiles.EnumerateArray())
        {
            if(!apps.TryGetValue(Text(item,"AppId"),out var app)) throw new InvalidDataException("LaunchDesk 配置引用了不存在的应用。");
            var kind=Text(app,"Kind"); var executable=Text(app,"ExecutablePath");
            if(kind is not ("Monitor" or "MarineBatch") || !Path.IsPathFullyQualified(executable))
                throw new InvalidDataException("LaunchDesk 中有未支持的入口类型或非绝对路径。");
            result.Add(new LaunchProfile { Name=Text(item,"Name"), Kind=kind, Executable=executable,
                Initialization=Text(app,"InitializationPath"), WorkingDirectory=Text(app,"WorkingDirectory"), Product=Text(app,"ProductCode"),
                Project=Text(item,"Project"),User=Text(item,"Username"),Mdb=Text(item,"Mdb"),Module=Text(item,"Module"),
                CredentialKey="",RememberPassword=true });
        }
        if(result.Count==0) throw new InvalidDataException("LaunchDesk 文件里没有可带入的启动配置。");
        return result.ToArray();
    }
}
