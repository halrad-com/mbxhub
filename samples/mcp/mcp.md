# MBXHub + MCP: Give AI Agents Control of MusicBee

Example MCP source files result of the prompt below here: [musicbee-mbxhub-mcp.zip](https://mbxhub.com/downloads/examples/musicbee-mbxhub-mcp.zip)



## Getting MBXHub

Everything on this page assumes a running MBXHub — a plugin for
[MusicBee](https://getmusicbee.com/) (3.x or later, Windows). Get it from the
[download page](https://mbxhub.com/download.html). Setup is a couple of minutes:

1. **Download & unzip** the latest bundle.
2. **Copy the DLL and EXE files** to your MusicBee `Plugins\` folder
   (default: `C:\Program Files (x86)\MusicBee\Plugins\`).
3. **Start MusicBee** — MBXHub loads automatically.
4. **Allow network access**: open Tools → MBXHub Settings and use the
   Firewall section, or open the port in Windows Firewall yourself.
5. **Enjoy**: browse to `http://localhost:8080` for the dashboard, or
   `http://localhost:8080/llms.txt` for the API reference this page builds on.

## The bridge

MBXHub is a MusicBee plugin that exposes the player and library as a REST API —
transport controls, library search, playlists, now-playing, WebSocket events.
It also self-documents for LLMs: a running Hub serves its complete API reference
at `/llms.txt`, written specifically for machine consumption.

That means AI agents can already drive MusicBee two ways:

1. **Directly over HTTP** — coding agents like Claude Code can read
   [`llms.txt`](https://mbxhub.com/llms.txt) and call the API with plain HTTP.
   No adapter needed.
2. **Through a thin MCP server** — for MCP-only clients (Claude Desktop and
   similar), wrap the Hub in a small stdio adapter. This page shows how.

The key architectural point: **the adapter contains no MusicBee code.** MBXHub
already solved the hard part (the plugin, the MusicBee API, threading, search,
playlists). The MCP layer is ~100 lines of glue — one HTTP call per tool.

## The minimal adapter

One file, stdio transport, official MCP SDK, one env var (`MBXHUB_URL`).

| MCP tool                         | MBXHub endpoint                                                                                    |
| -------------------------------- | -------------------------------------------------------------------------------------------------- |
| `search_library(query)`          | `GET /search?q=…` — supports the query DSL (`artist:Miles Davis`); results include full file paths |
| `now_playing()`                  | `GET /nowplaying`                                                                                  |
| `player(action)`                 | `POST /player/play` · `pause` · `stop` · `next` · `previous`                                       |
| `list_playlists()`               | `GET /playlists`                                                                                   |
| `create_playlist(name, files[])` | `POST /playlists` with `{"name":"…","files":[…]}`                                                  |
| `add_to_playlist(url, files[])`  | `POST /playlists/{url}/files` with `{"urls":[…]}`                                                  |
| `play_playlist(url)`             | `POST /playlists/{url}/play`                                                                       |

Seven tools cover the whole "agent DJ" loop: find music, inspect state,
control playback, build playlists.

## Build it with one prompt

Because the Hub publishes its own API contract at `/llms.txt`, you don't write
the adapter — you ask an agent to. Paste this into Claude Code (or any coding
agent), fill in your Hub's address, and it builds the server against the real
documentation instead of guessing:

```text
Build a minimal MCP server that lets AI agents control MusicBee through MBXHub
(the MusicBee plugin that exposes a REST API — https://mbxhub.com).

1. Fetch http://<my-hub-host:port>/llms.txt — that is MBXHub's complete,
   canonical API documentation, written for LLM consumption. Use it as the
   source of truth for every endpoint, request body, and response shape.
   (Also published at https://mbxhub.com/llms.txt.)

2. Create a single-file stdio MCP server (official MCP SDK, TypeScript or
   Python — pick one) with these tools, each a thin HTTP call to the Hub:
   - search_library(query) → GET /search?q=  (the query DSL is documented in
     llms.txt; results include full file paths)
   - now_playing() → GET /nowplaying
   - player(action: play|pause|stop|next|previous) → POST /player/{action}
   - list_playlists() → GET /playlists
   - create_playlist(name, files[]) → POST /playlists
   - add_to_playlist(playlistUrl, files[]) → POST /playlists/{url}/files
   - play_playlist(playlistUrl) → POST /playlists/{url}/play

3. Read the Hub base URL from the MBXHUB_URL environment variable.
   Return the Hub's JSON responses as tool results. Surface HTTP errors
   with the Hub's error body so the agent can self-correct.

4. Give me the `claude mcp add` command to register it.

Keep it to one file plus a manifest. No other dependencies beyond the MCP SDK.
```

## Example: a Last.fm top-tracks playlist

With the adapter registered alongside a Last.fm MCP server, an agent can
compose them — no new code anywhere:

1. Fetch your most-played tracks from Last.fm (via the Last.fm MCP).
2. For each track, call `search_library("artist:… title:…")` on the Hub.
3. Keep the matches — each result carries the full file path.
4. `create_playlist("Last.fm Top Tracks", files)` with the collected paths.
5. Optionally `play_playlist(...)`.

The agent handles fuzzy cases (remasters, live versions, missing tracks) by
reasoning over the search results — that's the part that used to require
bespoke matching code, and now it's just the agent's job.

## Why this shape

- **The Hub is the durable bridge.** Plugin constraints, the MusicBee API,
  search, and playlist plumbing live in one maintained place.
- **The MCP layer is disposable.** It's a protocol shim. If MCP evolves, you
  regenerate the shim from the same `llms.txt` in minutes.
- **Everything stays local.** The Hub and the adapter run on your machine;
  no cloud dependency between the agent and your library.

---

MBXHub: [mbxhub.com](https://mbxhub.com) · API docs: [/docs](https://mbxhub.com/docs.html) · LLM reference: [/llms.txt](https://mbxhub.com/llms.txt)
