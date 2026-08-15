# Fosi Audio S3 — field notes

> **STATUS: PRE-BENCH (opened 2026-08-15). Nothing about the S3 itself is measured yet.**
> The one measured section is §0, and it is measured on *other* hardware — the LinkPlay
> devices we already own, used as a yardstick to hold the S3 against when it arrives.
> Every other note in this directory states bench-verified truth. This one does not, yet —
> it is the desk-research file opened *before* the unit reaches the bench, so the probing
> is planned rather than improvised. Each claim below carries its provenance:
> **[vendor]** = published by Fosi, **[press]** = reviewer/press reporting,
> **[inferred]** = our reasoning from platform knowledge, **[hypothesis]** = to be tested,
> **[measured]** = verified here on the bench (none yet).
> This note is deliberately **not** listed in the [field notes index](README.md) until the
> first measurement lands.

**Device:** Fosi Audio S3 — Balanced HiFi Streamer / DAC / Preamp.
AK4493SEQ DAC, Wi-Fi 6, Bluetooth 5.3, XLR balanced + RCA + optical + sub out + HDMI eARC,
AirPlay 2 / Google Cast / DLNA / Spotify Connect / TIDAL Connect.

**Why we care:** it is a candidate for an MBXHub *charm* — a local-network control page served
by the hub, driving the device directly over its own interfaces with no vendor cloud and no
vendor app. The WiiM notes in this directory are the template for what "owned" looks like.

---

## 0. The yardstick — what a LinkPlay device looks like from outside **[measured]**

Before the S3 arrived, the read-only probe was written and run against two known LinkPlay
devices (a WiiM Ultra and a WiiM Sound Lite) to validate the tooling and, more usefully, to
establish what "this is a stock LinkPlay box" looks like from the network. Measured
2026-08-15, both devices identical in profile:

| Probe | Result on stock LinkPlay firmware |
| --- | --- |
| TCP **80** | **closed** |
| TCP 443, 8443 | open (HTTPS, self-signed) |
| TCP 8819 | open — LinkPlay's private/app port |
| TCP 49152 | open — UPnP device description |
| TCP 8008 / 8009 | open — Google Cast |
| TCP 22, 23, 5000, 8080, 8888, 1400, 7000 | closed |
| `http://<ip>/httpapi.asp?command=getStatusEx` | **no answer** (connection refused — there is no :80) |
| `https://<ip>/httpapi.asp?command=getStatusEx` | **HTTP 200**, full status JSON |
| UPnP description | `http://<ip>:49152/description.xml` |
| UPnP services | `AVTransport:1`, `ConnectionManager:1`, `RenderingControl:1`, plus vendor `urn:schemas-wiimu-com:service:PlayQueue:1` and `urn:schemas-tencent-com:service:QPlay:1` |
| `manufacturer` in the description XML | `Linkplay Technology Inc.` |

**This gives us a one-glance discriminator for the S3.** Fosi's own documentation puts
`settings.fcgi` on **plain HTTP at the device IP** — and stock LinkPlay firmware does not serve
port 80 at all. So:

- **Port 80 open, 8819 closed, `manufacturer` not Linkplay** → hypothesis **B**: Fosi replaced the
  application layer. The WiiM reference does not transfer; the charm is built on UPnP + whatever
  `settings.fcgi` exposes.
- **8819 open and `httpapi.asp` answering over HTTPS** → hypothesis **A**: it is a LinkPlay box
  wearing a Fosi app, and most of [wiim-http-api-reference.md](wiim-http-api-reference.md) applies.
- **Both** (80 open *and* 8819 open) → the most interesting outcome: a Fosi layer added *over* a
  live LinkPlay stack, meaning two independent control surfaces, one of them undocumented.

Note also what the baseline says about the plane ranking below: **every** LinkPlay device already
exposes `AVTransport` + `RenderingControl` on :49152. If the S3 does the same — and vendor-confirmed
DLNA says it should — then a UPnP-based charm works regardless of which hypothesis wins. That is the
argument for building the renderer charm first and treating any vendor API as an enhancement.

---

## 1. The platform (this is the whole story)

| Fact | Provenance |
| --- | --- |
| SoC is an **Amlogic A113X**, integrated in the **LinkPlay Stream1832AE** module | [press] TNT-Audio teardown/review |
| DAC AK4493SEQ, op-amps OPA1612, ADC Burr-Brown PCM1894 | [press] |
| Streaming stack: AirPlay 2, Google Cast, DLNA, Spotify Connect, TIDAL Connect; Qobuz Connect in progress; **Roon Ready not yet certified** ("connect via AirPlay 2 or Google Cast") | [vendor] product page |
| Network: 802.11 a/b/g/n/ac/**ax** dual-band + **10/100M Ethernet**; BT 5.3 with A2DP/AVRCP/BTLE, SBC + AAC only | [vendor] |
| Sample-rate ceiling differs by transport: **768 kHz over Ethernet, 384 kHz over Wi-Fi** | [vendor] |
| **The Fosi Audio app is *not* based on LinkPlay** — Fosi says so explicitly | [vendor] app statement |

**The central tension, and the thing the whole investigation turns on:**
the *hardware* is a LinkPlay module — the same family that gives WiiM, Arylic and a hundred
white-label brands the `httpapi.asp` command surface we already documented in
[wiim-http-api-reference.md](wiim-http-api-reference.md). But the *app* is Fosi's own, not the
LinkPlay app. Two possibilities, and they lead to completely different charms:

- **[hypothesis A] Fosi built their app on top of a stock-ish LinkPlay firmware.**
  Then `http(s)://<device-ip>/httpapi.asp?command=getStatusEx` answers, and our entire WiiM
  reference is ~80% reusable on day one. Cheapest possible charm.
- **[hypothesis B] Fosi replaced the application layer on the module (own Linux userland on
  the A113X), keeping only the SoC/radio.** Then `httpapi.asp` is gone and control has to come
  from the standard planes (UPnP/DLNA, Google Cast) plus whatever their own web surface exposes.

There is one hard piece of evidence already pointing at **B**, below.

---

## 2. Known local HTTP surface — `settings.fcgi`

**[vendor]** Fosi's own firmware-update instructions tell the user to:

1. open the Fosi Audio app → *My Device* → note the device's IP address;
2. in a browser on the same network, go to `http://<device-ip>/settings.fcgi`;
3. in the resulting **debugging interface**, use the *Firmware Update* → *File* control to
   upload the region-appropriate `image.XXX.swu` file.

Three deductions worth writing down:

- **[inferred] There is a real HTTP server on port 80 with FastCGI handlers.** `.fcgi` is not a
  LinkPlay convention — WiiM/LinkPlay devices answer on `httpapi.asp` and (on newer firmware)
  are HTTPS-only. A `settings.fcgi` served over plain HTTP is a *different* web stack. This is
  the strongest single argument for hypothesis B.
- **[inferred] `.swu` means SWUpdate**, the standard embedded-Linux OTA framework. SWUpdate ships
  its own web front-end and a documented upload endpoint. If Fosi is using it more or less stock,
  the update surface has known shapes to look for (and, notably, SWUpdate's own server commonly
  sits on **:8080** — worth probing separately from :80).
- **[inferred] "Debugging interface" is Fosi's own word for it.** Pages that call themselves
  debug interfaces usually have neighbours. Enumerating what else that server routes is the single
  highest-value hour of bench time on this device.

**[hypothesis]** A sibling product (Fosi DS3) is documented with a "Firmware Update & Control
Interface" walkthrough — if the DS3 shares the web stack, its interface is a preview of the S3's.

---

## 3. Control planes, ranked by how much of the charm they can carry

| Plane | Why it might work | Confidence | First probe |
| --- | --- | --- | --- |
| **UPnP / DLNA (AVTransport + RenderingControl)** | Vendor-confirmed DLNA support means the device is a **UPnP MediaRenderer** — a standards-defined, vendor-independent control surface: play/pause/stop/seek, SetAVTransportURI, volume, mute. We already own the discovery half (`SsdpCore`, and the hub's endpoint scan) | **high** — this is the plane most likely to carry a working charm on day one | SSDP `M-SEARCH` for `urn:schemas-upnp-org:device:MediaRenderer:1`, then fetch the `LOCATION` description XML and read the service list |
| **`settings.fcgi` / vendor web surface** | Vendor-documented to exist; local; no cloud | **high that it exists**, unknown what it exposes beyond firmware | `GET http://<ip>/settings.fcgi`, read the page, watch its own XHR traffic |
| **LinkPlay `httpapi.asp`** | The module lineage. Costs one request to test | **low-to-moderate** (the `.fcgi` evidence argues against) | `GET http(s)://<ip>/httpapi.asp?command=getStatusEx` |
| **Google Cast** | Vendor-confirmed; the workspace already has a casting lane (MBXCast) | moderate — good for *sending* audio, weaker for device control | mDNS `_googlecast._tcp` |
| **AirPlay 2** | Vendor-confirmed | low for our purposes — sender-side, not a control API we want to reimplement | mDNS `_airplay._tcp` / `_raop._tcp` |
| **Spotify / TIDAL / Qobuz Connect** | — | **out of scope** — cloud-mediated, and the charm is offline-first by rule | — |
| **Bluetooth AVRCP** | The S3 is a BT *sink*; AVRCP gives transport control from the source side | situational | pair and test transport keys |

**[inferred] The charm shape this implies.** If UPnP holds, the S3 charm looks less like the WiiM
charm (a thin skin over one rich vendor API) and more like a *renderer* charm: standards-based
transport + volume, plus whatever device-specific extras `settings.fcgi` turns out to expose
(input select, output mode, EQ). That is a different — and more reusable — piece of software:
anything that speaks UPnP MediaRenderer would inherit it.

---

## 4. Open questions (the bench agenda)

1. Does `httpapi.asp` answer at all? (One request settles hypothesis A vs B.)
2. What ports are open? Expect 80; check 443, 8080, 8443, 49152–49155 (UPnP), 8819 (LinkPlay's
   private port on WiiM hardware), 5000, 1900/udp.
3. What does `settings.fcgi` actually render, and what endpoints does its own JavaScript call?
   (Browser devtools network tab is the fastest route; a port-mirror capture is the fallback —
   the method is written up in the WiiM capture notes.)
4. Is there authentication on any of it? (A debug interface reachable unauthenticated on the LAN
   is a finding in its own right, and shapes what we're willing to ship a charm against.)
5. Does the UPnP `MediaRenderer` description advertise `AVTransport` **and** `RenderingControl`,
   or is it render-only/pull-only?
6. Can input selection (optical / HDMI eARC / BT / network) and output mode be driven from any
   local surface, or is that app-private? (On the WiiM this class of control *was* app-private in
   places — see the WiiM notes on features that exist in the app with no reachable command.)
7. EQ: the product listing advertises a **10-band EQ**, the product page's own spec text says
   **5-band**, and the development log lists ten-band as "coming soon". Which is in the firmware
   we actually receive, and is it reachable locally?
8. Volume granularity: users report the app steps volume by ±5. If the local surface allows ±1,
   that is a concrete charm win over the vendor app — the same shape of win as the
   [Windows volume steps](windows-volume-steps.md) note.

---

## 5. Day one on the bench — the read-only ladder

A scripted read-only sweep lives beside this note in [`fosi-samples/`](fosi-samples/) and has been
validated against two LinkPlay devices (it produced §0). It runs SSDP discovery, the port plan, the
`httpapi.asp` A-vs-B test, the vendor web-surface fetch, and the UPnP description parse in one pass,
saving every response verbatim:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File fosi-probe.ps1 -Ip <address>
```

The same ladder by hand, in order, if the script is not available. Every step is a GET; nothing
below changes device state:

```sh
# 0. find it (and note its MAC - the module OUI is a strong platform hint)
arp -a

# 1. what answers? (80 vs 8819 is the discriminator from section 0)
#    any port scanner, or just try the two that matter:
curl -sS -m 5 -o /dev/null -w '%{http_code}\n' http://<ip>/
curl -sS -m 5 -k -o /dev/null -w '%{http_code}\n' https://<ip>/

# 2. hypothesis A vs B - one request settles it
curl -sS -m 6 -k "https://<ip>/httpapi.asp?command=getStatusEx"
curl -sS -m 6    "http://<ip>/httpapi.asp?command=getStatusEx"

# 3. the vendor surface Fosi documents
curl -sS -m 6 "http://<ip>/settings.fcgi"

# 4. UPnP: the description tells you the services, the services tell you the charm
curl -sS -m 6 "http://<ip>:49152/description.xml"
```

Then, and only then, open `settings.fcgi` in a browser with devtools on the network tab — the
page's own XHR calls are the fastest route to whatever endpoints Fosi did not document. A
port-mirror packet capture is the fallback if the page turns out to talk over something other
than plain HTTP.

**Do not** exercise the firmware-update control while exploring. It is the one thing on that page
that can brick the unit.

---

## 6. Rules carried over from the WiiM work

These are hard-won and apply to any LinkPlay-lineage or embedded-Linux streamer, so they govern
this investigation from the start:

- **Success is read-back, never the reply.** LinkPlay-family devices answer `OK` to any parseable
  command, including no-ops and invented verbs. Three false OKs were measured in a single day on
  the WiiM. **No command is "verified" here until a getter shows the changed state**, and the
  device is restored afterwards.
- **Snapshot → test → restore** for every write probe.
- **Read-only first.** Complete the whole read sweep before sending a single write.
- **Never flash firmware to explore.** `settings.fcgi` is an update surface; we read it, we do not
  exercise it.
- **Label the unknowns.** "No command found" ≠ "impossible" — it means our search failed, and the
  capture rig is the next step, not a conclusion.

---

## 7. Charm prerequisites already in place

- `POST /api/proxy` — the hub's LAN proxy (browser→device CORS bypass), private targets only, and
  since the WiiM work it accepts **https** targets with per-request certificate handling. If the S3
  is plain-HTTP on :80, no proxy change is needed at all.
- SSDP discovery (`SsdpCore`) and the hub's endpoint scan already find and name UPnP devices on the
  LAN — the S3 should appear there the moment it joins the network, which is itself probe #0.
- The charm pattern (JSON manifest + full console page, proxy-only I/O, poll on an interval,
  explicit UNREACHABLE state) is established; the S3 charm follows it.

---

## 8. Status log

| Date | Event |
| --- | --- |
| 2026-08-15 | Note opened. Desk research only. Device **not seen on the bench LAN** (ARP sweep shows no new LinkPlay-OUI host beyond the two known WiiM units) — nothing probed yet. |
| 2026-08-15 | Unit confirmed inbound, not yet on hand. Read-only probe written and **validated against two LinkPlay devices**, producing the §0 baseline. Day-one ladder written. Waiting on hardware. |

---

## Sources

- Fosi Audio — [S3 product page](https://fosiaudio.com/products/s3-balanced-hifi-streamer) (specs, protocol list, Roon status, `settings.fcgi` update procedure)
- Fosi Audio — [Introducing the S3](https://fosiaudio.com/blogs/news/introducing-the-s3-our-first-hifi-music-streamer)
- Fosi Audio — [Official statement on the S3 app experience](https://fosiaudio.com/blogs/news/official-statement-on-the-fosi-audio-s3-app-experience) (app is not LinkPlay-based; Qobuz Connect, 10-band EQ roadmap)
- Fosi Audio Community — [S3 Streamer Development Log](https://community.fosiaudio.com/threads/s3-streamer-development-log.5521/) (DLNA/SMB behaviour, ten-band EQ "coming soon", volume-step complaints, Roon timeline, Home Assistant deprioritised)
- TNT-Audio — [Fosi S3 review](https://www.tnt-audio.com/sorgenti/fosi_s3_e.html) (Amlogic A113X in LinkPlay Stream1832AE module)
- Darko.Audio — [Fosi Audio enters the streaming age with the S3](https://darko.audio/2026/03/fosi-audio-enters-the-streaming-age-with-the-s3/)
- audioXpress — [Fosi Audio introduces S3](https://audioxpress.com/news/fosi-audio-introduces-s3-high-resolution-streamer-dac-and-preamp)
- LinkPlay `httpapi.asp` community documentation — [AndersFluur/LinkPlayApi](https://github.com/AndersFluur/LinkPlayApi), [n4archive/LinkPlayAPI](https://github.com/n4archive/LinkPlayAPI), [Arylic HTTP API](https://developer.arylic.com/httpapi/)
- SWUpdate (`.swu` format) — [swupdate.org](https://sbabic.github.io/swupdate/)
