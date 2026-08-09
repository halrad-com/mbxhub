# WiiM HTTP API reference (as measured)

*Command reference for driving a WiiM Ultra over its local HTTP API — built by firing
every command against a live unit (fw `Linkplay.5.2.824843`, 2026-08) and recording what
actually happened. Companion to the narrative
[WiiM Ultra field notes](wiim-ultra-field-notes.md); read that first for transport
basics (HTTPS-only, self-signed cert, hex-encoded metadata).*

All commands go to:

```
https://<device-ip>/httpapi.asp?command=<command>
```

```sh
curl -k -s "https://192.168.1.100/httpapi.asp?command=getPlayerStatus"
```

## The one rule: never trust `OK`

LinkPlay firmware answers `OK` (or `{"status":"OK"}`) for any command it can *parse* —
including commands that then do nothing. We measured three distinct false-OKs on one
bench day. **A write is verified only when a read-back shows the state changed.** Every
status column below reflects that standard:

- **verified** — round-tripped: write fired, getter confirmed the change (or it's a getter
  that returned real data).
- **accepted** — returned OK, but the effect wasn't (or couldn't be) confirmed.
- **false-OK** — returned OK and demonstrably did nothing.
- **dead** — returns `Failed`/`unknown command` on this firmware.

## Quick reference

### Status / identity

| Command | Returns | Status |
|---|---|---|
| `getStatusEx` | full device JSON: name, firmware, network, tz, feature flags, BLE remote, `preset_key`, `EQ_support` | verified |
| `getPlayerStatus` | playback: `status`, `mode` (source enum), `loop`, `eq`, `vol`, `mute`, `curpos`/`totlen`, hex `Title/Artist/Album` | verified |
| `getMetaInfo` | richer track metadata, plain text (not on every source) | verified |
| `getNewAudioOutputHardwareMode` | `{"hardware":"1","source":"0","audiocast":"0"}` — output route (1 = optical out on our unit) | verified |

### Playback (`setPlayerCmd` family)

All answer plain-text `OK`; judge by HTTP status + read-back.

| Command | Effect | Status |
|---|---|---|
| `setPlayerCmd:pause` / `:resume` | distinct verbs, not a toggle | verified (production) |
| `setPlayerCmd:prev` / `:next` | track skip | verified (production) |
| `setPlayerCmd:vol:N` | volume 0–100 | verified (production) |
| `setPlayerCmd:mute:0\|1` | mute | verified (production) |
| `setPlayerCmd:seek:N` | seconds | verified (production) |
| `setPlayerCmd:switchmode:<name>` | source switch: `wifi`, `bluetooth`, `optical`, `line-in`, `line-in2`, `co-axial`, `udisk`, `PCUSB` | verified (optical, bluetooth) |
| `setPlayerCmd:loopmode:N` | repeat/shuffle — see enum below | verified (round-trip) |

### EQ — the good stuff

The Ultra runs a 10-band graphic EQ (`EQ_support: Eq10HP_ver_2.0` — the CAPS Eq10HP
LV2 plugin). The EQ state is **per-source** (`source_name` rides every EQ reply).

| Command | Effect | Status |
|---|---|---|
| `EQGetList` | 24 preset names (`Flat`, `Acoustic`, … `Vocal Booster`) | verified |
| `EQOn` / `EQOff` | enable/disable | verified (round-trip via `EQGetBand`) |
| `EQLoad:<name>` | load preset; **reply echoes the full EQ state** (bands in dB) | verified |
| `EQGetBand` | **UNDOCUMENTED.** Full state getter: `EQStat` On/Off, preset `Name`, 10 band values (0–100 scale), `channelMode`, `EQLevel`, `source_name`. Works while EQ is off — the poll target | verified |
| `EQSetBand:{"EQBand":[{"index":N,"value":V}]}` | **UNDOCUMENTED.** Custom band write — JSON payload, URL-encoded; `index` 0–9 (31 Hz→16 kHz), `value` 0–100 (50 = flat). Clears preset `Name` to `""` (= custom) | verified (round-trip) |
| `EQSetBand:0:55` / `EQSetBand:band31hz:55` | simpler syntaxes | **false-OK** — accepted, no effect |
| `EQGetStat` | supposed On/Off getter | **dead** — `{"status":"Failed"}` always, even with EQ on. Use `EQGetBand` |

Two scales to keep straight: `EQLoad` replies report bands in **dB** (0.0 = flat);
`EQGetBand`/`EQSetBand` use a **0–100 UI scale** (50 = flat).

```sh
# read full EQ state
curl -k -s "https://192.168.1.100/httpapi.asp?command=EQGetBand"
# boost 31Hz slightly (custom curve)
curl -k -s -G "https://192.168.1.100/httpapi.asp" \
  --data-urlencode 'command=EQSetBand:{"EQBand":[{"index":0,"value":55}]}'
```

### Presets

| Command | Effect | Status |
|---|---|---|
| `getPresetInfo` | `{"preset_num":N,"preset_list":[…]}` — names + count, data-driven | verified |
| `MCUKeyShortClick:N` | fire preset N (1–12, `preset_key: 12`) | accepted — OK with zero presets configured; real fire untested |

No API to *create* presets has surfaced — as far as we know that remains app-side
(candidate for private-channel capture).

### Sound tuning

| Command | Effect | Status |
|---|---|---|
| `getChannelBalance` / `setChannelBalance:F` | stereo balance −1.0…1.0 (0 = center) | verified (round-trip 0 → 0.2 → 0) |
| `setAudioOutputHardwareMode:N` | select output route | accepted — fired at current value only (actually switching would have cut our audio) |

### Bluetooth (both directions)

The Ultra transmits to BT sinks as well as receiving. Whole surface is API-reachable:

| Command | Effect | Status |
|---|---|---|
| `getbthistory` | paired devices with `role` (`Audio Sink` = something the Ultra plays *to*) | verified |
| `startbtdiscovery:SEC` | scan for BT devices | verified |
| `getbtdiscoveryresult` | `{"num":…,"scan_status":…,"list":[{name, ad, role, rssi}]}` | verified — found a nearby TV as sink, rssi −73 |
| `connectbta2dpsynk:<mac>` / `disconnectbta2dpsynk:<mac>` | route audio to/from a BT sink | not fired — re-routes audio; verify in a bench window |

### Clock / timers / alarms

| Command | Effect | Status |
|---|---|---|
| `timeSync:YYYYMMDDHHMMSS` | set clock now | verified (but see field notes — fix your network's time source instead) |
| `setTimezone:<offset>` | **UNDOCUMENTED**, case-sensitive; persists, surfaces as `tz` and `app_timezone_id` in `getStatusEx` | verified (state); display effect none |
| `getShutdownTimer` | remaining seconds (0 = none) | verified |
| `setShutdownTimer:SEC` | arm sleep timer | **suspect false-OK** — OK but getter kept reading 0; true verify means letting it fire, which needs a bench window |
| `getAlarmClock:N` | alarm N state (`{"enable":"0"}`) | verified |

### System / cosmetic

| Command | Effect | Status |
|---|---|---|
| `LED_SWITCH_SET:0\|1` | status LED | accepted — no getter exists to round-trip |
| `Button_Enable_SET:0\|1` | touch controls | accepted — same |
| `Squeezelite:getState` | Lyrion/LMS integration | dead on this fw (`Failed`) |
| `reboot`, firmware-update family | — | not fired, deliberately |

## Enum tables

**`getPlayerStatus.mode`** (source; partial map — display raw code alongside):
`0` idle · `1` AirPlay · `2` DLNA · `10`/`20` network · `11` USB disk · `16`/`31` Spotify ·
`32` TIDAL · `36` Qobuz · `40` line-in · `41` Bluetooth · `43` optical · `44` RCA ·
`45` coaxial · `47` line-in 2 · `51` USB DAC · `99` multiroom slave

**`getPlayerStatus.loop`** (community map; `0` and `4` round-trip-confirmed):
`0` repeat-all · `1` repeat-one · `2` shuffle+repeat · `3` shuffle-once · `4` none ·
`5` shuffle-no-repeat

**`getNewAudioOutputHardwareMode.hardware`**: `1` = optical out (confirmed on our unit);
rest of the enum unmapped — treat as opaque until measured.

**EQ band indexes** (`EQGetBand`/`EQSetBand`): `0`=31 Hz `1`=63 Hz `2`=125 Hz `3`=250 Hz
`4`=500 Hz `5`=1 kHz `6`=2 kHz `7`=4 kHz `8`=8 kHz `9`=16 kHz

## What still lives on the private channel

Preset creation, some onboarding/setup, and possibly the sleep-timer path the app uses.
The vendor app talks to `communication_port` (8819) with a non-HTTP handshake; if a
control ever matters and `httpapi.asp` can't reach it, port-mirror the device and capture
the app's traffic — that's the documented next step, not guesswork.
