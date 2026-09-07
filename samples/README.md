# MBXHub Samples

Examples that build on MBXHub's public interfaces. See each folder's `README.md` / `PROMPT.md` for setup, toolchains, dependencies and known limitations.

**Read [the integration guide](../MBXHUB-SDK/integration-guide.md) first**, then run the sample that
matches your shape. `hello-page`, `hello-charm` and `hello-endpoint` introduce the three `kind`
values, which decide how MBXHub reaches you. Their installation and credential flows differ;
the file-installed page does not collect a ticket. Use the [SDK index](../MBXHUB-SDK/MBXHub-SDK.md)
and [SDK reference](../MBXHUB-SDK/charms-sdk.md) for the full contract and implementation limits.

| Sample | Language | What it shows |
| --- | --- | --- |
| [`hello-charm/`](hello-charm/) | C# (.NET 8) | **`proc`** — the whole registration path: find the hub, register a manifest, wait for a person to approve it, receive a ticket, make gated calls, receive events over the hub's WebSocket, and be launched from inside MusicBee. |
| [`hello-endpoint/`](hello-endpoint/) | C# (.NET 8) | **`endpoint`** — declare an address and let MBXHub POST your activations to it. Nothing held open, no ticket collected, and the rule it exists to show: registration is loopback-only, the address you declare need not be. |
| [`hello-page/`](hello-page/) | HTML / JS | **`page`** — the cheapest charm there is: two files, no process, no registration, no ticket. A hub-served page is same-origin with the API, so the open surface needs no credential. Also: how an activation reaches a page, and how to find the folder the two files go in. |
| [`hello-app/`](hello-app/) | C# (.NET 8) | **`proc`, with a window** — a charm the hub starts and you can see. Also the other direction: it asks the Shell to start MusicBee, minimized, which is what makes an integration feel seamless. |
| [`hello-spout/`](hello-spout/) | C++ / CMake | **Video.** Sends through the directly linked Spout SDK and receives through `mbxspout.dll`. Needs MSVC, the sibling **mbxspout** checkout and its built DLL; see its README for setup. |
| [`dynaport/`](dynaport/) | HTML / JS | A browser control that talks to the hub's REST API. |
| [`lms-dstm/`](lms-dstm/) | Perl | A [Lyrion Music Server](https://lyrion.org/) (formerly Logitech Media Server) plugin that plays MusicBee through MBXHub. |
| [`mcp/`](mcp/) | TypeScript | A Model Context Protocol server exposing MBXHub to AI assistants. |
| [`playlist-charm/`](playlist-charm/) | Prompt | A prompt recipe for building playlists through the API. |
| [`library-tree/`](library-tree/) | Prompt | A prompt recipe for a lazy-loading three-level library tree page (charm). |

Hub integration uses public REST / WebSocket interfaces; Spout video uses native interfaces as
described above. None require MBXHub's core source.
