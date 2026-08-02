# mbxhub-mcp

A minimal MCP (Model Context Protocol) stdio server that lets AI agents control
MusicBee through [MBXHub](https://mbxhub.com). It contains **no MusicBee code** —
every tool is one HTTP call to a running Hub. The Hub's own `/llms.txt` is the
API contract this adapter was built against.

One file (`index.ts`), one dependency (the official MCP SDK), one env var
(`MBXHUB_URL`).

## Requirements

- Node.js >= 22.18 (runs the TypeScript directly via native type stripping — no build step)
- MusicBee running with the MBXHub plugin enabled (default: `http://localhost:8080`)

## Setup

```
npm install
```

Register with Claude Code:

```
claude mcp add mbxhub --env MBXHUB_URL=http://localhost:8080 -- node C:\Users\HARO\source\repos\MBX\restfulbee\mcp\index.ts
```

Or for Claude Desktop, add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "mbxhub": {
      "command": "node",
      "args": ["C:\\Users\\HARO\\source\\repos\\MBX\\restfulbee\\mcp\\index.ts"],
      "env": { "MBXHUB_URL": "http://localhost:8080" }
    }
  }
}
```

## Tools

| Tool                                    | Hub endpoint                                                                                                                                            |
| --------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `search_library(query, limit?)`         | `GET /search?q=…` — supports the query DSL (`artist:Miles Davis`, `year:1985..1990`, `rating:>=4`, `OR`, `-exclusion`); results include full file paths |
| `now_playing()`                         | `GET /nowplaying`                                                                                                                                       |
| `player(action)`                        | `POST /player/play` · `pause` · `stop` · `next` · `previous`                                                                                            |
| `list_playlists()`                      | `GET /playlists`                                                                                                                                        |
| `create_playlist(name, files[])`        | `POST /playlists`                                                                                                                                       |
| `add_to_playlist(playlistUrl, files[])` | `POST /playlists/{url}/files`                                                                                                                           |
| `play_playlist(playlistUrl)`            | `POST /playlists/{url}/play`                                                                                                                            |

The agent loop: `search_library` returns full file paths → collect them →
`create_playlist` / `add_to_playlist` → `play_playlist`. Playlist identifiers
are the `url` values returned by `list_playlists`.

## Behavior notes

- Hub JSON responses are returned verbatim as tool results.
- HTTP errors surface the Hub's error body so the agent can self-correct
  (e.g. `422 INVALID_DSL` includes parse position and suggestions).
- If the Hub is unreachable, the error says so and points at `MBXHUB_URL`.
- All logging goes to stderr (one line per Hub call with status and latency);
  stdout is reserved for the MCP transport.

## Regenerating

If the Hub API evolves, this shim is disposable: re-fetch `/llms.txt` from your
Hub and ask a coding agent to rebuild it. See `LUCKY-CHARMS.md` and
[mbxhub.com](https://mbxhub.com) for the philosophy.
