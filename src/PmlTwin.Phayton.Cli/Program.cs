using Microsoft.Extensions.Logging.Abstractions;
using PmlTwin.Phayton;
using YuzuhaToolkit.Mcp;

YuzuhaRpcBridge CreateBridge() => new(NullLogger<YuzuhaRpcBridge>.Instance, new AvevaSessionDiscovery());
using var bridge = CreateBridge();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var executor = YuzuhaOperationCatalog.Create(bridge, new PmlFunctionTrustStore(), CreateBridge);
return await OperationCli.RunAsync(args, executor, Console.Out, cancellation.Token);
