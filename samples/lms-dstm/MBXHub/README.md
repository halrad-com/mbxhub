# MBXHub plugin for Lyrion Music Server (LMS)

A basic LMS plugin (Perl) that turns a running [MBXHub](https://mbxhub.com)
into a music source for every Squeezebox / squeezelite player in the house,
plus a **"Don't Stop the Music"** provider backed by MBXHub's saved AutoQ
stations. Built against the Hub's own API reference
([`/llms.txt`](https://mbxhub.com/llms.txt)); contains no MusicBee code and
no mood math.

Companion to the worked example at
`deploy/mbxhub.com/downloads/examples/lms-dstm-prompt.md`.

## What it does

- **MBXHub Library** menu (Home / My Apps):
  - **Albums** — `GET /library/albums/detailed` (one call; artwork via each
    album's `firstTrackUrl`)
  - **Artists** — `GET /library/artists`, drilldown via
    `GET /library/albums/by-artist?artist=…` (ArtistPeople — featured and
    guest credits surface too)
  - **Search** — `GET /search?q=…&types=tracks&dsl=true`; the Hub's full
    query DSL passes straight through (`artist:miles year:1958..1965`)
  - **Playlists** — `GET /playlists` + `GET /playlists/{url}/files`.
    Journeys saved as playlists in MBXHub appear here with zero plugin code.
  - **Stations** — `GET /autoq/stations`, **read-only**. Picking one stores
    the station id per player for the DSTM provider. No create / rename /
    delete from the plugin.
- **DSTM provider "AutoQ (MBXHub)"** (Settings → Player → Don't Stop the
  Music): when the queue runs dry, the plugin fetches the picked station's
  record (`GET /autoq/stations/{id}`) and POSTs its `seedUrls` + `flow` to
  `/autoq/radio/generate` (`count: 10`), returning the picks as stream URLs.
  Stateless on the Hub — never touches MusicBee's queue. No station picked →
  no tracks, gracefully.
- Every track plays via `GET /stream/{URL-encoded absolute path}` — the Hub
  supports HTTP range requests, so seeking works.

**Deliberate boundary:** read / stream / ask only. The plugin never controls
MusicBee playback and never mirrors LMS transport state back to the Hub.

## Install (manual)

1. Copy the `MBXHub/` folder into your LMS `Plugins/` directory
   (e.g. `/var/lib/squeezeboxserver/Plugins/` or
   `C:\ProgramData\Squeezebox\Plugins\`), then restart LMS.
2. LMS Settings → Plugins → confirm **MBXHub Library** is enabled.
3. Settings → Advanced → **MBXHub Library**: enter the Hub's host and port
   (manual entry — no discovery). Username/password only if the Hub sits
   behind HTTP Basic Auth.
4. Browse: Home → **MBXHub Library**.
5. For the hero feature: **MBXHub Library → Stations** → pick a station on
   the player, then Settings → Player → **Don't Stop the Music** →
   **AutoQ (MBXHub)**.

## Install (repository)

`repo.xml` is a ready-to-fill third-party repository manifest: zip the
`MBXHub/` folder (the zip's top-level entry must be `MBXHub/`), host it,
fill in `<url>` and `<sha>` (SHA1 of the zip), and add the repo.xml URL under
LMS Settings → Plugins → Additional Repositories.

## Files

```
MBXHub/
├── install.xml     LMS plugin manifest
├── Plugin.pm       menus, drilldowns, search, stations, DSTM provider
├── API.pm          async REST client (timeouts, Basic Auth, JSON envelope)
├── Settings.pm     web settings page (host/port/username/password)
├── strings.txt     EN string tokens
└── HTML/EN/plugins/MBXHub/settings/basic.html
repo.xml            third-party repository manifest template
```

## Behavior notes

- All calls are LAN HTTP with a 10-second timeout; failures log a warning
  and render an empty list / return no DSTM tracks rather than hanging the
  player.
- Basic Auth is sent as an `Authorization` header on API calls, and embedded
  as `user:pass@host` in stream/artwork URLs because players fetch those
  directly. Plain HTTP on a trusted LAN — same trust model as the Hub itself.
- Track `duration` from the Hub is milliseconds; the plugin converts to
  seconds for LMS.
- Lists are fetched in one shot (artists 10000, albums 5000, tracks 1000 per
  list, search 100) — a basic plugin, no pagination.
- If players stop streaming mid-party, that's MBXHub's party gate
  (`GET /stream/*` returns 404 while a party is active unless
  `partyAllowStreaming` is on), not the plugin.
- AutoQ must be enabled on the Hub for the DSTM provider
  (`/autoq/radio/generate` returns 409 `AUTOQ_DISABLED` otherwise — the
  plugin logs it and returns no tracks); the browse/stream half works
  regardless.
