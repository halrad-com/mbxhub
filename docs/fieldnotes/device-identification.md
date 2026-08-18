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

| | **LinkPlay** (WiiM) | **StreamUnlimited StreamSDK** (Fosi S3) |
| --- | --- | --- |
| Device description | `:49152/description.xml` | `:16500/[uuid].xml` |
| `modelURL` | — | `http://www.streamunlimited.com/` ← **decisive** |
| SSDP `Server:` header | — | `Linux/…  UPnP/1.0 GUPnP/…` (Rygel/GUPnP stack) |
| Port 80 | **closed** | **open** |
| Port 8819 | open | closed |
| Ports 49152–49155 | 49152 open | closed |
| Vendor API | `httpapi.asp`, **HTTPS only** (plain HTTP refused) | `/api/getData?path=…` over plain HTTP |
| UPnP services | `AVTransport` + `ConnectionManager` + `RenderingControl` | same three |

Two independent tells per family, from one document plus a two-port check. **The description port
alone separates them** — 49152 versus 16500 — which is a pleasant accident of two vendors picking
different defaults, and worth confirming rather than trusting blindly on hardware you have not seen.

**Devialet** does not fingerprint from UPnP the same way; it is identified by its control API
answering at all (below).

> **Unverified, and attractive enough to be worth checking:** the LinkPlay description is believed
> to carry `manufacturer` = "Linkplay Technology Inc." and vendor service types
> (`wiimu:PlayQueue`, `tencent:QPlay`). That would be the cleanest tell of the lot. It is
> recollection from a probe run, it is **not** confirmed here, and the port/path discriminators
> above stand without it. Confirm before relying on it.

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
