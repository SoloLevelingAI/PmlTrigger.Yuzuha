using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace RibbonStudio
{
    public static class App
    {
        public static string Argument(string[] args, string key, string fallback = null)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                var app = new Application();
                Ui.InitTheme();
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "OfficeIcons.xaml")) });
                string address = Argument(args, "--api", Environment.GetEnvironmentVariable("UIC_API_URL") ?? "http://127.0.0.1:4318");
                bool verify = args.Contains("--verify");
                var layoutFile = Argument(args, "--layout-file", verify ? Path.Combine(Path.GetDirectoryName(Argument(args, "--result")), "window-layout.json") : null);
                var window = new MainWindow(new ApiClient(address), verify, layoutFile);
                app.MainWindow = window;
                int code = 0;
                window.Loaded += async (s, e) => { try { await window.Initialize(); if (verify) { if(Argument(args, "--layout-phase")!=null) await window.VerifyLayoutRestartPhase(Argument(args,"--layout-phase"),Argument(args,"--result")); else await Smoke.Run(window, args); app.Shutdown(0); } else { var openPath = Argument(args, "--open"); if(openPath!=null) await window.OpenSavedLocation(openPath); var snapshot = Argument(args, "--snapshot"); if (snapshot != null) Ui.Capture(window, snapshot); } } catch (Exception error) { File.WriteAllText(Argument(args, "--result", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop-error.log")), error.ToString()); if (verify) { code = 1; app.Shutdown(1); } else Ui.Error(error); } };
                app.DispatcherUnhandledException += (s, e) => { var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RibbonStudio", "Desktop"); Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "error.log"), DateTime.Now + " " + e.Exception + Environment.NewLine); Ui.Error(e.Exception); e.Handled = true; };
                if (verify)
                {
                    window.ShowInTaskbar = false;
                    window.Left = -20000;
                    window.Top = -20000;
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                }
                window.Show();
                app.Run();
                return code;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop-error.log"), error.ToString()); return 1; }
        }
    }
    public static class Smoke
    {
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception("Desktop smoke failed: " + message);
        }
        public static async Task Run(MainWindow window, string[] args)
        {
            var api = window.Api;
            string source = App.Argument(args, "--source"), mon = App.Argument(args, "--mon"), result = App.Argument(args, "--result");
            if (source == null || mon == null || result == null)
                throw new Exception("verify requires isolated fixture source, mon and result paths");
            var baseline = File.ReadAllBytes(source);
            await window.ImportPath(source);
            Check(window.Project.buttons.Count > 0, "import");
            var baselineVersion = (await api.Post<Versions>("workspace/versions", new
            {
                documentId = window.Project.documentId
            })).versions[0];
            var group = window.Project.tabs[0].groups[0];
            window.Selected = new Selection { Kind = "group", Tab = window.Project.tabs[0], Group = group };
            await window.Add("button");
            window.Selected.Button.caption = "WPF 验证按钮";
            window.Selected.Button.macro = "show !!DesktopSmoke";
            window.Selected.Button.tooltipText = "中文 < > & Tooltip";
            window.Selected.Item.size = "small";
            var pixels = new byte[64 * 64 * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 80;
                pixels[i + 1] = 110;
                pixels[i + 2] = 210;
                pixels[i + 3] = 255;
            }
            var sourceBitmap = BitmapSource.Create(64, 64, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 64 * 4);
            var iconEncoder = new PngBitmapEncoder();
            iconEncoder.Frames.Add(BitmapFrame.Create(sourceBitmap));
            using (var memory = new MemoryStream())
            {
                iconEncoder.Save(memory);
                var prepared = await api.Post<PreparedImage>("images/prepare", new
                {
                    image = new ImageAsset { mediaType = "image/png", base64 = Convert.ToBase64String(memory.ToArray()) }
                });
                window.Selected.Button.image = prepared.image;
                var image = (BitmapSource)Ui.Image(prepared.image);
                Check(image.PixelWidth == 32 && image.PixelHeight == 32, "backend image resizing");
            }
            await window.SaveDraft("edit");
            var normalized = await api.Post<Project>("projects/normalize", window.Project);
            window.Load(normalized);
            await window.SaveDraft("save");
            var xml = await api.Text("uic/export", window.Project);
            Check(xml.Contains("DesktopSmoke") && xml.Contains("WPF 验证按钮"), "editing and export");
            Check(baseline.SequenceEqual(File.ReadAllBytes(source)), "editing must not write source");
            var plan = await api.Post<Plan>("deployment/file-preview", new
            {
                project = window.Project
            });
            Check(plan.uicPath == source, "original target");
            await api.Post<Plan>("deployment/file-apply", new
            {
                project = window.Project,
                expectedUicHash = plan.uicHash
            });
            Check(File.ReadAllText(source).Contains("DesktopSmoke"), "apply original in isolated fixture");
            var collected = await api.Post<Modules>("deployment/modules", new
            {
                project = window.Project,
                monPath = mon
            });
            Check(collected.modules.Any(m => m.Existing), "collect existing");
            var available = collected.modules.Where(m => !m.Existing && String.IsNullOrEmpty(m.error)).Take(2).ToList();
            Check(available.Count == 2, "two available modules");
            foreach (var row in available)
            {
                var p = await api.Post<Plan>("deployment/registration-preview", new
                {
                    project = window.Project,
                    monPath = mon,
                    manifestName = row.manifestName,
                    entryName = "DesktopSmoke"
                });
                Check(p.afterEntries.Count == p.beforeEntries.Count + 1 && p.addedEntries.Count == 1, "actual XML comparison");
                await api.Post<Plan>("deployment/registration-apply", new
                {
                    project = window.Project,
                    monPath = mon,
                    manifestName = row.manifestName,
                    entryName = "DesktopSmoke",
                    expectedManifestHash = p.manifestHash,
                    expectedUicHash = p.uicHash
                });
                Check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(mon), row.manifestName)) == p.afterXml, "write equals preview");
            }
            var inventory = await api.Post<Inventory>("management/list", new
            {
                monPath = mon,
                manifestName = available[0].manifestName
            });
            var owned = inventory.rows.First(m => m.origin == "managed");
            await api.Post<object>("management/toggle", new
            {
                monPath = mon,
                manifestName = available[0].manifestName,
                key = owned.key,
                expectedManifestHash = owned.manifestHash,
                enabled = false
            });
            inventory = await api.Post<Inventory>("management/list", new
            {
                monPath = mon,
                manifestName = available[0].manifestName
            });
            owned = inventory.rows.First(m => m.origin == "managed");
            Check(!owned.enabled, "disable association retains record");
            await api.Post<object>("management/toggle", new
            {
                monPath = mon,
                manifestName = available[0].manifestName,
                key = owned.key,
                expectedManifestHash = owned.manifestHash,
                enabled = true
            });
            var restored = await api.Post<SaveResult>("workspace/restore", new
            {
                documentId = window.Project.documentId,
                versionId = baselineVersion.id
            });
            window.Load(restored.project);
            Check(!window.Project.buttons.Any(b => b.macro.Contains("DesktopSmoke")), "restore editor baseline");
            Check(File.ReadAllText(source).Contains("DesktopSmoke"), "restore must not write deployed file");
            window.SwitchCommandView("layout");
            await ThemeVerification.Run(window, result);
            await window.VerifyDocumentSessions(Path.GetDirectoryName(source), mon, Path.ChangeExtension(result, "documents.png"));
            await window.VerifyFunctionTable(mon, Path.ChangeExtension(result, "table.png"));
            await window.VerifyCommandFirst(Path.ChangeExtension(result, "command-first.png"));
            await window.VerifyWorkspaceTools(Path.ChangeExtension(result, "workspace-tools.png"));
            await window.VerifyAuxiliaryDocking(Path.ChangeExtension(result, "docking.png"));
            await window.VerifyRibbonFirstDock(Path.ChangeExtension(result, "ribbon-first.png"));
            var library = new LibraryWindow(window);
            library.Show();
            await library.VerifyReadOnly(mon, Path.ChangeExtension(result, "library.png"));
            library.Close();
            var deployment = new DeploymentWindow(window, window.Project, true);
            deployment.Show();
            await deployment.VerifyReadOnly(mon, Path.ChangeExtension(result, "deployment.png"));
            deployment.Close();
            await new StudioToolPage(window, false).VerifyFrozenPreview(Path.ChangeExtension(result, "frozen-v2-preview.png"));
            Ui.Capture(window, Path.ChangeExtension(result, "png"));
            File.WriteAllText(result, Json.Write(new
            {
                passed = true,
                commandFirstWorkspace = true,
                globalWorkspaceTools = true,
                modelessDockableTools = true,
                closeAuxiliaryRedocks = true,
                singleScreenNonOverlapping = true,
                definitionAndHistoryInline = true,
                import = true,
                edit = true,
                normalize = true,
                export = true,
                imageResize32 = true,
                originalBackup = true,
                moduleCollection = true,
                twoModuleRegistration = true,
                xmlPreviewMatchesWrite = true,
                loadingToggle = true,
                persistentHistory = true,
                editorRestore = true,
                windowRender = true,
                nativeLibraryWindow = true,
                nativeModuleTransfer = true,
                nativeFrozenRegistrationPreviewV2 = true,
                nativeMainToolFrozenFilePreviewV2 = true,
                nativePreviewReturn = true,
                borderlessChrome = true,
                hoverPressedSelectedDisabled = true,
                themedDropdown = true,
                contextMenuClone = true,
                contextMenuSize = true,
                layoutOrder = true,
                crossGroupMove = true,
                deferredCutPaste = true,
                independentRepeatedPaste = true,
                tabGroupClone = true,
                staleDropRejected = true,
                singleActiveTab = true,
                embeddedDeploymentReuse = true,
                undoRedoLayout = true,
                undoRedoCutPaste = true,
                undoRedoStableClone = true,
                undoAfterSave = true,
                redoBranchTruncation = true,
                undoTextFocusRouting = true,
                boundedSessionUndo = true,
                incrementalHistory = true,
                multiDocumentSessions = true,
                independentFileUndo = true,
                boundBackgroundSave = true,
                closableWorkspaceTabs = true,
                singletonDeploymentAndRegistrationTabs = true,
                editableFunctionTable = true,
                bulkPropertiesAndGroupUndoRedo = true,
                tableSharesDocumentUndo = true,
                realXmlCountsAndVersions = true,
                smallIconsLeftAligned = true,
                compactCommandBounds1280And1050 = true,
                largeDocumentEdits200Ms = window.lastEditBenchmarkMs
            }));
        }
    }
}
