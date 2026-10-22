[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts/docfx'
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in 'ui-trace.json','ui-trace-metadata.json') {
    $receipt = Join-Path $output $name
    if (Test-Path -LiteralPath $receipt) { Remove-Item -LiteralPath $receipt -Force }
}
$snapshots = @()
Push-Location $root
try {
    dotnet build src/YuzuhaToolkit.Desktop -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Cannot build desktop trace dependencies.' }
    # MSBuild supplies evaluated compiler inputs, including linked files, references and WPF output.
    # Generator output is exported into artifacts, never added to production sources.
    foreach ($name in 'YuzuhaToolkit.Desktop','YuzuhaToolkit.Mcp','PlantHost.Rpc.Net10') {
        $generated = Join-Path $output "ui-generated/$name"
        if (Test-Path -LiteralPath $generated) {
            $resolved = [IO.Path]::GetFullPath($generated)
            if (-not $resolved.StartsWith([IO.Path]::GetFullPath($output) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe generator output path.' }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
        # Rebuild is required: an incremental Compile could skip generator export to a fresh directory.
        $raw = & dotnet msbuild "src/$name/$name.csproj" -target:Rebuild -verbosity:quiet -property:Configuration=Release -property:BuildProjectReferences=false -property:EmitCompilerGeneratedFiles=true "-property:CompilerGeneratedFilesOutputPath=$generated" -getItem:Compile,ReferencePath -getProperty:AssemblyName,DefineConstants,OutputType
        if ($LASTEXITCODE -ne 0) { throw "Cannot capture compiler inputs for $name : $raw" }
        $text = $raw -join "`n"
        $start = $text.IndexOf('{')
        if ($start -lt 0) { throw "No compiler snapshot for $name" }
        $snapshot = $text.Substring($start) | ConvertFrom-Json
        $sources = @($snapshot.Items.Compile | ForEach-Object { $_.FullPath })
        if (Test-Path -LiteralPath $generated) { $sources += @(Get-ChildItem -LiteralPath $generated -Filter '*.cs' -Recurse -File | ForEach-Object FullName) }
        $snapshots += @{ name=$snapshot.Properties.AssemblyName; sources=@($sources | Sort-Object -Unique); references=@($snapshot.Items.ReferencePath | ForEach-Object FullPath); defines=$snapshot.Properties.DefineConstants; outputType=$snapshot.Properties.OutputType }
    }
    @{projects=$snapshots; xaml=@(Get-ChildItem src/YuzuhaToolkit.Desktop -Filter '*.xaml' -File | ForEach-Object FullName)} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'ui-inputs.json') -Encoding utf8
    dotnet run --project tools/Documentation -c Release -- --ui-trace $root (Join-Path $output 'ui-inputs.json') (Join-Path $output 'ui-trace.json')
    if ($LASTEXITCODE -ne 0) { throw 'UI semantic trace failed.' }
    @{commit=(& git rev-parse HEAD).Trim(); dirty=[bool](& git status --porcelain); environment=[Runtime.InteropServices.RuntimeInformation]::OSDescription;
      finishedUtc=[DateTime]::UtcNow.ToString('o'); scope='Static compiler inputs; not runtime/AVEVA evidence'} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'ui-trace-metadata.json') -Encoding utf8
}
finally { Pop-Location }
