using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        public async Task Initialize()
        {
            try
            {
                await Api.Get<object>("api/health");
                string last = null;
                if (!smoke && File.Exists(settingsPath))
                {
                    try
                    {
                        var settings = Json.Read<Dictionary<string, string>>(File.ReadAllText(settingsPath));
                        if (settings.ContainsKey("documentId"))
                            last = settings["documentId"];
                    }
                    catch { }
                }
                Project p = null;
                if (!String.IsNullOrEmpty(last))
                    try
                    {
                        p = await Api.Post<Project>("workspace/open", new
                        {
                            documentId = last
                        });
                    }
                    catch { }
                if (p == null)
                {
                    var docs = await Api.Get<Documents>("api/v1/workspace/documents");
                    if (!smoke && docs.documents.Count > 0)
                        p = await Api.Post<Project>("workspace/open", new
                        {
                            documentId = docs.documents[0].id
                        });
                    else
                        p = await Api.Get<Project>("api/v1/projects/example");
                }
                await OpenDocument(p);
                status.Text = "后端已连接 · 编辑草稿自动保留最近 5 次";
            }
            catch (Exception error) { status.Text = "后端未连接：" + error.Message; projectOverview.Children.Add(Ui.Text("请启动独立后端，或在“后端设置”中填写地址。", 14)); }
            await RestoreWorkspaceLayout();
        }
        void Remember()
        {
            if (smoke || Project == null)
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath, Json.Write(new
            {
                api = Api.Address,
                documentId = Project.documentId
            }));
        }
        public async Task Flush()
        {
            if (activeDocument == null)
                return;
            CaptureSession(false);
            autosave.Stop();
            await FlushDocument(activeDocument);
        }
        public Task SaveDraft(string kind)
        {
            if (activeDocument == null)
                return Task.FromResult(0);
            CaptureSession(false);
            autosave.Stop();
            return QueueDocumentSave(activeDocument, kind);
        }
        Task QueueDocumentSave(DocumentSession doc, string kind)
        {
            var prior = doc.Queue;
            doc.Queue = SaveDocumentAfter(doc, prior, kind);
            if (activeDocument == doc)
                saveQueue = doc.Queue;
            return doc.Queue;
        }
        async Task SaveDocumentAfter(DocumentSession doc, Task prior, string kind)
        {
            try
            {
                await prior;
            }
            catch { }
            var at = doc.Revision;
            var project = doc.Project;
            var snapshot = Json.Copy(project);
            var result = await Api.Post<SaveResult>("workspace/save", new
            {
                project = snapshot,
                kind,
                note = kind == "save" ? "WPF 手动保存" : "WPF 自动编辑草稿"
            });
            project.documentId = result.document.id;
            if (doc.Project == project && doc.Revision == at)
                doc.Dirty = false;
            doc.Status = (kind == "save" ? "长期快照" : "草稿已记录") + " #" + result.version.sequence + " · " + DocumentName(doc) + (HasPendingLibraryDrafts(doc.Project) ? " · 详情尚有未应用修改" : "");
            if (activeDocument == doc)
            {
                dirty = doc.Dirty;
                status.Text = doc.Status;
                Remember();
            }
            UpdateWorkspaceHeaders();
        }

        async Task OpenProject()
        {
            var path = Ui.Open("Ribbon 项目|*.ribbon.json;*.json");
            if (path == null)
                return;
            await OpenProjectPath(path);
        }
        async Task OpenProjectPath(string path)
        {
            var value = Json.Read<Project>(File.ReadAllText(path));
            if (value == null || value.schemaVersion != 1)
                throw new Exception("不支持的项目格式");
            var normalized = await Api.Post<Project>("projects/normalize", value);
            await Flush();
            if (!String.IsNullOrEmpty(normalized.documentId))
            {
                try
                {
                    await Api.Post<Document>("workspace/context", new
                    {
                        documentId = normalized.documentId
                    });
                }
                catch { normalized.documentId = null; }
            }
            await OpenDocument(normalized);
            Changed();
        }
        async Task SaveProject()
        {
            if (Project == null)
                return;
            RequireAppliedCommandDrafts(Project);
            await Flush();
            await NormalizeCurrent();
            await SaveDraft("save");
            RenderAll();
        }
        async Task ExportProject()
        {
            RequireAppliedCommandDrafts(Project);
            await Flush();
            var path = Ui.Save("Ribbon 项目|*.ribbon.json", SafeName() + ".ribbon.json");
            if (path != null)
                File.WriteAllText(path, Json.Write(Project));
        }
        async Task NormalizeCurrent()
        {
            var normalized = await Api.Post<Project>("projects/normalize", Project);
            for (int i = 0; i < Project.buttons.Count; i++)
            {
                Project.buttons[i].commandKey = normalized.buttons[i].commandKey;
                Project.buttons[i].image = normalized.buttons[i].image;
            }
            UpdateHistoryButtons();
        }
        string SafeName()
        {
            return String.Join("_", Project.@namespace.Split(Path.GetInvalidFileNameChars()));
        }




    }
}
