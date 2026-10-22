[CmdletBinding()]
param(
    [string]$LegacySdkDirectory = $env:YUZUHA_TRACE_LEGACY_SDK,
    [string]$E3dSdkDirectory = $env:YUZUHA_TRACE_E3D_SDK
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts/docfx'
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in 'ui-trace.json','ui-trace-metadata.json') {
    $receipt = Join-Path $output $name
    if (Test-Path -LiteralPath $receipt) { Remove-Item -LiteralPath $receipt -Force }
}
$snapshots = @()
$xaml = @()
$omittedProjects = @()
Push-Location $root
try {
    dotnet build src/YuzuhaToolkit.Desktop -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Cannot build desktop trace dependencies.' }
    # MSBuild supplies evaluated compiler inputs, including linked files, references and WPF output.
    # Generator output is exported into artifacts, never added to production sources.
    $pending = [Collections.Generic.Queue[string]]::new()
    $pending.Enqueue((Join-Path $root 'src/YuzuhaToolkit.Desktop/YuzuhaToolkit.Desktop.csproj'))
    foreach ($hostName in 'Net35','Net48') {
        $sdk = if ($hostName -eq 'Net35') { $LegacySdkDirectory } else { $E3dSdkDirectory }
        if ($sdk) {
            if (-not (Test-Path -LiteralPath (Join-Path $sdk 'PMLNet.dll'))) { throw "Missing PMLNet.dll for $hostName" }
            $pending.Enqueue((Join-Path $root "src/YuzuhaToolkit.PmlHost.$hostName/YuzuhaToolkit.PmlHost.$hostName.csproj"))
        } else { $omittedProjects += "YuzuhaToolkit.PmlHost.$hostName : no local AVEVA SDK configured; not analyzed" }
    }
    if ($E3dSdkDirectory) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $frameworkMsbuild = & $vswhere -latest -prerelease -products '*' -requires Microsoft.Component.MSBuild -find MSBuild/Current/Bin/MSBuild.exe | Select-Object -First 1
        if (-not $frameworkMsbuild) { throw 'NET48 trace requires Visual Studio MSBuild.' }
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    while ($pending.Count -gt 0) {
        $project = [IO.Path]::GetFullPath($pending.Dequeue())
        if (-not $seen.Add($project)) { continue }
        if (-not $project.StartsWith($root + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Trace project reference escapes repository.' }
        $name = [IO.Path]::GetFileNameWithoutExtension($project)
        $generated = Join-Path $output "ui-generated/$name"
        if (Test-Path -LiteralPath $generated) {
            $resolved = [IO.Path]::GetFullPath($generated)
            if (-not $resolved.StartsWith([IO.Path]::GetFullPath($output) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe generator output path.' }
            for ($ancestor = (Get-Item -LiteralPath $resolved).Parent; $ancestor -and $ancestor.FullName.Length -ge $output.Length; $ancestor = $ancestor.Parent) {
                if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Generator output parent cannot be a junction.' }
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
        # Rebuild is required: an incremental Compile could skip generator export to a fresh directory.
        $buildArguments = @($project, '-target:Rebuild', '-verbosity:quiet', '-property:Configuration=Release', '-property:BuildProjectReferences=false', '-property:EmitCompilerGeneratedFiles=true', "-property:CompilerGeneratedFilesOutputPath=$generated", '-getItem:Compile,ReferencePath,ProjectReference,Page,ApplicationDefinition', '-getProperty:AssemblyName,DefineConstants,OutputType')
        if ($name -eq 'YuzuhaToolkit.PmlHost.Net35') { $buildArguments += @('-restore', "-property:AvevaInstallDir=$LegacySdkDirectory", '-property:AvevaProfile=AM') }
        if ($name -eq 'YuzuhaToolkit.PmlHost.Net48') {
            $buildArguments += @("-property:AvevaInstallDir=$E3dSdkDirectory", '-property:AvevaProfile=E3D2.1')
            $raw = & $frameworkMsbuild @buildArguments
        } else { $raw = & dotnet msbuild @buildArguments }
        if ($LASTEXITCODE -ne 0) { throw "Cannot capture compiler inputs for $name : $raw" }
        $text = $raw -join "`n"
        $start = $text.IndexOf('{')
        if ($start -lt 0) { throw "No compiler snapshot for $name" }
        $snapshot = $text.Substring($start) | ConvertFrom-Json
        $sources = @($snapshot.Items.Compile | ForEach-Object { $_.FullPath })
        if (Test-Path -LiteralPath $generated) { $sources += @(Get-ChildItem -LiteralPath $generated -Filter '*.cs' -Recurse -File | ForEach-Object FullName) }
        $snapshots += @{ name=$snapshot.Properties.AssemblyName; sources=@($sources | Sort-Object -Unique); references=@($snapshot.Items.ReferencePath | ForEach-Object FullPath); defines=$snapshot.Properties.DefineConstants; outputType=$snapshot.Properties.OutputType }
        foreach ($reference in $snapshot.Items.ProjectReference) { $pending.Enqueue($reference.FullPath) }
        $xaml += @($snapshot.Items.Page + $snapshot.Items.ApplicationDefinition | Where-Object { $_ } | ForEach-Object FullPath)
    }
    @{projects=$snapshots; xaml=@($xaml | Sort-Object -Unique); omittedProjects=$omittedProjects} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'ui-inputs.json') -Encoding utf8
    dotnet run --project tools/Documentation -c Release -- --ui-trace $root (Join-Path $output 'ui-inputs.json') (Join-Path $output 'ui-trace.json')
    if ($LASTEXITCODE -ne 0) { throw 'UI semantic trace failed.' }
    @{commit=(& git rev-parse HEAD).Trim(); dirty=[bool](& git status --porcelain); environment=[Runtime.InteropServices.RuntimeInformation]::OSDescription;
      finishedUtc=[DateTime]::UtcNow.ToString('o'); scope='Static compiler inputs and evidenced RPC correspondence; not runtime/AVEVA evidence'; omittedProjects=$omittedProjects} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'ui-trace-metadata.json') -Encoding utf8
}
finally { Pop-Location }
