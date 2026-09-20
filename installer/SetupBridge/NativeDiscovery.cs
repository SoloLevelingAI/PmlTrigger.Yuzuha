using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    // UTF-16 INI is a read-only interchange for Inno's native page controls.
    // Plans still use the existing JSON validation and approval protocol.
    static string IniValue(string value)
    {
        return (value ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\0", " ");
    }
    internal static void ExportNativeDiscovery(string output)
    {
        File.WriteAllText(output, NativeDiscoveryText(Discover()), Encoding.Unicode);
    }
    internal static string NativeDiscoveryText(JObject result)
    {
        var rows = new StringBuilder(); int count = 0;
        foreach (JObject candidate in (JArray)result["candidates"])
        foreach (string file in ((JArray)candidate["preferredFiles"]).Values<string>().DefaultIfEmpty(""))
        {
            bool noInit = (bool?)candidate["scanComplete"] == true && file.Length > 0 &&
                !((JArray)candidate["files"]).Values<string>().Any(x => Path.GetExtension(x).Equals(".init", StringComparison.OrdinalIgnoreCase));
            rows.AppendLine("[row" + count++ + "]");
            rows.AppendLine("product=" + IniValue(candidate["product"] + " " + candidate["version"]));
            rows.AppendLine("path=" + IniValue(file));
            rows.AppendLine("profile=" + IniValue((string)candidate["profile"]));
            rows.AppendLine("evidence=" + IniValue((string)candidate["registry"]));
            rows.AppendLine("noInit=" + (noInit ? "1" : "0"));
            rows.AppendLine("multiple=" + (((JArray)candidate["preferredFiles"]).Count > 1 ? "1" : "0"));
        }
        return "[discovery]\r\ncount=" + count + "\r\nwarning=" +
            IniValue(string.Join(" | ", ((JArray)result["warnings"]).Values<string>())) + "\r\n" + rows;
    }
}
