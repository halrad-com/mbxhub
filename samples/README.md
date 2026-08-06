# MBXHub Samples

Small, self-contained examples that build on the public MBXHub API. Each folder stands alone — see its own `README.md` / `PROMPT.md` for setup and usage.

| Sample | Language | What it shows |
| --- | --- | --- |
| [`dynaport/`](dynaport/) | HTML / JS | A browser control that talks to the hub's REST API. |
| [`lms-dstm/`](lms-dstm/) | Perl | A Logitech Media Server plugin that bridges MusicBee through MBXHub. |
| [`mcp/`](mcp/) | TypeScript | A Model Context Protocol server exposing MBXHub to AI assistants. |
| [`playlist-charm/`](playlist-charm/) | Prompt | A prompt recipe for building playlists through the API. |
| [`library-tree/`](library-tree/) | Prompt | A prompt recipe for a lazy-loading three-level library tree page (charm). |

All samples talk to MBXHub over its public REST / WebSocket API — none require the core source.
