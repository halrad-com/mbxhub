param(
    [string]$ScratchRoot = (Join-Path ([System.IO.Path]::GetTempPath()) "mbxdist-packaging-tests")
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$publishScript = Join-Path $repoRoot "tools\publish.ps1"
$promoteScript = Join-Path $repoRoot "tools\promote.ps1"
. (Join-Path $repoRoot "tools\common.ps1")
$appVersion = Get-MBXDistAppVersion -AppInfoPath (Join-Path $repoRoot "src\MBXDist.App\AppInfo.cs")
$runRoot = Join-Path $ScratchRoot ([Guid]::NewGuid().ToString("N"))
$failures = [System.Collections.Generic.List[string]]::new()
$passed = 0

function Invoke-Publish {
    param([hashtable]$Parameters)

    try {
        $output = & $publishScript @Parameters *>&1 | Out-String
        return [pscustomobject]@{ ExitCode = 0; Output = $output }
    }
    catch {
        return [pscustomobject]@{ ExitCode = 1; Output = ($_ | Out-String) }
    }
}

function Invoke-Promote {
    param([hashtable]$Parameters)

    try {
        $output = & $promoteScript @Parameters *>&1 | Out-String
        return [pscustomobject]@{ ExitCode = 0; Output = $output }
    }
    catch {
        return [pscustomobject]@{ ExitCode = 1; Output = ($_ | Out-String) }
    }
}

function Test-Case {
    param([string]$Name, [scriptblock]$Body)

    try {
        & $Body
        $script:passed++
        Write-Host "PASS $Name"
    }
    catch {
        $script:failures.Add("${Name}: $($_.Exception.Message)")
        Write-Host "FAIL $Name"
        Write-Host "     $($_.Exception.Message)"
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function New-CaseRoot {
    param([string]$Name)
    $path = Join-Path $runRoot $Name
    New-Item -ItemType Directory -Path $path | Out-Null
    return $path
}

New-Item -ItemType Directory -Force -Path $runRoot | Out-Null
$offlineSource = Join-Path $runRoot "offline-nuget-source"
New-Item -ItemType Directory -Path $offlineSource | Out-Null

try {
    Test-Case "one resource is emitted as a JSON array" {
        $caseRoot = New-CaseRoot "one-file-array"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "payload.bin"
        [System.IO.File]::WriteAllBytes($source, [byte[]](1, 2, 3))

        $result = Invoke-Publish @{
            PackageDir = "sample"
            Version = "1.2.3"
            TargetRoot = "musicbee-plugins"
            Files = @($source)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        Assert-True ($result.ExitCode -eq 0) "publish failed: $($result.Output)"
        $fragment = Get-Content -Raw (Join-Path $feedRoot "sample\1.2.3\manifest-fragment.json")
        Assert-True ($fragment.TrimStart().StartsWith("[")) "manifest-fragment.json must have an array root"
        $resources = @($fragment | ConvertFrom-Json)
        Assert-True ($resources.Count -eq 1) "expected exactly one resource"
        Assert-True ($resources[0].size -eq 3) "resource size does not describe the staged bytes"
        Assert-True ($resources[0].sha256 -eq (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()) "resource hash does not describe the supplied bytes"
        Assert-True ($resources[0].authenticode -eq $false) "-NoAuthenticode was not recorded"
    }

    Test-Case "an existing package version is never overwritten" {
        $caseRoot = New-CaseRoot "overwrite"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "payload.bin"
        [System.IO.File]::WriteAllBytes($source, [byte[]](1, 2, 3))
        $parameters = @{
            PackageDir = "sample"
            Version = "1.2.3"
            TargetRoot = "musicbee-plugins"
            Files = @($source)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        $first = Invoke-Publish $parameters
        Assert-True ($first.ExitCode -eq 0) "initial publish failed: $($first.Output)"
        $published = Join-Path $feedRoot "sample\1.2.3\payload.bin"
        $originalHash = (Get-FileHash -LiteralPath $published -Algorithm SHA256).Hash
        [System.IO.File]::WriteAllBytes($source, [byte[]](9, 9, 9))

        $second = Invoke-Publish $parameters
        Assert-True ($second.ExitCode -ne 0) "second publish unexpectedly succeeded"
        Assert-True ((Get-FileHash -LiteralPath $published -Algorithm SHA256).Hash -eq $originalHash) "existing bytes were overwritten"
    }

    Test-Case "unsafe package paths are rejected" {
        $caseRoot = New-CaseRoot "unsafe-path"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "payload.bin"
        [System.IO.File]::WriteAllBytes($source, [byte[]](1, 2, 3))

        $result = Invoke-Publish @{
            PackageDir = "..\escape"
            Version = "1.2.3"
            TargetRoot = "musicbee-plugins"
            Files = @($source)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        Assert-True ($result.ExitCode -ne 0) "publish accepted a traversal package path"
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $caseRoot "escape"))) "publish wrote outside the feed root"
    }

    Test-Case "a failed publish leaves no partial version" {
        $caseRoot = New-CaseRoot "partial-cleanup"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "payload.bin"
        $missing = Join-Path $caseRoot "missing.bin"
        [System.IO.File]::WriteAllBytes($source, [byte[]](1, 2, 3))

        $result = Invoke-Publish @{
            PackageDir = "sample"
            Version = "1.2.3"
            TargetRoot = "musicbee-plugins"
            Files = @($source, $missing)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        Assert-True ($result.ExitCode -ne 0) "publish unexpectedly succeeded with a missing source"
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $feedRoot "sample\1.2.3"))) "partial destination remains after failure"
    }

    Test-Case "unsigned input requires an explicit opt-out" {
        $caseRoot = New-CaseRoot "unsigned"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "payload.bin"
        [System.IO.File]::WriteAllBytes($source, [byte[]](1, 2, 3))

        $result = Invoke-Publish @{
            PackageDir = "sample"
            Version = "1.2.3"
            TargetRoot = "musicbee-plugins"
            Files = @($source)
            FeedRoot = $feedRoot
        }

        Assert-True ($result.ExitCode -ne 0) "publish accepted unsigned input without -NoAuthenticode"
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $feedRoot "sample\1.2.3"))) "unsigned input left a destination"
    }

    Test-Case "invalid versions are rejected" {
        $caseRoot = New-CaseRoot "unsafe-version"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "payload.bin"
        [System.IO.File]::WriteAllBytes($source, [byte[]](1, 2, 3))

        $result = Invoke-Publish @{
            PackageDir = "sample"
            Version = "..\escape"
            TargetRoot = "musicbee-plugins"
            Files = @($source)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        Assert-True ($result.ExitCode -ne 0) "publish accepted an invalid version"
    }

    Test-Case "invalid target roots are rejected" {
        $caseRoot = New-CaseRoot "unsafe-target"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "payload.bin"
        [System.IO.File]::WriteAllBytes($source, [byte[]](1, 2, 3))

        $result = Invoke-Publish @{
            PackageDir = "sample"
            Version = "1.2.3"
            TargetRoot = "..\outside"
            Files = @($source)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        Assert-True ($result.ExitCode -ne 0) "publish accepted an invalid target-root name"
    }

    Test-Case "duplicate destination filenames are rejected" {
        $caseRoot = New-CaseRoot "duplicate-name"
        $feedRoot = Join-Path $caseRoot "feed"
        $firstDir = Join-Path $caseRoot "first"
        $secondDir = Join-Path $caseRoot "second"
        New-Item -ItemType Directory -Path $firstDir, $secondDir | Out-Null
        $first = Join-Path $firstDir "payload.bin"
        $second = Join-Path $secondDir "payload.bin"
        [System.IO.File]::WriteAllBytes($first, [byte[]](1, 2, 3))
        [System.IO.File]::WriteAllBytes($second, [byte[]](4, 5, 6))

        $result = Invoke-Publish @{
            PackageDir = "sample"
            Version = "1.2.3"
            TargetRoot = "musicbee-plugins"
            Files = @($first, $second)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        Assert-True ($result.ExitCode -ne 0) "publish accepted duplicate destination filenames"
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $feedRoot "sample\1.2.3"))) "duplicate input left a destination"
    }

    Test-Case "generated metadata filename is reserved case-insensitively" {
        $caseRoot = New-CaseRoot "reserved-output-name"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "MANIFEST-FRAGMENT.JSON"
        [System.IO.File]::WriteAllBytes($source, [byte[]](1, 2, 3))

        $result = Invoke-Publish @{
            PackageDir = "sample"
            Version = "1.2.3"
            TargetRoot = "musicbee-plugins"
            Files = @($source)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        Assert-True ($result.ExitCode -ne 0) "publish accepted its reserved generated metadata filename"
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $feedRoot "sample\1.2.3"))) "reserved filename input left a destination"
    }

    Test-Case "zero-byte resources are rejected" {
        $caseRoot = New-CaseRoot "empty-input"
        $feedRoot = Join-Path $caseRoot "feed"
        $source = Join-Path $caseRoot "empty.bin"
        [System.IO.File]::WriteAllBytes($source, [byte[]]@())

        $result = Invoke-Publish @{
            PackageDir = "sample"
            Version = "1.2.3"
            TargetRoot = "musicbee-plugins"
            Files = @($source)
            FeedRoot = $feedRoot
            NoAuthenticode = $true
        }

        Assert-True ($result.ExitCode -ne 0) "publish accepted a zero-byte resource"
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $feedRoot "sample\1.2.3"))) "zero-byte input left a destination"
    }

    Test-Case "promotion rejects placeholder embedded hashes before staging" {
        $caseRoot = New-CaseRoot "promote-placeholder"
        $feedRoot = Join-Path $caseRoot "feed"
        New-Item -ItemType Directory -Path $feedRoot | Out-Null

        $result = Invoke-Promote @{
            MbxdistVersion = $appVersion
            FeedRoot = $feedRoot
            RuntimeFrameworkVersion = "8.0.25"
            OfflineRestoreSource = $offlineSource
        }

        Assert-True ($result.ExitCode -ne 0) "promotion accepted placeholder embedded release metadata"
        Assert-True ($result.Output -match 'placeholder') "promotion failed for the wrong reason: $($result.Output)"
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $feedRoot "mbxdist\$appVersion"))) "promotion staged a version despite invalid metadata"
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $feedRoot "mbxdist\latest.txt"))) "promotion wrote the marker despite invalid metadata"
    }

    Test-Case "promotion refuses an existing version before publishing" {
        $caseRoot = New-CaseRoot "promote-overwrite"
        $feedRoot = Join-Path $caseRoot "feed"
        $existing = Join-Path $feedRoot "mbxdist\$appVersion"
        New-Item -ItemType Directory -Path $existing | Out-Null
        $sentinel = Join-Path $existing "sentinel.txt"
        [System.IO.File]::WriteAllText($sentinel, "original")

        $result = Invoke-Promote @{
            MbxdistVersion = $appVersion
            FeedRoot = $feedRoot
        }

        Assert-True ($result.ExitCode -ne 0) "promotion accepted an existing version directory"
        Assert-True ((Get-Content -LiteralPath $sentinel -Raw) -eq "original") "promotion changed the existing version"
    }
}
finally {
    $resolvedScratch = [System.IO.Path]::GetFullPath($ScratchRoot)
    $resolvedRun = [System.IO.Path]::GetFullPath($runRoot)
    if ($resolvedRun.StartsWith($resolvedScratch + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedRun)) {
        Remove-Item -LiteralPath $resolvedRun -Recurse -Force
    }
}

Write-Host ""
Write-Host ("{0} passed; {1} failed" -f $passed, $failures.Count)
if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Host " - $failure" }
    exit 1
}
