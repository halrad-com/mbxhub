# WiiM Ultra: owning it over the API (no app)

*Bench notes, 2026-08. Firmware `Linkplay.5.2.824843`, release 2026-07-31. Everything below
was measured against a live unit on Ethernet.*

The goal: full control of a WiiM Ultra from scripts and our own pages — never the vendor app.
Verdict: **achievable**. Every control we've needed so far is API-reachable, including one
command the official documentation doesn't admit exists.

## The API surface

- **HTTPS only.** The Ultra refuses plain HTTP outright (older LinkPlay devices served it;
  `securemode` firmware does not). The cert is self-signed — use `curl -k` or the
  equivalent skip-validation in your client. Treat it as a LAN appliance: the cert proves
  nothing and doesn't need to.
- **There is no human web UI.** Browsing to the device gets you nothing; the only
  browser-reachable surface is the raw API:
  `https://<device-ip>/httpapi.asp?command=<command>`
- Primary references: the official
  [HTTP API for WiiM Products PDF](https://www.wiimhome.com/pdf/HTTP%20API%20for%20WiiM%20Products.pdf)
  (thin but authoritative), plus community maps
  [cvdlinden/wiim-httpapi](https://github.com/cvdlinden/wiim-httpapi) and
  [DanBrezeanu/wiim-extended-http-api](https://github.com/DanBrezeanu/wiim-extended-http-api)
  (EQ, LED, network — the undocumented extras).

## The two workhorses

- `getStatusEx` — device identity, firmware, network, clock/timezone, feature flags,
  BLE-remote telemetry. Big JSON, poll-friendly.
- `getPlayerStatus` — playback state, source mode, volume/mute, track metadata, position.

**Gotcha: `Title`, `Artist`, `Album` come back hex-encoded** (`556E6B6E6F776E` =
"Unknown"). Decode hex → UTF-8 before display.

## Setting the clock without the app

The display clock defaults to UTC because the timezone offset ships unset (`tz: 0.0`) —
NTP keeps the internal clock right, in the wrong zone.

- Documented: `timeSync:YYYYMMDDHHMMSS` sets the clock immediately. Works.
- **Undocumented find:** `setTimezone:<offset>` (e.g. `setTimezone:-7`) returns `OK` and
  persists — `tz` moves from `0.0` to `-7.0` and survives. It appears in neither the
  official PDF nor the community repos. **It is case-sensitive**: `setTimeZone` and
  `timezone` both return `unknown command`.
- **Confirmed after longer observation: the front display follows the *raw* internal
  clock and ignores the stored timezone.** The tz value persists (`-7.0` held across
  hours), but the device's periodic NTP sync sets the internal clock to UTC — and the
  display reverts with it. `timeSync` fixes the display every time; NTP undoes it on its
  next cycle — measured at **under an hour** between reverts on our unit.
- **Reboot tested: no help.** After a power cycle the display came up UTC again with the
  tz still stored — the display ignores the timezone unconditionally, boot included.
- Durable app-free options, in order of appeal: (1) block the device's NTP (outbound
  UDP 123) at the router — a manual `timeSync` then holds indefinitely; (2) a scheduled
  `timeSync` push every 15–30 minutes (one `curl -k` line on a timer — cadence must beat
  the sub-hour NTP cycle, a daily push is not enough). Or accept the app for the one
  settings write it can apparently do that the API can't surface.

## Gotchas worth knowing

- **`cast_enable: 0`** — Chromecast can be off at device level. If you're testing the Cast
  plane and it looks dead, check this flag before blaming your code.
- **Volume is 0–100 in integer steps** (`max_volume: 100`) — true 1% granularity on the
  device's own stage, finer than Windows' stock 2% master-volume stepping.
- The Ultra ships a **BLE remote** whose telemetry is visible in `getStatusEx`
  (`BleRemoteConnected`, battery %, RSSI) — handy bench canary for the BLE link.
- `getPlayerStatus.mode` is a numeric source code (40 = line-in, 41 = Bluetooth,
  10 = network stream, 31 = Spotify Connect, ...). Community docs map most values;
  unmapped codes do turn up — display the raw number alongside your label.
- The vendor app talks to the device on a separate private channel (`communication_port`
  in `getStatusEx`) — some settings (initial Wi-Fi setup, certain onboarding) may only be
  reachable there or via the device's setup hotspot. Everything we've needed post-setup
  has been HTTP-API reachable.
- **UPnP/DLNA is a second, fully standard control plane** (`upnp_version` in status) —
  AVTransport/RenderingControl work alongside the HTTP API, so anything that speaks DLNA
  can drive the device without touching either the app or the private channel.

## Watching it live

See [`samples/wiim-bench/`](../../samples/wiim-bench/) — a dependency-free PowerShell
watcher that polls the two workhorse commands and renders a self-refreshing local HTML
"bench truth" page: player state, decoded track, volume, clock-drift verdict (catches NTP
reverts at a glance), cast flag, BLE remote battery.
