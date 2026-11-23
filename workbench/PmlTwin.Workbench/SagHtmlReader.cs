using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Core30.Knowledge;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private void OpenSagHtml(SagSearchHit hit, string path)
    {
        using var view = Id(new WebView2 { Height = 480 }, "SagHtmlOriginal");
        var loading = Muted("正在加载原文件…");
        var readingPane = new ContentControl { Content = Stack(loading, Below(view, 4)) };
        var closed = false;
        void UseTextView()
        {
            if (closed) return;
            try
            {
                readingPane.Content = Code(SagKnowledgeLibrary.ReadHtmlOriginal(hit), "SagHtmlOriginalText", 480);
            }
            catch (Exception error) when (WorkbenchError(error)) { readingPane.Content = Muted(error.Message); }
        }
        async void OpenFile(object sender, RoutedEventArgs args)
        {
            view.Loaded -= OpenFile;
            try
            {
                var data = Path.Combine(_store.DirectoryPath, ".core30", "sag-html-viewer");
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: data);
                if (closed) return;
                await view.EnsureCoreWebView2Async(environment);
                if (closed) return;
                var browser = view.CoreWebView2;
                browser.Settings.IsScriptEnabled = false;
                browser.Settings.AreDefaultScriptDialogsEnabled = false;
                browser.NewWindowRequested += (_, e) => e.Handled = true;
                browser.NavigationStarting += (_, e) =>
                {
                    if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) || !target.IsFile)
                        e.Cancel = true;
                };
                browser.NavigationCompleted += (_, e) =>
                {
                    loading.Visibility = Visibility.Collapsed;
                    if (!e.IsSuccess) UseTextView();
                };
                browser.Navigate(new Uri(path).AbsoluteUri);
            }
            catch (Exception error) when (WorkbenchError(error) || error is COMException or DllNotFoundException)
            { UseTextView(); }
        }
        view.Loaded += OpenFile;
        try
        {
            Modal(hit.Title, Stack(Muted(path), Below(readingPane, 7)), "完成", () => true, width: 880, cancel: false);
        }
        finally { closed = true; }
    }
}
