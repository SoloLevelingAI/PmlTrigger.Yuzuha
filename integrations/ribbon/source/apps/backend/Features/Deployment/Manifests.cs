using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace RibbonStudio.Backend
{
    public sealed class ManifestFile
    {
        public string Mon, Directory, Name, Path, Xml; public byte[] Bytes; public Encoding Encoding; public bool Bom; public List<XmlElement> Entries;
        public byte[] Encode(string value)
        {
            var bytes = Encoding.GetBytes(value);
            return Bom ? Encoding.GetPreamble().Concat(bytes).ToArray() : bytes;
        }
    }
    public static class Manifests
    {
        public static readonly Regex Pattern = new Regex(@"^[\w.-]+Customi[sz]ation\.xml$", RegexOptions.IgnoreCase);
        public static Target Inspect(string mon)
        {
            if (String.IsNullOrEmpty(mon) || !System.IO.Path.IsPathRooted(mon) || !System.IO.Path.GetFileName(mon).Equals("mon.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(mon))
                throw new ApiError(422, "请选择完整有效的 mon.exe 路径");
            string full = System.IO.Path.GetFullPath(mon), directory = System.IO.Path.GetDirectoryName(full);
            var files = System.IO.Directory.GetFiles(directory).Select(System.IO.Path.GetFileName).Where(n => Pattern.IsMatch(n)).OrderBy(n => n).ToList();
            if (files.Count == 0)
                throw new ApiError(422, "mon.exe 同目录没有模块加载清单");
            var result = new Target { monPath = full, directory = directory, label = directory, manifests = files };
            RegistrationFeature.Metadata(result);
            return result;
        }
        static List<Target> cachedTargets; static DateTime discoveredAt; static readonly object discoveryGate = new object();
        public static List<Target> Discover()
        {
            lock (discoveryGate)
            {
                if (cachedTargets != null && (DateTime.UtcNow - discoveredAt).TotalSeconds < 60)
                    return cachedTargets;
                var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var root in new[] { @"C:\Program Files (x86)\AVEVA", @"C:\Program Files\AVEVA", @"C:\AVEVA\Marine" })
                    if (System.IO.Directory.Exists(root))
                        foreach (var folder in System.IO.Directory.GetDirectories(root))
                            candidates.Add(System.IO.Path.Combine(folder, "mon.exe"));
                try
                {
                    string script = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "discover-installations.ps1");
                    var start = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -File \"" + script + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using (var process = System.Diagnostics.Process.Start(start))
                    {
                        var read = process.StandardOutput.ReadToEndAsync();
                        if (process.WaitForExit(8000))
                        {
                            foreach (var item in Data.Items(Json.Read<object>(read.Result)))
                                if (item is string && System.IO.Path.IsPathRooted((string)item))
                                    candidates.Add(System.IO.Path.GetFileName((string)item).Equals("mon.exe", StringComparison.OrdinalIgnoreCase) ? (string)item : System.IO.Path.Combine((string)item, "mon.exe"));
                        }
                        else
                            process.Kill();
                    }
                }
                catch { }
                var result = new List<Target>();
                foreach (var candidate in candidates)
                    try
                    {
                        result.Add(Inspect(candidate));
                    }
                    catch { }
                cachedTargets = result;
                discoveredAt = DateTime.UtcNow;
                return result;
            }
        }
        public static string Decode(byte[] bytes, out Encoding encoding, out bool bom)
        {
            bom = false;
            int skip = 0;
            if (bytes.Length >= 2 && bytes[0] == 255 && bytes[1] == 254)
            {
                encoding = new UnicodeEncoding(false, true, true);
                bom = true;
                skip = 2;
            }
            else if (bytes.Length >= 2 && bytes[0] == 254 && bytes[1] == 255)
            {
                encoding = new UnicodeEncoding(true, true, true);
                bom = true;
                skip = 2;
            }
            else
            {
                encoding = new UTF8Encoding(true, true);
                if (bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191)
                {
                    bom = true;
                    skip = 3;
                }
            }
            string xml;
            try
            {
                xml = encoding.GetString(bytes, skip, bytes.Length - skip);
            }
            catch { throw new ApiError(422, "文件编码无效"); }
            var declared = Regex.Match(xml, "<\\?xml[^>]*encoding\\s*=\\s*[\"']([^\"']+)", RegexOptions.IgnoreCase);
            if (declared.Success && !new[] { "utf-8", "utf8", "utf-16", "utf-16le", "utf-16be" }.Contains(declared.Groups[1].Value.ToLowerInvariant()))
                throw new ApiError(422, "仅支持 UTF-8 或带 BOM 的 UTF-16");
            return xml;
        }
        public static List<XmlElement> Entries(string xml)
        {
            try
            {
                var root = UicFiles.ParseXml(xml).DocumentElement;
                var lists = root.GetElementsByTagName("UICustomizationFiles");
                if (root.Name != "UICustomizationSet" || lists.Count != 1 || lists[0].ParentNode != root)
                    throw new Exception();
                return lists[0].ChildNodes.OfType<XmlElement>().Where(n => n.Name == "CustomizationFile").ToList();
            }
            catch { throw new ApiError(422, "加载清单不是有效的 UICustomizationSet XML"); }
        }
        public static ManifestFile Load(string mon, string name)
        {
            var target = Inspect(mon);
            if (!target.manifests.Contains(name) || !Pattern.IsMatch(name))
                throw new ApiError(422, "加载清单不在所选安装目录");
            string path = System.IO.Path.Combine(target.directory, name);
            byte[] bytes = Data.Read(path);
            Encoding encoding;
            bool bom;
            string xml = Decode(bytes, out encoding, out bom);
            return new ManifestFile { Mon = target.monPath, Directory = target.directory, Name = name, Path = path, Bytes = bytes, Xml = xml, Encoding = encoding, Bom = bom, Entries = Entries(xml) };
        }
        public static string Actual(ManifestFile m, string path)
        {
            return Regex.IsMatch(path, "[$%]") ? "" : System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(m.Directory, path));
        }
        public static string Patch(string xml, string name, string path)
        {
            var entries = Entries(xml);
            var same = entries.Where(e => e.GetAttribute("Name") == name).ToList();
            if (same.Count > 1)
                throw new ApiError(409, "同名加载项有歧义");
            if (same.Count == 1)
            {
                if (same[0].GetAttribute("Path") != path)
                    throw new ApiError(409, "同名条目指向其他文件");
                return xml;
            }
            if (entries.Any(e => String.Equals(e.GetAttribute("Path"), path, StringComparison.OrdinalIgnoreCase)))
                throw new ApiError(409, "文件已被其他条目引用");
            var node = new XmlDocument().CreateElement("CustomizationFile");
            node.SetAttribute("Name", name);
            node.SetAttribute("Path", path);
            string eol = xml.Contains("\r\n") ? "\r\n" : "\n", snippet = node.OuterXml;
            var close = Regex.Match(xml, @"(^[ \t]*)</UICustomizationFiles\s*>", RegexOptions.Multiline);
            string next;
            if (close.Success)
                next = xml.Insert(close.Index, close.Groups[1].Value + "  " + snippet + eol);
            else if (Regex.IsMatch(xml, @"<UICustomizationFiles\s*/>"))
                next = Regex.Replace(xml, @"<UICustomizationFiles\s*/>", "<UICustomizationFiles>" + eol + "    " + snippet + eol + "  </UICustomizationFiles>");
            else
            {
                int at = xml.IndexOf("</UICustomizationFiles>", StringComparison.Ordinal);
                if (at < 0)
                    throw new ApiError(422, "无法定位加载清单");
                next = xml.Insert(at, eol + "    " + snippet + eol + "  ");
            }
            Entries(next);
            return next;
        }
        public static bool Inside(string file, string directory)
        {
            return Data.Same(System.IO.Path.GetDirectoryName(file), directory) || System.IO.Path.GetFullPath(file).StartsWith(System.IO.Path.GetFullPath(directory) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
