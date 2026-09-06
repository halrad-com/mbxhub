# hello-page

A charm that is **only a page**. Two files, no process, no registration, no ticket.

`kind: "page"` is the cheapest shape a charm has. MBXHub serves the page, the browser runs it, and
because the page comes from the hub it is same-origin with the API — so everything a caller that
never registered could read, it can read with no credential at all. Most charms that only *show*
something never need to be anything else.

## Install

Two copies, both into the hub's own data folder.

**Finding that folder.** It is not a fixed path, and it is not under `%LOCALAPPDATA%`. MusicBee gives
each install its own storage and MBXHub keeps `charms\` and `pages\` beside its settings file there,
so the folder belongs to the *install*, not to the machine — two MusicBees on one PC have two of
them. **Nothing over the API reports it**: the register route is loopback-only but tells you nothing
about the disk, by design.

MBXHub keeps its folder at `<MusicBee's data root>\MBXHub`, always. **What moves is the root, not
the layout:** a portable MusicBee carries its own `AppData` folder inside the install, while an
ordinary installed one uses the user's AppData. The two are otherwise the same shape, so code that
takes the root as a parameter handles both, and only the step that *finds* the root differs.

Three install cases, and they do not behave alike:

| Install | Where the folder is | For an installer |
|---|---|---|
| **Portable** — MusicBee unzipped into a folder of its own | `<MusicBee>\AppData\MBXHub\` — the install carries its own `AppData`, beside the executable | derivable from the running process; see below |
| **Installed** — the ordinary Win32 setup, program under `Program Files` | `%APPDATA%\MusicBee\MBXHub\` — the user's AppData, **not** beside the executable | the process path does not lead to it; look in the user location, then confirm |
| **Windows Store** | a packaged app, with its writes redirected | **not recommended for plugins**, now or foreseeably — do not build an install path against it |

**A library folder is not the data root.** MusicBee keeps one folder per library for its database —
`Library\` for the default, `Remote\`, and one named after each library the person has made — and
those sit *beside* the data root, not inside it. MBXHub's folder is **per install**: one `MBXHub\`
serving every library, however many there are. So do not look for `charms\` under a library folder,
and do not install per library. (The thing that *is* per library is the music data — which is why a
moods file follows the active library and this folder does not.)

For the first two install cases, cheapest first:

1. **Ask the running MusicBee where it is.** A portable install keeps its storage under the install
   folder, so the process path answers it:

   ```powershell
   $mb = (Get-Process MusicBee -ErrorAction SilentlyContinue | Select-Object -First 1).Path
   $portable = Join-Path (Split-Path $mb) 'AppData\MBXHub'          # portable
   $installed = Join-Path $env:APPDATA 'MusicBee\MBXHub'            # ordinary install
   @($portable, $installed) | Where-Object { Test-Path $_ }         # whichever exists is the one
   ```

   **Test, never create.** A folder you invent is a folder the hub does not read, and a charm
   written into it looks installed while doing nothing at all. If neither path exists, fall through
   to 2 rather than guessing a third.

2. **The Settings Folder link**, in MBXHub's settings dialog. It opens the right folder in Explorer
   whatever the install shape is, including the ones this document does not enumerate. This is the
   one to put in your own instructions — it cannot go stale.

3. **Ask the person to drag your two files in.** For a charm that is only a page, this is a
   perfectly good install.

With the folder open:

With the folder open:

```
manifest.json    ->  <hub data>\charms\com.halrad.samples.hello-page.json
hello-page.html  ->  <hub data>\pages\hello-page.html
```

Reload the dashboard. *Hello Page* is on the charm bar; click it and the page opens in place.

Files in `pages\` are served **instead of** the copies built into the hub, so use a name of your own
— a file dropped there under an existing page's name shadows that page with no warning anywhere.

## What it does

- Reads `GET /nowplaying` with no ticket, to show that the open surface is open to a page charm the
  same way it is open to anybody.
- Listens for its own activations and lists them.

## How an activation reaches a page

A page charm has no socket and no address, so the event arrives through the page that **frames** it:
the hub broadcasts `CharmActivated`, and the framing page posts it into this frame.

```js
window.addEventListener('message', function (e) {
  if (e.origin !== location.origin) return;          // same-origin channel; anything else is not ours
  var m = e.data;
  if (!m || m.type !== 'mbxhub.charmEvent') return;  // extensions and dev tools post here too
  handle(m.event);                                   // { charmId, event, placement, entryIndex, entryLabel, atUtc }
});
```

Both checks are load-bearing. A window receives messages from anyone who can reach it.

**What will not happen with the install above:** nothing activates this charm, so the list stays
empty. A drop-in manifest renders and is clickable, but activations come from *registered* entries —
a `menu` entry under MusicBee's Tools, which is only drawn once a person has approved the
registration. Registration is loopback-only and is done by a process: for a page charm that means
its installer, or an application that ships the page alongside itself. `hello-charm` is that half.

So read this sample for the two things it is honest about: what a page charm gets for free, and the
exact shape of an event when one arrives.

## The manifest

```json
{
  "id": "com.halrad.samples.hello-page",
  "schemaVersion": 2,
  "kind": "page",
  "label": "Hello Page",
  "publisher": "HALRAD LLC (sample)",
  "version": "0.1.0",
  "display": "both",
  "expand": [
    { "label": "Hello Page", "action": "webapp /pages/hello-page.html", "display": "both" }
  ]
}
```

- `kind: "page"` — the substrate. A page charm is never placed on the trust ladder and is never
  prompted over: it can do nothing a person with a browser could not already do.
- `webapp <path>` — the verb that opens a page. The path is a hub path, so the page must be one the
  hub serves; a charm does not get to point the frame at somewhere else.
- `display: "both"` — click opens it in place, shift-click opens a new tab.
- No `scopes`, so no `scopeReasons`. There is nothing to ask for.

## Offline, like everything else here

No CDN, no framework, no build step. The hub is usually on a network with no internet, and a charm
that fetches its own stylesheet from the outside is a charm that draws nothing there.
