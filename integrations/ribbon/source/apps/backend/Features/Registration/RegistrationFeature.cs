using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace RibbonStudio.Backend
{
    public sealed class RegistrationFeature
    {
        readonly WorkspaceFiles store;
        public RegistrationFeature(WorkspaceFiles files) { store = files; }
        public static void Metadata(Target target)
        {
            var info = FileVersionInfo.GetVersionInfo(target.monPath);
            string source = (info.ProductName ?? "") + " " + target.directory;
            target.product = Regex.IsMatch(source, "Everything3D|\\bE3D", RegexOptions.IgnoreCase) ? "E3D" : Regex.IsMatch(source, "Marine|\\bAM\\b", RegexOptions.IgnoreCase) ? "AM" : Regex.IsMatch(source, "PDMS", RegexOptions.IgnoreCase) ? "PDMS" : "未知";
            target.version = !String.IsNullOrWhiteSpace(info.ProductVersion) ? info.ProductVersion : info.FileVersion;
            target.versionSource = !String.IsNullOrEmpty(target.version) ? "mon.exe 文件版本" : "未知";
            if (String.IsNullOrEmpty(target.version))
            {
                var match = Regex.Match(Path.GetFileName(target.directory), @"\d+\.\d+(?:[. _-]*SP\d+)?", RegexOptions.IgnoreCase);
                if (match.Success) { target.version = match.Value; target.versionSource = "安装目录名称"; }
                else target.version = "未知";
            }
            var servicePack = Regex.Match(Path.GetFileName(target.directory), @"SP\d+", RegexOptions.IgnoreCase);
            if (servicePack.Success && target.versionSource == "mon.exe 文件版本" && target.version.IndexOf("SP", StringComparison.OrdinalIgnoreCase) < 0) { target.version += " · " + servicePack.Value.ToUpperInvariant(); target.versionSource += " + 安装目录服务包"; }
            target.label = target.product + " " + target.version + " · " + target.directory;
        }
        public DeploymentProfiles Profiles()
        {
            lock (Data.WriteGate)
            {
                string file = Path.Combine(store.Root, "deployment-profiles.json");
                return File.Exists(file) ? Json.Read<DeploymentProfiles>(File.ReadAllText(file)) : new DeploymentProfiles { profiles = new List<DeploymentProfile>() };
            }
        }
        public object SaveProfile(object input)
        {
            lock (Data.WriteGate)
            {
                var profile = Data.As<DeploymentProfile>(input);
                var target = Manifests.Inspect(profile.monPath);
                if (String.IsNullOrWhiteSpace(profile.name)) throw new ApiError(422, "请输入配置名称");
                profile.monPath = target.monPath;
                profile.manifests = (profile.manifests ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (profile.manifests.Any(m => !target.manifests.Contains(m))) throw new ApiError(422, "模块不在所选安装中");
                var all = Profiles();
                if (String.IsNullOrEmpty(profile.id)) profile.id = Guid.NewGuid().ToString();
                else if (!all.profiles.Any(p => p.id == profile.id)) throw new ApiError(404, "配置不存在");
                all.profiles.RemoveAll(p => p.id == profile.id);
                all.profiles.Add(profile);
                Data.AtomicJson(Path.Combine(store.Root, "deployment-profiles.json"), all);
                return profile;
            }
        }
        public RegistrationScan Scan(object input)
        {
            var paths = Manifests.Discover().Select(t => t.monPath).Concat(Profiles().profiles.Select(p => p.monPath)).Concat(Data.Items(Data.Get(input, "monPaths")).Select(Convert.ToString)).ToList();
            var owned = new List<Binding>();
            foreach (var doc in store.List()) owned.AddRange(store.Bindings(Data.Text(doc, "id")));
            paths.AddRange(owned.Select(b => b.monPath));
            var result = new RegistrationScan { rows = new List<XmlRegistration>(), targets = new List<Target>(), errors = new List<string>(), scannedAt = Data.Now() };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mon in paths.Where(p => !String.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Target target;
                try { target = Manifests.Inspect(mon); result.targets.Add(target); }
                catch (Exception e) { result.errors.Add(mon + "：" + e.Message); continue; }
                foreach (string name in target.manifests)
                    try
                    {
                        var m = Manifests.Load(target.monPath, name);
                        foreach (var entry in m.Entries)
                        {
                            string raw = entry.GetAttribute("Path"), path = Manifests.Actual(m, raw);
                            if (String.IsNullOrEmpty(path)) { result.errors.Add(m.Path + "：无法解析 " + raw); continue; }
                            if (!seen.Add(path + "\0" + m.Path)) continue;
                            bool managed = owned.Any(b => Data.Same(b.monPath, m.Mon) && String.Equals(b.manifestName, m.Name, StringComparison.OrdinalIgnoreCase) && b.entryName == entry.GetAttribute("Name") && Data.Same(b.filePath, path) && b.registrationPath == raw);
                            result.rows.Add(new XmlRegistration { uicPath = path, xmlPath = m.Path, manifestName = name, monPath = m.Mon, entryName = entry.GetAttribute("Name"), product = target.product, version = target.version, versionSource = target.versionSource, origin = managed ? "部署器" : Manifests.Inside(path, m.Directory) ? "原生 / 只读" : "既有自定义 / 未接管" });
                        }
                    }
                    catch (Exception e) { result.errors.Add(Path.Combine(target.directory, name) + "：" + e.Message); }
            }
            return result;
        }
    }
}
