# promote.ps1 - ship a new MBXDist carrying the updated embedded catalog (spec: the promote act).
#
# Prerequisite: you have already edited src\MBXDist.App\Embedded\catalog.json (+ manifests.*.json,
# typically from publish.ps1 fragments) to point at the versions you are recommending, and bumped
# AppInfo.Version.
#
# This script: builds MBXDist.App Release, copies the exe into <FeedRoot>\mbxdist\<version>\,
# and writes <FeedRoot>\mbxdist\latest.txt. It does NOT sign (token; command printed) and does
# NOT git-commit/push - review, sign, then commit yourself.
#
# Example:  .\promote.ps1 -MbxdistVersion 0.2.0

param(
    [Parameter(Mandatory = $true)][string]$MbxdistVersion,
    [string]$FeedRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$appProj = Join-Path $PSScriptRoot "..\src\MBXDist.App\MBXDist.App.csproj"

# Guard: AppInfo.Version must match the version being promoted.
$appInfo = Get-Content (Join-Path $PSScriptRoot "..\src\MBXDist.App\AppInfo.cs") -Raw
if ($appInfo -notmatch [regex]::Escape("Version = `"$MbxdistVersion`"")) {
    throw "AppInfo.Version does not equal $MbxdistVersion - bump it first (the self-update compare depends on it)."
}
if ($appInfo -match "0000000000000000000000000000000000000000") {
    Write-Warning "PinnedThumbprints is still the PLACEHOLDER - clients will refuse everything. Bake the real token cert thumbprint before shipping."
}

dotnet build $appProj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$exe = Join-Path $PSScriptRoot "..\src\MBXDist.App\bin\Release\net8.0-windows\mbxdist.exe"
if (-not (Test-Path $exe)) { throw "built exe not found at $exe" }

$destDir = Join-Path (Join-Path $FeedRoot "mbxdist") $MbxdistVersion
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
Copy-Item -Path $exe -Destination (Join-Path $destDir "mbxdist.exe") -Force

$marker = Join-Path (Join-Path $FeedRoot "mbxdist") "latest.txt"
Set-Content -Path $marker -Value $MbxdistVersion -Encoding ASCII -NoNewline

$hash = (Get-FileHash -Path (Join-Path $destDir "mbxdist.exe") -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host ""
Write-Host ("staged  mbxdist\{0}\mbxdist.exe  sha256={1}" -f $MbxdistVersion, $hash)
Write-Host ("marker  mbxdist\latest.txt -> {0}" -f $MbxdistVersion)
Write-Host ""
Write-Host "SIGN on the token BEFORE committing (clients verify signature + pinned thumbprint):"
Write-Host ("  signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 `"{0}`"" -f (Join-Path $destDir "mbxdist.exe"))
Write-Host ""
Write-Host "then review + git add/commit yourself. Clients move when the signed exe + marker are pushed."
