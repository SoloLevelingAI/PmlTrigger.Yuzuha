using System.Diagnostics;
using System.Net.Sockets;

namespace LaunchDesk.Core;

public static class LicenseMonitor
{
 public static async Task<ProbeResult> CheckAsync(LicenseEntry entry, Func<string, bool> localService, CancellationToken token = default)
 {
  var watch = Stopwatch.StartNew();
  var rmsTask=entry.RmsCheck==RmsCheckKind.LocalService?Task.FromResult(localService(entry.LocalServiceName)):TcpAsync(entry.Host,entry.RmsPort,token);
  var rulesTask=entry.RequireRules?TcpAsync(entry.Host,entry.RulesPort,token):Task.FromResult(true);
  await Task.WhenAll(rmsTask,rulesTask);bool rms=rmsTask.Result,rules=rulesTask.Result;
  return new ProbeResult(entry.Id, rms, rules,
   entry.RmsCheck == RmsCheckKind.LocalService ? (rms ? "本机 RMS 服务运行中" : "本机 RMS 服务未运行") : $"RMS TCP {entry.RmsPort}：{(rms ? "可达" : "不可达")}",
   entry.RequireRules ? $"RULES TCP {entry.RulesPort}：{(rules ? "可达" : "不可达")}" : "此连接不要求 RULES", watch.ElapsedMilliseconds, DateTimeOffset.Now);
 }
 public static Task<ProbeResult> CheckWithRetryAsync(LicenseEntry entry,Func<string,bool> localService,CancellationToken token=default)=>RetryAsync(ct=>CheckAsync(entry,localService,ct),3,TimeSpan.FromMilliseconds(350),token);
 public static async Task<ProbeResult> RetryAsync(Func<CancellationToken,Task<ProbeResult>> probe,int maxAttempts,TimeSpan retryDelay,CancellationToken token=default)
 {
  if(maxAttempts is <1 or >5||retryDelay<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(maxAttempts));
  var watch=Stopwatch.StartNew();ProbeResult? result=null;
  for(var attempt=1;attempt<=maxAttempts;attempt++){
   token.ThrowIfCancellationRequested();result=await probe(token);
   if(result.Ready||attempt==maxAttempts)return result with{Attempts=attempt,Milliseconds=watch.ElapsedMilliseconds};
   await Task.Delay(retryDelay,token);
  }
  return result!;
 }
 public static async Task<bool> TcpAsync(string host, int port, CancellationToken token = default)
 {
  try {
   using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
   using var client = new TcpClient(); await client.ConnectAsync(host, port, timeout.Token); return client.Connected;
  } catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
  catch (Exception e) when (e is SocketException or OperationCanceledException or ArgumentException) { return false; }
 }
}
