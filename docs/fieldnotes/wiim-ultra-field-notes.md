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

- A third read earns its keep: `getMetaInfo` returns richer track metadata (plain text,
  not hex) — but not on every source. Use `getPlayerStatus`'s tags as the change signal
  and call `getMetaInfo` only when the track actually changes.

> **Full command reference:** the complete measured command table — including the
> undocumented EQ getters/setters and every false-OK we caught — lives in
> [WiiM HTTP API reference](wiim-http-api-reference.md). The table below is the
> production-proven core.

## The command set, by verification status

Everything below ran against the live unit. "Proven in production" means it's been driving
the device daily through our own control page; "bench-verified" means a one-shot test
returned the documented result; anything we haven't fired stays labeled community-documented.

**Proven in production** (control page, real daily use):

| Command | Notes |
|---|---|
| `setPlayerCmd:pause` / `:resume` | distinct verbs, not a toggle — track your own state |
| `setPlayerCmd:prev` / `:next` | |
| `setPlayerCmd:vol:N` | 0–100, integer |
| `setPlayerCmd:mute:0\|1` | |
| `setPlayerCmd:seek:N` | seconds |
| `setPlayerCmd:switchmode:<name>` | source switch — `wifi`, `bluetooth`, `optical`, `line-in`, … Optical and Bluetooth both proven live |
| `getMetaInfo` | see above |

Write commands answer `OK` as **plain text, not JSON** — judge success by HTTP status,
not by parsing the body.

**Bench-verified reads** (2026-08, one-shot):

| Command | Result on our unit |
|---|---|
| `getPresetInfo` | `{"preset_num":0,"preset_list":[]}` — data-driven preset list (names + count); empty just means none configured |
| `EQGetList` | 24 named EQ presets (`Flat`, `Acoustic`, `Bass Booster`, … `Vocal Booster`) |
| `getShutdownTimer` | `0` = no sleep timer pending |
| `getChannelBalance` | `0` = centered (range −1.0…1.0) |
| `getNewAudioOutputHardwareMode` | `{"hardware":"1","source":"0","audiocast":"0"}` — hardware `1` = optical out on our unit; enum only partially mapped publicly |
| `getbthistory` | paired-device list with `role` — see the Bluetooth note below |

**Measured but inconclusive:**

- `EQGetStat` returned `{"status":"Failed"}` where community docs promise
  `{"EQStat":"On"/"Off"}`. Plausibly "Failed" *is* the answer when the EQ subsystem is
  inactive — don't wire UI state to this reply until you've re-tested with EQ actually on.
- `Squeezelite:getState` returned bare `Failed` — the Lyrion/LMS integration is absent or
  disabled on this firmware.

**Still community-documented only** (not yet fired here): `MCUKeyShortClick:N` (fire
preset N), `EQOn`/`EQOff`/`EQLoad:<name>`, `setPlayerCmd:loopmode:<n>` (shuffle/repeat),
`setShutdownTimer:<sec>`, `setChannelBalance:<f>`, `LED_SWITCH_SET`, `Button_Enable_SET`,
the BT write family. Verify each once before building UI on it — LinkPlay command behavior
varies across firmware.

## Bluetooth goes both ways

`getbthistory` on our unit listed a paired BT speaker with `role: "Audio Sink"` — the
Ultra doesn't just *receive* Bluetooth, it can **transmit to BT speakers and headphones**.
The whole management surface is API-reachable: `startbtdiscovery:<sec>`,
`getbtdiscoveryresult`, `connectbta2dpsynk:<mac>`, `disconnectbta2dpsynk:<mac>`. We've
verified the read side only; treat the connect/disconnect writes with the usual
verify-before-wire care since they re-route audio.

## Setting the clock without the app

The display clock defaults to UTC because the timezone offset ships unset (`tz: 0.0`) —
NTP keeps the internal clock right, in the wrong zone.

- Documented: `timeSync:YYYYMMDDHHMMSS` sets the clock immediately. Works — but read on
  before you build automation around it.
- **Undocumented find:** `setTimezone:<offset>` (e.g. `setTimezone:-7`) returns `OK` and
  persists — `tz` moves from `0.0` to `-7.0` and survives reboots. It appears in neither
  the official PDF nor the community repos. **It is case-sensitive**: `setTimeZone` and
  `timezone` both return `unknown command`. (Its observable effect on the display: none
  that we could measure — see below for why.)
- **The real lesson, learned the hard way: check your network's time source before
  blaming the device.** Our display kept "reverting to UTC" on a sub-hour cadence, and
  survived reboots wrong — every heuristic said the device was ignoring its stored
  timezone. It wasn't. Our managed switch was serving time as UTC; every sync cycle the
  device obediently took the network's word for it, stomping our `timeSync` pushes within
  the hour. Setting the switch to the local zone (PDT) fixed the display **permanently,
  with zero device-side configuration** — while the API's internal clock keeps reporting
  UTC (`getStatusEx` time is UTC base; the display applies the network-derived local
  offset). Everything we measured was true; the conclusion we drew from it was wrong.
- Practical order of operations for a wrong WiiM clock: (1) fix the time/zone your
  switch/router/DHCP serves the LAN — almost certainly the actual problem; (2) `timeSync`
  for a one-shot correction on an isolated network; (3) `setTimezone` exists if you want
  the field set, but don't expect it to move the display.

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
- A **desktop WiiM Home app** (Windows/macOS) exists as of spring 2026 at wiimhome.com/app —
  noted for completeness; nothing in this workflow has needed it.
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
