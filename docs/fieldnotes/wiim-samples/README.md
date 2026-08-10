# wiim-bench — live "bench truth" for a WiiM device

A dependency-free PowerShell watcher for WiiM devices (built against a WiiM Ultra).
Polls the device's HTTP API every few seconds and rewrites a local, self-refreshing HTML
page with the real state — no vendor app, no cloud, no install.

## What it shows

- **Player** — status, source (decoded from the numeric mode), volume/mute, current track
  (hex-encoded metadata decoded), position, playlist slot
- **Clock truth** — the device clock compared against your PC in both local and UTC, with
  a verdict line (`LOCAL (correct)` / `UTC (NTP reverted!)`) so timezone drift is visible
  at a glance — see the [WiiM field notes](../../docs/fieldnotes/wiim-ultra-field-notes.md)
  for why that matters
- **Device** — firmware, network, internet reach, UPnP version
- **Flags & remote** — Chromecast enable state, BLE remote battery/RSSI, preset count

## Run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File wiim-bench.ps1 -DeviceIp 192.168.1.100
```

Then open the `wiim-bench.html` it writes next to itself in your browser — the page
refreshes itself every poll. `Ctrl+C` stops the watcher. `-IntervalSec` changes the cadence.

## Notes

- The device API is HTTPS with a self-signed certificate; the script uses `curl.exe -k`
  (bundled with Windows 10+). Treat LAN appliance certs as necessary, not trusted.
- Everything is plain PowerShell + HTML — no modules, works offline, Firefox-tested.
