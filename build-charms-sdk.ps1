#Requires -Version 7.0
<#!
Refresh shared SDK tables and render the partner Markdown with PowerShell's bundled
Markdown renderer. -Check validates without writing. No network or deployment.
!#>
[CmdletBinding()]
param([string]$InternalRepo, [switch]$Check)
$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
$sdkRoot = Join-Path $PSScriptRoot 'MBXHUB-SDK'
$tables = Get-Content -LiteralPath (Join-Path $sdkRoot 'charms-sdk-contract.json') -Raw | ConvertFrom-Json -AsHashtable
$publicPath = Join-Path $sdkRoot 'charms-sdk.md'
$paths = @($publicPath)
if ($InternalRepo) {
    $InternalRepo = (Resolve-Path -LiteralPath $InternalRepo).Path
    $paths += Join-Path $InternalRepo 'docs/superpowers/specs/2026-09-04-charms-sdk-spec.md'
}
$pending = [ordered]@{}
foreach ($path in $paths) {
    $text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    foreach ($key in ($tables.Keys | Sort-Object)) {
        $table = $tables[$key]
        $lines = @('| ' + ($table.columns -join ' | ') + ' |')
        $lines += '| ' + (($table.columns | ForEach-Object { '---' }) -join ' | ') + ' |'
        foreach ($row in $table.rows) {
            if ($row.Count -ne $table.columns.Count) { throw "Invalid column count: $key" }
            $lines += '| ' + ($row -join ' | ') + ' |'
        }
        $start = "<!-- sdk:${key}:start -->"
        $end = "<!-- sdk:${key}:end -->"
        $pattern = '(?s)' + [regex]::Escape($start) + '.*?' + [regex]::Escape($end)
        if ([regex]::Matches($text, $pattern).Count -ne 1) { throw "Expected one $key block in $path" }
        $replacement = $start + "`n" + ($lines -join "`n") + "`n" + $end
        $text = [regex]::Replace($text, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $replacement })
    }
    if ($text.Contains([char]0xfffd)) { throw "Replacement character in $path" }
    foreach ($match in [regex]::Matches($text, '(?ms)^```json\s*\n(.*?)^```')) {
        $null = $match.Groups[1].Value | ConvertFrom-Json
    }
    $pending[$path] = $text
}
# Resolve repository-relative links for HTML served outside the repository.
# Root-relative website links and in-page anchors stay unchanged.
$publicText = $pending[$publicPath]
foreach ($match in [regex]::Matches($publicText, '\]\(([^\s)]+)\)')) {
    $target = $match.Groups[1].Value
    if ($target -match '^(?:[a-z]+:|/|#)') { continue }
    $local = Join-Path $sdkRoot ($target.Split('#')[0])
    if (!(Test-Path -LiteralPath $local)) { throw "Broken local link: $target" }
}
$body = (ConvertFrom-Markdown -InputObject $publicText).Html
$body = [regex]::Replace($body, 'href="([^"#/:][^":]*)"', [Text.RegularExpressions.MatchEvaluator]{
    param($m)
    $url = [Uri]::new([Uri]'https://github.com/halrad-com/mbxhub/blob/main/MBXHUB-SDK/', $m.Groups[1].Value)
    'href="' + [Net.WebUtility]::HtmlEncode($url.AbsoluteUri) + '"'
})
$html = @"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Charms SDK — MBXHub</title>
<!-- Generated from MBXHUB-SDK/charms-sdk.md by build-charms-sdk.ps1. Do not edit. -->
<style>
:root { color-scheme: light dark; font: 17px/1.65 system-ui, sans-serif; }
body { margin: 0 auto; max-width: 76rem; padding: 2rem clamp(1rem,4vw,4rem); }
main { min-width: 0; } h1,h2,h3 { line-height: 1.25; } h2 { margin-top: 2.5rem; }
a { color: light-dark(#1559a6,#87beff); } p,li,td { overflow-wrap: anywhere; }
pre { overflow: auto; padding: 1rem; background: light-dark(#f1f4f8,#202733); }
code { font-size: .88em; } table { border-collapse: collapse; display: block; overflow-x: auto; margin: 1.5rem 0; }
td,th { border: 1px solid #8893a2; padding: .6rem .8rem; text-align: left; min-width: 8rem; }
th { background: light-dark(#e9eef5,#202733); } blockquote { border-left: 3px solid #8893a2; margin-left: 0; padding-left: 1rem; }
@media print { body { max-width: none; font-size: 11pt; } table { display: table; } pre { white-space: pre-wrap; } }
</style>
</head>
<body><main>
$body
</main></body></html>
"@
$html = $html.Replace("`r`n", "`n").TrimEnd() + "`n"
$pending[(Join-Path $sdkRoot 'charms-sdk.html')] = $html
if ($InternalRepo) {
    $pending[(Join-Path $InternalRepo 'deploy/mbxhub.com/downloads/docs/charms-sdk.html')] = $html
}
$drift = @()
foreach ($entry in $pending.GetEnumerator()) {
    $existing = if (Test-Path -LiteralPath $entry.Key) { [IO.File]::ReadAllText($entry.Key).Replace("`r`n", "`n") } else { $null }
    if ($existing -cne $entry.Value) {
        if ($Check) { $drift += $entry.Key; continue }
        $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($entry.Key))
        [IO.File]::WriteAllText($entry.Key, $entry.Value, $utf8)
        Write-Host "Updated $($entry.Key)"
    }
}
if ($drift.Count) { throw "Generated documentation is stale: $($drift -join ', ')" }
Write-Host 'SDK tables, JSON examples, relative links and HTML are consistent.'
