# Integrating with MBXHub

**This is the how. The [REST API](https://mbxhub.com/api.html) is the what** — every route, every
field, every error code lives there, and your own hub serves the current copy at `/docs`. This guide
is the part the reference cannot tell you: how the pieces fit, which shape your application should
be, and what the whole path looks like end to end.

Written against MBXHub 0.5.5.1. Every sample it names is in [`samples/`](../samples/).
The three introductory charms use .NET 8 or HTML/JS; other samples have their own toolchain
requirements. Read each sample's README before building. For the contract and implementation
limits, use the [SDK reference](charms-sdk.md); the [SDK index](MBXHub-SDK.md) links the documentation.

---

## The one thing to know first

**Registration adds an identity and capabilities; the anonymous API remains available.**
The compatibility commitment covers the enumerated read endpoints and separately tested player
verbs in the [SDK reference](charms-sdk.md#the-one-commitment). It is not a permanent guarantee for
every reachable route: the reference describes an explicit automation exception.
Calls without a ticket use the anonymous path; calls presenting a ticket are subject to its gates.

So integrating is never a migration. You can ship against the open API, and register later only if
you want the things registration brings: an identity on the hub, a menu entry inside MusicBee,
events pushed to you, and capabilities a person can grant you.

## Two ways in

1. **Announce yourself** — `POST /charms/register` with a manifest. This is the route for anything
   that runs: it gets you a registration, an approval a person gives, and a ticket.
2. **Be a file** — drop a manifest into the hub's charms folder. The hub reads that folder at
   startup; it is how the charms shipped with MBXHub get there, and a manifest beside them is read
   the same way. No registration, no ticket, nothing granted — but it renders and it is clickable.

Neither route has the hub going looking. It reads its own folder and listens on its own route, and
nothing scans your machine for applications.

## Pick your shape

`kind` is the one field that changes how MBXHub reaches you. Everything else in the contract — the
manifest, registration, approval, the ticket, the capability gate — is identical for all three.

| `kind` | You are… | Events reach you by | Start from |
|---|---|---|---|
| `page` | HTML and JS the hub serves | a message posted into the frame showing your page | [`samples/hello-page`](../samples/hello-page/) |
| `proc` | a program on the MusicBee machine (**headless** or windowed) | a WebSocket you hold open | [`samples/hello-charm`](../samples/hello-charm/) |
| `endpoint` | a service **elsewhere** — another machine, a container | the hub POSTs to an address you declared | [`samples/hello-endpoint`](../samples/hello-endpoint/) |

Choosing:

- **Showing something?** Be a `page`. It is two files, no process, no ticket, and because the hub
  serves your page it is same-origin with the API — the open surface needs no credential at all.
- **Doing something on that machine?** Be a `proc`. It is the only shape with real evidence behind
  it — the hub sees the image that registered, so it can tell an update from an impostor, and a
  signed one prompts less.
- **Already running somewhere else?** Be an `endpoint`. Note the rule before you design around it:
  **registration is loopback-only, the address you declare is not.** Something local must make the
  announcement — your installer, your launcher, a small agent — and that local half is what a
  remote service integrates *through*.

A manifest with no `kind` is a `page`, because every charm that shipped before the field existed
was one.

---

## The walk-through

This is what [`samples/hello-charm`](../samples/hello-charm/) does, one step per heading. Run it
while you read; it prints every call and every answer.

### 1. Find the hub

```
GET /ping  →  {"success":true,"data":{"status":"ok","service":"MBXHub","apiVersion":"1"}}
```

`service` must be `"MBXHub"`. Something else answering on 8080 is not the hub, and registering with
it is how you leak a manifest to a stranger. Remember the port; do not probe again.

**There is no separate local channel.** No named pipe, no COM object, no shared memory — the
loopback HTTP API *is* the IPC. That is why a program on the MusicBee machine and a phone across
the room speak the same protocol, and why the only privilege boundary that matters is *where the
call came from* rather than which door it used.

**8080 is a default, not a guarantee.** Ports are claimed as an **even pair** — REST on the even
port, the Shell's own listener on the odd one above — so a machine whose 8080/8081 was taken is on
8082, and a second MusicBee instance is elsewhere again. Four ways to find it, best first:

| | How | Works when |
|---|---|---|
| 1 | **SSDP / mDNS / WS-Discovery** — `modelName: MBXHub`, a stable UUID, `presentationUrl` | the only one that works **off the machine**, and the only one that enumerates several instances by name |
| 2 | **The http.sys registration table** — which application owns the port | same machine, authoritative, works with discovery switched off. `netstat` cannot answer this: every `HttpListener` port reports as `System` |
| 3 | **The config file** — `mbxhub.json`, top-level `restPort` | the only one that answers **while the hub is stopped**. It also carries `restEnabled`, `requireBasicAuth` and `allowRemoteConnections`, which turns "not reachable" into a diagnosis |
| 4 | **Ladder probe** — even ports 8080, 8082 … 8098, `GET /ping`, `service: "MBXHub"` in the body | last resort; needs no file or API access. A port being open proves nothing — only the identity in the body is evidence |

Walking the ladder is not a remote-only technique and not a local-only one: it is simply the tier
that needs the least. If you are on the machine, prefer 3 and 2 — you can read a file and ask the
kernel. If you are not, you have 1 and 4.

### 2. Announce yourself

```
POST /charms/register        Content-Type: application/json
{ "id": "com.example.thing", "schemaVersion": 2, "kind": "proc",
  "label": "Thing", "publisher": "Example Ltd", "version": "1.4.7",
  "scopes": ["playback:control"],
  "scopeReasons": { "playback:control": "to pause the music when a call comes in" },
  "expand": [ { "label": "Do the thing", "placement": "menu" } ] }

→ 200 { "charmId": "…", "status": "pending", "restartRequired": ["menu"], "apiVersion": "1" }
```

Four things worth knowing at this line:

- **Loopback only.** The route answers callers on the MusicBee machine and refuses everyone else
  with `403 NOT_LOCAL`. The `Content-Type` must be `application/json` — that is what stops a page
  the person happens to be visiting from registering on their behalf.
- **`pending` is the normal first answer.** It is not a failure and not a queue.
- **Announce on every startup.** Same content is idempotent; changed content is an update. You do
  not accumulate registrations, and you do not need to remember whether you have registered before.
- **`id` is yours and it is claimed.** Use a reverse-domain name you control. A later registration
  of the same id by a different `publisher` is refused, and the first record is untouched.

### 3. Carry on regardless

`pending` costs you nothing. Every route open to a caller that never registered is open to you,
answering identically. Build your integration so this state is *normal*, not an error screen — many
users will approve you a day later, or never, and your application should be useful either way.

### 4. A person approves you

At the console: **MusicBee → MBXHub settings → Charm Manager**, tick the row, **Approve**. There is
no REST route that grants anything; approval happens on the machine, by a person, or not at all.

They are reading the row while they decide, so the row is your pitch: your label and publisher, what
you would launch, where you would appear, and — **in your own words** — why you want each
capability. That last part is `scopeReasons`, one sentence per scope, and it is the only thing about
your request they see in your words. Write it true. From `schemaVersion: 2` it is required, and a
missing, over-long or unrequested sentence is refused by name before anything is written.

### 5. Collect the ticket

Poll `GET /charms/{id}` until `status` is `active`, then **register once more**. While the plaintext
ticket remains available, the first subsequent local announcement whose caller the hub can resolve
collects it, exactly once:

```
→ 200 { "status": "active", "grantedScopes": ["playback:control"], "ticket": "…" }
```

Store it the moment you receive it; nothing re-reads it. If you lose a collected ticket, a person
must revoke and approve again in **MBXHub settings → Charm Manager**.

An `active` answer without a ticket needs context:

- `ticketWithheld: "caller-unresolved"`: the hub kept the ticket because it could not identify
  the calling process. Announce again from a connection it can resolve; do not revoke for this case.
- A ticket was already collected: use the stored credential. It survives a hub restart.
- The hub restarted after approval but before collection: the undelivered plaintext was lost.
  Console approval can mint a replacement; an active status alone does not prove collection.

See the [credential lifecycle table](charms-sdk.md#registration-and-credential-lifecycle) for
re-approval, revocation and update behavior. Announcing again does not undo a revocation.

### 6. Use it, and expect to be refused

The ticket goes in the **`X-MBXHub-Ticket` header**, over loopback, never in a query string.

A refusal is never an undifferentiated no. `403` carries `error.state` — one of `not-granted`,
`declined`, `reserved`, `banned` — and `error.capability` naming the scope. They call for opposite
behaviour: `not-granted` means ask; `declined` means a person already answered and asking again is
noise they will resent; `banned` is the only one that means no and stays no. `401` means your ticket
is unknown or revoked: behave exactly as you did before you registered, because that still works.

**A partial grant is normal.** Ask for four capabilities, be granted three, receive a ticket
carrying three. Design for it; it is the expected outcome.

### 7. Receive events

By kind (§ *Pick your shape*). A `proc` opens `ws://<hub>/ws` locally and sends
`{"bind":"<ticket>"}`; a bound socket receives its own charm's events whatever else it subscribed
to. A WebSocket activation carries the `CharmActivated` envelope and its `data` payload:

```json
{ "event": "CharmActivated",
  "data": { "charmId": "com.example.thing", "event": "activated", "placement": "menu",
    "entryIndex": 0, "entryLabel": "Do the thing", "atUtc": "2026-09-06T04:15:22Z" } }
```

For HTTP callbacks and page forwarding, see [activation delivery](charms-sdk.md#delivery--how-an-activation-reaches-you).

**Menu entries appear after MusicBee next starts** — menus are read at startup, which is what
`restartRequired` on your registration answer is telling you.

### Where your charm actually shows up

Most of the time: **on the dashboard**, as a button on the charm bar. That is the default placement
(`rail`) and it needs no restart and no menu — a person clicks your icon and your page opens in
place, or in its own tab if you asked for `standalone`. The Shell's **Charm Bar** — the strip docked
to the edge of the screen — draws the same charms, and a click there opens your charm as a desktop
window. Both surfaces update the moment a person approves or revokes you.

A `menu` entry, under **Tools → MBXHub**, is the extra door: it costs a MusicBee restart to appear.

**And the HUD can start your program.** Declare `placement: "overlay"` alongside a `launch` target and
you are drawn as a tile in the HUD's Services drawer; pressing it starts what your manifest declares,
under the same policy as the menu. Two routes back it:

```
GET  /charms/services      → { "local": true, "services": [ { "id", "label", "icon", "launchKind" } ] }
POST /charms/{id}/launch   → 200, or a named refusal
```

`launchKind` is `scheme` or `exe` — enough to render a button — and **your launch target is never
returned**; that is the operator's to read on the approval row. The press is **local-only**, so
from another machine the list comes back empty rather than drawing tiles that cannot work. Declaring the
placement is how you *ask* for that surface: without it, a launch charm stays on the Tools menu where
its author put it. A second press on a running executable **raises** its window rather than starting
a second copy, and the `200` says which happened (`launched` or `raised`); the
[reference](charms-sdk.md) has the refusals.

---

## Three use cases

### A page — "show me something while the music plays"

A lyrics panel, a tour-date board, a visualiser. Two files: a manifest and an HTML page, copied into
the hub's `charms\` and `pages\` folders. No process, no registration, no ticket, and the open API
is same-origin so `fetch('/nowplaying')` just works.

It appears **on the dashboard's charm bar** as soon as the folder is read, and on the Shell's Charm
Bar beside it — click the icon and your page opens in place on the dashboard, or as its own desktop
window from the Charm Bar. That is the whole install: no restart, no approval, nothing granted.

Reach for this first. Most integrations that only *display* never need to be anything else.
→ [`samples/hello-page`](../samples/hello-page/)

### A headless program — "do something on this machine"

A hardware bridge, a scrobbler, a lighting controller. It runs on the MusicBee machine, announces
itself at startup, and holds a WebSocket so activations reach it while it is up. A `menu` entry
under **Tools → MBXHub** is how a person pokes it — and how the hub *starts* it when it is not
running, if the manifest declares a `launch` target. That target must be the image that registered:
a charm may launch itself and nothing else.

This is the shape with the strongest evidence behind it, and therefore the quietest prompts.
→ [`samples/hello-charm`](../samples/hello-charm/)

### An external service — "I already run somewhere else"

A phone app's companion service, a home-automation box, a container on your NAS. It declares an
address; the hub POSTs activations there. Three properties of that call shape your handler:

- **Two seconds for the whole call.** Answer first, work after.
- **Only the status code is read.** Any `2xx` is delivered; your response body is never read.
- **No redirects are followed**, and the address must be `http`/`https` on a private or loopback
  host. A public address is refused at registration.

Its remote half cannot hold a ticket — a ticket is refused off the machine — so its privileged reach
is whatever its local half does on its behalf.
→ [`samples/hello-endpoint`](../samples/hello-endpoint/)

---

## Things that will bite you

**Your approval can be taken back.** Five things do it, each on your next announcement, each putting
you back to `pending` with your ticket cleared. Four are about what you declared, and each fires
exactly when the approval row would read differently: a **different executable**, a **different
`launch` target**, a **different set of placements**, or a **reworded `scopeReasons` sentence** for a
scope you already asked for. Those keep your grants — the person is re-approving the same decision
about a changed program. The fifth is about the hub: a record approved under a *different install*
goes back to pending and **releases the grants**, because the person who granted them is not the
person running this hub.

An ordinary version bump — new label, new icon, new entry text, new order — moves none of it.

**Adopting `scopeReasons` costs one re-approval.** The compare runs over scopes your record already
carried, so the first announcement in which an already-approved charm starts sending sentences reads
as a change. Ship it in a release you expect a re-approval in.

**A narrowed launch allow-list is invisible to you.** The person running the hub can restrict which
URL schemes launch at all. Your menu click then starts nothing, the hub log says why, and *nothing
on the wire tells you* — so name the schemes your application relies on in your own README.

**Registered charms must be active to be eligible.** Pending and revoked registrations are hidden
on the rails. A MusicBee menu item built earlier can remain visible until restart, but its click
re-checks eligibility and is refused. A file-installed manifest with no registration block is
eligible without registration approval; launch policy still applies.

**Prompts are a person, not a rate limit.** An outward-acting call from an unsigned charm can raise
a *Not now / Trust* prompt on the MusicBee machine, and your call waits for the answer. *Not now* is
not an error; it is an answer.

## Where things live on disk

MBXHub keeps its folder at `<MusicBee's data root>\MBXHub`, with `charms\` and `pages\` inside it.
**The root moves with the install shape, the layout does not:**

| Install | Root |
|---|---|
| **Portable** | `<MusicBee>\AppData\` — the install carries its own, beside the executable |
| **Installed** (Program Files) | `%APPDATA%\MusicBee\` — the user's AppData, *not* beside the executable |
| **Windows Store** | a packaged app with redirected writes — **not recommended for plugins**; do not build an install path against it |

A **library folder is not the data root**: `Library\`, `Remote\` and each user-named library hold
MusicBee's databases and sit beside it. `MBXHub\` is per install — one of them, serving every
library.

For an installer, test both known roots and use whichever exists — **test, never create**, because a
folder you invent is one the hub does not read. When in doubt, send the person to the **Settings
Folder** link in MBXHub's settings dialog: it opens the right folder whatever the install shape is.

## Where the details are

- **[SDK index](MBXHub-SDK.md)** — the tutorial, contract, browser reference and examples.
- **[Charms SDK reference](charms-sdk.md)** — manifest fields, supported placements, credential
  lifecycle, named refusals and verification limits.
- **Your own hub** serves the current reference at `/docs`, and a terse machine-readable copy at
  `/llms.txt`. These describe the REST API for the build you are talking to; they do not replace
  the Charms SDK reference linked above.
- **[mbxhub.com/api.html](https://mbxhub.com/api.html)** — the same reference, published.
- **[`samples/`](../samples/)** — the three introductory charms, a desktop app, Spout video,
  browser, Perl, TypeScript and prompt examples. Requirements and limitations are in each README.

Found something here that the hub does not do? That is a bug in this document. Say so — the
contract is only worth what its description is worth.
