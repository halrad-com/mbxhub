# MBXHub + Lyrion (LMS): MusicBee on Every Squeezebox, with AutoQ Picking What Plays Next

Example LMS plugin source files result of the prompt below here: [musicbee-mbxhub-lms-dstm.zip](https://mbxhub.com/downloads/examples/musicbee-mbxhub-lms-dstm.zip)

A prompt-style worked example (like the
[playlist charm](../playlist-charm/PROMPT.md)): everything an AI coding agent
needs to build a Lyrion Music Server plugin against a running MBXHub. Two
pieces, one plugin:

1. **A music source** — browse, search, and stream the MusicBee library on
   every Squeezebox / squeezelite player in the house.
2. **The hero: a "Don't Stop the Music" provider** — pick one of your saved
   AutoQ stations on the player and go: when the queue runs dry, AutoQ
   decides what plays next, programmed by the engine running inside
   MusicBee. Stations are exposed read-only; you build and train them in
   MBXHub.

The plugin contains no MusicBee code and no mood math. MBXHub already solved
the hard parts; the LMS plugin is a thin REST client.

## Getting MBXHub

Everything on this page assumes a running MBXHub — a plugin for
[MusicBee](https://getmusicbee.com/) (3.5 or later, Windows). Get it from the
[download page](https://mbxhub.com/download.html). Setup is a couple of minutes: 

1. Download-install MusicBee 3.7 recomended.
   1. Get 3.6 [Downloads](https://getmusicbee.com/downloads/)
   2. 3.7 patch https://getmusicbee.com/patches/MusicBee37_Patched.zip
2. Download & unzip** the latest bundle.
3.  https://mbxhub.com/downloads/rc/MBX-bundle-latest.zip
4. **Copy the DLL and EXE files** to your MusicBee `Plugins\` folder
   (default: `C:\Program Files (x86)\MusicBee\Plugins\`).
   
   (portable MusicBee\Plugins)
5. **Start MusicBee** — MBXHub loads automatically.
6. **Allow network access**: open Tools → MBXHub Settings and use the
   Firewall section, or open the port in Windows Firewall yourself.
7. **Enjoy**: browse to `http://localhost:8080` for the dashboard, or
   `http://localhost:8080/llms.txt` for the API reference this page builds on.

You also need a running [Lyrion Music Server](https://lyrion.org/) (the
community continuation of Logitech Media Server) with at least one player —
Squeezebox hardware or [squeezelite](https://github.com/ralph-irving/squeezelite)
software.

## The architecture

```
Squeezebox players ──► LMS (Perl plugin) ──► MBXHub REST ──► MusicBee library
                            │                       │
                            └── DSTM hook ──► AutoQ engine (moods, stations,
                                              reactions, candidate filter)
```

One deliberate boundary: **transport state is never bridged.** MusicBee stays
the curator and brain; LMS players own their own play queues. The plugin only
ever *reads* library data, *streams* audio, and *asks* AutoQ for picks.

## The endpoint map — same base set as the MCP example, plus stations

| Plugin concern                        | MBXHub endpoint                                                                                                                                            |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Browse albums / drill down            | `GET /library/albums`, `GET /library/files?album=…&people=…`                                                                                               |
| Search (full query DSL)               | `GET /search?q=artist:Miles%20Davis&types=tracks&dsl=true`                                                                                                 |
| Stream a track                        | `GET /stream/{URL-encoded absolute path}` (HTTP range supported — seeking works)                                                                           |
| Artwork                               | `GET /library/file/{URL-encoded absolute path}/artwork` (binary)                                                                                          |
| Playlists (list / contents)           | `GET /playlists`, `GET /playlists/{url}/files`                                                                                                             |
| Saved stations (read-only list)       | `GET /autoq/stations`                                                                                                                                      |
| Station detail (seeds + flow)         | `GET /autoq/stations/{id}`                                                                                                                                 |
| **DSTM picks for the chosen station** | `POST /autoq/radio/generate` with `{"seedUrls": <station seeds>, "flow": <station flow>, "count":10}` — stateless, never touches MusicBee's queue or radio |

Stations are **exposed, not edited**: the plugin lists them, you pick one on
the player, and that's it. When LMS asks for more music, the plugin fetches
the chosen station's seeds and calls `/autoq/radio/generate` — AutoQ's funnel,
taste model, quotas, and the operator's candidate filter all apply
server-side, and the plugin just forwards `/stream/…` URLs. Creating,
renaming, and training stations stays in MusicBee/MBXHub where it belongs.

**Journeys don't need plugin code at all**: build a journey in MBXHub (browse
tray or the AutoQ Workbench), *Save as playlist* — it appears in the plugin's
Playlists menu and plays on any Squeezebox like any other playlist.

## Build it with one prompt

Paste this to Claude (or any coding agent that can fetch a URL):

> Read https://mbxhub.com/llms.txt — it is the complete API reference for
> MBXHub, a REST layer over a MusicBee library.
> 
> Build a basic Lyrion Music Server (LMS, formerly Logitech Media Server)
> plugin in Perl called `MBXHub`. Crib the plugin skeleton, install.xml, and
> repository conventions from a current open-source LMS source plugin. Keep
> it to the base REST calls plus read-only stations:
> 
> 1. A browse/search/stream music source: top-level menu "MBXHub Library"
>    with Albums (GET /library/albums), Artists drilldown, Search wired to
>    GET /search?q=…&types=tracks&dsl=true, and Playlists (GET /playlists,
>    tracks via GET /playlists/{url}/files). Every track plays via
>    GET /stream/{URL-encoded absolute file path} on the Hub (range requests
>    are supported, so seeking works). Show artwork via the artwork endpoint.
> 
> 2. A "Stations" menu + a "Don't Stop the Music" provider named
>    "AutoQ (MBXHub)": list saved stations from GET /autoq/stations
>    (READ-ONLY — no create, rename, or delete from the plugin). The user
>    picks a station; when LMS asks for more tracks, fetch that station's
>    record from GET /autoq/stations/{id} and POST its seedUrls + flow to
>    /autoq/radio/generate as {"seedUrls":[…], "flow":"…", "count":10},
>    returning the picks as stream URLs. If no station is picked, do nothing
>    gracefully.
> 
> Plugin settings page: MBXHub host and port (no discovery — manual entry),
> optional HTTP Basic Auth username/password applied to every request.
> All calls are LAN HTTP with short timeouts and graceful failure (log and
> return empty rather than hanging the player). Do not attempt to control
> MusicBee playback or mirror LMS transport state back to the Hub — this
> plugin is read/stream/ask-only. No journey features: journeys are built in
> MBXHub and saved as playlists, which this plugin already plays.

## What you get

- Every Squeezebox in the house plays the MusicBee library — synced zones,
  rooms with no PC.
- Pick a station and go: "Don't Stop the Music" backed by a live
  valence/arousal mood engine, instead of the long-dead MusicIP lineage
  those menus were built around.
- Journeys ride along for free: build one in MBXHub, save it as a playlist,
  play it in any room.
- The mood engine keeps improving as you use MusicBee — reactions, station
  training, and the trained model all live Hub-side, so the LMS plugin gets
  smarter without an update.

## Notes

- LMS plugins are Perl, installed via a repository XML or a zip dropped into
  the Plugins folder; the LMS web UI has the DSTM provider picker under
  Settings → Don't Stop the Music.
- MBXHub's party mode gates streaming for guests (`GET /stream/*` returns 404
  during a party unless `partyAllowStreaming` is on) — if your players stop
  mid-party, that's the gate, not the plugin.
- AutoQ must be enabled on the Hub (Settings → AutoQ) for the DSTM provider;
  the source half works regardless.
