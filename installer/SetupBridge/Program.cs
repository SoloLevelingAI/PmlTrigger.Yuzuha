using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Installer-only helper. Never invokes PowerShell, PML, or AVEVA commands.
internal static partial class Program
{
    static readonly string[] Names = { "YuzuhaToolkit", "YuzuhaToolkitKnowledge" };
    static readonly string[] Exes = { "YuzuhaToolkit.Mcp.exe", "YuzuhaToolkit.Knowledge.exe" };
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    static string Full(string path) { return Path.GetFullPath(path).TrimEnd('\\'); }
    static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    static string Command(string root, int i) { return Path.Combine(root, "runtime", "net10", Exes[i]); }
    static JObject ReadObject(string path)
    {
        using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(path))))
            return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    }
    static string Bytes(string path) { return File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null; }
    static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".yuzuha-" + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, text, Utf8);
        if (File.Exists(path)) File.Replace(temp, path, path + ".yuzuha-" + Guid.NewGuid().ToString("N") + ".bak");
        else File.Move(temp, path);
    }
    static void CheckPath(string root)
    {
        if (!Path.IsPathRooted(root) || root.StartsWith("\\\\") ||
            root.IndexOfAny(new[] { '"', '%', '\r', '\n' }) >= 0 ||
            Path.GetFileName(root).IndexOf("PmlTrigger", StringComparison.OrdinalIgnoreCase) < 0)
            throw new Exception("Choose a local installation folder whose name contains PmlTrigger; quotes and percent signs are not allowed.");
        foreach (string reserved in new[] { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.GetPathRoot(root).TrimEnd('\\') })
            if (Same(root, reserved)) throw new Exception("A system or profile root cannot be the installation folder.");
        string cursor = root;
        while (!string.IsNullOrEmpty(cursor))
        {
            if (Directory.Exists(cursor) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new Exception("Installation through junctions or symbolic links is not supported.");
            cursor = Path.GetDirectoryName(cursor);
        }
    }
    static void CheckProcesses(string root)
    {
        foreach (Process process in Process.GetProcesses())
        using (process)
        {
            if (process.Id == Process.GetCurrentProcess().Id) continue;
            string name;
            try { name = process.ProcessName; } catch { continue; }
            if (name.Equals("des", StringComparison.OrdinalIgnoreCase) || name.Equals("design", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("paragon", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Close AVEVA and Yuzuha MCP processes before installation/uninstallation: " + name + " (" + process.Id + "). No process was terminated.");
            try
            {
                string path = process.MainModule.FileName;
                if (Same(Full(path), Path.Combine(root, "uninstall", "unins000.exe"))) continue;
                if (Full(path).StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Close the process using the installation: " + name);
            }
            catch (InvalidOperationException)
            {
                // Process snapshots can outlive short-lived processes during preflight.
                // Ignore only a verified exit, never a still-running installation lock.
                if (process.HasExited) continue;
                throw;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                if (name.StartsWith("YuzuhaToolkit.", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Cannot verify running MCP process path; close it before setup: " + name);
            }
        }
    }
    static JObject Merge(string path, string root)
    {
        JObject json = File.Exists(path) ? ReadObject(path) : new JObject();
        JObject servers = json["mcpServers"] as JObject;
        if (json["mcpServers"] != null && servers == null) throw new Exception("mcpServers must be a JSON object.");
        if (servers == null) { servers = new JObject(); json["mcpServers"] = servers; }
        for (int i = 0; i < Names.Length; i++)
        {
            foreach (var property in servers.Properties())
            {
                var entry = property.Value as JObject;
                string command = entry == null ? null : (string)entry["command"];
                if (property.Name != Names[i] && command != null &&
                    (Same(Full(command), Command(root, i)) || Same(Path.GetFileName(command), Exes[i])))
                    throw new Exception("Possible duplicate MCP registration: " + property.Name);
            }
            if (servers[Names[i]] != null)
            {
                var entry = servers[Names[i]] as JObject;
                if (entry == null || entry["command"] == null || !Same(Full((string)entry["command"]), Command(root, i)) ||
                    (entry["args"] != null && (!(entry["args"] is JArray) || entry["args"].HasValues)) ||
                    (bool?)entry["disabled"] == true || (bool?)entry["enabled"] == false ||
                    (entry["type"] != null && (string)entry["type"] != "stdio") || entry["env"] != null)
                    throw new Exception("Conflicting MCP registration: " + Names[i] + ". Existing configuration was not overwritten.");
            }
            else servers[Names[i]] = new JObject { ["command"] = Command(root, i), ["args"] = new JArray() };
        }
        return json;
    }
    static string Marker(string root) { return Path.Combine(root, ".yuzuha-inno.json"); }
    static void Preflight(string root, string config, string transaction)
    {
        CheckPath(root); CheckProcesses(root);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            if (!File.Exists(Marker(root)))
                throw new Exception("This folder is not owned by the Inno installer. Choose a new folder; legacy PS1 installation migration is not automatic.");
            var marker = ReadObject(Marker(root));
            if ((string)marker["owner"] != "Yuzuha.Inno.v1" || !Same((string)marker["root"], root))
                throw new Exception("Installation ownership mismatch.");
            if (!Same((string)marker["config"] ?? "", config))
                throw new Exception("Keep the same MCP JSON path when updating this installation.");
        }
        var state = new JObject { ["root"] = root, ["config"] = config, ["before"] = config == "" ? null : Bytes(config) };
        if (config != "")
        {
            if (config.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) throw new Exception("Client configuration must be outside the installation folder.");
            state["after"] = Merge(config, root).ToString(Formatting.Indented) + Environment.NewLine;
        }
        File.WriteAllText(transaction, state.ToString(), Utf8);
    }
    static void Apply(string transaction)
    {
        var state = ReadObject(transaction); string root = (string)state["root"], config = (string)state["config"];
        if (config != "")
        {
            if (Bytes(config) != (string)state["before"]) throw new Exception("MCP JSON changed after preflight; no overwrite performed.");
            Write(config, (string)state["after"]);
        }
        try
        {
            var sample = Merge(Path.Combine(root, "nonexistent-config-example.json"), root);
            File.WriteAllText(Path.Combine(root, "mcpServers.example.json"), sample.ToString(Formatting.Indented), Utf8);
            File.WriteAllText(Marker(root), new JObject { ["owner"] = "Yuzuha.Inno.v1", ["root"] = root,
                ["config"] = config, ["updatedUtc"] = DateTime.UtcNow.ToString("o") }.ToString(), Utf8);
        }
        catch { Rollback(transaction); throw; }
    }
    static void Rollback(string transaction)
    {
        if (!File.Exists(transaction)) return;
        var state = ReadObject(transaction); string config = (string)state["config"];
        if (config == "" || !File.Exists(config)) return;
        if (File.ReadAllText(config) != (string)state["after"]) throw new Exception("Rollback stopped: MCP JSON changed concurrently; backup retained.");
        string before = (string)state["before"];
        if (before == null) File.Delete(config); else File.WriteAllBytes(config, Convert.FromBase64String(before));
    }
    static void Unregister(string root, bool checkOnly)
    {
        CheckPath(root); CheckProcesses(root);
        var marker = ReadObject(Marker(root));
        if ((string)marker["owner"] != "Yuzuha.Inno.v1" || !Same((string)marker["root"], root)) throw new Exception("Uninstall ownership mismatch.");
        string path = (string)marker["config"];
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        string before = Bytes(path); var json = ReadObject(path); var servers = json["mcpServers"] as JObject;
        if (servers == null) throw new Exception("Cannot safely inspect MCP JSON; uninstall cancelled.");
        foreach (var property in servers.Properties().ToList())
        {
            var entry = property.Value as JObject; string command = entry == null ? null : (string)entry["command"];
            // Bare commands are resolved by the client, not by the uninstaller's cwd.
            if (command == null || !Path.IsPathRooted(command) ||
                !Full(command).StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) continue;
            int i = Array.IndexOf(Names, property.Name);
            if (i < 0 || !Same(Full(command), Command(root, i)) ||
                (entry["args"] != null && (!(entry["args"] is JArray) || entry["args"].HasValues)) || entry["env"] != null)
                throw new Exception("A changed or additional MCP entry still refers to this installation: " + property.Name + ". Uninstall cancelled.");
            property.Remove();
        }
        if (before != Bytes(path)) throw new Exception("MCP JSON changed during uninstall preflight.");
        if (!checkOnly) Write(path, json.ToString(Formatting.Indented) + Environment.NewLine);
    }
    [STAThread]
    static int Main(string[] args)
    {
        string newReport = args.LastOrDefault(x => x.StartsWith("--report=", StringComparison.Ordinal));
        if (newReport != null) args = args.Where(x => x != newReport).ToArray();
        try { if (args.Length > 0 && NewCommand(args)) return 0; }
        catch (Exception ex)
        {
            if (newReport != null) File.WriteAllText(newReport.Substring(9), ex.Message, Utf8);
            Console.Error.WriteLine(ex); return 1;
        }
        string report = args.Length > 1 ? args[1] : null;
        try
        {
            if (args.Length < 3) throw new Exception("Invalid installer helper arguments.");
            switch (args[0])
            {
                case "preflight": Preflight(Full(args[2]), args[3] == "" ? "" : Full(args[3]), args[4]); break;
                case "apply": Apply(args[2]); break;
                case "rollback": Rollback(args[2]); break;
                case "check-uninstall": Unregister(Full(args[2]), true); break;
                case "unregister": Unregister(Full(args[2]), false); break;
                default: throw new Exception("Unknown installer operation.");
            }
            File.WriteAllText(report, "OK", Utf8); return 0;
        }
        catch (Exception ex)
        {
            if (report != null) File.WriteAllText(report, ex.Message, Utf8);
            Console.Error.WriteLine(ex.Message); return 1;
        }
    }
}
