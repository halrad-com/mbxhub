# MBXHub

**The bridge to everything else.**

MBXHub transforms your media library into a network-accessible exchange service. Control or access it from any device on your local network. Integrate with other systems using standard HTTP/REST and WebSocket protocols.

A MusicBee plugin with a companion desktop Shell, MBXHub brings together a browser-based media hub, device controls, automation and an API. Browse, play, search and manage your queue from a phone, tablet or desktop — no cloud, internet connection or account required.

> **Prerelease — v0.5.x.x (release candidate).** MBXHub is under active development. The standing rule: every build should be better than the last.

---

## What this repository is

This repository contains the **[Charms SDK documentation](MBXHUB-SDK/MBXHub-SDK.md)**, **[integration examples](samples/README.md)** and companion works. SDK documentation lives in the root-level `MBXHUB-SDK/` folder; examples live in `samples/`. Hub releases live at **[mbxhub.com](https://mbxhub.com)**.

---

## Six products behind one surface

| Product | What it does |
| --- | --- |
| **Web application** | Browse, play, search and manage the queue from your phone, tablet or desktop. The Now Playing dashboard includes artwork, ratings, reactions and playback controls. |
| **AutoQ — Music Mood Classification** | Use Truedat to scan your library's musical properties. Build stations influenced by your tastes, with queue selection based on mood, reactions, audio features and diversity. |
| **Listen Here — Streamer** | Play through your normal speakers or stream selected music to the device in your hand. |
| **ARiA — Automated Remote Input** | Send keyboard and mouse input to the host, launch applications, trigger hotkeys and run macros with DuckyScript syntax. |
| **Charms — Application Host and Device Proxy** | Bring mini web apps and device controls onto the dashboard. Control volume, sources and configuration on supported WiiM and Fosi devices; Devialet devices are identified on the network. |
| **REST / WebSockets** | Integrate through the REST and MusicBee APIs, with push events for playback, queue changes, artwork and reactions. |

The **desktop Shell** adds a customizable HUD for browsing, playback and the queue, plus a system-tray application and Windows media controls through SMTC. It can target different MBXHub instances on the network and reconnect after an interruption.

Clients discover MBXHub through **SSDP / WS-Discovery**. Bring your own HTML web app to build a player UI against the API, or use Party Mode for QR-code joining, song requests and reactions.

See the [feature overview](https://mbxhub.com/features.html) and [AutoQ audio features](https://mbxhub.com/12sfaq.htm) for details.

---

## Documentation

### Getting started

Once the plugin is installed in MusicBee:

1. **Open the dashboard** — **Tools → MBXHub → Dashboard**. The Now Playing dashboard and built-in pages work right away on this PC; nothing else needed to get going.
2. **Open network access** so phones and other devices can reach it — **Tools → MBXHub → Settings → Firewall** (Step 4 of the [install guide](https://mbxhub.com/docs.html#installation)). This also confirms the **port** MBXHub is using.
3. **Connect other devices** — scan the **QR code** on the dashboard, once the firewall is open.

MBXHub uses **port 8080** by default. Ports are claimed as an **even pair** — the REST API on the even port, the Shell's own listener on the odd one above it — so a first run that finds 8080/8081 taken steps to `8082/8083`, and so on up to `8098/8099`. Port 80 is never picked automatically: it needs a URL-ACL reservation, so set it by hand for intentional production use. You can set it to **80** or any other port you like. The current port is shown in **MBXHub Settings** (Tools → MBXHub → Settings, or Preferences → Plugins → Configure).

### Live docs

MBXHub serves its own documentation. With the hub reachable at `http://<hub>/`:

| Endpoint | What |
| --- | --- |
| `http://<hub>/docs` | Full REST API reference |
| `http://<hub>/changelog` | Release notes |
| `http://<hub>/llms.txt` | Machine-readable API summary |

Project site: **[mbxhub.com](https://mbxhub.com)**

### Requirements

- Windows 10 or later
- MusicBee 3.x
- .NET Framework 4.8 for the plugin
- .NET 8.0 Desktop Runtime for the MBXHub Shell

[Download MBXHub](https://mbxhub.com/download.html).

---

## Charms SDK & Examples

**Writing an extension?** Start at the **[SDK index](MBXHUB-SDK/MBXHub-SDK.md)**.

| Documentation | What it covers |
| --- | --- |
| [Integration guide](MBXHUB-SDK/integration-guide.md) | Choose an integration kind and follow the registration flow. |
| [Charms SDK reference](MBXHUB-SDK/charms-sdk.md) | Manifest fields, capabilities, credential lifecycle and implementation limits. |
| [Browser reference](MBXHUB-SDK/charms-sdk.html) | Generated HTML version of the SDK reference. |
| [Examples](samples/README.md) | Sample selection, setup and requirements. |

For documentation changes, see [SDK maintenance](MBXHUB-SDK/charms-sdk-maintenance.md). Run `./build-charms-sdk.ps1` from this repository root to regenerate the reference, then `./build-charms-sdk.ps1 -Check` to verify it (PowerShell 7 required).

Examples live in [`samples/`](samples/): three introductory charms, one per kind — a page, a program on the machine, and a service somewhere else — plus a desktop app, Spout video, a browser control, a [Lyrion Music Server](https://lyrion.org/) plugin, an MCP server, and prompt recipes. They use MBXHub's public interfaces without the hub's source. Each README lists its requirements; the Spout sample also needs the sibling `mbxspout` checkout and its built DLL.

---

## About

MBXHub is built by **[HALRAD Research](https://halrad.com)**.

© 2026 HALRAD Research · haro@halrad.com
