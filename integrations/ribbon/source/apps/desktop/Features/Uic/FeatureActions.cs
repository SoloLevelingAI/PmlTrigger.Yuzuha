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
        public async Task ImportPath(string path)
        {
            if (ActivateOpenPath(path))
                return;
            await Flush();
            var result = await Api.Post<SaveResult>("uic/import", new
            {
                path = path
            });
            await OpenDocument(result.project);
            status.Text = "已导入原 UIC；源文件保持不变";
        }
        async Task Import()
        {
            var paths = Ui.OpenMany("UIC 或 Ribbon 项目|*.uic;*.ribbon.json;*.json|UIC 文件|*.uic|Ribbon 项目|*.ribbon.json;*.json");
            foreach (var path in paths)
            {
                if (Path.GetExtension(path).Equals(".uic", StringComparison.OrdinalIgnoreCase))
                    await ImportPath(path);
                else
                    await OpenProjectPath(path);
            }
        }
        public async Task Export()
        {
            if (Project == null)
                return;
            RequireAppliedCommandDrafts(Project);
            await Flush();
            await NormalizeCurrent();
            await SaveDraft("save");
            var xml = await Api.Text("uic/export", Project);
            var path = Ui.Save("UIC 文件|*.uic", SafeName() + ".uic");
            if (path != null)
                File.WriteAllText(path, xml, new System.Text.UTF8Encoding(false));
            status.Text = "UIC 已生成 · " + path;
        }
    }
}
