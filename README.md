# MBXHub

**Turn MusicBee into a network-accessible music service.**

MBXHub is a MusicBee plugin — paired with a lightweight Windows companion — that exposes your library and playback over a clean local **HTTP + WebSocket API**. Any device on your network can search, browse, control, and stream your music: web browsers, phones, scripts, home-automation, and companion apps all talk to the same hub.

> **Prerelease — v0.5.x.x (release candidate).** MBXHub is under active development. The standing rule: every build should be better than the last.

---

## What this repository is

This is for MBXHub examples and companion works. Hub releases live at **[mbxhub.com](https://mbxhub.com)**.

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

Project site: **mbxhub.com**

---

## Samples & Extensions

**Writing an extension?** Start with the **[integration guide](docs/integration-guide.md)** — how the pieces fit, which shape your application should be, and the whole path walked end to end. The REST reference answers *what*; the guide answers *how*.

Working examples live in [`samples/`](samples/): three `hello-*` charms, one per kind — a page, a program on the machine, and a service somewhere else — plus a browser control, a [Lyrion Music Server](https://lyrion.org/) plugin, an MCP server, and prompt recipes. Each one talks to MBXHub over its public REST / WebSocket API and stands on its own; none need the hub's source.

---

## About

MBXHub is built by **HALRAD LLC**.

© 2026 HALRAD LLC · haro@halrad.com
