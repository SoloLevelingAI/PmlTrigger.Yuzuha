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
        async Task NewProject()
        {
            await Flush();
            var example = await Api.Get<Project>("api/v1/projects/example");
            example.documentId = null;
            example.origin = null;
            example.preferredBinding = null;
            example.@namespace = "MyRibbon";
            example.buttons = example.buttons.Take(1).ToList();
            example.tabs = example.tabs.Take(1).ToList();
            example.tabs[0].groups = example.tabs[0].groups.Take(1).ToList();
            example.tabs[0].groups[0].items = example.tabs[0].groups[0].items.Take(1).ToList();
            await OpenDocument(example);
            Changed();
        }
        async Task Example()
        {
            await Flush();
            await OpenDocument(await Api.Get<Project>("api/v1/projects/example"));
            Changed();
        }
        public async Task Validate()
        {
            if (Project == null)
                return;
            var result = await Api.Post<Validation>("projects/validate", Project);
            status.Text = result.valid ? "校验通过" : "存在 " + result.issues.Count + " 个问题";
            if (!smoke)
                Ui.Message(result.valid ? "校验通过，可以导出或预览部署。" : String.Join("\n", result.issues.Select(i => i.path + "：" + i.message)), "项目检查");
        }
    }
}