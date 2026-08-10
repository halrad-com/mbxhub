# WiiM HTTP API reference (as measured)

*Command reference for driving a WiiM Ultra over its local HTTP API — built by firing
every command against a live unit (fw `Linkplay.5.2.824843`, 2026-08) and recording what
actually happened. Companion to the narrative
[WiiM Ultra field notes](wiim-ultra-field-notes.md); read that first for transport
basics (HTTPS-only, self-signed cert, hex-encoded metadata).*

*This reference is proven in software, not just on the bench: it is the command contract
behind **MBXHub's WiiM charm** — an HTML control surface (`/pages/wiim.html` on your hub)
with transport, volume, source switching, shuffle/repeat, saved presets, a full 10-band
EQ editor, and device status, all through the hub's device proxy. Only commands marked
**verified** below are wired there, and the charm judges every write by read-back — the
same false-OK discipline this document preaches.*

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
| `EQSetBand:{"EQBand":[{"index":N,"value":V}]}` | **UNDOCUMENTED.** Custom band write — JSON payload, URL-encoded; `index` 0–9 (31 Hz→16 kHz), `value` 0–100 (50 = flat). Clears preset `Name` to `""` (= custom). The JSON's *internal colons* may stay literal — A/B-verified: a payload with unencoded `:` round-tripped identically to the fully-encoded form | verified (round-trip) |
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

### Multiroom (bench-verified 2026-08-10, Ultra master + Sound Lite V2 slave, both wired)

The headline finding first: **an input source fans out.** With the Ultra playing its
optical-in, the grouped slave renders the same audio over the LAN while the Ultra's
optical-out passthrough keeps feeding its own chain — both outputs at once (verified by
ear). A network stream relays too. One measured caveat: the passthrough output and the
multiroom slave are **slightly out of sync** — perceptually a "big empty room" echo when
both are in earshot (a small offset, slap-echo scale, not a gross lag). The passthrough
is direct hardware, the slave is a buffered LAN relay, and the group's sync domain
doesn't include the passthrough. Fine in separate rooms; expect the echo in one room.
No API-side mitigation found yet.

| Command | Fired at | Effect | Status |
|---|---|---|---|
| `ConnectMasterAp:JoinGroupMaster:eth<masterIP>:wifi0.0.0.0` | **slave** | join the master's group | verified — master's `getSlaveList` goes to `slaves:1`, slave flips to `type:1`/`mode:99` |
| `multiroom:getSlaveList` | master | group truth: slave name/uuid/ip/volume/mute | verified — **this is the only success signal** |
| `multiroom:SlaveVolume:<slaveIP>:N` | master | per-slave volume | verified (round-trip via `getSlaveList`) |
| `multiroom:SlaveMute:<slaveIP>:0\|1` | master | per-slave mute | verified (round-trip) |
| `multiroom:Ungroup` | master | dissolve the group | verified — `slaves:0` |

More community-documented verbs, not yet fired here: `multiroom:SlaveKickout:<slaveIP>`
(remove one follower), `setMultiroomSrc:eth0|wifi` (force wired/wireless sync routing),
`setPlayerCmd:groupVol:0-100` (group-wide volume), and `ConnectMasterAp:JoinGroupMaster:eth0`
as a leave-group form.

**The `multiroom:` prefix false-OKs ANY sub-verb** — `multiroom:getNonsenseXyz` answers a
cheerful `OK` on our firmware. The false-OK rule applies at the command-family level, not
just per command: an `OK` from anything `multiroom:` proves only that the prefix parsed.

Two app-side features we hunted and could NOT reach over `httpapi.asp` (probes returned
`unknown command`; official PDF, community repos, and the forum's API list all silent):
**Simultaneous Line Out** (Ultra-only beta — line-out mirrors while optical/coax is
selected) and **Group Audio Delay** (per-group latency trim under Audio Input on the group
lead — the knob that would tune the passthrough-vs-slave echo). Both live on the app's
private channel; the port-mirror capture rig is the honest next step if either matters
enough.

For Simultaneous Line Out we went one step further than name-guessing: a **differential
capture** — full `getStatusEx` + `getNewAudioOutputHardwareMode` snapshots before and
after toggling the feature ON in the app. Result: **byte-identical except the clock.**
The state is not surfaced anywhere on the public API, so neither reading nor setting it
app-free is possible on this firmware. (A brief output pause was observed at toggle time
but coincided with a possible track change — unconfirmed, so not claimed.) With the
toggle enabled, the optical passthrough and the multiroom relay verifiably keep running;
the analog line-out mirror itself is presumed live per the feature's contract but was not
audibly verified (nothing on the bench was wired to it). If it holds, that is **three
simultaneous outputs** — passthrough, LAN multiroom, analog — from one input. The setting
persists, so a one-time app toggle then permanent API-driven use is a workable pattern.

Gotchas measured on the way:

- **The slave's `getPlayerStatus.status` lies.** It reported `stop` while audibly
  playing — both for network relay and optical relay. Judge the slave by the master's
  `getSlaveList`, the slave's `vendor` field, and its position counter tracking the
  master; never by its `status`.
- **Re-joining does not preserve the group-set slave volume** (came back at a different
  level than the pre-ungroup setting). Re-assert `SlaveVolume` after every join.
- A silent source is indistinguishable from a broken relay — the master's position
  counter freezes when optical-in has no signal. Confirm the source is actually playing
  before concluding anything about the group.
- Discovery: both units answer SSDP `MediaRenderer:1` (`description.xml` on :49152) —
  that plus `getStatusEx` (`project` field names the model) is a clean app-free way to
  find LinkPlay devices and their IPs.

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
