# Devialet Phantom: driving it over the LAN

*Working notes from building and running MBXHub's speaker integration against a live
Phantom. Everything stated was exercised against real hardware; firmware variations are
called out where we've had to code around them.*

The Phantom exposes a local REST API — **plain HTTP, no auth, no TLS** (a different
posture from, say, WiiM's HTTPS-only stance; know which appliance you're talking to):

```
http://<device-ip>/ipcontrol/v1/...
```

## The endpoints that matter

Devialet's API is organized around *systems/groups* and *sources*. For a single speaker
(or a stereo pair acting as one), the `groups/current` namespace resolves to "whatever
this device currently is," which sidesteps needing IDs:

| What | Call |
|---|---|
| Reachability probe | `GET /ipcontrol/v1/devices/current` |
| Volume + mute state | `GET /ipcontrol/v1/groups/current/sources/current/soundControl/volume` |
| Set volume | `POST` same URL, body `{"volume": N}` (0–100) |
| Mute / unmute | `POST /ipcontrol/v1/groups/current/sources/current/playback/mute` (or `/unmute`) |
| List sources | `GET /ipcontrol/v1/groups/current/sources` |
| Current source | `GET /ipcontrol/v1/groups/current/sources/current` → `source.sourceId` |
| **Switch source** | `POST /ipcontrol/v1/groups/current/sources/{sourceId}/playback/play` |

Note the source-switch shape: there is no "select input" verb — **playing a source *is*
the switch.**

## Firmware drift — parse defensively

Across firmware revisions the volume payload's field names wander: we've seen `volume`
vs `currentVolume`, and `mute` vs `muted`. Read whichever is present. The sources list
arrives under `sources` on some revisions and `items` on others. Source `type` strings
worth mapping for display: `spotifyconnect`, `airplay2`, `upnp`, `bluetooth`,
`optical`/`toslink`, `coaxial`, `analog`, `line`, `raat` (Roon), `digital_left`/`digital_right`
(stereo-pair internals).

## The trap: phantom mutes during volume ramps

The one that costs an afternoon: **while the speaker ramps volume, polls transiently
report `muted: true`.** If your UI polls state and renders the reply naively, every
volume drag makes the mute indicator flicker on and off.

The fix that holds up: after *you* set volume, ignore the polled mute value for a short
suppression window (~5 s) and keep your optimistic state. Outside the window, trust the
poll — you still want to see mutes that arrive from elsewhere (the vendor app, the
physical controls). We use the same pattern for the volume value itself on other devices;
"suppress the echo of your own command" turns out to be the universal idiom for
poll-driven device UIs.

## Operational notes

- Keep HTTP timeouts short (we use 2 s). It's a LAN appliance: either it answers
  instantly or it's asleep/unplugged, and a long timeout just makes your UI feel broken.
- The speaker keeps its own volume stage. In a chain (player → streamer → optical →
  Phantom), that's the *last* of several gain stages — when "there's no sound," walk the
  chain: source playing? streamer output live (optical carries a visible red light)?
  speaker awake, right input selected, unmuted? Each stage holds independent state, and
  the API only shows you this one.
- No CORS headers, of course — browsers can't call the device directly. Same story as
  every other LAN appliance: proxy through your own service.
