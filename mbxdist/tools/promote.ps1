# promote.ps1 - stage a new MBXDist carrying the updated embedded catalog (the promote act).
#
# Prerequisite: you have already edited src\MBXDist.App\Embedded\catalog.json (+ manifests.*.json,
# typically from publish.ps1 fragments) to point at the versions you are recommending, and bumped
# AppInfo.Version.
#
# This script publishes MBXDist.App as a self-contained Windows x64 single file, launches only that
# file from an isolated directory, validates its embedded metadata against the actual feed, stages
# the unsigned EXE, then atomically updates latest.txt. It never signs, commits, pushes, or publishes.
#
# Example:  .\promote.ps1 -MbxdistVersion 0.2.0

param(
    [Parameter(Mandatory = $true)][string]$MbxdistVersion,
    [string]$FeedRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$RuntimeFrameworkVersion = "",
    [string]$OfflineRestoreSource = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$appProj = Join-Path $PSScriptRoot "..\src\MBXDist.App\MBXDist.App.csproj"
$appInfoPath = Join-Path $PSScriptRoot "..\src\MBXDist.App\AppInfo.cs"

Assert-MBXDistVersion -Value $MbxdistVersion
if ($RuntimeFrameworkVersion) { Assert-MBXDistVersion -Value $RuntimeFrameworkVersion }
$resolvedFeedRoot = Resolve-MBXDistAbsoluteRoot -Path $FeedRoot -Label 'feed root'
if (-not (Test-Path -LiteralPath $resolvedFeedRoot -PathType Container)) {
    throw "feed root does not exist: $resolvedFeedRoot"
}
[string]$resolvedOfflineSource = ""
if ($OfflineRestoreSource) {
    $resolvedOfflineSource = Resolve-MBXDistAbsoluteRoot -Path $OfflineRestoreSource -Label 'offline restore source'
    if (-not (Test-Path -LiteralPath $resolvedOfflineSource -PathType Container)) {
        throw "offline restore source does not exist: $resolvedOfflineSource"
    }
}

# Guard: AppInfo.Version must match the version being promoted.
$appVersion = Get-MBXDistAppVersion -AppInfoPath $appInfoPath
if ($appVersion -ne $MbxdistVersion) {
    throw "AppInfo.Version is '$appVersion', not '$MbxdistVersion'."
}
[void](Get-MBXDistPinnedThumbprints -AppInfoPath $appInfoPath)

$mbxdistRoot = Join-Path $resolvedFeedRoot "mbxdist"
$destDir = Join-Path $mbxdistRoot $MbxdistVersion
if (Test-Path -LiteralPath $destDir) {
    throw "MBXDist version destination already exists: $destDir"
}

$operationRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('mbxdist-promote-' + [Guid]::NewGuid().ToString('N'))
$publishDir = Join-Path $operationRoot "publish"
$isolatedDir = Join-Path $operationRoot "isolated"
$artifactsDir = Join-Path $operationRoot "artifacts"
$stageDir = $null
$destinationCreated = $false

try {
    [void][System.IO.Directory]::CreateDirectory($publishDir)
    [void][System.IO.Directory]::CreateDirectory($isolatedDir)

    $restoreArgs = @(
        'restore', $appProj,
        '-r', 'win-x64',
        '-p:MBXDistPublish=true',
        '--artifacts-path', $artifactsDir,
        '--nologo'
    )
    if ($RuntimeFrameworkVersion) { $restoreArgs += "-p:RuntimeFrameworkVersion=$RuntimeFrameworkVersion" }
    if ($resolvedOfflineSource) { $restoreArgs += @('--source', $resolvedOfflineSource) }
    $restoreOutput = & dotnet @restoreArgs 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "restore failed:`n$restoreOutput" }

    $publishArgs = @(
        'publish', $appProj,
        '-c', 'Release',
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:MBXDistPublish=true',
        '--artifacts-path', $artifactsDir,
        '--no-restore',
        '--nologo',
        '--output', $publishDir
    )
    if ($RuntimeFrameworkVersion) { $publishArgs += "-p:RuntimeFrameworkVersion=$RuntimeFrameworkVersion" }
    $publishOutput = & dotnet @publishArgs 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "publish failed:`n$publishOutput" }

    $publishedExe = Join-Path $publishDir "mbxdist.exe"
    if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
        throw "published executable not found at $publishedExe"
    }

    # Copy only the executable. Successful launch here proves no adjacent application files are needed.
    $isolatedExe = Join-Path $isolatedDir "mbxdist.exe"
    Copy-Item -LiteralPath $publishedExe -Destination $isolatedExe

    $versionOutput = (& $isolatedExe version --json 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "published executable version check failed: $versionOutput" }
    $reportedVersion = $null
    try { $reportedVersion = ($versionOutput | ConvertFrom-Json -ErrorAction Stop).version }
    catch {
        if ($versionOutput -eq "MBXDist $MbxdistVersion") { $reportedVersion = $MbxdistVersion }
    }
    if ($reportedVersion -ne $MbxdistVersion) {
        throw "published executable reported version '$reportedVersion', expected '$MbxdistVersion'."
    }

    $validationOutput = (& $isolatedExe validate-feed --feed-root $resolvedFeedRoot --json 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "embedded feed validation failed: $validationOutput" }
    try { $validation = $validationOutput | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "validate-feed did not return JSON: $validationOutput" }
    if ($validation.valid -ne $true) { throw "validate-feed did not report valid=true: $validationOutput" }

    [void][System.IO.Directory]::CreateDirectory($mbxdistRoot)
    if (Test-Path -LiteralPath $destDir) {
        throw "MBXDist version destination already exists: $destDir"
    }

    $stageDir = Join-Path $mbxdistRoot ('.mbxdist-stage-' + [Guid]::NewGuid().ToString('N'))
    [void][System.IO.Directory]::CreateDirectory($stageDir)
    Copy-Item -LiteralPath $isolatedExe -Destination (Join-Path $stageDir "mbxdist.exe")

    if (Test-Path -LiteralPath $destDir) {
        throw "MBXDist version destination already exists: $destDir"
    }
    [System.IO.Directory]::Move($stageDir, $destDir)
    $stageDir = $null
    $destinationCreated = $true

    $marker = Join-Path $mbxdistRoot "latest.txt"
    Write-MBXDistAtomicText -Path $marker -Value $MbxdistVersion
}
catch {
    if ($destinationCreated -and (Test-Path -LiteralPath $destDir)) {
        Remove-Item -LiteralPath $destDir -Recurse -Force
    }
    throw
}
finally {
    if ($null -ne $stageDir -and (Test-Path -LiteralPath $stageDir)) {
        Remove-Item -LiteralPath $stageDir -Recurse -Force
    }

    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $resolvedOperationRoot = [System.IO.Path]::GetFullPath($operationRoot)
    if ($resolvedOperationRoot.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedOperationRoot)) {
        Remove-Item -LiteralPath $resolvedOperationRoot -Recurse -Force
    }
}

$hash = (Get-FileHash -Path (Join-Path $destDir "mbxdist.exe") -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host ""
Write-Host ("staged unsigned  mbxdist\{0}\mbxdist.exe  pre-sign-sha256={1}" -f $MbxdistVersion, $hash)
Write-Host ("marker  mbxdist\latest.txt -> {0}" -f $MbxdistVersion)
Write-Host ""
Write-Host "SIGN THE STAGED EXE IN PLACE on the token BEFORE committing or publishing it:"
Write-Host ("  signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 `"{0}`"" -f (Join-Path $destDir "mbxdist.exe"))
Write-Host ""
Write-Host "Then verify the signature and review the staged EXE plus marker. This script does not commit or push."
