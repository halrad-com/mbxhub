# wiim-bench.ps1 — WiiM Ultra bench-truth watcher (app-free, no product deps)
# Polls the Ultra's HTTPS API every 3s and rewrites wiim-bench.html next to this
# script; open that file in Firefox — it self-refreshes via <meta refresh>.
# Run:  powershell -NoProfile -ExecutionPolicy Bypass -File wiim-bench.ps1
# Stop: Ctrl+C
param([string]$DeviceIp = "192.168.1.100", [int]$IntervalSec = 3)

$htmlPath = Join-Path $PSScriptRoot "wiim-bench.html"

function HexToStr([string]$h) {
    if ($h -and $h -match '^[0-9A-Fa-f]+$' -and ($h.Length % 2) -eq 0) {
        try {
            $bytes = [byte[]]::new($h.Length / 2)
            for ($i = 0; $i -lt $bytes.Length; $i++) { $bytes[$i] = [Convert]::ToByte($h.Substring($i * 2, 2), 16) }
            return [Text.Encoding]::UTF8.GetString($bytes)
        } catch { return $h }
    }
    return $h
}

# LinkPlay source modes — labeled where known, raw otherwise (honest-labeling rule)
$modeMap = @{ "0"="idle"; "1"="AirPlay"; "2"="DLNA/UPnP"; "10"="network stream"; "11"="USB disk";
              "31"="Spotify Connect"; "32"="Tidal Connect"; "40"="line-in"; "41"="Bluetooth";
              "43"="optical in"; "49"="HDMI in" }

function Esc([string]$s) { if ($null -eq $s) { return "" }; $s -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;' }

function Row([string]$k, [string]$v, [string]$cls = "") {
    "<tr><td>$(Esc $k)</td><td class='v $cls'>$(Esc $v)</td></tr>"
}

Write-Host "wiim-bench: watching $DeviceIp every ${IntervalSec}s -> $htmlPath  (Ctrl+C to stop)"
$lastGood = "never"

while ($true) {
    $stampLocal = Get-Date
    $ok = $true
    try {
        $stat = curl.exe -sk --max-time 5 "https://$DeviceIp/httpapi.asp?command=getStatusEx" | ConvertFrom-Json
        $play = curl.exe -sk --max-time 5 "https://$DeviceIp/httpapi.asp?command=getPlayerStatus" | ConvertFrom-Json
        if (-not $stat -or -not $play) { $ok = $false }
    } catch { $ok = $false }

    $rows = New-Object System.Collections.Generic.List[string]
    if ($ok) {
        $lastGood = $stampLocal.ToString("HH:mm:ss")

        # Clock truth: device internal clock vs this PC (local and UTC) — NTP-revert watch
        $clockRows = ""
        try {
            $dev = [datetime]::ParseExact("$($stat.date) $($stat.time)", "yyyy:MM:dd HH:mm:ss", [Globalization.CultureInfo]::InvariantCulture)
            $dLoc = [Math]::Round(($dev - $stampLocal).TotalMinutes)
            $dUtc = [Math]::Round(($dev - $stampLocal.ToUniversalTime()).TotalMinutes)
            $verdict = if ([Math]::Abs($dLoc) -le 2) { "LOCAL (correct)" } elseif ([Math]::Abs($dUtc) -le 2) { "UTC (NTP reverted!)" } else { "NEITHER (drifting)" }
            $cls = if ([Math]::Abs($dLoc) -le 2) { "good" } else { "bad" }
            $clockRows = (Row "device clock" "$($stat.date) $($stat.time)") +
                         (Row "vs PC local / vs UTC" "$dLoc min / $dUtc min") +
                         (Row "clock is showing" $verdict $cls) +
                         (Row "stored tz" "$($stat.tz)")
        } catch { $clockRows = Row "clock parse" "failed: $($stat.date) $($stat.time)" "bad" }

        $mode = "$($play.mode)"; $modeLbl = if ($modeMap.ContainsKey($mode)) { "$($modeMap[$mode]) (mode $mode)" } else { "mode $mode (unmapped)" }
        $pos = try { "{0:n0}s / {1:n0}s" -f ([int]$play.curpos/1000), ([int]$play.totlen/1000) } catch { "-" }
        $muteLbl = if ("$($play.mute)" -eq "1") { "MUTED" } else { "unmuted" }

        $sections = @"
<h2>Player</h2><table>
$(Row "status" "$($play.status)" $(if ("$($play.status)" -eq "play") { "good" } else { "" }))
$(Row "source" $modeLbl)
$(Row "volume" "$($play.vol) / 100  ($muteLbl)")
$(Row "track" ((HexToStr $play.Title) + " — " + (HexToStr $play.Artist) + " — " + (HexToStr $play.Album)))
$(Row "position" $pos)
$(Row "playlist" "$($play.plicurr) of $($play.plicount)")
</table>
<h2>Clock truth</h2><table>$clockRows</table>
<h2>Device</h2><table>
$(Row "name" "$($stat.DeviceName)")
$(Row "firmware" "$($stat.firmware)  (release $($stat.Release))")
$(Row "network" "eth $($stat.eth0)  MAC $($stat.MAC)")
$(Row "internet / NTP reach" $(if ("$($stat.internet)" -eq "1") { "online" } else { "offline" }))
$(Row "UPnP ver" "$($stat.upnp_version)")
</table>
<h2>Flags &amp; remote</h2><table>
$(Row "Chromecast (cast_enable)" $(if ("$($stat.cast_enable)" -eq "0") { "OFF" } else { "ON" }))
$(Row "BLE remote" $(if ("$($stat.BleRemoteConnected)" -eq "1") { "connected — battery $($stat.BleRemoteBatterylevel)%, RSSI $($stat.BleRemoteRSSI)" } else { "not connected" }))
$(Row "presets available" "$($stat.preset_key)")
$(Row "max volume" "$($stat.max_volume)")
</table>
"@
    } else {
        $sections = "<h2 class='bad'>DEVICE UNREACHABLE</h2><p>last good poll: $lastGood</p>"
    }

    $html = @"
<!DOCTYPE html>
<html><head><meta charset="utf-8"><meta http-equiv="refresh" content="$IntervalSec">
<title>WiiM bench truth — $DeviceIp</title>
<style>
 body { background:#101216; color:#d7dae0; font-family:system-ui,sans-serif; max-width:640px; margin:2em auto; padding:0 1em; }
 h1 { font-size:1.1em; color:#8ab4f8; } h2 { font-size:0.95em; margin:1.2em 0 0.3em; color:#9aa4b2; border-bottom:1px solid #2a2f3a; }
 table { width:100%; border-collapse:collapse; } td { padding:2px 6px; font-size:0.9em; vertical-align:top; }
 td:first-child { color:#7f8896; width:40%; } .v { font-family:Consolas,monospace; }
 .good { color:#7dd97b; } .bad { color:#ff7b72; font-weight:bold; }
 footer { margin-top:1.5em; color:#5c6470; font-size:0.8em; }
</style></head><body>
<h1>WiiM Ultra — bench truth &#183; $DeviceIp</h1>
$sections
<footer>polled $($stampLocal.ToString("yyyy-MM-dd HH:mm:ss")) &#183; refreshes every ${IntervalSec}s &#183; wiim-bench.ps1 (app-free)</footer>
</body></html>
"@
    Set-Content -Path $htmlPath -Value $html -Encoding UTF8
    Write-Host ("{0}  ok={1}  status={2} vol={3}" -f $stampLocal.ToString("HH:mm:ss"), $ok, $play.status, $play.vol)
    Start-Sleep -Seconds $IntervalSec
}
