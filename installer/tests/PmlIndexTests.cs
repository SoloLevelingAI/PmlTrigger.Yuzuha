using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

internal static partial class SetupTests
{
    static void VerifyPmlIndexLifecycle(string temp)
    {
        string area = Path.Combine(temp, "index-lifecycle"), root = Path.Combine(area, "PmlTrigger.IndexTest");
        string library = Path.Combine(root, "PMLLIB"), index = Path.Combine(library, "pml.index");
        Directory.CreateDirectory(library);
        Check(Invoke("PmlIndexChange", root) == null && !File.Exists(index), "Missing generated index must not be created");
        const string originalText = "/Addins/\r\nYuzuhaAddin.pmlobj\r\n/Bootstrap/\r\nYuzuhaResolveRuntimePath.pmlfnc\r\n/Examples/\r\n用户.pmlfnc\r\n";
        const string entry = "YuzuhaStartupPolicy.pmlobj";
        foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true), new UnicodeEncoding(false, true), new UnicodeEncoding(true, true), Encoding.GetEncoding(936) })
        {
            byte[] before = encoding.GetPreamble().Concat(encoding.GetBytes(originalText)).ToArray();
            File.WriteAllBytes(index, before);
            var change = (JObject)Invoke("PmlIndexChange", root);
            byte[] after = Convert.FromBase64String((string)change["after"]);
            byte[] expected = encoding.GetPreamble().Concat(encoding.GetBytes(originalText.Replace("/Examples/", entry + "\r\n/Examples/"))).ToArray();
            Check(after.SequenceEqual(expected), "Index insertion changed original BOM, encoding, bytes or order: " + encoding.WebName);
            Check(File.ReadAllBytes(index).SequenceEqual(before), "Index preview wrote the file");
            File.WriteAllBytes(index, after);
            Check((string)((JObject)Invoke("PmlIndexChange", root))["action"] == "unchanged", "Repeated index planning duplicated the entry");
        }
        File.WriteAllText(index, "/bootstrap/\nyUZUHAstartupPOLICY.PMLobj\n", new UTF8Encoding(false));
        Check((string)((JObject)Invoke("PmlIndexChange", root))["action"] == "unchanged", "Index matching must be case insensitive");
        File.WriteAllText(index, "/Addins/\nExisting.pmlobj", new UTF8Encoding(false));
        var appended = (JObject)Invoke("PmlIndexChange", root);
        Check((string)appended["afterText"] == "/Addins/\nExisting.pmlobj\n/Bootstrap/\n" + entry + "\n", "Absent section append changed original bytes or newline style");
        File.WriteAllText(index, "/Other/\r\n" + entry + "\r\n"); Reject(() => Invoke("PmlIndexChange", root));
        File.WriteAllText(index, "/Bootstrap/\n/Other/\n/Bootstrap/\n"); Reject(() => Invoke("PmlIndexChange", root));
        File.WriteAllText(index, "/Bootstrap/\n" + entry + "\n/Other/\n/Bootstrap/\n"); Reject(() => Invoke("PmlIndexChange", root));
        File.WriteAllBytes(index, new byte[] { 47, 0, 66, 0 }); Reject(() => Invoke("PmlIndexChange", root));
        Console.WriteLine("PASS: generated index absent/UTF8/GBK/UTF16LE/UTF16BE, BOM and original byte preservation, Bootstrap insertion, case-insensitive idempotence and ambiguous index refusal.");

        byte[] baseline = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(originalText)).ToArray();
        File.WriteAllBytes(index, baseline);
        string marker = Path.Combine(root, ".yuzuha-inno.json"), manifest = Path.Combine(area, "manifest.json"), environment = Path.Combine(area, "evars.init");
        string sourceRelative = "PMLLIB/Bootstrap/" + entry;
        string sourceFile = Path.Combine(root, sourceRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)); File.WriteAllText(sourceFile, "synthetic source fixture; never execute");
        File.WriteAllText(marker, new JObject { ["owner"] = "Yuzuha.Inno.Plan.v2", ["root"] = root, ["managed"] = new JArray() }.ToString());
        byte[] markerBefore=File.ReadAllBytes(marker);
        File.WriteAllText(manifest, new JObject { ["version"] = "0.3.4.2", ["files"] = new JArray(new JObject { ["path"] = sourceRelative, ["sha256"] = Program.FileDigest(sourceFile) }) }.ToString());
        byte[] environmentBefore = Encoding.ASCII.GetBytes("set pmllib=vendor\r\nset pmlui=vendor-ui\r\n"); File.WriteAllBytes(environment, environmentBefore);
        var request = new JObject { ["integrationMode"] = "aveva", ["root"] = root, ["mcpJson"] = "", ["environments"] = new JArray(new JObject {
            ["path"] = environment, ["profile"] = "E3D2.1", ["confirmed"] = true, ["encoding"] = "auto", ["startupActions"] = "none" }) };
        var plan = Program.CreatePlan(request, manifest, area);
        var indexChange = ((JArray)plan["changes"]).OfType<JObject>().Single(c => (string)c["kind"] == "pml-index");
        Check(((JArray)plan["payload"]).Count == 1 && Program.Preview(plan).IndexOf("transaction-only", StringComparison.OrdinalIgnoreCase) >= 0 && File.ReadAllBytes(index).SequenceEqual(baseline), "Index must be a visible planned change, never payload or preview mutation");
        string planFile = Path.Combine(area, "plan.json"); Program.SavePlan(plan, planFile);
        Invoke("VerifyPlan", planFile, Program.FileDigest(planFile), manifest, false);
        var omitted = (JObject)plan.DeepClone();
        ((JArray)omitted["changes"]).OfType<JObject>().Single(c => (string)c["kind"] == "pml-index").Remove();
        string omittedFile = Path.Combine(area, "omitted-plan.json"); Program.SavePlan(omitted, omittedFile);
        Reject(() => Invoke("VerifyPlan", omittedFile, Program.FileDigest(omittedFile), manifest, false));
        File.AppendAllText(index, "concurrent index change\r\n");
        Reject(() => Invoke("VerifyPlan", planFile, Program.FileDigest(planFile), manifest, false));
        File.WriteAllBytes(index, baseline);
        var forged = (JObject)plan.DeepClone(); var forgedIndex = ((JArray)forged["changes"]).OfType<JObject>().Single(c => (string)c["kind"] == "pml-index");
        forgedIndex["after"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("arbitrary rewrite")); forgedIndex["afterSha256"] = Program.Digest(Encoding.UTF8.GetBytes("arbitrary rewrite"));
        string forgedFile = Path.Combine(area, "forged-plan.json"); Program.SavePlan(forged, forgedFile);
        Reject(() => Invoke("VerifyPlan", forgedFile, Program.FileDigest(forgedFile), manifest, false));
        Reject(() => Invoke("ProtectUserData", root, "PMLLIB/pml.index", "fixture", false));

        string success = Path.Combine(area, "commit-success"); Directory.CreateDirectory(success);
        JObject committedState = null;
        Invoke("CommitConfigurationChanges", plan, success, new Action<JObject, JArray>((state, actual) => { committedState = state; }));
        Check(Program.FileDigest(index) == (string)indexChange["afterSha256"], "Actual configuration transaction did not write the approved index");
        Check(((JArray)committedState["managed"]).Count == 1 && ((JArray)committedState["managed"]).OfType<JObject>().All(c => (string)c["kind"] != "pml-index"), "Generated index leaked into long-term managed ownership");
        var result = JObject.Parse(File.ReadAllText(Path.Combine(success, "result.json")));
        var indexReceipt = ((JArray)result["actual"]).OfType<JObject>().Single(c => (string)c["kind"] == "pml-index");
        Check(File.ReadAllBytes((string)indexReceipt["backup"]).SequenceEqual(baseline), "Index backup did not preserve exact original bytes");
        File.WriteAllText(marker, committedState.ToString());
        File.AppendAllText(index, "/User/\r\nLaterLegalRehash.pmlobj\r\n");
        var afterRehash = Program.CreatePlan(request, manifest, area);
        Check(((JArray)afterRehash["changes"]).OfType<JObject>().Single(c => (string)c["kind"] == "pml-index")["action"].ToString() == "unchanged", "Legal future index regeneration blocked an upgrade");

        File.WriteAllBytes(index, baseline); File.WriteAllBytes(environment, environmentBefore);File.WriteAllBytes(marker,markerBefore);
        string failure = Path.Combine(area, "commit-rollback"); Directory.CreateDirectory(failure);
        Reject(() => Invoke("CommitConfigurationChanges", plan, failure, new Action<JObject, JArray>((state, actual) => { throw new IOException("Synthetic finalization failure"); })));
        Check(File.ReadAllBytes(index).SequenceEqual(baseline) && File.ReadAllBytes(environment).SequenceEqual(environmentBefore), "Failed transaction did not restore both environment and index bytes");
        string conflict = Path.Combine(area, "commit-concurrent"); Directory.CreateDirectory(conflict);
        byte[] concurrent = Encoding.ASCII.GetBytes("later external index contents");
        Reject(() => Invoke("CommitConfigurationChanges", plan, conflict, new Action<JObject, JArray>((state, actual) => { File.WriteAllBytes(index, concurrent); throw new IOException("Synthetic concurrent finalization failure"); })));
        Check(File.ReadAllBytes(index).SequenceEqual(concurrent) && File.ReadAllBytes(environment).SequenceEqual(environmentBefore), "Rollback overwrote a later index edit or failed to restore unchanged environment");
        Check(File.ReadAllText(Path.Combine(conflict, "result.json")).Contains("Concurrent change; backup retained"), "Concurrent rollback conflict was not reported");
        Check(Directory.GetFiles(conflict, "*.bak").Any(path => File.ReadAllBytes(path).SequenceEqual(baseline)), "Original index backup was lost after rollback conflict");

        var freshRequest = (JObject)request.DeepClone(); freshRequest["root"] = Path.Combine(area, "PmlTrigger.NewInstall");
        var freshPlan = Program.CreatePlan(freshRequest, manifest, area);
        Check(!((JArray)freshPlan["changes"]).OfType<JObject>().Any(c => (string)c["kind"] == "pml-index"), "New installation fabricated a generated index");
        Console.WriteLine("PASS: real temporary-file index plan/hash freeze, tampered delta refusal, same transaction backup+commit+rollback, concurrent edit preservation, no persistent index ownership and no index creation on fresh install.");
    }
}
