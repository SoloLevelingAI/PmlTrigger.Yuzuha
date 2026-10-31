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
        async Task Settings()
        {
            var value = Ui.Prompt("后端设置", "独立后端地址；更改前先记录当前草稿。", Api.Address, this);
            if (value == null)
                return;
            await FlushAllDocuments();
            var candidate = new ApiClient(value);
            try
            {
                await candidate.Get<object>("api/health");
            }
            catch { candidate.Dispose(); throw; }
            Api.Dispose();
            Api = candidate;
            Remember();
            status.Text = "已连接 " + Api.Address;
        }
    }
}
