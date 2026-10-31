using System.Text;
using PmlTwin.Phayton;
using YuzuhaToolkit.Manager;

Console.InputEncoding = new UTF8Encoding(false, true);
Console.OutputEncoding = new UTF8Encoding(false);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
if (args.FirstOrDefault() == "workbench")
    return await LaunchWorkbenchCommand.RunAsync(args[1..], cancellation.Token);
return await ManagerCliHost.RunAsync(args, ManagerOperationCatalog.Create(), Console.Out,
    cancellation.Token, Console.In);
