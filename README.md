# MBXHub

**Turn MusicBee into a network-accessible music service.**

MBXHub is a MusicBee plugin — paired with a lightweight Windows companion — that exposes your library and playback over a clean local **HTTP + WebSocket API**. Any device on your network can search, browse, control, and stream your music: web browsers, phones, scripts, home-automation, and companion apps all talk to the same hub.

> **Prerelease — v0.5.4.5 (release candidate).** MBXHub is under active development. Expect capability and polish; not stability guarantees.

---

## What this repository is

This is the **public face** of MBXHub — samples and companion works. Hub releases live at **[mbxhub.com](https://mbxhub.com)**.

- **Samples & extensions** — self-contained companion projects that build on the public MBXHub API (see [below](#samples--extensions)).

---

## Highlights

- 🎛 **Full playback & library control** over REST — play/pause, next/previous, volume, mute, shuffle/repeat, position, queue, ratings, and more (150+ endpoints).
- 🔎 **MusicBee-aware search** — a real query language with field operators, autocomplete, saved searches, and history.
- ⚡ **Real-time events** over WebSocket, on the same port as REST — track changes, playback state, and library updates, no polling.
- 🖥 **Built-in web dashboard** — now-playing and full control in any browser, nothing to install on the client.
- 📡 **Zero-config discovery** — hubs announce themselves on the LAN over SSDP.
- 🔊 **Listen Here** — stream the current track straight to the browser.
- 🏠 **Local-first & offline** — no cloud, no external services, no CDNs. Everything stays on your network.

---

## Documentation

### Getting started

Once the plugin is installed in MusicBee:

1. **Open the dashboard** — **Tools → MBXHub → Dashboard**. The Now Playing dashboard and built-in pages work right away on this PC; nothing else needed to get going.
2. **Open network access** so phones and other devices can reach it — **Tools → MBXHub → Settings → Firewall** (Step 4 of the [install guide](https://mbxhub.com)). This also confirms the **port** MBXHub is using.
3. **Connect other devices** — scan the **QR code** on the dashboard, once the firewall is open.

MBXHub binds **port 80** when it's free, otherwise `8080` (then `8082`, …); the current port is shown in **MBXHub Settings** (Tools → MBXHub → Settings, or Preferences → Plugins → Configure).

### Live docs

MBXHub serves its own documentation. With the hub reachable at `http://<hub>/`:

| Endpoint | What |
| --- | --- |
| `http://<hub>/docs` | Full REST API reference |
| `http://<hub>/changelog` | Release notes |
| `http://<hub>/llms.txt` | Machine-readable API summary |

Project site: **mbxhub.com**

---

## Samples & Extensions

Browse working examples in [`samples/`](samples/) — a browser control, a Logitech Media Server plugin, and an MCP server, all built on the public API.

Companion projects here consume the **public MBXHub API**. Two naming lanes keep the ecosystem honest:

- **`MBXHub.<Role>`** — clients of the hub contract (e.g. `MBXHub.GameBar`). Backend-neutral: they speak REST/WebSocket, not MusicBee directly.
- **`MBXHub.<Backend>`** — source adapters (`MBXHub.MusicBee` today). MusicBee is adapter #1, not the ceiling.

**In progress:**

- 🎮 **Game Bar Widget** (`MBXHub.GameBar`) — control MusicBee from the Xbox Game Bar overlay without leaving your game. A thin client of the hub's REST/WebSocket API.

Every sample is **self-contained and offline-first** — no CDNs, no external services — unless the purpose of a specific integration is to provide a bridge to online services.

---

## About

MBXHub is built by **HALRAD LLC**.

© 2026 HALRAD LLC · haro@halrad.com
