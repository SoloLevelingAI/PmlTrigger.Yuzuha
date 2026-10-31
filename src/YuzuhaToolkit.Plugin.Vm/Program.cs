using System.Text;
using System.Text.Json;
using YuzuhaToolkit.Plugins;
using YuzuhaToolkit.Plugins.Vm;

Console.InputEncoding = new UTF8Encoding(false, true); Console.OutputEncoding = new UTF8Encoding(false);
if (args.SequenceEqual(new[] { "--describe" })) { Console.WriteLine(JsonSerializer.Serialize(VmPlugin.Capabilities, PluginJsonContext.Default.PluginCapabilities)); return 0; }
if (!args.SequenceEqual(new[] { "--manager-plugin-protocol" })) return 2;
using var cancellation = new CancellationTokenSource(); Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
return await PluginWorkerHost.RunAsync(VmPlugin.Capabilities, VmPlugin.Registrations(), Console.In, Console.Out, cancellation.Token);
