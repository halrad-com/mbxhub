# Worked example: Playlist CRUD charm

A copy-pasteable prompt that hands a released MBXHub instance and its
public API reference (`llms.txt`) to an AI assistant and asks it to
build a brand-new feature — a Playlist viewer/editor charm — without
modifying or rebuilding MBXHub itself.

**Why this is interesting:** the AI never sees the MBXHub source. It
reads the public `llms.txt` cheat sheet, follows the documented charm
manifest schema and `/playlists` endpoints, and drops two text files
into the running instance's data directory. Reload the dashboard, the
charm appears in the bar, click it, full CRUD. Same path is available
for any new feature you want to bolt onto your own MBXHub.

---

## The prompt

> Read `https://mbxhub.com/llms.txt` end-to-end — pay attention to the
> **Charms** section (manifest fields, display modes, `expand[]` shape)
> and the **Playlists** section (the `/playlists` endpoints and what
> they return).
>
> Build me a new **Playlist charm**: full CRUD over MusicBee playlists,
> opened from the dashboard charm bar.
>
> **Deliverables — two files:**
>
> 1. **`charms/playlist.json`** — charm manifest using the documented
>    fields. Pick an icon, `display: "both"` so it works as inline
>    popover or standalone tab, and an `action` of
>    `webapp /pages/playlist.html`. Save to
>    `%APPDATA%\MusicBee\MBXHub\charms\playlist.json`.
>
> 2. **`pages/playlist.html`** — the actual viewer/editor. Use only
>    the endpoints `llms.txt` documents (don't invent any). CRUD scope:
>    - **Read**: list playlists in a picker; click one to load its tracks.
>    - **Create**: `+ New` button → name prompt → `POST /playlists`.
>    - **Update**: inline rename, drag-to-reorder tracks
>      (mouse + touch), add-tracks via a search bar that hits `/search`,
>      per-row remove.
>    - **Delete**: trash icon on each picker row (confirm first).
>    - **Play Now / Queue Next** buttons per playlist and per track.
>
> **Constraints `llms.txt` already describes — re-read and follow them:**
> - Plain HTML / CSS / vanilla JS, no frameworks, no CDNs (offline-first).
> - Link `/pages/components/shared.css` + `/pages/components/shared.js`
>   for theme palette + `MBXShared` helpers (`esc`, `normalize`,
>   `clientLog`, `connectWebSocket`).
> - Use the `__THEME_CSS__` + `__DATA_THEME__` head tokens for
>   server-rendered theme (see any existing `/pages/*.html` for the
>   pattern).
> - Relative URLs throughout. XSS-safe (`textContent` everywhere, no
>   `innerHTML` on user-derived strings).
> - Visual language of `/pages/browse.html` (album rows, action buttons,
>   theme-aware).
> - Responsive: two columns (picker + editor) ≥ 800px; stack below.
> - Route browser-side errors through `MBXShared.clientLog("warn", "...")`
>   so they land in `mbxhub.log`.
>
> **Install:** drop both files into `%APPDATA%\MusicBee\MBXHub\` (the
> `charms/` and `pages/` subfolders). Reload the dashboard — the
> Playlist charm appears in the bar. Click it.

---

## Notes

- The prompt deliberately doesn't enumerate every API field. `llms.txt`
  is the source of truth; pointing the AI at it (and re-reading it) is
  the point of the exercise. If the AI hallucinates an endpoint that
  isn't documented, that's the AI's bug — re-anchor it on `llms.txt`.

- If you're working against a private LAN MBXHub instead of the public
  `mbxhub.com`, swap the URL for `http://<your-host>:<port>/llms.txt`.
  Same content, served live from your running instance.

- Same approach works for any feature you want to layer on top: a
  playlist diff viewer, a podcast queue page, a per-genre dashboard, a
  remote control surface tuned to your living room — anything the
  documented API can express.
