# publish.ps1 - stage one immutable package version into the feed layout.
#
# Authenticode resources must be signed before this script runs so the manifest hashes the final
# bytes. Intentionally unsigned third-party resources require -NoAuthenticode; the production
# client then accepts them by the SHA-256 stored in its signed embedded manifest.
#
# Example:
#   .\publish.ps1 -PackageDir core -Version 0.5.4.6 -TargetRoot musicbee-plugins `
#       -Files <path-to-built>\mb_MBXHub.dll

param(
    [Parameter(Mandatory = $true)][string]$PackageDir,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string[]]$Files,
    [Parameter(Mandatory = $true)][string]$TargetRoot,
    [switch]$NoAuthenticode,
    [string]$FeedRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

Assert-MBXDistName -Value $PackageDir -Label 'package directory'
Assert-MBXDistVersion -Value $Version
Assert-MBXDistName -Value $TargetRoot -Label 'target root'
$resolvedFeedRoot = Resolve-MBXDistAbsoluteRoot -Path $FeedRoot -Label 'feed root'
if ($Files.Count -eq 0) { throw 'at least one input file is required.' }

$inputs = [System.Collections.Generic.List[object]]::new()
$names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($file in $Files) {
    $input = Resolve-MBXDistInputFile -Path $file
    if ([string]::Equals($input.Name, 'manifest-fragment.json', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "input filename '$($input.Name)' is reserved for generated package metadata."
    }
    if ((Get-Item -LiteralPath $input.Path).Length -le 0) {
        throw "input '$($input.Path)' is empty; feed resources must contain at least one byte."
    }
    if (-not $names.Add($input.Name)) {
        throw "multiple inputs resolve to destination filename '$($input.Name)'."
    }
    $inputs.Add($input)
}

if (-not $NoAuthenticode) {
    $appInfoPath = Join-Path $PSScriptRoot "..\src\MBXDist.App\AppInfo.cs"
    $pins = @(Get-MBXDistPinnedThumbprints -AppInfoPath $appInfoPath)
    foreach ($input in $inputs) {
        [void](Assert-MBXDistAuthenticodeSigner -Path $input.Path -PinnedThumbprints $pins)
    }
}

$packageRoot = Join-Path $resolvedFeedRoot $PackageDir
$destDir = Join-Path $packageRoot $Version
if (Test-Path -LiteralPath $destDir) {
    throw "package version destination already exists: $destDir"
}

$stageDir = Join-Path $packageRoot ('.mbxdist-stage-' + [Guid]::NewGuid().ToString('N'))
$resources = [System.Collections.Generic.List[object]]::new()
$moved = $false

try {
    [void][System.IO.Directory]::CreateDirectory($packageRoot)
    if (Test-Path -LiteralPath $destDir) {
        throw "package version destination already exists: $destDir"
    }
    [void][System.IO.Directory]::CreateDirectory($stageDir)

    foreach ($input in $inputs) {
        $dest = Join-Path $stageDir $input.Name
        Copy-Item -LiteralPath $input.Path -Destination $dest

        $hash = (Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash.ToLowerInvariant()
        $size = (Get-Item -LiteralPath $dest).Length

        $resources.Add([ordered]@{
            filename     = $input.Name
            url          = "$PackageDir/$Version/$($input.Name)"
            sha256       = $hash
            size         = $size
            target       = [ordered]@{ root = $TargetRoot; path = $input.Name }
            locked       = $false
            authenticode = (-not $NoAuthenticode)
        })
        Write-Host ("prepared  {0}  sha256={1}  size={2}" -f $input.Name, $hash, $size)
    }

    $fragmentPath = Join-Path $stageDir "manifest-fragment.json"
    $resourceArray = [object[]]$resources.ToArray()
    $json = ConvertTo-Json -InputObject $resourceArray -Depth 5
    [System.IO.File]::WriteAllText(
        $fragmentPath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))

    if (Test-Path -LiteralPath $destDir) {
        throw "package version destination already exists: $destDir"
    }
    [System.IO.Directory]::Move($stageDir, $destDir)
    $moved = $true
}
finally {
    if (-not $moved -and (Test-Path -LiteralPath $stageDir)) {
        Remove-Item -LiteralPath $stageDir -Recurse -Force
    }
}

$fragmentPath = Join-Path $destDir "manifest-fragment.json"
Write-Host ""
Write-Host "wrote $fragmentPath"
Write-Host "paste its resources into src\MBXDist.App\Embedded\manifests.<package-id>.json (set target/locked as needed)"
Write-Host ""
if ($NoAuthenticode) {
    Write-Host "unsigned resources (authenticode=false): the client verifies the staged SHA-256 bytes."
} else {
    Write-Host "Authenticode signer certificate matched an AppInfo SHA-256 pin for every staged resource."
    Write-Host "The client performs Authenticode trust verification when installing."
}
Write-Host ""
Write-Host "publish does NOT change the recommendation - run promote.ps1 to repoint the embedded catalog."
