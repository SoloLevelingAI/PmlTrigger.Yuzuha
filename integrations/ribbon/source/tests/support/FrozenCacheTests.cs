using System;
using System.IO;
using System.Collections.Generic;
using RibbonStudio;
using RibbonStudio.Backend;
public static class FrozenCacheTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static object Ref(object plan) { return new Dictionary<string, object> { { "planId", Data.Text(plan, "planId") }, { "planDigest", Data.Text(plan, "planDigest") } }; }
    public static int Main(string[] args)
    {
        try
        {
            var store = new WorkspaceFiles(Path.Combine(args[0], "data")); var workspace = new WorkspaceFeature(store);
            var source = Path.Combine(args[0], "source.uic"); var project = Json.Read<Project>(File.ReadAllText(args[1]));
            File.WriteAllText(source, UicFiles.Export(project));
            var p = workspace.Import(new Dictionary<string, object> { { "path", source } }).project;
            p.buttons[0].caption = "changed";
            var input = new Dictionary<string, object> { { "project", p } }; var clock = DateTime.UtcNow;
            var feature = new FrozenDeploymentFeature(store, new DeploymentFeature(store), () => clock);
            var plan = feature.Preview(input, "file-update"); var before = Data.Text(store.Get(p.documentId), "lastVersion");
            clock = clock.AddMinutes(6);
            try { feature.Apply(Ref(plan), "file-update"); throw new Exception("expired plan accepted"); } catch (ApiError e) { Check(e.Status == 409, "expiration status"); }
            Check(Data.Text(store.Get(p.documentId), "lastVersion") == before, "expired plan saved history");
            for (int i = 0; i < 64; i++) feature.Preview(input, "file-update");
            try { feature.Preview(input, "file-update"); throw new Exception("cache budget exceeded"); } catch (ApiError e) { Check(e.Status == 423, "budget status"); }
            clock = clock.AddMinutes(6); plan = feature.Preview(input, "file-update");
            File.SetAttributes(source, FileAttributes.ReadOnly);
            try
            {
                var result = feature.Apply(Ref(plan), "file-update"); Check(Data.Text(result, "state") == "unknown", "failed write must be unknown");
                var version = Data.Text(store.Get(p.documentId), "lastVersion");
                Check(Json.Write(feature.Apply(Ref(plan), "file-update")) == Json.Write(result), "unknown receipt changed");
                Check(Data.Text(store.Get(p.documentId), "lastVersion") == version, "unknown write was replayed");
            }
            finally { File.SetAttributes(source, FileAttributes.Normal); }
            Console.WriteLine("cache expiration/admission/unknown: PASS"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
