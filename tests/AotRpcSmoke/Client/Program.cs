using PlantHost.Rpc;
using YuzuhaToolkit.Mcp;
using var client = RpcClient.Connect(args[0], 3000);
var service = new AotPmlCommandService(client);
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
using var heartbeat = client.StartHeartbeat(new RpcHeartbeatOptions { InstanceId = "aot-smoke", Interval = TimeSpan.FromMilliseconds(100) });
var identity = await service.GetHostIdentityAsync(new(), timeout.Token);
if (identity.ProcessId <= 0 || identity.Model != "Design" || identity.PipeName != args[0]) throw new Exception("Identity mismatch");
var result = await service.RunPmlCommandAsync(new() {
    PmlCommand = "unicode 测试 'quoted'", ReturnList = true, GlobalVar = "Smoke", DeleteGlobalVar = true
}, timeout.Token);
if (!result.Success || result.Code != "SMOKE" || result.PmlCommand != "unicode 测试 'quoted'" ||
    result.ResultList?.Length != 2 || result.ResultList[0] != "Smoke" || result.ResultList[1] != "True" ||
    result.ServerTimeUtc.Year < 2026) throw new Exception("Contract roundtrip mismatch");
await Task.Delay(250, timeout.Token);
Console.WriteLine("PASS: published AOT adapter to Framework Host: identity, command, Unicode, booleans, array, date, heartbeat.");
