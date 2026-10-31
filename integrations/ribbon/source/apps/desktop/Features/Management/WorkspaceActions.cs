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
        async Task Library()
        {
            await Flush();
            var view=workspaceViews.FirstOrDefault(v=>v.Kind=="history");
            if(view==null){ view=AddWorkspaceView("history",null);var page=new LibraryWindow(this,true);view.Controller=page;var content=page.Content;page.Content=null;view.Tab.Content=content; }
            SelectWorkspaceView(view);await ((LibraryWindow)view.Controller).Ready();
        }
    }
}
