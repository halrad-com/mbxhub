# publish.ps1 - stage a package version into the feed layout (spec: publish != promote).
#
# Copies built artifacts into <FeedRoot>\<PackageDir>\<Version>\, computes sha256 + size for
# each, and writes manifest-fragment.json next to them - the resources array to paste into the
# MBXDist App embedded manifest (src\MBXDist.App\Embedded\manifests.<package-id>.json).
#
# It does NOT sign (Authenticode signing happens on the hardware token - the exact command is
# printed), does NOT touch the embedded catalog (that is promote.ps1), and does NOT git-push.
#
# Example:
#   .\publish.ps1 -PackageDir core -Version 0.5.4.6 -TargetRoot musicbee-plugins `
#       -Files <path-to-built>\mb_MBXHub.dll

param(
    [Parameter(Mandatory = $true)][string]$PackageDir,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string[]]$Files,
    [string]$TargetRoot = "",
    [string]$FeedRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$destDir = Join-Path (Join-Path $FeedRoot $PackageDir) $Version
New-Item -ItemType Directory -Force -Path $destDir | Out-Null

$resources = @()
foreach ($file in $Files) {
    $src = Resolve-Path $file
    $name = Split-Path $src -Leaf
    $dest = Join-Path $destDir $name
    Copy-Item -Path $src -Destination $dest -Force

    $hash = (Get-FileHash -Path $dest -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item $dest).Length

    $resources += [ordered]@{
        filename = $name
        url      = "$PackageDir/$Version/$name"
        sha256   = $hash
        size     = $size
        target   = [ordered]@{ root = $TargetRoot; path = $name }
        locked   = $false
    }
    Write-Host ("staged  {0}  sha256={1}  size={2}" -f $name, $hash, $size)
}

$fragmentPath = Join-Path $destDir "manifest-fragment.json"
($resources | ConvertTo-Json -Depth 5) | Set-Content -Path $fragmentPath -Encoding UTF8
Write-Host ""
Write-Host "wrote $fragmentPath"
Write-Host "paste its resources into src\MBXDist.App\Embedded\manifests.<package-id>.json (set target/locked as needed)"
Write-Host ""
Write-Host "SIGN each binary on the token BEFORE committing:"
foreach ($r in $resources) {
    Write-Host ("  signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 `"{0}`"" -f (Join-Path $destDir $r.filename))
}
Write-Host ""
Write-Host "publish does NOT change the recommendation - run promote.ps1 to repoint the embedded catalog."
