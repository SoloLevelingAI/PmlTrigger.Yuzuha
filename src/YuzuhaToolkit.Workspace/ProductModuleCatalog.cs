using System.Text.RegularExpressions;

namespace YuzuhaToolkit.Workspace;

/// <summary>按产品隔离原生模块值，优先引用所选安装的启动脚本和 Addins 元数据。</summary>
public sealed class ProductModuleCatalog : IProductModuleCatalog
{
    /// <summary>列出产品模块；安装元数据证实与实机启动验证分别标记。</summary>
    public ProductModuleSet Get(InstalledProductIdentity installation)
    {
        var product = NormalizeProduct(installation.Product);
        (string Value, string Label)[] known = product switch
        {
            "AM" => [("Outfitting", "Outfitting Design · 舾装设计"), ("HullDesign", "Hull Design · 船体设计"),
                ("OutfittingDraft", "Outfitting Draft · 舾装出图"), ("MarineDrafting", "Marine Drafting · 船体出图"),
                ("Paragon", "Paragon · 元件库"), ("Isodraft", "Isodraft"), ("Spooler", "Spooler"),
                ("Admin", "Admin · 项目管理"), ("Monitor", "Monitor")],
            "E3D" => [("Design", "Design · 三维设计"), ("Draw", "Draw · 出图"), ("Paragon", "Paragon · 元件库"),
                ("Isodraft", "Isodraft"), ("Spooler", "Spooler"), ("Monitor", "Monitor")],
            "PDMS" => [("Design", "Design · 三维设计"), ("Draft", "Draft · 出图"), ("Paragon", "Paragon · 元件库"),
                ("Isodraft", "Isodraft"), ("Spooler", "Spooler"), ("Admin", "Admin · 项目管理"), ("Monitor", "Monitor")],
            _ => []
        };
        var folder = string.IsNullOrWhiteSpace(installation.Executable) ? "" : Path.GetDirectoryName(installation.Executable) ?? "";
        var metadata = new List<string>();
        var batchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(installation.Executable) && new[] { ".bat", ".cmd" }.Contains(Path.GetExtension(installation.Executable), StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(installation.Executable);
            if (info.Length <= 1024 * 1024)
            {
                var text = File.ReadAllText(installation.Executable);
                foreach (Match match in Regex.Matches(text, "(?i)%moduleArg%\\\"\\s*==\\s*\\\"([A-Za-z][A-Za-z0-9]*)\\\""))
                    batchNames.Add(match.Groups[1].Value);
                if (batchNames.Count > 0) metadata.Add(installation.Executable);
            }
        }
        var options = known.Select(module =>
        {
            var addins = string.IsNullOrEmpty(folder) ? "" : Path.Combine(folder, module.Value + "Addins.xml");
            var verified = batchNames.Contains(module.Value) || (!string.IsNullOrEmpty(addins) && File.Exists(addins));
            if (verified && File.Exists(addins)) metadata.Add(addins);
            return new ProductModuleOption(module.Value, module.Label, verified);
        }).ToArray();
        var source = metadata.Count > 0 ? string.Join("; ", metadata.Distinct(StringComparer.OrdinalIgnoreCase))
            : "产品模块映射；原生命令名称依据 AVEVA moduledata.pmlfnc 与产品分类";
        var warning = options.Length == 0 ? "无法识别产品，请选择 AM、E3D 或 PDMS。"
            : options.Any(o => !o.Verified) ? "部分模块仅按产品映射列出，尚未在此安装元数据中确认；不代表许可、登录或 Host 已验证。"
            : "模块在安装元数据中存在；许可、登录及 Host 仍需实际启动验证。";
        return new(product, installation.Version ?? "", source, options, warning);
    }

    /// <summary>统一产品别名；不从模块名称推断产品。</summary>
    public static string NormalizeProduct(string? product) => product?.Trim().ToUpperInvariant() switch
    {
        "AM" or "MARINE" or "AVEVA MARINE" or "HULLANDOUTFITTING" => "AM",
        "E3D" or "3D" or "EVERYTHING3D" => "E3D",
        "PDMS" => "PDMS",
        _ => product?.Trim().ToUpperInvariant() ?? ""
    };
}
