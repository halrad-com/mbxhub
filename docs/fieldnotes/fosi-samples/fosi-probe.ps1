<#
.SYNOPSIS
    Read-only reconnaissance sweep for a Fosi Audio S3 streamer (or any unknown
    LinkPlay-lineage / UPnP network audio device).

.DESCRIPTION
    Answers, in one run, the questions in the "bench agenda" of the field note
    beside it (fosi-s3-field-notes.md):

      0. Is it on the network, and what does SSDP say it is?
      1. Which TCP ports answer?
      2. Does the LinkPlay httpapi.asp surface exist? (hypothesis A vs B)
      3. What does the vendor's settings.fcgi page look like?
      4. Does it advertise a UPnP MediaRenderer, with which services and control URLs?

    STRICTLY READ-ONLY. Every request is a GET. Nothing is written to the device,
    no firmware surface is exercised, no state is changed. Safe to run repeatedly.

    Everything fetched is saved verbatim under .\out\ so findings can be re-read
    later without re-probing, and a summary report is written to
    .\out\fosi-probe-report.md.

.PARAMETER Ip
    Device IP address. If omitted, the script runs discovery only (SSDP sweep) and
    prints candidates.

.PARAMETER DiscoverSeconds
    How long to listen for SSDP replies. Default 5.

.EXAMPLE
    .\fosi-probe.ps1
    .\fosi-probe.ps1 -Ip 192.168.1.123
#>
[CmdletBinding()]
param(
    [string] $Ip,
    [int]    $DiscoverSeconds = 5
)

$ErrorActionPreference = 'Continue'
$script:OutDir = Join-Path $PSScriptRoot 'out'
if (-not (Test-Path $script:OutDir)) { New-Item -ItemType Directory -Path $script:OutDir | Out-Null }

$script:Report = New-Object System.Collections.Generic.List[string]

function Log {
    param([string] $Message, [string] $Level = 'INFO')
    $stamp = (Get-Date).ToString('HH:mm:ss')
    $color = switch ($Level) {
        'OK'   { 'Green' }
        'WARN' { 'Yellow' }
        'FAIL' { 'DarkGray' }
        'HIT'  { 'Cyan' }
        default { 'Gray' }
    }
    Write-Host "[$stamp] [$Level] $Message" -ForegroundColor $color
}

function Add-Report { param([string] $Line) $script:Report.Add($Line) | Out-Null }

function Save-Artifact {
    param([string] $Name, [string] $Content)
    if ([string]::IsNullOrEmpty($Content)) { return $null }
    $path = Join-Path $script:OutDir $Name
    Set-Content -Path $path -Value $Content -Encoding UTF8
    return $path
}

# ---------------------------------------------------------------------------
# Stage 0 — SSDP discovery
# ---------------------------------------------------------------------------
function Invoke-SsdpSweep {
    param([int] $Seconds = 5, [string] $SearchTarget = 'ssdp:all')

    Log "SSDP M-SEARCH ST=$SearchTarget, listening ${Seconds}s ..."
    $results = @{}
    $udp = $null
    try {
        $udp = New-Object System.Net.Sockets.UdpClient
        $udp.Client.SetSocketOption([System.Net.Sockets.SocketOptionLevel]::Socket,
                                    [System.Net.Sockets.SocketOptionName]::ReuseAddress, $true)
        $udp.Client.ReceiveTimeout = 1000
        $udp.Client.Bind((New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)))

        $msg = "M-SEARCH * HTTP/1.1`r`n" +
               "HOST: 239.255.255.250:1900`r`n" +
               "MAN: `"ssdp:discover`"`r`n" +
               "MX: 2`r`n" +
               "ST: $SearchTarget`r`n`r`n"
        $bytes = [System.Text.Encoding]::ASCII.GetBytes($msg)
        $ep = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Parse('239.255.255.250'), 1900)

        # send twice - UDP, and one datagram is not a guarantee
        $udp.Send($bytes, $bytes.Length, $ep) | Out-Null
        Start-Sleep -Milliseconds 250
        $udp.Send($bytes, $bytes.Length, $ep) | Out-Null

        $deadline = (Get-Date).AddSeconds($Seconds)
        while ((Get-Date) -lt $deadline) {
            try {
                $remote = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
                $data = $udp.Receive([ref]$remote)
                $text = [System.Text.Encoding]::ASCII.GetString($data)
                $addr = $remote.Address.ToString()

                $location = ''
                $server   = ''
                $st       = ''
                foreach ($line in ($text -split "`r?`n")) {
                    if ($line -match '^(?i)location:\s*(.+)$') { $location = $Matches[1].Trim() }
                    if ($line -match '^(?i)server:\s*(.+)$')   { $server   = $Matches[1].Trim() }
                    if ($line -match '^(?i)(st|nt):\s*(.+)$')  { $st       = $Matches[2].Trim() }
                }

                $key = "$addr|$location"
                if (-not $results.ContainsKey($key)) {
                    $results[$key] = [pscustomobject]@{
                        Address  = $addr
                        Location = $location
                        Server   = $server
                        Target   = $st
                    }
                    Log "  <- $addr  $server  $location" 'HIT'
                }
            } catch [System.Net.Sockets.SocketException] {
                # receive timeout - keep waiting until the deadline
            }
        }
    } catch {
        Log "SSDP sweep failed: $($_.Exception.Message)" 'WARN'
    } finally {
        if ($udp) { $udp.Close() }   # never leave a socket open
    }

    return $results.Values | Sort-Object Address
}

# ---------------------------------------------------------------------------
# Stage 1 — TCP port probe
# ---------------------------------------------------------------------------
function Test-Port {
    param([string] $Address, [int] $Port, [int] $TimeoutMs = 700)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($Address, $Port, $null, $null)
        if ($async.AsyncWaitHandle.WaitOne($TimeoutMs, $false) -and $client.Connected) {
            $client.EndConnect($async)
            return $true
        }
        return $false
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

# Ports worth asking about, and why (the "why" is the point - a bare list rots)
$PortPlan = @(
    @{ Port = 80;    Why = 'vendor settings.fcgi is documented on plain HTTP' }
    @{ Port = 443;   Why = 'WiiM/LinkPlay firmware is HTTPS-only; does this one follow?' }
    @{ Port = 8080;  Why = "SWUpdate's built-in web server default" }
    @{ Port = 8443;  Why = 'alt HTTPS' }
    @{ Port = 8819;  Why = 'LinkPlay private/app port seen on WiiM hardware' }
    @{ Port = 8888;  Why = 'common embedded admin/alt-HTTP' }
    @{ Port = 5000;  Why = 'common embedded API port' }
    @{ Port = 1400;  Why = 'Sonos-style control port; cheap to rule out' }
    @{ Port = 49152; Why = 'UPnP dynamic range' }
    @{ Port = 49153; Why = 'UPnP dynamic range' }
    @{ Port = 49154; Why = 'UPnP dynamic range' }
    @{ Port = 49155; Why = 'UPnP dynamic range' }
    @{ Port = 8008;  Why = 'Google Cast HTTP' }
    @{ Port = 8009;  Why = 'Google Cast TLS' }
    @{ Port = 7000;  Why = 'AirPlay 2 RTSP' }
    @{ Port = 22;    Why = 'ssh - would be a finding all by itself' }
    @{ Port = 23;    Why = 'telnet - ditto' }
)

# ---------------------------------------------------------------------------
# HTTP fetch (curl.exe: ships with Windows 10+, -k handles the self-signed certs
# these devices use, and it avoids touching the process-wide cert callback)
# ---------------------------------------------------------------------------
function Get-Url {
    param([string] $Url, [int] $TimeoutSec = 6)
    $body = & curl.exe -sS -k --max-time $TimeoutSec -w "`n---HTTPSTATUS:%{http_code}---" $Url 2>&1 | Out-String
    $status = '000'
    if ($body -match '---HTTPSTATUS:(\d+)---') {
        $status = $Matches[1]
        $body = $body -replace '---HTTPSTATUS:\d+---', ''
    }
    # status 000 means curl never got a response - what is in $body is curl's own
    # error text, not device output. Reporting its length as a body size reads as
    # "the device answered with 575 bytes", which is a lie. Blank it.
    if ($status -eq '000') {
        return [pscustomobject]@{ Status = $status; Body = ''; Url = $Url; Error = $body.Trim() }
    }
    return [pscustomobject]@{ Status = $status; Body = $body.Trim(); Url = $Url; Error = '' }
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '=== Fosi S3 read-only probe ===' -ForegroundColor White
Add-Report "# Fosi S3 probe report"
Add-Report ""
Add-Report "Run: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') on $env:COMPUTERNAME"
Add-Report "Mode: READ-ONLY (GET requests only, no device state changed)"
Add-Report ""

# --- Stage 0
Add-Report "## Stage 0 - SSDP discovery"
Add-Report ""
$ssdp = Invoke-SsdpSweep -Seconds $DiscoverSeconds
if (-not $ssdp -or $ssdp.Count -eq 0) {
    Log 'No SSDP responders at all - check the interface/VLAN before concluding anything.' 'WARN'
    Add-Report "_No SSDP responders answered. This is a rig finding, not a device finding._"
} else {
    Add-Report "| Address | Server | Location |"
    Add-Report "| --- | --- | --- |"
    foreach ($d in $ssdp) { Add-Report "| $($d.Address) | $($d.Server) | $($d.Location) |" }
}
Add-Report ""

$renderers = @($ssdp | Where-Object { $_.Target -match 'MediaRenderer' })
if ($renderers.Count -gt 0) {
    Log "MediaRenderer advertised by: $(($renderers | Select-Object -ExpandProperty Address | Sort-Object -Unique) -join ', ')" 'OK'
}

if (-not $Ip) {
    Log 'No -Ip given: discovery-only run. Re-run with -Ip <address> once the device is identified.' 'WARN'
    Add-Report "_Discovery-only run; no device probed._"
    $reportPath = Join-Path $script:OutDir 'fosi-probe-report.md'
    Set-Content -Path $reportPath -Value ($script:Report -join "`n") -Encoding UTF8
    Log "Report: $reportPath"
    return
}

# --- Stage 1
Write-Host ''
Log "Port probe against $Ip ..."
Add-Report "## Stage 1 - Open ports on $Ip"
Add-Report ""
Add-Report "| Port | Open | Why we asked |"
Add-Report "| --- | --- | --- |"
$openPorts = @()
foreach ($p in $PortPlan) {
    $open = Test-Port -Address $Ip -Port $p.Port
    if ($open) { $openPorts += $p.Port; Log "  $($p.Port) OPEN" 'OK' } else { Log "  $($p.Port) closed" 'FAIL' }
    Add-Report "| $($p.Port) | $(if ($open) { '**OPEN**' } else { 'closed' }) | $($p.Why) |"
}
Add-Report ""

# --- Stage 2: hypothesis A vs B (LinkPlay httpapi.asp)
Write-Host ''
Log 'Testing for the LinkPlay httpapi.asp surface (hypothesis A vs B) ...'
Add-Report "## Stage 2 - LinkPlay httpapi.asp (hypothesis A vs B)"
Add-Report ""
$linkplayHit = $false
foreach ($scheme in @('http', 'https')) {
    foreach ($cmd in @('getStatusEx', 'getPlayerStatus')) {
        $r = Get-Url "${scheme}://${Ip}/httpapi.asp?command=$cmd"
        $verdict = if ($r.Status -eq '200' -and $r.Body.Length -gt 0) { 'ANSWERED' } else { 'no' }
        if ($verdict -eq 'ANSWERED') {
            $linkplayHit = $true
            Log "  $scheme $cmd -> $($r.Status) ANSWERED" 'HIT'
            Save-Artifact "httpapi-$scheme-$cmd.txt" $r.Body | Out-Null
        } else {
            Log "  $scheme $cmd -> $($r.Status)" 'FAIL'
        }
        $preview = ($r.Body -replace "`r?`n", ' ')
        if ($preview.Length -gt 160) { $preview = $preview.Substring(0, 160) + ' ...' }
        Add-Report "- ``${scheme}://${Ip}/httpapi.asp?command=$cmd`` -> HTTP $($r.Status) $verdict"
        if ($verdict -eq 'ANSWERED') { Add-Report "  - body: ``$preview``" }
    }
}
Add-Report ""
if ($linkplayHit) {
    Add-Report "**VERDICT: hypothesis A** - the LinkPlay httpapi surface is present. The WiiM HTTP API reference in this repo largely applies; verify each command by read-back before trusting it (LinkPlay answers OK to anything parseable)."
    Log 'VERDICT: hypothesis A - LinkPlay httpapi.asp is alive.' 'OK'
} else {
    Add-Report "**VERDICT: hypothesis B (so far)** - no httpapi.asp response. Control must come from UPnP/DLNA, Google Cast, or the vendor's own web surface. Note this is negative evidence from two commands; if the device later answers on a different path, revise."
    Log 'VERDICT: hypothesis B (so far) - no httpapi.asp.' 'WARN'
}

# --- Stage 3: vendor web surface
Write-Host ''
Log 'Fetching vendor web surface ...'
Add-Report "## Stage 3 - Vendor web surface"
Add-Report ""
$vendorPaths = @('/', '/settings.fcgi', '/index.html', '/status.fcgi', '/info.fcgi', '/api', '/cgi-bin/')
foreach ($path in $vendorPaths) {
    $r = Get-Url "http://${Ip}$path"
    Log "  GET $path -> $($r.Status) ($($r.Body.Length) bytes)" $(if ($r.Status -eq '200') { 'HIT' } else { 'FAIL' })
    Add-Report "- ``GET http://${Ip}$path`` -> HTTP $($r.Status), $($r.Body.Length) bytes"
    if ($r.Status -eq '200' -and $r.Body.Length -gt 0) {
        $safe = ($path -replace '[^A-Za-z0-9]', '_')
        $saved = Save-Artifact "vendor$safe.html" $r.Body
        Add-Report "  - saved: $(Split-Path $saved -Leaf)"
        # surface any script/endpoint references - this is where the neighbours hide
        $refs = [regex]::Matches($r.Body, '(?i)(?:href|src|url|action)\s*=\s*["'']([^"'']+)["'']') |
                ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
        $apiRefs = [regex]::Matches($r.Body, '(?i)["'']([/A-Za-z0-9_.\-]*\.(?:fcgi|cgi|asp|json))(?:\?[^"'']*)?["'']') |
                   ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
        foreach ($set in @(@{ n = 'linked'; v = $refs }, @{ n = 'endpoint-looking'; v = $apiRefs })) {
            if ($set.v.Count -gt 0) {
                $label = $set.n
                $list = ($set.v | Select-Object -First 25) -join ' , '
                Add-Report "  - $label references: $list"
            }
        }
    }
}
Add-Report ""

# --- Stage 4: UPnP device description + services
Write-Host ''
Log 'Reading UPnP device descriptions ...'
Add-Report "## Stage 4 - UPnP device description and services"
Add-Report ""
$locations = @($ssdp | Where-Object { $_.Address -eq $Ip -and $_.Location } |
                Select-Object -ExpandProperty Location -Unique)
if ($locations.Count -eq 0) {
    # device did not answer our M-SEARCH (or discovery ran before it joined) - try the usual paths
    $locations = @("http://${Ip}:49152/description.xml", "http://${Ip}/description.xml", "http://${Ip}:8080/description.xml")
    Add-Report "_No SSDP LOCATION for $Ip; trying conventional description paths._"
}

foreach ($loc in $locations) {
    $r = Get-Url $loc
    Add-Report "- ``$loc`` -> HTTP $($r.Status), $($r.Body.Length) bytes"
    if ($r.Status -ne '200' -or $r.Body.Length -eq 0) { Log "  $loc -> $($r.Status)" 'FAIL'; continue }
    Log "  $loc -> 200" 'HIT'
    $safe = ($loc -replace '[^A-Za-z0-9]', '_')
    Save-Artifact "upnp_$safe.xml" $r.Body | Out-Null

    try {
        $xml = [xml] $r.Body
        $dev = $xml.root.device
        Add-Report "  - friendlyName: **$($dev.friendlyName)**"
        Add-Report "  - manufacturer: $($dev.manufacturer) / model: $($dev.modelName) $($dev.modelNumber)"
        Add-Report "  - deviceType: $($dev.deviceType)"
        Log "    friendlyName: $($dev.friendlyName) [$($dev.manufacturer) $($dev.modelName)]" 'OK'

        $services = @($dev.serviceList.service)
        if ($dev.deviceList -and $dev.deviceList.device) {
            foreach ($sub in @($dev.deviceList.device)) { $services += @($sub.serviceList.service) }
        }
        Add-Report "  - services:"
        foreach ($s in $services) {
            if (-not $s) { continue }
            Add-Report "    - ``$($s.serviceType)`` control=``$($s.controlURL)`` event=``$($s.eventSubURL)`` scpd=``$($s.SCPDURL)``"
            Log "      service: $($s.serviceType)" 'OK'
        }
        $hasAv  = @($services | Where-Object { $_.serviceType -match 'AVTransport' }).Count -gt 0
        $hasRc  = @($services | Where-Object { $_.serviceType -match 'RenderingControl' }).Count -gt 0
        Add-Report ""
        Add-Report "  - **AVTransport: $(if ($hasAv) { 'YES' } else { 'no' }) / RenderingControl: $(if ($hasRc) { 'YES' } else { 'no' })**"
        if ($hasAv -and $hasRc) {
            Add-Report "  - A standards-based transport + volume charm is viable against this device."
        }
    } catch {
        Add-Report "  - _description XML did not parse: $($_.Exception.Message)_"
        Log "    description XML did not parse: $($_.Exception.Message)" 'WARN'
    }
}
Add-Report ""

# --- wrap up
Add-Report "## Notes"
Add-Report ""
Add-Report "- Read-only run: no command was sent that could change device state."
Add-Report "- Anything not answered here is 'not found by this probe', which is not the same as 'not present'."
Add-Report "- Before trusting any write command later: snapshot -> write -> read back -> restore. On LinkPlay-lineage"
Add-Report "  hardware an ``OK`` reply proves only that the string parsed."
Add-Report ""
Add-Report "Raw artifacts saved beside this report in ``out/``."

$reportPath = Join-Path $script:OutDir 'fosi-probe-report.md'
Set-Content -Path $reportPath -Value ($script:Report -join "`n") -Encoding UTF8
Write-Host ''
Log "Report written: $reportPath" 'OK'
Log "Open ports: $(if ($openPorts.Count) { $openPorts -join ', ' } else { 'none' })"
