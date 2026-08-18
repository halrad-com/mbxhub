# Telling network audio devices apart — safely

*How to work out what kind of box you just discovered, without poking it in ways you'd regret.*

**Provenance:** measured against real hardware on a home bench — a WiiM Ultra and WiiM Lite
(LinkPlay), a Fosi Audio S3 (StreamUnlimited StreamSDK), and a Devialet Phantom. Anything not
measured is labelled. IPs are written `<device-ip>`; nothing here is specific to one network.

---

## The problem

Discovery is the easy half. An SSDP sweep of a home LAN turns up a handful of devices that all
answer as **UPnP MediaRenderers** — same three services, same generic description, indistinguishable
if that is all you look at.

The hard half is that they are not interchangeable. Each family speaks its own control dialect on
top of that shared UPnP veneer, and the dialects share verb names without sharing meaning. So a
program that guesses wrong does not fail loudly — it sends a well-formed request in the wrong
language and gets a plausible-looking answer back. On at least one of these families it gets `OK`.

That is the whole reason to identify carefully: **the failure mode is silence, not an error.**

---

## The principle: identify from what you already hold

The instinct is to write a probe. Resist it for one more minute, because **discovery has usually
already fetched the answer.**

An SSDP responder points at a device description document, and any scanner worth using fetches and
parses that document to get a friendly name. That same document carries manufacturer, model URL and
the full service list — and for two of the three families below, those fields are decisive on their
own.

So the order is:

1. **Passive.** Classify from the description you already have. No new traffic, no new risk, works
   on a device that is asleep behind a firewall for everything except what it advertised.
2. **Active, only if passive was inconclusive**, only reads, and only one device at a time.

That ordering is not just politeness. Active probing *per device across a whole scan* turns a
discovery sweep into something shaped exactly like a port scan against every box on the network —
including your neighbours' if the scan ever escapes its subnet. Discovery should stay cheap and
quiet; identification is a second, deliberate step.

---

## Passive fingerprints

All measured.

### The rule that makes this work on white-label hardware

**`manufacturer` is the brand. `modelURL` is the platform.** These devices are OEM builds — the
badge on the front and the software inside come from different companies — so the field that names
the *seller* tells you nothing about the *dialect*, while the field that names the platform vendor
tells you everything.

Measured on a Fosi S3: `manufacturer` is **`Fosi Audio`**, `manufacturerURL` is `fosiaudio.com` —
and `modelURL` is **`http://www.streamunlimited.com/`**. Identify on the second one. Any other
brand shipping the same platform will look identical there and different everywhere else.

### StreamUnlimited StreamSDK — measured live

| Signal | Value |
| --- | --- |
| `modelURL` | `http://www.streamunlimited.com/` ← **decisive** |
| Device description | `:16500/[uuid].xml` (UDN as the filename) |
| SSDP `Server:` | `Linux/… UPnP/1.0 GUPnP/…` |
| Control URLs | `/Control/LibRygelRenderer/…` ← **decisive, and free**: already in the document |
| Icon URLs | `/LibRygelRenderer-120x120x24.png` — the same tell again |
| `deviceType` | `MediaRenderer:2`, with all three services at version `:2` |
| `presentationURL` | `http://<device-ip>:80/` — corroborates port 80 open |
| DLNA | `DMR-1.51` |
| Vendor API | `/api/getData?path=…` over plain HTTP |

`LibRygelRenderer` appearing in the control and icon URLs is the strongest passive tell of the lot,
because it is structural — it names the UPnP implementation, cannot be rebranded without changing
the stack, and is present in the document you already fetched.

### LinkPlay (WiiM) — measured live

| Signal | Value |
| --- | --- |
| Device description | `:49152/description.xml` |
| Service type | **`urn:schemas-wiimu-com:service:PlayQueue:1`** ← **decisive** |
| `manufacturer` | `Linkplay Technology Inc.` (model: `WiiM Ultra Receiver`) |
| Service versions | all core services at **`:1`** |
| Open ports | 443, 8443, 8819, 49152, 8008, 8009 |
| Port 80 | **closed** — discriminating on its own |
| Vendor API | `httpapi.asp`, **HTTPS only** (plain HTTP refused outright) |
| Ethernet OUI | `00:22:6C` |

`wiimu:PlayQueue` is the dependable tell — it names the platform's own service, in the document
discovery already fetched. It is the LinkPlay counterpart of StreamSDK's `LibRygelRenderer`.

### ⚠ Do not identify on the description port

`49152` looks specific. It is not — it is **the first port of the IANA dynamic/private range**
(49152–65535, RFC 6335), which is the conventional starting point for any stack that asks the OS
for "an ephemeral port". It recurs across completely unrelated UPnP implementations for that reason
alone, and a device that finds it busy will happily bind something else.

What is actually supported by measurement here:

- The WiiM Ultra served its description on `:49152` — **on this unit, on this boot**.
- Of six other SSDP responders on the same LAN, **none** used `:49152`: they were on `80`, `8080`,
  `16500`, `35707` and `50201`.
- The second LinkPlay unit could not be compared — it was offline during this pass, so
  **consistency across the family is unverified**.

So treat the port as **corroborating, never decisive**. The same caution applies to StreamSDK's
`:16500`: it is a more distinctive choice, but it is still a default, not an identity.

Identify on the **service types and control URLs** — `wiimu:PlayQueue`, `LibRygelRenderer`,
`modelURL`. Those name the implementation. A port number names a coincidence.

### Two cheap tells that fall out of comparing the two

**Service version.** LinkPlay advertises its core services at `:1`; the Rygel-based StreamSDK box
advertises them at `:2` and declares `MediaRenderer:2`. A free extra signal from a document you
already have — corroborating, not decisive on its own.

**And a caution on `manufacturer`.** On the WiiM it names the *platform* (`Linkplay Technology
Inc.`, since WiiM is LinkPlay's own brand); on the Fosi it names the *reseller* (`Fosi Audio`,
with the platform hiding in `modelURL`). So the field means different things on different boxes and
**cannot be trusted alone** — which is exactly why the structural tells (`wiimu:PlayQueue`,
`LibRygelRenderer`, `modelURL`) are the ones to identify on.

> ### ⚠ `QPlay` is NOT a LinkPlay tell — measured
>
> An earlier draft of this note listed `tencent:QPlay` as a LinkPlay fingerprint. **That is wrong,
> and it would have produced a false positive on the first device it met.** The Fosi S3 —
> StreamSDK, not LinkPlay — advertises `urn:schemas-tencent-com:service:QPlay:1`, a
> `qq:X_QPlay_SoftwareCapability` of `QPlay:2.1`, and the tencent XML namespace on its root element.
>
> QPlay is a Chinese-market streaming service integration carried by *several* audio platforms. It
> identifies a market, not a vendor. The general lesson is worth more than the specific correction:
> **a service type proves the device speaks that service, never who built the device.** Identify on
> fields that name the implementation — `modelURL`, control-URL paths — not on features that any
> platform may license.

**Devialet** does not fingerprint from UPnP the same way; it is identified by its control API
answering at all (below).

---

## The active ladder — ordered by harm, not convenience

Only if passive was inconclusive. Every one of these is a plain `GET`.

**1 — StreamSDK.** `GET http://<device-ip>/api/getData?path=settings:/system/productName`

Safest of the three, and not by accident: on this platform a wrong path returns **HTTP 500 with a
named JSON error**. The device tells you what you got wrong. A platform that answers honestly is a
platform you can probe first.

**2 — Devialet.** `GET http://<device-ip>/ipcontrol/v1/devices/current`

The documented reachability probe. Read-only, and the reply identifies the device.

**3 — LinkPlay, last.** `GET https://<device-ip>/httpapi.asp?command=getStatusEx` (self-signed
certificate; HTTPS only — plain HTTP is refused)

Last for a specific reason, below.

---

## Why LinkPlay goes last: the false-OK rule

**LinkPlay answers `OK` to any command it can parse — including no-ops, including nonsense.**
Measured three separate false-OKs in a single bench day, and the `multiroom:` prefix false-OKs *any*
sub-verb, so the behaviour is family-wide rather than per-command.

The consequence for identification is sharper than it first looks:

> **A device accepting your command is not evidence of anything.** Identification must ride on reads
> whose *content* you can inspect — a body you can parse and check — never on "it did not reject me".

This is the same rule that governs writing to these devices (see the
[WiiM HTTP API reference](wiim-http-api-reference.md)): success is read-back, never the acknowledgement.

---

## Reading a non-answer

The most common identification bug is scoring silence as a negative. It is not one.

| What came back | What it means |
| --- | --- |
| **Connection refused** | A real signal. The port is closed — LinkPlay's closed port 80 is genuinely discriminating. |
| **Timeout** | **No signal at all.** Firewall, sleeping device, wrong NIC, wrong VLAN. Never record it as "not this kind". |
| **HTTP 404** | Weak negative — a real web server, wrong path. |
| **HTTP 500 + named JSON error** | **Positive** for StreamSDK. That is its documented bad-path behaviour, not a failure. |
| **Bare `OK`** | No information on LinkPlay. See above. |
| **TLS refused / plain HTTP refused** | Informative — LinkPlay's vendor API is HTTPS-only. |

The timeout row has bitten this bench directly: a port was scored closed on a device that simply was
not answering that day, and the wrong conclusion survived into notes until it was re-measured.

### A device can be up and not yet advertising

Measured while writing this note. Seconds after a WiiM Ultra was powered on, its **vendor API
already answered** (`httpapi.asp` over HTTPS, `200`) while its **UPnP description on `:49152` was
still refusing connections**. Minutes later the same port served the description fine.

The subsystems come up at different times, and SSDP is worse: the unit did not appear in a
multicast sweep at all while it was perfectly reachable by direct address.

Two rules follow, and they are the difference between a classifier that works and one that
intermittently loses devices:

- **A refusal on one surface says nothing about the device.** During that window, probing `:49152`
  and concluding "not LinkPlay" would have been wrong on a device whose vendor API was answering at
  that exact moment.
- **Absence from a discovery sweep is not absence from the network.** Re-probe by address before
  concluding anything, and prefer the passive fingerprint from a description you have *already*
  fetched over a fresh probe against a device that may be half-awake.

This is the same lesson as the timeout row, arriving from a direction that looks like a real
negative rather than a silence — which is what makes it dangerous.

### …and it may never advertise at all

Boot order is only the transient version of the problem. **UPnP is a feature, and features can be
switched off.** A device whose owner has disabled UPnP/DLNA — or whose platform ships it off, or
firewalls it — will never serve a description, no matter how long you wait.

So a missing description has at least three causes that look identical from outside:

1. still booting,
2. **advertising disabled by configuration**,
3. genuinely not that platform.

Only the third is the one you were testing for. This is the strongest argument for the confidence
field below: **passive identification can fail permanently on a device that is definitely a
supported platform**, so "unknown" has to be a real outcome that falls through to an active probe
or to asking a human — never a quiet default to whichever family is most common.

It is also why the active ladder exists at all. Passive-first is right because it is free and safe,
not because it is sufficient.

*(Whether these specific units expose a UPnP toggle is unverified here — the point stands on the
general fact that advertising is optional.)*

---

## Never send

- **Anything that writes.** Identification is reads only. No `setPlayerCmd`, no `setData`, no
  `POST`, no `PUT`. You are talking to hardware you have not identified yet — by definition you do
  not know what the verb means there.
- **Anything near firmware or update paths.** These platforms document update endpoints
  (`/settings.fcgi`, `image.*.swu` on one of them). Never probe them, on any device, for any reason.
- **A vendor command as a liveness test.** See the false-OK rule.
- **Anything to an address that is not LAN-scoped.** A device description's `LOCATION` is
  device-controlled input: it can point anywhere, including at a public host, and a redirect can
  move it after you have validated it. Require a private IP literal, refuse redirects, and cap the
  response body.

---

## Report confidence, not a boolean

Identification is evidence, and evidence has strength. A useful result carries a verdict *and* how
it was reached:

- **certain** — a decisive passive tell (`modelURL`, or the description port plus a port check).
- **probable** — one weak tell, or an active probe that answered but without a distinctive body.
- **unknown** — nothing conclusive. This is a legitimate outcome and must be surfaceable to a human,
  not silently defaulted to whichever family is most common. **A wrong guess is worse than no
  guess**, because the wrong dialect fails quietly and a blank field does not.

---

## See also

- [Fosi Audio S3 field notes](fosi-s3-field-notes.md) — where the StreamSDK fingerprints and the
  LinkPlay-vs-StreamSDK contrast table were measured
- [WiiM HTTP API reference](wiim-http-api-reference.md) — the false-OK ladder in full
- [Devialet Phantom field notes](devialet-phantom-field-notes.md) — the `ipcontrol/v1` surface
- [First-contact probe kit](fosi-samples/) — the read-only recon sweep these fingerprints came from
