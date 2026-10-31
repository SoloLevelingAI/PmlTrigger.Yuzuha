namespace LaunchDesk.Core;

public sealed class ConnectionHealth
{
 private int failures;
 private bool hasBeenReady;
 public (ProbeResult Result,bool NotifyDisconnect) Observe(ProbeResult probe)
 {
  if(probe.Ready){hasBeenReady=true;failures=0;return(probe with{ConsecutiveFailures=0},false);}
  failures=Math.Min(failures+1,1000);
  return(probe with{ConsecutiveFailures=failures},hasBeenReady&&failures==3);
 }
}
