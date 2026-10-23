[CmdletBinding()]
param([Parameter(Mandatory)][string]$HostExecutable, [Parameter(Mandatory)][string]$Site,
      [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$HostExecutable = [IO.Path]::GetFullPath($HostExecutable)
$Site = [IO.Path]::GetFullPath($Site)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$probe.Start(); $port = $probe.LocalEndpoint.Port; $probe.Stop()
$address = "http://127.0.0.1:$port"
$process = Start-Process -FilePath $HostExecutable -ArgumentList @('--urls',$address,'--docs-root',('"'+$Site+'"'),'--docs-only','true') -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $OutputDirectory 'host.stdout.log') -RedirectStandardError (Join-Path $OutputDirectory 'host.stderr.log')
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(3)
try {
    $ready = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) { throw 'Read-only documentation host exited.' }
        try { $response=$client.GetAsync("$address/docs/index.html").GetAwaiter().GetResult(); if($response.IsSuccessStatusCode) { $ready=$true;break } } catch { }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw 'Documentation host did not become ready.' }
    $bytes=$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    $served=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    if ($served -ne (Get-FileHash -LiteralPath (Join-Path $Site 'index.html')).Hash) { throw 'Served document differs from packaged site.' }
    if ([Text.Encoding]::UTF8.GetString($bytes) -notmatch 'Phayton') { throw 'Phayton document brand is missing.' }
    if ([int]$client.GetAsync("$address/api/guides").GetAwaiter().GetResult().StatusCode -ne 404) { throw 'Read-only mode exposes development routes.' }
    if ([int]$client.PostAsync("$address/api/pml/preview",[Net.Http.StringContent]::new('{}')).GetAwaiter().GetResult().StatusCode -ne 403) { throw 'Write requests were accepted.' }
    $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get,"$address/docs/index.html")
    $request.Headers.Host='untrusted.example'
    if ([int]$client.SendAsync($request).GetAwaiter().GetResult().StatusCode -ne 403) { throw 'Unexpected Host header accepted.' }
    if ([int]$client.GetAsync("$address/docs/public/phayton-r2.css").GetAwaiter().GetResult().StatusCode -ne 200) { throw 'R2 stylesheet not served.' }
    @{passed=$true; scope='Actual packaged loopback documentation host; no AVEVA'; indexSha256=$served;
      readOnly=$true; noDevelopmentRoutes=$true; hostGuard=$true; finishedUtc=[DateTime]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'documentation-host.json') -Encoding utf8
    Write-Host 'PASS: packaged documentation, stylesheet, write refusal, Host guard and absence of development routes.'
}
finally { $client.Dispose(); if(-not $process.HasExited){Stop-Process -Id $process.Id}; $process.Dispose() }
