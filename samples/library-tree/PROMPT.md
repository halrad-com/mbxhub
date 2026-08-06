# Worked example: Library tree page

A copy-pasteable prompt that hands a released MBXHub instance and its
public API reference (`llms.txt`) to an AI assistant and asks it to
build a brand-new feature — a whole-library browsable tree — without
modifying or rebuilding MBXHub itself.

**Why this is interesting:** the AI never sees the MBXHub source. It
reads the public `llms.txt` cheat sheet, follows the documented charm
manifest schema and the `/library/*` + `/queue/*` + `/autoq/send`
endpoints, and drops two text files into the running instance's data
directory. Reload the dashboard, the charm appears in the bar, click it,
and the entire library expands as a lazy-loading three-level tree — play
or queue anything from any row. Same path is available for any new
feature you want to bolt onto your own MBXHub.

---

## The prompt

> Read `https://mbxhub.com/llms.txt` end-to-end — pay attention to the
> **Charms** section (manifest fields, display modes, `action` shape),
> the **Library** section (`/library/album-artists`,
> `/library/albums/by-artist`, and `/library/files` — including the
> `?people=` and `?sort=` params), the **Queue** section
> (`/queue/playnow`, `/queue/add`), and the **AutoQ** `/autoq/send` verb.
>
> Build me a new **Library Tree charm**: the whole MusicBee library as
> one big collapsible tree, opened from the dashboard charm bar.
>
> **Deliverables — two files:**
>
> 1. **`charms/librarytree.json`** — charm manifest using the documented
>    fields. Pick an icon, `display: "both"` so it works as an inline
>    popover or a standalone tab, and an `action` of
>    `webapp /pages/librarytree.html`. Save to
>    `%APPDATA%\MusicBee\MBXHub\charms\librarytree.json`.
>
> 2. **`pages/librarytree.html`** — the tree itself. Use only the
>    endpoints `llms.txt` documents (don't invent any). It is a **lazy
>    three-level tree** — each level fetches its children the first time
>    it's opened and caches them so re-expanding is instant:
>    - **Level 1 — album artists.** Load once at startup from
>      `/library/album-artists`; render collapsed rows.
>    - **Level 2 — albums.** On expanding an artist, load
>      `/library/albums/by-artist?albumArtist=<name>` sorted ascending by
>      year; label each row `YYYY - Album` with its track count.
>    - **Level 3 — tracks.** On expanding an album, load
>      `/library/files?album=<name>&people=<albumArtist>&sort=track`;
>      label each row `NN. Title` (zero-padded track number).
>    - **Row actions — the same verbs Browse uses, on every album and
>      track row:** **Play Now** (`/queue/playnow`), **Queue Next** and
>      **Queue End** (`/queue/add` with `position: "next" | "last"`), and
>      **Send to AutoQ** (`/autoq/send`). An album action resolves the
>      album's tracks first, then applies the verb to the whole set.
>      Clicking an action must never toggle the row open/closed.
>    - **A–Z jump rail** down one side, derived from the loaded artists:
>      click a letter to scroll to the first artist under it; dim letters
>      with no artist.
>    - **Keyboard nav:** arrow keys move a highlight across visible rows,
>      Right expands/descends, Left collapses/ascends, Enter plays a
>      track or toggles a group.
>
> **Constraints `llms.txt` already describes — re-read and follow them:**
> - Plain HTML / CSS / vanilla JS, no frameworks, no CDNs (offline-first).
> - Link `/pages/components/shared.css` + `/pages/components/shared.js`
>   for the theme palette + `MBXShared` helpers (`esc`, `normalize`,
>   `clientLog`, `connectWebSocket`).
> - Use the `__THEME_CSS__` + `__DATA_THEME__` head tokens for
>   server-rendered theme (see any existing `/pages/*.html` for the
>   pattern).
> - Relative URLs throughout; `encodeURIComponent()` every artist/album
>   name in a query (libraries have non-ASCII names). XSS-safe
>   (`textContent` everywhere, no `innerHTML` on library-derived strings).
> - Visual language of `/pages/browse.html` (rows, action buttons,
>   theme-aware, muted secondary text).
> - Log every fetch (URL + result count) and every expand/collapse behind
>   a `DEBUG` flag; route real errors through
>   `MBXShared.clientLog("warn", "...")` so they land in `mbxhub.log`.
>
> **Install:** drop both files into `%APPDATA%\MusicBee\MBXHub\` (the
> `charms/` and `pages/` subfolders). Reload the dashboard — the Library
> Tree charm appears in the bar. Click it.

---

## Notes

- The prompt deliberately doesn't enumerate every API field. `llms.txt`
  is the source of truth; pointing the AI at it (and re-reading it) is
  the point of the exercise. If the AI hallucinates an endpoint or a
  response key that isn't documented, that's the AI's bug — re-anchor it
  on `llms.txt`. (A good first move is to `curl` each endpoint once and
  read the real keys before writing a line against them.)

- The row verbs — Play Now / Queue Next / Queue End / Send to AutoQ —
  are exactly the ones `/pages/browse.html` puts on its track rows, so
  the tree behaves consistently with the rest of the app. Copy that
  contract rather than inventing a new one.

- If you're working against a private LAN MBXHub instead of the public
  `mbxhub.com`, swap the URL for `http://<your-host>:<port>/llms.txt`.
  Same content, served live from your running instance.

- Same approach works for any browse-shaped feature you want to layer on
  top: a genre tree, a decade timeline, a "recently added" drill-down, a
  composer/conductor view — anything the documented API can express.
