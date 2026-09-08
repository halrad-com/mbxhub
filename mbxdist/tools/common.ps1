Set-StrictMode -Version Latest

function Assert-MBXDistName {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if ($Value -notmatch '\A[A-Za-z0-9][A-Za-z0-9._-]*\z' -or $Value.EndsWith('.')) {
        throw "$Label '$Value' is not a safe name."
    }

    $stem = $Value.Split('.')[0].ToUpperInvariant()
    if ($stem -in @('CON', 'PRN', 'AUX', 'NUL') -or $stem -match '\A(?:COM|LPT)[1-9]\z') {
        throw "$Label '$Value' is a reserved Windows name."
    }
}

function Assert-MBXDistVersion {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ($Value -notmatch '\A[0-9]+(?:\.[0-9]+)*\z') {
        throw "version '$Value' must contain dotted numeric segments."
    }

    foreach ($segment in $Value.Split('.')) {
        $parsed = 0
        if (-not [int]::TryParse(
            $segment,
            [System.Globalization.NumberStyles]::None,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$parsed)) {
            throw "version segment '$segment' is outside the supported 32-bit range."
        }
    }
}

function Resolve-MBXDistAbsoluteRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not [System.IO.Path]::IsPathFullyQualified($Path)) {
        throw "$Label must be an absolute path."
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        throw "$Label '$fullPath' is a file."
    }
    return [System.IO.Path]::TrimEndingDirectorySeparator($fullPath)
}

function Resolve-MBXDistInputFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    if (-not [System.IO.File]::Exists($resolved.Path)) {
        throw "input '$Path' is not a file."
    }

    $name = [System.IO.Path]::GetFileName($resolved.Path)
    Assert-MBXDistName -Value $name -Label 'input filename'
    return [pscustomobject]@{ Path = $resolved.Path; Name = $name }
}

function Get-MBXDistPinnedThumbprints {
    param([Parameter(Mandatory = $true)][string]$AppInfoPath)

    $content = Get-Content -LiteralPath $AppInfoPath -Raw -ErrorAction Stop
    $block = [regex]::Match(
        $content,
        'PinnedThumbprints\s*=\s*\{(?<values>.*?)\};',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $block.Success) {
        throw "PinnedThumbprints was not found in '$AppInfoPath'."
    }

    $pins = @(
        [regex]::Matches($block.Groups['values'].Value, '"(?<pin>[0-9A-Fa-f]{64})"') |
            ForEach-Object { $_.Groups['pin'].Value.ToUpperInvariant() }
    )
    if ($pins.Count -eq 0) {
        throw "PinnedThumbprints contains no SHA-256 certificate hashes."
    }
    if ($pins | Where-Object { $_ -match '\A0{64}\z' }) {
        throw "PinnedThumbprints contains a placeholder hash."
    }
    return $pins
}

function Get-MBXDistAppVersion {
    param([Parameter(Mandatory = $true)][string]$AppInfoPath)

    $content = Get-Content -LiteralPath $AppInfoPath -Raw -ErrorAction Stop
    $match = [regex]::Match($content, 'public\s+const\s+string\s+Version\s*=\s*"(?<version>[^"]+)"\s*;')
    if (-not $match.Success) {
        throw "AppInfo.Version was not found in '$AppInfoPath'."
    }
    return $match.Groups['version'].Value
}

function Assert-MBXDistAuthenticodeSigner {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$PinnedThumbprints
    )

    try {
        $embedded = [System.Security.Cryptography.X509Certificates.X509Certificate]::CreateFromSignedFile($Path)
        $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($embedded)
    }
    catch {
        throw "input '$Path' has no readable Authenticode signer certificate; use -NoAuthenticode only for intentionally unsigned resources."
    }

    try {
        $thumbprint = $certificate.GetCertHashString(
            [System.Security.Cryptography.HashAlgorithmName]::SHA256).ToUpperInvariant()
    }
    finally {
        if ($null -ne $certificate) { $certificate.Dispose() }
        if ($null -ne $embedded) { $embedded.Dispose() }
    }

    if ($thumbprint -notin $PinnedThumbprints) {
        throw "input '$Path' is signed by unpinned certificate SHA-256 $thumbprint."
    }
    return $thumbprint
}

function Write-MBXDistAtomicText {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $directory = [System.IO.Path]::GetDirectoryName($Path)
    if (-not [System.IO.Directory]::Exists($directory)) {
        [void][System.IO.Directory]::CreateDirectory($directory)
    }

    $temporary = Join-Path $directory ('.mbxdist-write-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [System.IO.File]::WriteAllText($temporary, $Value, [System.Text.Encoding]::ASCII)
        [System.IO.File]::Move($temporary, $Path, $true)
    }
    finally {
        if ([System.IO.File]::Exists($temporary)) {
            [System.IO.File]::Delete($temporary)
        }
    }
}
