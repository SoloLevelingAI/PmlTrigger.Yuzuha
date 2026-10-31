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
        void RemoveButtonImage(RibbonButton button)
        {
            RunEdit("移除图标", () => SetEdit(button.image, null, value => button.image = value));
        }
        async Task Upload()
        {
            if (Selected.Button == null)
                return;
            var targetButton = Selected.Button;
            int targetEpoch = epoch;
            int targetRevision = revision;
            var path = Ui.Open("图标|*.png;*.jpg;*.jpeg");
            if (path == null)
                return;
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > 12 * 1024 * 1024)
                throw new Exception("图片不能超过 12 MiB");
            var result = await Api.Post<PreparedImage>("images/prepare", new
            {
                image = new ImageAsset { mediaType = Path.GetExtension(path).ToLower() == ".png" ? "image/png" : "image/jpeg", base64 = Convert.ToBase64String(bytes) }
            });
            if (targetEpoch != epoch || targetRevision != revision || !Project.buttons.Contains(targetButton))
            {
                status.Text = "编辑上下文已改变，请重新选择图片";
                return;
            }
            RunEdit("更换图标", () => SetEdit(targetButton.image, result.image, value => targetButton.image = value));
            RenderTree();
            RenderInspector();
            RenderPreview();
        }
    }
}