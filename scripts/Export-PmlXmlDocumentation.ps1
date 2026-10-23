[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
$resolvedRoot = [IO.Path]::GetFullPath($Root)

# PML uses -- comments. Parse XML with the platform reader, never as HTML or executable PML.
function Convert-DocumentationText([System.Xml.XmlNode]$Node) {
    $parts = foreach ($child in $Node.ChildNodes) {
        if ($child -is [System.Xml.XmlText] -or $child -is [System.Xml.XmlWhitespace] -or $child -is [System.Xml.XmlSignificantWhitespace]) {
            [Net.WebUtility]::HtmlEncode(($child.Value -replace '\s+', ' '))
        } elseif ($child.LocalName -eq 'c') {
            '<code>' + [Net.WebUtility]::HtmlEncode($child.InnerText.Trim()) + '</code>'
        } elseif ($child.LocalName -eq 'para') {
            "`n`n" + (Convert-DocumentationText $child) + "`n`n"
        } elseif ($child.LocalName -eq 'code') {
            $language = $child.GetAttribute('language')
            if ($language -notmatch '^[A-Za-z0-9_-]*$') { throw 'Invalid example language.' }
            $lines = @($child.InnerText.Trim("`r", "`n") -split '\r?\n')
            $indent = ($lines | Where-Object { $_.Trim() } | ForEach-Object { ([regex]::Match($_, '^\s*')).Length } | Measure-Object -Minimum).Minimum
            $code = ($lines | ForEach-Object { if ($_.Length -ge $indent) { $_.Substring($indent) } else { '' } }) -join "`n"
            # Raw markup and Markdown fences in examples remain literal.
            '<pre><code class="language-' + $language + '">' + [Net.WebUtility]::HtmlEncode($code) + "</code></pre>`n"
        } else { throw "Unsupported XML documentation element: $($child.LocalName)" }
    }
    return ($parts -join '').Trim()
}

function Read-Documentation([string]$Xml, [string]$Declaration) {
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create([IO.StringReader]::new('<doc>' + $Xml + '</doc>'), $settings)
    $document = [Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try { $document.Load($reader) } finally { $reader.Dispose() }
    $doc = $document.DocumentElement
    foreach ($child in $doc.ChildNodes | Where-Object { $_ -is [Xml.XmlElement] }) {
        if ($child.LocalName -notin @('summary','param','returns','value','remarks','example')) { throw "Unsupported XML documentation tag: $($child.LocalName)" }
    }
    foreach ($tag in @('summary','returns','value','remarks','example')) {
        if (@($doc.SelectNodes($tag)).Count -gt 1) { throw "Duplicate $tag." }
    }
    if (@($doc.SelectNodes('summary')).Count -ne 1 -or !$doc.SelectSingleNode('summary').InnerText.Trim()) { throw 'A nonempty summary is required.' }
    $parameters = @([regex]::Matches($Declaration, '!(\w+)\s+is\s+(\w+)', 'IgnoreCase'))
    $documented = @($doc.SelectNodes('param'))
    if ($parameters.Count -ne $documented.Count) { throw 'XML parameter count does not match the declaration.' }
    for ($i = 0; $i -lt $parameters.Count; $i++) {
        if ($parameters[$i].Groups[1].Value -cne $documented[$i].GetAttribute('name') -or !$documented[$i].InnerText.Trim()) {
            throw 'XML parameter names/order/descriptions do not match the declaration.'
        }
    }
    if ($Declaration -match '^define\s+(method|function)\b') {
        $returnsValue = $Declaration -match '\)\s+is\s+\w+\s*$'
        if ($returnsValue -ne (@($doc.SelectNodes('returns')).Count -eq 1)) { throw 'XML returns does not match the declaration.' }
    }
    $body = "## ``$Declaration```n`n" + (Convert-DocumentationText $doc.SelectSingleNode('summary')) + "`n`n"
    if ($parameters.Count) {
        $body += "| 参数 | 类型 | 说明 |`n| --- | --- | --- |`n"
        for ($i = 0; $i -lt $parameters.Count; $i++) {
            $description = (Convert-DocumentationText $documented[$i]).Replace('|', '&#124;').Replace("`n", ' ')
            $body += '| `' + $parameters[$i].Groups[1].Value + '` | `' + $parameters[$i].Groups[2].Value + '` | ' + $description + " |`n"
        }
        $body += "`n"
    }
    foreach ($tag in @('returns','value','remarks','example')) {
        $node = $doc.SelectSingleNode($tag)
        if ($null -ne $node) {
            if (!$node.InnerText.Trim()) { throw "Empty $tag." }
            $label = @{ returns='返回'; value='取值'; remarks='说明'; example='示例' }[$tag]
            $body += "**${label}：**`n`n" + (Convert-DocumentationText $node) + "`n`n"
        }
    }
    return $body
}

$result = [ordered]@{}
foreach ($directory in @('PMLLIB','PMLUI','tests/ai-pml')) {
    $sourceDirectory = Join-Path $resolvedRoot $directory
    if (!(Test-Path -LiteralPath $sourceDirectory)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File | Where-Object { $_.Name -match '\.pml(?:obj|fnc|frm|cmd)$' }) {
        $relative = [IO.Path]::GetRelativePath($resolvedRoot, $file.FullName).Replace('\', '/')
        $pending = [Collections.Generic.List[string]]::new()
        $blocks = [Collections.Generic.List[string]]::new()
        $lineNumber = 0
        foreach ($line in [IO.File]::ReadAllLines($file.FullName)) {
            $lineNumber++
            if ($line -match '^\s*--\s?(.*)$') {
                $comment = $Matches[1]
                if ($pending.Count -or $comment -match '^\s*<summary(?:\s|>)') { $pending.Add($comment) }
                continue
            }
            if (!$line.Trim()) { continue }
            if ($pending.Count) {
                try {
                    if ($line -notmatch '^\s*(?:define\s+(?:object|method|function)\b|member\s+\.)') { throw 'XML comments must precede a declaration on one line.' }
                    $blocks.Add((Read-Documentation ($pending -join "`n") $line.Trim()))
                } catch { throw "${relative}:${lineNumber}: $($_.Exception.Message)" }
                $pending.Clear()
            }
        }
        if ($pending.Count) { throw "${relative}: XML documentation has no declaration." }
        if ($blocks.Count) { $result[$relative] = @{ count=$blocks.Count; markdown=($blocks -join "`n") } }
    }
}
$outputPath = [IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputPath)) | Out-Null
[IO.File]::WriteAllText($outputPath, (ConvertTo-Json -InputObject $result -Depth 6), [Text.UTF8Encoding]::new($false))
