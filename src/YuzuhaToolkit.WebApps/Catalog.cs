using System.Text.Json;
using System.Text.RegularExpressions;

namespace YuzuhaToolkit.WebApps;

/// <summary>本地目录版本、分类与应用；修改通过版本比较和原子替换保存。</summary>
public sealed record AppCatalog(int SchemaVersion, int Revision, string[] Categories, AppEntry[] Apps);
/// <summary>一个 HTML 数据界面或原生 Form 的分类信息及有序按钮集合。</summary>
public sealed record AppEntry(string Id, string Name, string Category, string Description,
    string View, bool Enabled, ActionBinding[] Actions);
/// <summary>稳定动作标识对应的 PML 目标、返回协议和参数契约；不接受任意 PML 文本。</summary>
public sealed record ActionBinding(string Id, string Label, string Kind, string Target,
    string Method, string Result, ParameterSpec[] Parameters);
/// <summary>参数顺序、类型、默认值和可选数值界限；默认值也是受校验的调用数据。</summary>
public sealed record ParameterSpec(string Name, string Label, string Type, JsonElement Default,
    double? Min = null, double? Max = null);

/// <summary>本地 JSON 目录读写，不由打开页面或目录登记推断 AVEVA 已加载相应版本。</summary>
public sealed class CatalogStore
{
    /// <summary>同一进程共享的 JSON 命名与缩进约定。</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object gate = new();
    private readonly string file;

    /// <summary>首次启动从随程序发布的目录模板初始化独立的数据目录。</summary>
    public CatalogStore(string directory, string seed)
    {
        Directory.CreateDirectory(directory);
        file = Path.Combine(directory, "catalog.json");
        if (!File.Exists(file)) File.Copy(seed, file);
        Validate(Read());
    }

    /// <summary>读取持久化目录；调用者持有的对象不会修改存储内容。</summary>
    public AppCatalog Read()
    {
        lock (gate) return JsonSerializer.Deserialize<AppCatalog>(File.ReadAllText(file), Json)
            ?? throw new InvalidDataException("应用目录为空。");
    }

    /// <summary>核验分类、稳定标识、目标及参数；有并发修改时拒绝覆盖。</summary>
    public AppCatalog Save(AppCatalog catalog)
    {
        lock (gate)
        {
            Validate(catalog);
            var current = Read();
            if (catalog.Revision != current.Revision) throw new InvalidOperationException("目录已被修改，请刷新后再保存。");
            var next = catalog with { Revision = checked(current.Revision + 1) };
            var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(next, Json));
            File.Move(temporary, file, true);
            return next;
        }
    }

    /// <summary>验证目录结构和每个参数默认值，不执行 PML。</summary>
    public static void Validate(AppCatalog catalog)
    {
        if (catalog.SchemaVersion != 1 || catalog.Revision < 1 || catalog.Apps is null || catalog.Categories is null ||
            catalog.Apps.Length > 200 || catalog.Categories.Length is < 1 or > 100 ||
            catalog.Categories.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 80) ||
            catalog.Categories.Distinct(StringComparer.Ordinal).Count() != catalog.Categories.Length)
            throw new ArgumentException("目录结构或分类无效。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var app in catalog.Apps)
        {
            if (!StableId(app.Id) || !ids.Add(app.Id) || string.IsNullOrWhiteSpace(app.Name) || app.Name.Length > 100 ||
                !catalog.Categories.Contains(app.Category) || app.View is not ("table" or "form") ||
                app.Actions is null || app.Actions.Length > 50)
                throw new ArgumentException("应用标识、分类或界面类型无效。");
            var actions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in app.Actions)
            {
                if (!StableId(action.Id) || !actions.Add(action.Id) || string.IsNullOrWhiteSpace(action.Label) ||
                    action.Parameters is null || action.Parameters.Length > 20 ||
                    action.Kind is not ("object" or "function" or "form") ||
                    !PmlName(action.Target) || !PmlName(action.Method) ||
                    action.Result is not ("array" or "scalar" or "none") ||
                    (action.Method == "show" && action.Kind == "form" && (action.Result != "none" || action.Parameters.Length != 0)))
                    throw new ArgumentException("按钮绑定无效。");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in action.Parameters)
                {
                    if (!PmlName(parameter.Name) || !names.Add(parameter.Name) || string.IsNullOrWhiteSpace(parameter.Label) ||
                        parameter.Min is { } min && !double.IsFinite(min) || parameter.Max is { } max && !double.IsFinite(max) ||
                        parameter.Min > parameter.Max)
                        throw new ArgumentException("参数名称或界限无效。");
                    ActionCompiler.Literal(parameter, parameter.Default);
                }
            }
        }
    }

    /// <summary>只接受单个 PML 名称，排除表达式、括号及命令拼接。</summary>
    internal static bool PmlName(string? text) => text is not null && Regex.IsMatch(text, "^[A-Za-z][A-Za-z0-9_]{0,79}$");
    private static bool StableId(string? text) => text is not null && Regex.IsMatch(text, "^[a-z][a-z0-9.-]{0,79}$");
}
