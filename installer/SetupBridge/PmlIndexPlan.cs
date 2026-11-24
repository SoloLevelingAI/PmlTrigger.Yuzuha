using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

/// <summary>为既有安装索引补入此次新增的启动策略对象，不扫描或重建其他 PML 索引。</summary>
internal static partial class Program
{
    const string StartupPolicySource = "PMLLIB/Bootstrap/YuzuhaStartupPolicy.pmlobj";
    const string StartupPolicyEntry = "YuzuhaStartupPolicy.pmlobj";
    static string GeneratedIndexPath(string root) { return Inside(root, "PMLLIB/pml.index"); }
    static bool HasStartupPolicy(JArray files)
    { return files.OfType<JObject>().Any(file => Same((string)file["path"], StartupPolicySource)); }

    static byte[] ExtendPmlIndex(byte[] bytes, out string before, out string after, out JArray edits)
    {
        if (bytes.Length > 16 * 1024 * 1024) throw Bad("PML index exceeds the bounded preview size.");
        Encoding encoding; int offset; bool bom;
        if (bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }))
        { encoding = new UTF8Encoding(true, true); offset = 3; bom = true; }
        else if (bytes.Take(2).SequenceEqual(new byte[] { 255, 254 }))
        { encoding = new UnicodeEncoding(false, true, true); offset = 2; bom = true; }
        else if (bytes.Take(2).SequenceEqual(new byte[] { 254, 255 }))
        { encoding = new UnicodeEncoding(true, true, true); offset = 2; bom = true; }
        else
        { encoding = Encoding.GetEncoding(28591, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); offset = 0; bom = false; }
        before = encoding.GetString(bytes, offset, bytes.Length - offset);
        if (before.IndexOf('\0') >= 0 || !Encode(encoding, bom, before).SequenceEqual(bytes))
            throw Bad("Unsupported PML index encoding; original index retained.");
        var lines = Regex.Matches(before, @"[^\r\n]*(?:\r\n|\n|\r|$)").Cast<Match>().Where(line => line.Length > 0).ToArray();
        string section = ""; int bootstrap = -1, bootstrapCount = 0, insertion = -1; bool present = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string text = lines[i].Value.TrimEnd('\r', '\n').Trim();
            if (text.StartsWith("/", StringComparison.Ordinal) && text.EndsWith("/", StringComparison.Ordinal))
            {
                if (Same(section, "/Bootstrap/") && insertion < 0) insertion = lines[i].Index;
                section = text;
                if (Same(section, "/Bootstrap/")) { bootstrap = i; bootstrapCount++; }
            }
            else if (Same(text, StartupPolicyEntry))
            {
                if (!Same(section, "/Bootstrap/")) throw Bad("Startup policy name exists outside /Bootstrap/; review index conflict.");
                present = true;
            }
        }
        edits = new JArray(); after = before;
        if (bootstrapCount > 1) throw Bad("Multiple /Bootstrap/ sections require index review.");
        if (present) return bytes;
        string newline = bootstrap >= 0 ? Regex.Match(lines[bootstrap].Value, @"\r\n|\n|\r").Value : "";
        if (newline.Length == 0) newline = Regex.Match(before, @"\r\n|\n|\r").Value;
        if (newline.Length == 0) newline = "\r\n";
        if (insertion < 0) insertion = before.Length;
        string addition = (insertion > 0 && before[insertion - 1] != '\r' && before[insertion - 1] != '\n' ? newline : "")
            + (bootstrap < 0 ? "/Bootstrap/" + newline : "") + StartupPolicyEntry + newline;
        after = before.Insert(insertion, addition);
        edits.Add(new JObject { ["variable"] = "/Bootstrap/ startup policy index", ["line"] = lines.Count(line => line.Index < insertion) + 1,
            ["before"] = "(entry absent)", ["after"] = addition.TrimEnd('\r', '\n'),
            ["placement"] = "仅在既有安装索引中增加这一项；原字节与条目顺序保留，不扫描、不执行 REHASH。" });
        return Encode(encoding, bom, after);
    }

    static JObject PmlIndexChange(string root)
    {
        string path = GeneratedIndexPath(root);
        if (!File.Exists(path)) return null;
        byte[] original = File.ReadAllBytes(path);
        string before, after; JArray edits;
        byte[] result = ExtendPmlIndex(original, out before, out after, out edits);
        var change = Change(path, result, "pml-index", before, after);
        if ((string)change["beforeSha256"] != Digest(original)) throw Bad("PML index changed while previewing; retry preview.");
        change["lineEdits"] = edits;
        change["ownership"] = "transaction-only-generated-index";
        return change;
    }

    static void VerifyPmlIndexChange(JObject plan, JObject change, JArray manifestFiles)
    {
        string root = (string)plan["root"];
        var prior = plan["priorState"] as JObject;
        if (prior == null || (string)prior["owner"] != Owner2 || !Same((string)prior["root"], root)
            || !Same((string)change["path"], GeneratedIndexPath(root)) || !HasStartupPolicy(manifestFiles)
            || change["before"]?.Type != JTokenType.String || (string)change["ownership"] != "transaction-only-generated-index")
            throw Bad("Generated PML index change is outside the reviewed existing-install scope.");
        string before, after; JArray edits;
        byte[] expected = ExtendPmlIndex(Convert.FromBase64String((string)change["before"]), out before, out after, out edits);
        if (!expected.SequenceEqual(Convert.FromBase64String((string)change["after"])))
            throw Bad("PML index plan may only add the known startup policy entry.");
    }
}
