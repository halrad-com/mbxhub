# MBXHub Samples

Small, self-contained examples that build on the public MBXHub API. Each folder stands alone — see its own `README.md` / `PROMPT.md` for setup and usage.

**Read [the integration guide](../docs/integration-guide.md) first**, then run the sample that
matches your shape. The three `hello-*` samples are one per `kind` — the field that decides how
MBXHub reaches you. Everything else in the contract — the manifest, registration, approval, the
ticket, the capability gate — is the same for all three.

| Sample | Language | What it shows |
| --- | --- | --- |
| [`hello-charm/`](hello-charm/) | C# (.NET 8) | **`proc`** — the whole registration path: find the hub, register a manifest, wait for a person to approve it, receive a ticket, make gated calls, receive events over the hub's WebSocket, and be launched from inside MusicBee. |
| [`hello-endpoint/`](hello-endpoint/) | C# (.NET 8) | **`endpoint`** — declare an address and let MBXHub POST your activations to it. Nothing held open, no ticket collected, and the rule it exists to show: registration is loopback-only, the address you declare need not be. |
| [`hello-page/`](hello-page/) | HTML / JS | **`page`** — the cheapest charm there is: two files, no process, no registration, no ticket. A hub-served page is same-origin with the API, so the open surface needs no credential. Also: how an activation reaches a page, and how to find the folder the two files go in. |
| [`dynaport/`](dynaport/) | HTML / JS | A browser control that talks to the hub's REST API. |
| [`lms-dstm/`](lms-dstm/) | Perl | A [Lyrion Music Server](https://lyrion.org/) (formerly Logitech Media Server) plugin that plays MusicBee through MBXHub. |
| [`mcp/`](mcp/) | TypeScript | A Model Context Protocol server exposing MBXHub to AI assistants. |
| [`playlist-charm/`](playlist-charm/) | Prompt | A prompt recipe for building playlists through the API. |
| [`library-tree/`](library-tree/) | Prompt | A prompt recipe for a lazy-loading three-level library tree page (charm). |

All samples talk to MBXHub over its public REST / WebSocket API — none require the core source.
