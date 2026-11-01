using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace RibbonStudio.Backend
{
    public static class Program
    {
        static readonly Dictionary<string, Func<object, object>> post = new Dictionary<string, Func<object, object>>(); static WorkspaceFiles store; static WorkspaceFeature workspace; static DeploymentFeature deployment; static ManagementFeature management; static string web; static bool serveWeb;
        public static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
                int port = Int32.Parse(Environment.GetEnvironmentVariable("UIC_PORT") ?? "4318");
                store = new WorkspaceFiles(Environment.GetEnvironmentVariable("UIC_DATA_DIR") ?? Path.Combine(Environment.CurrentDirectory, "data", "uic-workspace"));
                workspace = new WorkspaceFeature(store);
                deployment = new DeploymentFeature(store);
                management = new ManagementFeature(store, workspace);
                web = Environment.GetEnvironmentVariable("UIC_WEB_ROOT") ?? Path.Combine(Environment.CurrentDirectory, "apps", "web", "dist");
                serveWeb = args.Contains("--serve-web");
                Routes();
                var listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                Console.WriteLine("Ribbon Studio C# API ready: http://127.0.0.1:" + port);
                for (; ; )
                {
                    var context = listener.GetContext();
                    Task.Run(() => Handle(context));
                }
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        static Project P(object value)
        {
            try
            {
                return Data.As<Project>(value);
            }
            catch { throw new ApiError(422, "项目结构不正确"); }
        }
        static void Routes()
        {
            var frozen = new FrozenDeploymentFeature(store, deployment);
            post["v2/deployment/file-preview"] = x => frozen.Preview(x, "file-update");
            post["v2/deployment/file-apply"] = x => frozen.Apply(x, "file-update");
            post["v2/deployment/registration-preview"] = x => frozen.Preview(x, "registration-add");
            post["v2/deployment/registration-apply"] = x => frozen.Apply(x, "registration-add");
            var registration = new RegistrationFeature(store);
            post["registration/scan"] = registration.Scan;
            post["registration/profiles"] = x => registration.Profiles();
            post["registration/profile-save"] = registration.SaveProfile;
            post["projects/validate"] = x => ProjectRules.Validate(P(x));
            post["projects/normalize"] = x => ProjectRules.Normalize(P(x));
            post["uic/export"] = x => UicFiles.Export(P(x));
            post["uic/import"] = workspace.Import;
            post["images/prepare"] = x => { var image = IconImages.Resize(Data.As<ImageAsset>(Data.Get(x, "image")), 32); if (image == null) throw new ApiError(422, "请选择 PNG/JPEG 图片"); return new { image, width = 32, height = 32, originalBytes = Convert.FromBase64String(Data.Text(Data.Get(x, "image"), "base64")).Length, outputBytes = Convert.FromBase64String(image.base64).Length }; };
            post["workspace/save"] = x => store.Save(P(Data.Get(x, "project")), Data.Text(x, "kind") == "edit" ? "edit" : "save", Data.Text(x, "note"));
            post["workspace/context"] = x => store.Get(Data.Text(x, "documentId"));
            post["workspace/open"] = x => Data.Get(store.Version(Data.Text(x, "documentId"), Data.Text(x, "versionId")), "project");
            post["workspace/restore"] = x => workspace.Restore(Data.Text(x, "documentId"), Data.Text(x, "versionId"));
            post["workspace/original"] = x => workspace.Original(Data.Text(x, "documentId"), Data.Text(x, "versionId"));
            post["workspace/versions"] = x => new { versions = store.Versions(Data.Text(x, "documentId")).Select(v => { var p = P(v["project"]); return new { id = v["id"], sequence = v["sequence"], createdAt = v["createdAt"], kind = v["kind"], note = v["note"], valid = v["valid"], hash = v["hash"], buttons = p.buttons.Count, groups = p.tabs.Sum(t => t.groups.Count), hasOriginal = Data.Text(Data.Get(v, "artifacts"), "originalUicBase64") != "" || Data.Text(Data.Get(v, "artifacts"), "originalXml") != "" }; }).ToList() };
            post["deployment/inspect"] = x => Manifests.Inspect(Data.Text(x, "monPath"));
            post["deployment/modules"] = deployment.Modules;
            post["deployment/file-preview"] = x => deployment.FilePreview(P(Data.Get(x, "project")));
            post["deployment/file-apply"] = deployment.FileApply;
            post["deployment/registration-preview"] = x => deployment.Registration(x, false);
            post["deployment/registration-apply"] = x => deployment.Registration(x, true);
            post["deployment/preview"] = deployment.Preview;
            post["deployment/apply"] = deployment.Apply;
            post["management/list"] = x => management.Inventory(Data.Text(x, "monPath"), Data.Text(x, "manifestName"));
            post["management/open"] = management.Open;
            post["management/adopt"] = management.Adopt;
            post["management/toggle"] = management.Toggle;
        }
        static void Send(HttpListenerResponse response, int status, object value, string type = "application/json; charset=utf-8")
        {
            byte[] bytes = value as byte[] ?? Encoding.UTF8.GetBytes(type.StartsWith("application/json") ? Json.Write(value) : Convert.ToString(value));
            response.StatusCode = status;
            response.ContentType = type;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        static void Handle(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;
            res.Headers["X-Content-Type-Options"] = "nosniff";
            res.Headers["Cache-Control"] = "no-store";
            try
            {
                string route = req.Url.AbsolutePath;
                if (req.HttpMethod == "GET" && route == "/api/health")
                {
                    Send(res, 200, new
                    {
                        status = "ok",
                        version = "0.4.0",
                        implementation = "csharp",
                        contractVersion = 1
                    });
                    return;
                }
                if (req.HttpMethod == "GET" && route == "/api/v1/projects/example")
                {
                    Send(res, 200, Json.Read<object>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "example.json"))));
                    return;
                }
                if (req.HttpMethod == "GET" && route == "/api/v1/deployment/targets")
                {
                    Send(res, 200, new
                    {
                        targets = Manifests.Discover()
                    });
                    return;
                }
                if (req.HttpMethod == "GET" && route == "/api/v1/workspace/documents")
                {
                    Send(res, 200, new
                    {
                        documents = store.List()
                    });
                    return;
                }
                if (route.StartsWith("/api/"))
                {
                    string name = route.StartsWith("/api/v2/") ? "v2/" + route.Substring(8) : route.StartsWith("/api/v1/") ? route.Substring(8) : "";
                    if (!post.ContainsKey(name))
                        throw new ApiError(404, "API 不存在");
                    if (req.HttpMethod != "POST")
                        throw new ApiError(405, "请使用 POST");
                    if (req.ContentType == null || !req.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                        throw new ApiError(415, "请使用 application/json");
                    if (req.ContentLength64 > 20 * 1024 * 1024)
                        throw new ApiError(413, "请求不能超过 20 MiB");
                    object input;
                    using (var memory = new MemoryStream())
                    {
                        var buffer = new byte[8192];
                        int read;
                        while ((read = req.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (memory.Length + read > 20 * 1024 * 1024)
                                throw new ApiError(413, "请求不能超过 20 MiB");
                            memory.Write(buffer, 0, read);
                        }
                        try
                        {
                            input = Json.Read<object>(Encoding.UTF8.GetString(memory.ToArray()));
                        }
                        catch { throw new ApiError(400, "JSON 无法解析"); }
                    }
                    object output = post[name](input);
                    if (name == "uic/export" || name == "workspace/original")
                    {
                        res.Headers["Content-Disposition"] = "attachment; filename=\"" + (name == "uic/export" ? "ribbon.uic" : "original.uic") + "\"";
                        Send(res, 200, output, "application/xml; charset=utf-8");
                    }
                    else
                        Send(res, 200, output);
                    return;
                }
                if (serveWeb && (req.HttpMethod == "GET" || req.HttpMethod == "HEAD"))
                {
                    string root = Path.GetFullPath(web), path = Path.GetFullPath(Path.Combine(root, Uri.UnescapeDataString(route == "/" ? "index.html" : route.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar)));
                    if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                        throw new ApiError(404, "文件不存在");
                    string type = Path.GetExtension(path) == ".html" ? "text/html; charset=utf-8" : Path.GetExtension(path) == ".js" ? "text/javascript; charset=utf-8" : Path.GetExtension(path) == ".css" ? "text/css; charset=utf-8" : "application/octet-stream";
                    Send(res, 200, req.HttpMethod == "HEAD" ? new byte[0] : File.ReadAllBytes(path), type);
                    return;
                }
                throw new ApiError(404, "文件不存在");
            }
            catch (ApiError error) { Send(res, error.Status, error.Issues == null ? (object)new { message = error.Message } : new { message = error.Message, issues = error.Issues }); }
            catch (UnauthorizedAccessException error) { Console.Error.WriteLine(error);Send(res, 403, new { message = "没有目标目录写入权限，请由有权限的用户启动后端并重新预览" }); }
            catch (System.Xml.XmlException error) { Send(res, 422, new { message = "XML 无效：" + error.Message }); }
            catch (Exception error) { Console.Error.WriteLine(error); Send(res, 500, new { message = "服务处理失败，请检查后端日志" }); }
            finally { res.Close(); }
        }
    }
}
