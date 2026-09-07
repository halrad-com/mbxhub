# hello-app

A window MBXHub can start. `hello-charm` teaches the contract and prints it; this one is the other
half of the demonstration — something to **look at**, so "the hub started an app" is visible from
across a room.

```
dotnet build
dotnet run
```

It registers itself, declares its own executable as its launch target, and shows the MBXHub mark at
whatever size the window is. `Esc` closes it.

## What it demonstrates

- **A charm that is a program**, not a page: `kind: "proc"`, a `launch` target, a `menu` entry.
- **Being started by the hub.** Tools → MBXHub → Hello App, and the window says *launched from
  MusicBee* rather than *run directly* — the hub passes `--launched`, which is the manifest's own
  argument string coming back.
- **Failing usefully when the hub is closed.** Registration runs on a background task and stays
  silent if nothing answers; the window is the product, and it draws either way.

## Starting MusicBee from the app

The other direction, and the one that makes an integration feel seamless: open Steam, launch the
game, and the music is already there — nobody alt-tabs to start a player first.

The **Shell** answers that, not the hub, because the hub *is* MusicBee:

```
GET  http://<host>:<restPort+1>/meta/ping        → service: "MBXHub.Shell", and which hub it serves
GET  http://<host>:<restPort+1>/meta/app/start   → { canStart, reason }
POST http://<host>:<restPort+1>/meta/app/start   → starts it;  {"minimized": true} is opt-in
```

Three calls in that order, because the three failures need different sentences: *no Shell there*,
*not allowed to* (`canStart` is the AND of permission and installation, so the `reason` says which),
and *it did not start*.

**Minimized is the point.** A person who opened a game wants their music playing, not a player
taking the screen. The flag rides in the body — the route is body-less otherwise, and a caller that
sends nothing gets the old behaviour. It is a **request**, not a promise: Windows carries it in the
process start info and an application may ignore it, and a Store-packaged MusicBee is activated
through explorer, which carries no window state at all. The answer echoes `minimized` as what was
*asked for*.

**Where it lands is MusicBee's choice.** Its own `SystemMinimiseTo` setting decides whether a
minimized window sits on the taskbar or in the notification area — so on an install set to minimize
to the tray, this puts it in the tray. That is the request working, not a limitation of it: you are
asking for the player to be out of the way, and the person who owns the machine has already said
where out of the way is. (`SystemStartMinimised` is their own always-start-minimized preference; if
they have set it, they never needed you to ask.)

**Two modes, and they are different products:**

```
hello-app                              delay load - the window opens, registration retries quietly
hello-app --start-musicbee             load with launch - start MusicBee (minimized), then register
hello-app --start-musicbee --no-minimize
```

An app whose own job needs the library should launch it. An app that merely integrates should never
make somebody's music player appear because they opened something else. The button in the window
does it by hand, with a tick for minimized.

**Why the call is made from C# and not from the page.** `POST /meta/app/start` is state-changing, so
a browser-supplied `Origin` has to be LAN-scoped — and a page loaded from disk sends `Origin: null`,
which is refused. A native caller sends none and is allowed. So the button posts a message to the
host process, and the host makes the call. Worth copying if your own charm has a UI.

## The manifest

```json
{
  "id": "com.halrad.samples.hello-app",
  "kind": "proc",
  "label": "Hello App",
  "publisher": "HALRAD LLC (sample)",
  "version": "0.1.0",
  "icon": "🟦",
  "launch": "(filled in at startup: this executable's own path, plus --launched)",
  "expand": [
    { "label": "Hello App", "placement": "menu", "action": "launch", "msg": "Starting Hello App" }
  ]
}
```

**`launch` is a placeholder in the file and is written at startup**, and that is the whole lesson of
this sample:

- An **executable** target is refused unless it equals the image the hub *watched register*. So the
  value can only be this exe, and hand-typing a path gets it refused — the app fills in
  `AppContext.BaseDirectory\hello-app.exe` itself.
- Which means a `proc` charm with an exe target **cannot be a pure drop-in**. A manifest nobody
  announced has no observed image, so the press is refused. It has to run once to be seen.
- A **URL scheme** (`steam://run/…`) has no such rule — it never reaches the image gate — which is
  why the IKANDY example in `partners/` can be a file you drop in and this one cannot.

The action is the bare verb `launch`: what starts is always the manifest's `launch` field, the exact
string on the approval row. A target written after the verb is refused at registration.

## Why it hosts a page instead of drawing

The MBXHub mark is a vector that carries its own CSS animation. Hosting the asset means the window
shows the **real** mark rather than a redrawing of it, and it stays sharp from a taskbar preview to
a 4K second monitor. One package — WebView2, the version MBXHub's own Shell pins — and its runtime
ships with Edge on Windows 10 and later. If it is missing the window says so instead of dying.

Everything is local: the SVG sits next to the exe and nothing here reaches the internet.

It is also the obvious source the day a release wants to send a window's frames somewhere else — a
window already drawing every frame is what that needs.

## If the menu entry is not there

MusicBee reads menus at startup, so it appears after the next restart — the registration answer says
so in `restartRequired`. Before then the charm is registered and `pending`: approve it in
**MBXHub settings → Charm Manager**, tick the row, **Approve**.
