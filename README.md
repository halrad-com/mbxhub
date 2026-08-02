# MBXHub

**Turn MusicBee into a network-accessible music service.**

MBXHub is a MusicBee plugin — paired with a lightweight Windows companion — that exposes your library and playback over a clean local **HTTP + WebSocket API**. Any device on your network can search, browse, control, and stream your music: web browsers, phones, scripts, home-automation, and companion apps all talk to the same hub.

> **Prerelease — v0.5.4.5 (release candidate).** MBXHub is under active development. Expect capability and polish; not stability guarantees.

---

## What this repository is

This is the **public face** of MBXHub — releases and selected companion works, curated for the open.

- **Releases** — packaged bundles under [`publish/`](publish/), ready to download and install.
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

MBXHub serves its own live documentation. With a hub running (default port `8080`):

| Endpoint | What |
| --- | --- |
| `http://<hub>:8080/docs` | Full REST API reference |
| `http://<hub>:8080/changelog` | Release notes |
| `http://<hub>:8080/llms.txt` | Machine-readable API summary |

Project site: **mbxhub.com**

---

## Samples & Extensions

Companion projects here consume the **public MBXHub API**. Two naming lanes keep the ecosystem honest:

- **`MBXHub.<Role>`** — clients of the hub contract (e.g. `MBXHub.GameBar`). Backend-neutral: they speak REST/WebSocket, not MusicBee directly.
- **`MBXHub.<Backend>`** — source adapters (`MBXHub.MusicBee` today). MusicBee is adapter #1, not the ceiling.

**In progress:**

- 🎮 **Game Bar Widget** (`MBXHub.GameBar`) — control MusicBee from the Xbox Game Bar overlay without leaving your game. A thin client of the hub's REST/WebSocket API.

Every sample is **self-contained and offline-first** — no CDNs, no external services — unless the purpose of the integration is to provide a bridge to online services.

---

## About

MBXHub is built by **HALRAD LLC**.

© 2026 HALRAD LLC · haro@halrad.com
