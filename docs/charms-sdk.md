# Charms SDK

A **charm** is an extension of MBXHub. The ones that ship with it are pages we wrote; this document is how anyone writes one — how an application announces itself, what it may ask for, how a person grants it, and what MBXHub promises not to break. It is the companion to the [REST API](https://mbxhub.com/api.html) and [llms.txt](https://mbxhub.com/llms.txt), and it describes what this build does today.

Written against **MBXHub 0.5.5.1**. Your own hub serves the reference for the build you are actually
talking to at `/docs`, and a terse machine-readable copy at `/llms.txt`.

**New here? Read [the integration guide](integration-guide.md) first** — how the pieces fit and the
whole path walked end to end — then come back here for the detail. Working code for all three kinds
is in [`samples/`](../samples/).

### The one thing guaranteed regardless of everything below

An application that does not register keeps working exactly as it does today, indefinitely. Registration adds; it never takes away. Everything on this page is opt-in, and everything a caller can reach without a registration stays reachable without one.


## What a charm is — three separate questions

Three axes, and none of them answers another:

- **What is this thing?** Where its code lives, what evidence about it can exist, how events reach it, what “alive” means. That is the charm’s **kind**. One per charm.
- **Where does each entry appear?** The charm rail, a desktop window, a MusicBee menu, a tab. That is the entry’s **placement**.
- **What does activating it do?** Open a page, call an endpoint, send a command. That is the entry’s **action** — a verb with an argument.

The tests that keep them apart: if it changes what evidence can be gathered about the code, it is the kind; if it answers where the entry appears, it is a placement; if it answers what happens when the entry is activated, it is an action. The verb set stays small and closed — adding a new door into MusicBee adds a placement value, never a verb.

### Kind — the substrate

| `kind` | Where the code lives | What evidence can exist | Events reach it by | “Alive” means |
|---|---|---|---|---|
| `page` | HTML/JS that MBXHub hosts and renders | none of the executable kind — there is nothing to sign | a message to the frame, or the page’s own WebSocket | a window or frame is open |
| `proc` | a local executable on this machine | the strongest available: the image on disk and its signature | its own connection while it is up | the process is running |
| `endpoint` | somewhere else that answers HTTP — another machine, a container, a service | its address and reach; no local image | MBXHub calls a URL the charm declared | its URL answers |

**A manifest with no `kind` is a `page`.** Every charm that ships today is a page, so the default is the truth rather than a convenience. An unknown `kind` is carried verbatim rather than coerced, so nothing is ever described as something it is not.

**A charm with no surface at all needs nothing new.** A `proc` or an `endpoint` whose entries carry no placement registers, holds a credential, and either calls MBXHub, subscribes to its events, or declares a URL MBXHub calls. There is no `headless` kind and no flag — absence of a placement is the declaration.

### Placements — where an entry appears

| Placement | Where | In this build |
|---|---|---|
| `rail` | the charm rail in MBXHub’s pages — `display` selects its sub-mode | rendered; every shipping entry is one |
| `rail-menu` | the charm rail, as a popover menu — the legacy `action-menu` | rendered |
| `menu` | an item under MusicBee’s *Tools → MBXHub* menu; activating it delivers an event to you | rendered |
| `node` | an entry in MusicBee’s navigator; selecting it opens the charm’s tab | named only |
| `tab` | a MusicBee tab, per-charm opt-in | named only |
| `window` | a desktop window of its own, when the MBXHub Shell is running | named only |
| `overlay` | a surface inside an overlay page | named only |
| `hotkey` · `status` | a key chord; a background-task status line | named only |

**Named only means nothing draws them.** The values exist so the axis is closed and a manifest can declare one today; three of them are rendered by a surface in this build and the rest are not. A `window` placement is not the same thing as the Shell pop-out described under the Surfaces section below, which exists and needs no placement.

**The `placement` field is read.** An entry that declares none takes the charm’s own placement, and that is `rail-menu` when the charm’s `display` is `action-menu` and `rail` otherwise — which is why every shipping manifest loads unchanged. Placements are compared case-insensitively, like every other manifest word, and are **never coerced**: a value this build does not know comes back verbatim and is skipped by whatever reads it, rather than being drawn somewhere else. A placement MusicBee hosts is handed to MusicBee when the plugin loads, so one requested while MusicBee is running appears at the next start — which is what `restartRequired` says on the registration response.

### Actions — what activating an entry does

| Verb | Argument | Meaning |
|---|---|---|
| `webapp` | a path | open this page |
| `iframe-cmd` | a command | send this command to the page already open |
| `post` | a hub path | call this MBXHub endpoint |
| `http…` (bare URL) | a LAN address | call this device directly, through MBXHub’s LAN proxy |
| `launch` | **none** — the verb is bare | start the program the manifest’s top-level `launch` field names, under the launch policy below |

**`launch` takes no target, and writing one is refused.** An `expand[]` entry whose action is `launch thing.exe` is answered `400 INVALID_ACTION` at registration, naming the entry, before anything is written. What starts is the manifest’s top-level `launch` field — the exact string on the Charm Manager’s approval detail — so the thing that was approved and the thing that starts cannot come apart. The verb is matched on the first token, case-insensitively, so a later verb spelled `launcher` is a different one and is untouched.

**An entry whose verb this build does not know is skipped, and the rest of the charm works.** The dispatcher matches known prefixes and lets anything else fall through without error — so one manifest can target several builds: ship a new verb *and* a `webapp` fallback entry and both are satisfied from one file.

**The `action-menu` rail mode takes one verb, and only one.** Its items dispatch `post <path>` — the path is POSTed to this hub with an empty body. A bare path carrying no verb is still honored, with a deprecation warning in the client log. Anything else — `webapp`, `iframe-cmd`, an `http://` URL — is ignored with a warning by an `action-menu` item; those verbs belong on the charm button and on `expand[]` entries rendered as a button row.

## The manifest

A charm is a JSON file. Almost every shipping charm is exactly this shape:

```
{
  "id": "mixer",
  "label": "Mixer",
  "expand": [
    { "icon": "<svg …>", "label": "Mixer",     "action": "webapp /pages/mixer.html", "display": "both" },
    { "icon": "+",       "label": "Volume Up", "action": "iframe-cmd volumeUp",      "msg": "Vol +" }
  ]
}
```

**The complete field set** — the loader reads all of these, whether or not every charm uses them:

| Field | Where | Meaning |
|---|---|---|
| `id` | top level | unique, stable, the charm’s identity |
| `kind` | top level | the substrate. Parsed; absent means `page`, unknown is kept verbatim |
| `schemaVersion` | top level | the manifest format’s version. Parsed; absent means 1 |
| `label` | top level and per entry | what a person sees |
| `expand[]` | top level | the charm’s actions |
| `icon` | top level or per entry | inline SVG, or a character — passed through the icon contract below |
| `action` | top level or per entry | verb + argument |
| `display` | top level or per entry | the rail sub-mode — see below |
| `msg` | top level or per entry | short confirmation text after the action runs |
| `context` | top level | free text for the charm’s own use; the loader carries it and does not interpret it |
| `publisher` | top level | who you are, in your own words |
| `version` | top level | your release |
| `scopes` | top level | the capabilities you ask for. Parsed and recorded — asking is not being granted |
| `scopeReasons` | top level | one sentence per scope, **in your words**, saying why you want it — an object keyed by scope name. Shown verbatim beside the capability’s own sentence on the approval row, and again on the runtime prompt labelled *They say:*. One line, at most 200 characters, trimmed, the same set of names as `scopes`. **Required from `schemaVersion` 2**; a manifest declaring 1, or nothing, may omit it |
| `endpoint` | top level | where an `endpoint` charm answers — and where MBXHub POSTs its activations. Also the fallback route for a `proc` charm with no socket connected |
| `launch` | top level | what to start for a `proc` charm — its own executable, or a URL scheme. Absent is normal: an application whose own launcher starts it is still a `proc`. Read by the `launch` verb, under the policy below |
| `registration` | top level | the hub’s own record for a registered charm. Written by the hub, never by you |
| `placement` | per entry | where the entry appears. Read; absent means the charm’s own placement, which is `rail` unless `display` is `action-menu`. There is no top-level `placement` field in this build — a charm’s own placement comes from its `display` |
| `source` | per entry | for a placement that shows pixels (`tab` / `window` / `overlay`): a path or a render source such as `spout:<sender>`. Parsed and carried; **nothing in this build fills a surface from it** |

Top-level `icon` / `action` / `display` / `msg` are the single-action shorthand; `expand[]` is the list. A charm may use either.

**`display` — the rail sub-modes:**

| Value | Meaning |
|---|---|
| `both` | click opens in place; shift-click opens a new tab |
| `standalone` | always a new tab |
| `inline` | opens in place only |
| `action-menu` | one trigger, a menu of the `expand[]` entries — its items take one verb |

**Unknown `display` values fall back to a plain button.** That is a documented rule of the loader, not an accident.

> **The icon contract:** an icon is either plain text or one inert `<svg>`. Text with no `<` in it passes through; a single `<svg>` element carrying nothing that executes passes through; anything else is HTML-encoded, so it renders inert and visible rather than vanishing. You see the mistake instead of a blank button.

## What is promised, and what is merely true

### The one commitment

**The endpoints an integration reads stay open to callers that do not register, permanently.** They are frozen in our test suite and re-checked on every run:

`/ping` · `/system/uptime` · `/system/status` · `/nowplaying` · `/queue/current` · `/player/shuffle` · `/player/repeat` · `/playlists` · `/playlists/{url}/files` · `/library/file/{url}`

— together with the player verbs, frozen in a suite of their own. If your integration reads something not on that list, tell us and it goes on it. A promise nobody tests is a hope.

**What the commitment does not cover, said out loud.** The **automation** surface — input simulation, wake, library scan — is governed today by a single switch the person at the console controls, not by registration, and some of those routes answer to GET as well as POST. If any of that ever moves behind a granted capability, or a side-effecting GET form is retired, that is a break: it arrives with an `apiVersion` change and notice, never silently.

### The rest is loader behavior, not a promise

These are properties we like and intend to keep. They are stated so you can rely on them knowingly, not offered as a contract:

- An unknown verb is skipped, never fatal.
- So one manifest can target several builds.
- Unknown `display` values fall back to a plain button.
- An absent `kind` is a `page`, and an unknown one is carried verbatim.
- An absent `schemaVersion` is a version 1 manifest — which is what every shipping charm is.

The charm format has no outside consumers yet, and that has a consequence worth stating plainly rather than hiding behind reassuring language: **the format is still free to change, and while that is true we intend to get it right rather than only get it compatible.** If something here is wrong, now is when it costs nothing to say so.

## Registration — announcing yourself

An application announces itself — and **a charm can also arrive as a file**: the hub reads the manifests in its own charms folder when it starts, which is how the charms shipped with it get there, and a manifest dropped in beside them is read the same way. What *announcing* gets you that a file cannot is this section: a registration, an approval a person gave it, and the ticket that follows.

Neither route has the hub going looking. **MBXHub never scans your machine for applications.** It does identify the process on the other end of your registration connection — its executable path and its signature — because that is what places a `proc` charm in a trust tier. This happens at registration, once, for a `proc` charm only; a `page` or `endpoint` charm is not observed, because there is no local process to observe. Nothing else is read, and nothing leaves the machine.

### Finding the hub first

Walk the even ports of the ladder — 8080, 8082, and so on to 8098 — and treat a port as MBXHub only when `GET /ping` answers with `service: "MBXHub"`. A `200` from something else on a ladder port is somebody else’s server, not a quiet hub. Stop probing at the first port that answers that way. `service` is the only field to recognize us by — `apiVersion` tells you what you found, never that you found us.

### Announcing

```
POST /charms/register
Content-Type: application/json

{ "id": "com.example.thing", "kind": "proc", "label": "Thing", "publisher": "Example Ltd",
  "version": "1.4.7", "schemaVersion": 2,
  "scopes": [ "playback:control" ],
  "scopeReasons": { "playback:control": "to pause the music when a call comes in" },
  "expand": [ … ] }

→ 200 { "charmId": "…", "status": "pending", "restartRequired": [], "apiVersion": "1" }
```

- **Idempotent for an unchanged manifest.** Registering twice with the same `id` and the same content returns the same `charmId` and creates nothing new. An application that re-announces on every launch does not accumulate registrations.
- **A changed manifest is an update, and that is every release you ship.** Same `id`, new `version`, new entries, a new scope: the manifest half of the record is replaced and the hub’s half survives untouched — the `charmId`, the time you first registered, the status the person set, and anything recorded against you. A **newly requested** scope is not granted by the update; it is asked for at the next opportunity. A scope you stop requesting is released — the granted set is intersected with what the manifest still asks for. Declines and bans are not pruned that way: a grant is released when you stop asking, but a no is not yours to withdraw by shipping a version that omits the scope.
- **`pending` is not degraded.** While a person has not yet approved you, every call you could make before registering behaves identically. Pending costs you nothing.
- **`apiVersion` comes back on the first call**, so you learn what you are talking to at the moment you introduce yourself. It comes back on a refusal too: a caller that cannot register still needs to know which contract just refused it.
- **`restartRequired` lists what is registered but not yet on screen.** Those are the placements MusicBee itself hosts — `menu`, `node`, `tab`, `hotkey`, `status` — which MusicBee is handed when the plugin loads, so they appear at its **next start** rather than now. It is on every `200`, `[]` included, and absent from a refusal, where nothing was registered. Names come back spelled as your manifest spelled them, deduplicated case-insensitively with the first spelling kept. **It is not a diff:** re-announcing the same entry still lists it, because the answer describes the MusicBee session that is answering, not what changed since last time. A placement nothing hosts — `rail`, `rail-menu`, `window`, `overlay`, or a value this build does not know — is not listed, because claiming it waits on MusicBee would be a promise that never comes true.
- **Registration is performed locally.** `POST /charms/register` is accepted only from the machine MBXHub runs on. This is not a policy that might loosen: a hub that could be enrolled from across the network is a hub anyone on the network can enroll themselves into.
- **That is a rule about who announces you, not about where your code runs.** An `endpoint` charm answers from somewhere else by definition, and registers through something local acting on its behalf — its installer, its launcher, or a small local agent. What it *declares* is the address MBXHub calls; what must be *local* is the announcement. An application with nothing local at all is not a registered charm; it is an unregistered caller — which keeps working, permanently.
- **An `id` is claimed by whoever registers it first on that hub.** A later registration of the same `id` from a different publisher identity is refused (`ID_CLAIMED`) rather than allowed to take it over, and the first record is unchanged. A `pending` id is not yet claimed, and a revoked one stays claimed. *(Releasing a claim from the Charm Manager is designed, not built — no verb does it yet.)*
- **An `id` must be publisher-namespaced**, and that is enforced: at least one dot, no path separators, no dot-segments, 128 characters at most, and letters, digits, `.`, `-` and `_` only. Choose a reverse-domain name you control and this never concerns you.

### Reading your record back

```
GET /charms/com.example.thing

→ 200 { "id": "com.example.thing", "kind": "proc", "publisher": "Example Ltd",
        "version": "1.4.7", "status": "pending",
        "grantedScopes": [], "requestedScopes": [ … ] }
```

It is ungated and read-only, because it reports a decision already made and changes nothing. `grantedScopes` is stated as empty rather than omitted, so you can tell *nothing granted* from *field missing*. `tier` is absent until something has computed one — an unknown tier is left unsaid rather than asserted. **What is never returned is the hub’s private half of the record:** the observed image path and its digest are not on this response under any name, and an id that was never registered is a `404` — the same answer an id that could never be valid gets.

### How a registration is refused

A refusal is always a stated code, never a silent drop.

| Situation | Response |
|---|---|
| The caller is not on the machine MBXHub runs on | `403` `NOT_LOCAL` — checked first, ahead of everything below, so a remote caller is told this whatever it posted |
| `Content-Type` is not `application/json` | `415` `UNSUPPORTED_MEDIA_TYPE` — parameters such as `; charset=utf-8` are fine; declaring nothing is not. This is what keeps a page the person happens to be visiting from registering on your behalf |
| The install is in read-only mode | `403` `API_READ_ONLY` — the owner’s statement, made at the console, that this install is not to be changed, and registration writes a file |
| A party is running, or the hub is in kiosk mode | `302` to the guest surface. These sit in front of the route rather than in it, so the answer is a redirect and not an error body. A local caller is exempt from the party redirect |
| This hub already holds 500 registrations | `403` `REGISTRY_FULL` — the ceiling on distinct ids. An id **already** registered still updates at the ceiling, so a full hub cannot stop you shipping a new version |
| The body is missing, is not a JSON object, or has no `id` | `400` `INVALID_REQUEST` |
| The `id` is not publisher-namespaced (no dot) | `400` `INVALID_ID` — `com.example.thing`, never `thing` |
| The body is larger than 64 KB | `413` `REGISTER_TOO_LARGE` — a manifest is a page of JSON, not a payload |
| A scope name this build does not know | `400` `UNKNOWN_SCOPE`, naming the scope — never silently dropped |
| A `scopes` entry with no sentence in `scopeReasons`, or a blank one | `400` `SCOPE_RATIONALE_MISSING` — the offending scope is named and nothing is written. **The one refusal the schema version gates:** raised only for a manifest declaring `schemaVersion` 2 or higher, because a version-1 manifest predates the field. It pays for the silence on the approval row instead, where every capability it asks for reads *no reason given* |
| A sentence that is not one line, or is longer than 200 characters once trimmed | `400` `SCOPE_RATIONALE_INVALID` — the offending scope is named and nothing is written. Refused at *every* schema version: a sentence that cannot be shown on the one row it gets is not made showable by the version it arrived under |
| A `scopeReasons` key naming a scope this manifest does not ask for | `400` `SCOPE_RATIONALE_UNKNOWN` — refused rather than dropped, for exactly `UNKNOWN_SCOPE`’s reason: you wrote words nobody would ever read, and you are here now to fix them. Refused at every schema version |
| An `expand[]` entry writes a target after the `launch` verb — `"action": "launch thing.exe"` | `400` `INVALID_ACTION` — naming the entry. The verb is bare; the target belongs in the manifest’s top-level `launch` field, which is the string the person approves and the only one that is launched |
| The `id` is claimed by another publisher | `409` `ID_CLAIMED` — the first record is unchanged |
| The hub could not read its own registry (a scanner or indexer was holding the file, and one retry did not clear it) | `503` `REGISTRY_BUSY` — nothing was written; your approval, credential and `charmId` stand; announce again |

### Announcing again, after a person approves you

`pending` is the only status the hub assigns — nothing in a request can ask for `active`. **Approval happens in the Charm Manager** (MBXHub settings → *Charm Manager* tab), on the machine, and there is no REST route that grants anything. Registration is how you find out: the **next** announcement from the local machine under the same `id`, once a person has approved you, carries your credential.

```
→ 200 { "charmId": "…", "status": "active", "grantedScopes": [ … ], "ticket": "…",
        "restartRequired": [], "apiVersion": "1" }
```

**The plaintext ticket is handed over once.** It is held in the hub’s memory until it is claimed — only its digest is written — so a later announcement answers `active` and `grantedScopes` with no `ticket`, and a hub restart between the approval and your next announcement loses the parked plaintext and costs a re-approval. Store it the moment you receive it; nothing re-reads it.

**A caller the hub could not resolve does not collect it either.** When the peer lookup runs and cannot say which process is calling, the answer is still `active` and still states `grantedScopes`, the plaintext stays parked, and the body carries `"ticketWithheld": "caller-unresolved"`. Announce again from a connection the hub *can* resolve and the next answer delivers it, exactly once as before. That field is what tells this apart from the ordinary *you already collected it* answer, which is otherwise byte-identical and has the opposite remedy; it is **absent** from every other answer rather than sent as null, so its presence is the whole signal. It applies to every kind, not only `proc` — what failed is *who is calling*, which is not a question about substrate. The same lookup is what records your executable and your tier, so a registration nothing could resolve records neither: a charm no lookup has ever answered for carries no tier at all rather than `unsigned`, and one resolved before keeps what that lookup found.

**Five things take an approval back**, all of them on your next announcement, and all with the same disposition — back to `pending`, ticket cleared, and a person approves again. Four are about what you declared and keep your grants:

- announcing from a **different executable** than the one that was approved;
- declaring a **different `launch` target** — a scheme swapped for another, or an executable whose arguments changed;
- declaring a **different set of placements** than the row the person approved showed — your own placement plus whatever your `expand[]` entries resolve to;
- **rewording why you want a capability you already asked for** — the sentence in `scopeReasons`, compared trimmed and case-*sensitively*, because it is prose a person read rather than a path or a keyword. It is the basis of their consent, so new words need a new yes.

The fifth is about the hub, not you: a record approved under a **different hub install** — a charms folder carried to another machine — goes back to `pending` on its first announcement there, and this one also **releases the grants**, because the person who granted them is not the person running this hub.

The rationale trigger compares **only scopes the record already carried**: asking for a new capability is the grants-pruning path’s business, not this one, so adding a scope does not knock you back on a rule about words that were never read. It does mean that the first announcement in which an *already-approved* charm starts sending sentences costs it its approval once — the row goes from *no reason given* to your words, which is the row reading differently, exactly as the rule says. Adopt `scopeReasons` in a release you would expect a re-approval in.

The placement set is compared case-insensitively as a *set*, so order, duplicates, labels, icons and actions move nothing and an ordinary version bump is untouched: adding a second `rail` entry to a charm that is already a bar button changes no set, and adding a `menu` entry to a charm that declared only `rail` entries does. Removal counts too — the row said one thing and now says another. Clearing the ticket is part of the disposition rather than a consequence of it: the old credential resolves to nothing, so a gated call presenting it answers `401`, and a charm cannot move its own launch gate and keep the yes. A registration whose caller could not be resolved is not an observation at all, so it can neither move that gate nor erase what is on the record.

Until then, `pending` means registered and not yet approved: the record is on file, nothing is granted, the endpoints open to you are exactly the ones open to a caller that never registered, and nothing of yours is drawn on a charm surface.

## Permissions — what you may ask for

A capability is either **granted** or it is not, and when it is not there are **four distinct reasons** — never one undifferentiated no. These are the words used everywhere: here, on the wire, and in what a person is shown.

| State | `state` on the wire | Meaning |
|---|---|---|
| **granted** | *(no error — the call succeeds)* | a person authorized it; your ticket carries it |
| **not issued** | `not-granted` | never asked for, or asked for and not yet answered. Asking again is the remedy |
| **declined** | `declined` | a person was asked and said no, and that is remembered. Re-asking on retry does nothing; a new `version` may ask again |
| **reserved** | `reserved` | policy withholds it from **every** caller, ours included, with a reason. Temporary by intent |
| **banned** | `banned` | revoked for a specific charm and capability. **The only permanent refusal** |

**Why `declined` is its own state.** The two call for opposite behavior from you: `not-granted` means ask; `declined` means a person has already answered and asking again is noise they will resent. An integration that cannot tell them apart either nags or gives up, and both are wrong. The same reasoning separates `reserved` — *not available to anyone yet* — from `banned`, which is the only one that means no and stays no.

- **Nothing is reserved to its author.** There is no capability that charms we write can hold and yours cannot. If a class is reserved, it is reserved for everyone, including us.
- **A partial grant is normal, not a failure.** Ask for four classes, be granted three, and you receive a ticket carrying three. The fourth returns an error naming itself. Design for this: it is the expected outcome, not an edge case.

**What you may ask for is a named list, not a guess.** A capability is named `area:verb` — `proxy:lan`, `library:read`, `automation:wake` — and the authoritative list for the build you are talking to is readable at `GET /charms/capabilities`, with each entry’s current maturity and whether it is reserved. A scope name the build does not know is refused by name at registration, never silently dropped.

**Every scope you request carries one sentence of your own saying why.** Write it as the truth, for the person, not for us; a rationale that does not match what the application then does is what gets an extension revoked. The carrier is a top-level `scopeReasons` object, scope name to sentence, beside `scopes` rather than inside it, so no manifest that has ever shipped changes shape:

```
{
  "id": "com.example.thing",
  "schemaVersion": 2,
  "scopes": [ "playback:control", "library:read" ],
  "scopeReasons": {
    "playback:control": "to pause the music when a call comes in",
    "library:read": "to show what you are listening to on the lock screen"
  }
}
```

**Declare `schemaVersion: 2` and give every scope its sentence** — that is how a charm written to this document registers. Version 1, declared or absent, is tolerated for manifests that predate the field: such a charm registers exactly as it always did, and pays for it on the approval row, where each capability it asks for reads *no reason given*. A sentence that *is* present is judged at every version, because one that cannot be shown is not made showable by the version it arrived under.

**The shape:** one line, at most 200 characters, trimmed — a row beside a capability’s name, not a paragraph and not a layout you control. **The same set as `scopes`:** a scope with no sentence is `SCOPE_RATIONALE_MISSING` (the one refusal the version gates), a sentence that will not fit is `SCOPE_RATIONALE_INVALID`, and a sentence for a scope you did not ask for is `SCOPE_RATIONALE_UNKNOWN` — refused, not dropped, because words nobody will ever read are worth being told about while you are still here. All three name the offending scope, and all three are answered before a byte is written.

**Where it is read.** Verbatim on the Charm Manager’s approval row, beside the capability’s own sentence, which is ours; and again on the runtime prompt, on its own line and labelled *They say:*, in quotes. The two are never merged: ours says what the capability lets *anybody* do, yours is a claim you make about your own application, and an unlabelled sentence of yours would borrow the standing of ours. Nothing is composed on your behalf — a record carrying no sentence shows only ours, and says so. **Rewording it takes the approval back**, which is the fourth trigger above.

### Automation classes

Split against what they actually do, because “automation” is too coarse for a person to authorize honestly — waking a machine and typing on it are not the same decision:

| Class | What a person is agreeing to |
|---|---|
| Automation status | see whether automation is available |
| Remote wake | wake this computer |
| Library maintenance | trigger a library scan |
| Saved automations | run a routine **the person wrote themselves** |
| Remote control | type and click on this computer — **the heavy one** |

**Remote control is withheld from nobody — and it is not open to anyone either.** It is a deliberate grant made at the console: named plainly, granted at the MusicBee machine, visible and revocable. Any caller may ask for it and no publisher is privileged in asking; what it never is, for us or for you, is on by default.

**How this meets the surface that exists today.** These routes are currently governed by one switch the person at the console controls. The classes above apply to *registered* callers presenting a ticket — they are not yet enforced against an unregistered caller, whose behavior is the open switch and is unchanged. Whether that changes is the break described above: announced, versioned, never silent.

## Enforcement — the ticket and the errors

- The ticket is **opaque**, 32 random bytes, and travels in the **`X-MBXHub-Ticket`** header. Never in a query string — those end up in logs and referrers; one there is refused even when valid.
- It is checked **once, at dispatch**. Existing endpoints are not rewritten to know about it.
- **No ticket means today’s behavior.** That is the compatibility door.
- **It does not expire, and there is nothing to refresh.** A ticket ends in exactly two ways: a person revokes you, or the hub’s own identity is reset. Either way your next privileged call answers `401`, and the remedy is the one you already implement — register again. Do not build a refresh loop; there is nothing for it to do.
- **You need not drop the header to recover.** A ticket, valid or stale, is looked at only when the route you called is gated. On the open surface — which includes `POST /charms/register` — a stale ticket costs you nothing and is not even resolved. Holding a ticket never makes a route harder to reach than it is without one.
- **It is bound to the locality it was issued in.** A ticket is refused from a non-local address regardless of validity. A charm whose code answers remotely therefore cannot present one from its remote half: its privileged reach is what its local half does on its behalf, and everything else it reads is the permanently open surface, which needs no ticket.

| Situation | Response |
|---|---|
| No ticket | unchanged behavior |
| Unknown or revoked ticket | `401` |
| Valid ticket, capability **not granted** | `403` with `"state": "not-granted"`, **naming the capability** |
| Valid ticket, capability **declined** by the person | `403` with `"state": "declined"` — re-asking on retry does nothing; a new `version` may ask again |
| Capability **reserved** | `403` with `"state": "reserved"` and a reason — *not available yet*, never confused with declined or banned |
| Capability **banned** | `403` with `"state": "banned"` — the one answer that means no, and says so |

Every `403` carries `"capability": "<name>"` so an integration problem is a five-minute fix rather than a support thread, and `state` so the four outcomes are distinguishable on the wire and not only in a person’s head.

### The runtime prompt

**A consent prompt is rendered on the MusicBee machine, by a path that accepts requests only from that machine.** That is a security property, not a convenience: a prompt that could be raised from the network would be a consent dialog with a *Trust* button that anyone on the network could put in front of the person. The prompt cannot be relayed to a user somewhere else, precisely because it must not be forgeable from somewhere else.

It is raised only for a charm whose tier is `pinned` or `unsigned` — a signed-and-known charm’s enable is its consent — only for an outward-acting capability, and never for a `page` or `endpoint` charm. **A `proc` charm with no recorded tier is asked as well,** for every outward-acting call: nothing has looked at an image, and an absence of evidence is not evidence of quiet. The *kind* is what settles a page, not the tier — a page carries no tier either, and a rule keyed on tier alone would be relying on that staying true. It leads with the tier, names one capability in the capability’s own words, and offers **Not now** (first, and the default) or **Trust**.

- **A *Trust* click cannot walk past a refusal.** After *Trust* the whole dispatch decision runs again from scratch; a capability that is banned, reserved or declined stays refused. Trust adds an allowance, it never removes a rule.
- ***Trust* is not a grant.** It is remembered in memory for that charm and that target for the life of the MBXHub process, and spreads to no other capability or charm. A persistent grant is made only in the Charm Manager. *Not now* silences that capability for the session. **A person’s absence is never a yes:** if nothing can render the prompt, the answer is *not now*.
- The prompt carries a nonce the hub minted, and the answer rides back on the same exchange — no inbound route can answer a question that was not asked.

**Known limit today:** for the network-devices capability a *Trust* is remembered per capability, not per device — the destination lives in the request body, which the dispatch check does not read — so a second device does not re-prompt within the session. Stated so nobody relies on the finer grain.

**The prompt can be switched off, and a charm cannot switch it off.** *Show runtime prompts* sits in the Charm Manager tab, on by default. Off means every question that would have been asked answers *not now*, silently and immediately: the call gets the same `403 not-granted` a person clicking *Not now* would have produced, nothing is granted, and nothing is declined for the session — the answer is held as an absence, on a short cooldown, so turning prompts back on lets the question reach a person again without a restart. **What you may do is identical either way:** the switch sets how loud MBXHub is, never what is granted.

> **Grants live at the console, and only there.** Approving, declining, revoking and banning happen in MBXHub settings → *Charm Manager* tab, on the MusicBee machine. There is no REST route and no web page that grants a capability. The route that writes settings answers `403` for any key in the console-only categories, naming the offending keys, and applies **nothing** for that request — pairing a protected key with an ordinary one writes neither. A control that decides whether a person is consulted must not be reachable by the software that wants their answer.

**Revocation is survivable by design.** When a person revokes you, your next privileged call returns `401` and everything unprivileged keeps working. **The expected behavior is that you degrade to what you did before you registered.** An extension that breaks on revocation is treating a person’s decision as a fault.

**Pages we host are subject to the same rules.** If MBXHub displays your page inside one of its own surfaces, that page reaches privileged actions only within the capabilities you were granted. The convenience of being hosted does not carry our authority.

## Surfaces — where a charm appears

**Inside MBXHub’s own pages is where a charm renders by default.** When the MBXHub Shell is running, a charm can also be popped out to a desktop window of its own — that needs no MusicBee surface and no restart. Without the Shell there is no window to pop out to and the charm stays inside the pages; a plugin-only install deliberately shows no pop-out.

**A registration is not on a surface until a person approves it.** Announcing yourself puts a manifest in the folder the charm surfaces read, so without this rule registering would put a button on somebody’s bar that nobody said yes to. A registration that is *pending* or *revoked* is therefore not rendered — not on the dashboard bar, not on the Charm Bar rail, not as a window — while the record itself is untouched and still listed in the Charm Manager, which is where a person sees what is waiting on them. A manifest carrying no registration block at all renders as it always did; absence means *not a registration*, never *pending*. **Approval takes effect without a restart:** the surfaces re-read the folder when the registry says something was written, so approving or revoking reaches the screen on the next render.

**A framed charm’s page is not isolated from the page that frames it, and that is a convention rather than a boundary.** A charm rendered inside one of our pages is loaded in an ordinary un-sandboxed frame: what stops it reaching a privileged action is the host page’s fixed list of open verbs — a host page performs only those for a page it frames, and anything else is refused with one log line naming the verb — not the browser. That is honest for the pages we ship and for a page somebody with access to the machine dropped into their own charms folder, but it is **not** a boundary we can offer for a page we did not author. Treat the frame as a rendering arrangement between parties who already trust each other, and read the enforcement section for the boundary that is real: the dispatch check, which no page can talk its way past, because no page holds a ticket.

### The `menu` placement

**An approved charm’s `menu` entries appear under *Tools → MBXHub* at MusicBee’s next start**, one submenu per charm, named after the charm’s `label` — its `id` when the label is blank. Only an `active` registration gets one, and a manifest with no `registration` block gets nothing here. That is the opposite reading from what renders on the charm bar, deliberately: a button on the bar is something a person can scroll past, an item in MusicBee’s own chrome is not.

**The status is re-checked at the click**, against the charm as it is on disk at that moment, so a charm revoked after the menu was built is refused when its item is clicked and the reason is logged. MusicBee has no API for removing a menu item, so the item stays visible until MusicBee next starts — what is gated is the click, not the drawing. A click that passes delivers the activation by your kind, and starts your `launch` target as well when the entry’s action is the bare `launch` verb.

### Delivery — how an activation reaches you

**Delivery is part of the contract.** A menu item whose click reaches nothing is worse than no menu item. When a person activates a surface you registered, the event is delivered to you the way your kind is reached, and registering a surface and receiving its events are one feature, not two. The event is `CharmActivated`, and its payload is the same either way:

```
{ "event": "CharmActivated",
  "data": { "charmId": "com.example.thing", "event": "activated", "placement": "menu",
            "entryIndex": 0, "entryLabel": "Open Thing", "atUtc": "…" } }
```

- **A `proc` charm reads it on its own socket.** Connect to the hub’s WebSocket at `ws://<host>/ws` from this machine and name yourself by sending `{"bind": "<your ticket>"}` — the ticket you were handed at registration. Binding is **loopback only**: a socket from across the network is not bound and is not told whether the ticket it presented was real, and it carries on as an ordinary event socket either way. A bound socket receives its own charm’s activations **whatever it subscribed to**, so a forgotten subscription cannot turn a menu item into a click that does nothing. There is no unbind; the connection is the lifetime.
- **A `proc` charm with no socket connected falls back to its `endpoint`**, and an `endpoint` charm has that route and no other. The POST is `http` or `https` only, to a private or loopback host — checked as a string and **never resolved**, because a name whose owner controls the answer is not a boundary. The whole call gets 2000 ms, redirects are not followed, and the response body is never read: the status code is all delivery needs.
- **A `page` charm’s activation is broadcast**, because the hub has no handle on the frame — the dashboard does, and its socket handler forwards the event into the right frame. There is nothing for a page to bind.
- **A `proc` with neither a bound socket nor an `endpoint`** is the one case where nothing can be done, and it is logged as a warning naming the gap. A click that reaches nothing *silently* is worse than one that reaches nothing, because the person has nowhere to look.

### Launching — what a `launch` target may start

A `menu` entry whose action is the bare verb `launch` starts the manifest’s `launch` field when a person clicks it. The target is always that field — never anything in the entry, never anything a caller supplies — so the string the person approved is the string that runs, and only an `active` charm launches at all. Two kinds of target exist, and they are judged differently.

**An executable is judged by identity.** It must be the image the hub watched register — the same file after path normalization, compared case-insensitively — so a charm may launch itself and nothing else, and a sibling file in a folder it can already write to is not itself. Arguments after the image are passed through verbatim. No list applies to an executable.

**A URL scheme is judged by policy, in a fixed order, and the order is the whole shape.**

- **The hub’s deny-list, first, and it wins.** A closed set carried in code: `javascript`, `vbscript`, `data`, `file`, `http`, `https`, `ms-msdt`, `ms-appinstaller`, `shell`, `search-ms`, `res`, `about`, `microsoft-edge`, **and every scheme beginning `ms-`**. Those are script hosts, document handlers and shell verbs rather than app launchers; `http` and `https` are refused because a page is a `webapp` and that keeps `launch` meaning one thing; `microsoft-edge` is the same door one hop along, since the scheme takes a URL and hands it to a browser. A denied scheme is refused on every install, whatever the allow-list says. Naming it on the allow-list does not buy it back. No vendor scheme is on the list.
- **The operator’s allow-list, second.** A console-only setting, `charmLaunchAllowedSchemes`, which **ships as `*`**: every scheme the deny-list has not refused, which is exactly what every install did before the setting existed. A list of names allows only those — exact names, compared case-insensitively, separated by commas, semicolons or whitespace, a trailing `:` tolerated, and a `*` anywhere in the list means everything. **There are no globs**, so a per-install scheme such as `FirefoxURL-<hash>` has to be written out by its full name. **An empty list allows no scheme at all** — that is the locked-down install. Executables are unaffected either way, because identity judges them.

**What it does, and what it does not.** At `*` it does not make a stock install safer — the deny-list is still the only thing standing, as before. What it does is make a *deliberate* install lockable. It is a security setting, so it is absent from the web settings page and from `GET /system/settings/schema`, refused by `PUT /system/config` with `403 SETTING_CONSOLE_ONLY`, and answered in one place: *MBXHub settings → API Access tab → Remote Control → “Launch allow-list (schemes)”*.

**What you see, and what you do not.** None of this reaches the wire: registration answers the same whether or not the list would refuse you, because the launch is judged at the click and not at registration. A refused click starts nothing and writes one warning to the hub’s log naming the scheme and which list refused it. The person approving you sees it earlier — the Charm Manager’s approval detail reads *Refused: … is not on the launch allow-list* before the yes, so an approval that would start nothing is visible at the one moment that is worth knowing. So name the schemes your charm relies on in your own documentation: the operator who has narrowed the list is the only one who can add yours.

**The residual, stated rather than hidden:** a vendor scheme can be registered to anything on the machine. That is the shell’s contract rather than ours, and a person approving `Launch: steam://run/4813240` is approving that string. The Charm Manager says so on the row.

The remaining MusicBee-side doors — a navigator node, a tab, a hotkey, a background-task status line — are not in this build, and none of them ships without its delivery path.

## Trust — it sets the noise, never the capability

Signature and catalog status decide **how loudly you have to ask**, never **what you may hold**.

| Tier | Evidence required |
|---|---|
| `halrad-signed` | shipped by us, or signed by our key |
| `verified` | an Authenticode signature that chains to a trusted root — a code-signing certificate from a public CA |
| `pinned` | a signature that does **not** chain, first seen at install and continuous since. **Not reachable in this build** — see below |
| `unsigned` | no signature at all — **and, today, any signature that does not chain** |

**Why `pinned` is not reachable, stated rather than hidden.** The operating system check this build relies on reports one state for “the signature does not chain to a trusted root” whether the cause is a self-signed certificate, an expired one, or a **tampered image** — and a patched binary must never be quieter than an honest unsigned one. So until the check can tell those apart, every non-chaining signature tiers as `unsigned`. A self-signed publisher is exactly as loud as an unsigned one today, and reaches quiet only through a certificate that chains.

- Being signed and known buys **quiet**. Being unknown costs **interruption** — prompts, a visible tier — never function.
- **Refusal is reserved for banned.** Absent a ban the answer is yes-with-friction.
- An unsigned extension is a tier, not an error. Distribution by any channel stays viable.

**Certificate renewal must not cost you a tier.** Continuity is judged on the certificate subject, not the key: a renewed certificate with the same subject keeps your tier, and a change of subject is treated as a new publisher and asks the person once — one re-consent, not a drop to `unsigned`. *The successor mechanism for a subject change is not in this build.*

**The ladder is evidence about an executable, and that is the only kind it applies to.** A `page` is a file on disk, and something with access to that machine put it there — that is its provenance, and a signature would re-prove locally what local access already established. An `endpoint` has no local image and is not asked for one; what can be known about it is where it answers from — the local machine, this subnet, or beyond — and whether that has changed since first sight. So **a `page` or an `endpoint` carries no tier at all**, rather than sitting at the bottom of the ladder: `unsigned` is a statement about an executable that was not signed, and neither of them is an executable.

> **The hub applies the same check to itself.** The plugin verifies its own image against the Halrad signing certificate the way it verifies a `proc` charm — a valid signature *and* our certificate, since either alone can be satisfied by a file that should not pass — and reports the result on `GET /system/version` as `signature` and `signatureVerified`. If you want to know you are talking to a shipped, unaltered build, read that. “Could not determine” is its own state and is never reported as verified.

## License grants

A signed grant `{publisher, class, expiry}` may unlock a license-gated class. Verification is offline, at registration, and re-checked on every start; a verified grant is recorded on the registration as a *licensed* class and the dispatch check honors it.

- **`publisher` is the string we signed.** At issue it is bound to your certificate subject, so the grant and your signature name the same party. There is no separate registry of publishers to be in.
- **Grants bind to the publisher**, not to an installation. Resetting or reinstalling MBXHub does not cost you a license you hold.
- **A tampered or expired grant fails closed for that class only.** Everything else continues to work.
- **The local clock decides expiry.** Verification is ECDSA P-256 against a public key compiled into the build and selected by `keyId` — no new dependency, and **no network call, ever**. A disconnected machine never stalls, and we never phone home to check. A wrong clock fails closed for that class only, the same as expiry, which is checked at every decision rather than once at the write.
- **The wire shape.** `base64url(payloadJson) "." base64url(signature)`. The payload is `{ "publisher", "class", "expiry" (ISO-8601 UTC), "keyId" }` and is verified over the **bytes exactly as sent** — never a re-serialization, because two serializers disagree on whitespace and a grant that verifies on one build and fails on the next would look like tampering. The signature is the raw IEEE P1363 form — `r` followed by `s`, 64 bytes for P-256 — **not DER**.
- **A license is outranked.** A ban, a decline, a reservation and a revoked record each outrank a valid license, and a license never loosens the ticket rules.
- **Our signing key will rotate, and your grant survives it.** Every grant names the key that signed it, and a build carries the public halves of the current key **and its predecessors**. A grant issued under a retired key keeps verifying for as long as it has not expired; you are never asked to obtain a new grant because *we* changed a key.
- **A grant is presented at registration**, in the manifest. It is not a per-request credential and never travels in a header; the ticket does that work.

## Versioning — what evolves, on separate clocks

Named separately, because merging any two of them is how a format rots.

| What | Field | Who owns it | Moves when | In this build |
|---|---|---|---|---|
| Manifest format | `schemaVersion` | us | a field or verb is added — including a new `kind` value | parsed; absent means 1 |
| Your release | `version` | you | you ship | parsed and recorded |
| The contract | `apiVersion` | us | the API surface changes | `"1"`, answered by `/ping` and by every registration response |
| Capability maturity | `lab` / `optional` / `default` | us, per capability | a capability becomes safe to offer | every capability carries one and `GET /charms/capabilities` states it; this build uses `lab` and `default` |

**What `apiVersion` is intended to promise:** under a fixed value we will not remove a field, change a response type, or retire a route; we may add. A change gets a version bump and notice two releases ahead. It is deliberately not our product version, and it is the one to pin against. That promise is proposed rather than committed — the one commitment on this page is the open read surface, and it says so on purpose.

## What is checked

A specification that cannot be tested is a wish. The charms that ship with MBXHub are the regression corpus — written before any of this existed, and never edited to make a test pass. Each promise below is checked on every run:

| Promise | What is checked |
|---|---|
| The one commitment | An existing integration runs unchanged against a build with registration in it, and the ten frozen endpoints answer as before — from an unregistered caller and from a registered one alike. This is the regression that matters most |
| `kind` and `schemaVersion` absent | Every shipping charm loads unchanged on a build that knows both fields; each is treated as a `page` and as a version 1 manifest; a declared kind wins, and an unknown one is kept verbatim rather than coerced |
| Icons cannot carry markup that runs | An icon that is neither plain text nor an inert `<svg>` renders encoded — on the button, on `expand[]` entries, and on an `action-menu` trigger and its items alike |
| Finding the hub | The ladder is the even ports 8080–8098, and `service` — not `apiVersion` — is what identifies us; `apiVersion` moves on its own clock |
| Registration is idempotent | Registering twice with one `id` and unchanged content yields one `charmId` and one record, including under concurrent callers |
| An update keeps the hub’s half | A changed manifest replaces the manifest half and keeps the `charmId`, first-registered time, status and grants; a new scope is not silently granted, and a dropped scope is released |
| Pending is not degraded | A pending caller’s responses are identical to an unregistered caller’s, asserted against the same frozen rows |
| Refusals are named, never silent | Each refusal code answers on its own, a refusal writes nothing, and it still carries `apiVersion` |
| The registry ceiling | A new `id` is refused at 500; an `id` already on file still updates at the ceiling; charms we ship do not count toward it |
| Reading a record withholds the hub’s half | `GET /charms/{id}` returns identity, status and scopes and never the observed image path or its digest under any name; an unknown or impossible id is a `404` |
| Registration is local only | A registration from a non-local address is refused before its body is judged, and nothing is written |
| An `id` is claimed | A second registration of an existing `id` from a different publisher is refused and the first record is unchanged; a revoked `id` stays claimed by its publisher |
| An entry may not carry a launch target | An `expand[]` entry written as `launch <target>` is refused `400 INVALID_ACTION` naming the entry, and nothing is written; the match is on the first token, so `launcher` is a different verb |
| The ticket is withheld rather than mis-delivered | A registration whose caller cannot be resolved answers `active` with `ticketWithheld` and no `ticket`; the plaintext stays parked, the next resolvable announcement delivers it exactly once, and that lookup writes no image, digest or tier |
| Five things take an approval back | A different executable, a different `launch` target, a different placement set and a reworded `scopeReasons` sentence for a scope already asked for each return an `active` record to `pending` and clear its ticket, with the grants kept; a record approved under a different hub install does the same and releases the grants; an ordinary version bump — new labels, icons, actions, order — moves nothing. The count of triggers is read from the source itself, so a sixth cannot land unnamed |
| Your sentence is carried and shown as you wrote it | `scopeReasons` survives the round trip through the record and reaches the approval row and the runtime prompt unaltered, `&` and `<` included; a record with no `scopeReasons` at all still renders, falling back to the capability’s own sentence and saying *no reason given*; a missing, blank, multi-line, over-length or unrequested sentence is refused by name with nothing written, and a version-1 manifest may omit them but is still held to any sentence it does send |
| Partial grant | Ask four, granted three: `grantedScopes` is exactly those three and the fourth returns `403 not-granted` |
| An unknown scope is refused by name | Requesting a scope the build does not know returns `400` naming it, and nothing is registered |
| The capability list is readable | `GET /charms/capabilities` lists every scope the build knows, each with maturity and reserved status, and every name it lists is accepted at registration |
| Reserved is refused at the source | Approving a reserved scope is refused and writes nothing; only a requested capability is offered for approval |
| Only the unknown are prompted | A signed-and-known charm never raises a runtime prompt; `pinned` and `unsigned` do; a `page` or `endpoint` charm is never prompted over, whatever is stamped on it; only outward-acting capabilities prompt at all |
| The prompt is local-only and attributable | A grant prompt from a non-local caller is refused with the reason; one with no nonce is refused; an ordinary notification from the network still works |
| A *Trust* click cannot walk past a refusal | After *Trust* the whole decision runs again from scratch; a banned, reserved or declined capability stays refused |
| Answers are remembered as designed | *Trust* is remembered for the charm and target it was given for, for the process lifetime, and spreads no further; *Not now* silences that capability for the session across every target; nothing is recorded against an anonymous caller |
| Four states on the wire | `not-granted`, `declined`, `reserved` and `banned` are distinguishable by `state`, each naming the capability, and a refusal never carries a state it has no business naming |
| Prompts off changes the noise, not the answer | With the switch off nothing reaches a person, the refusal is the one *Not now* produces, nothing is granted and nothing is declined; the absence expires on its cooldown and never overwrites an answer a person already gave |
| Console-only settings are not writable | A console-only key sent to the config route is refused by name and nothing is written; pairing one with an ordinary key writes neither; none of them is drawn on the web page |
| An unapproved registration is not rendered | A `pending` or `revoked` registration is absent from the one list every charm surface reads while remaining on disk and on the record; a manifest with no registration block renders as it always did |
| An approval reaches the surface without a restart | Approving puts the charm on the list the surfaces read on the next render, and revoking takes it off, with no plugin restart |
| A menu click is gated at the click | Only an `active` registration is asked for a menu item, and a charm revoked after the menu was built is refused when its item is clicked, with the reason logged |
| An activation reaches the charm | The event goes by the route the kind has — the bound socket, the declared endpoint, or the frame — and a bound socket receives its own charm’s activations whatever it subscribed to; the endpoint POST follows no redirect, reads no body and is bounded at 2000 ms |
| Launch — deny first, deny wins | A scheme on the deny-list is refused under every allow-list, the wildcard and an entry naming it included, and the refusal names the deny-list rather than the setting |
| Launch — the wildcard ships, a list narrows | A fresh settings object carries `*`, and no list at all decides as `*` does; a list of names allows only what it names, case and a trailing `:` notwithstanding; an empty list allows no scheme and leaves executables alone |
| Launch — console-only, and said before the yes | `charmLaunchAllowedSchemes` is not drawn on the web settings page and is refused by the write gate with its stored value unchanged; a scheme off the list reads as refused on the Charm Manager row before approval |
| The ticket is header-only, and local | The ticket works in `X-MBXHub-Ticket` and is rejected in a query string even beside a good header; a valid ticket from a non-local address is refused |
| A stale ticket costs nothing on the open surface | With a revoked or foreign ticket in the header, an ungated route — `POST /charms/register` included — answers exactly as it does with no header, and the registry is not consulted |
| Revocation is survivable | After revocation the next privileged call returns `401` and the record survives; the extension keeps running |
| Hosted pages are gated | A host page performs only the open verbs for a page it frames; anything else is refused with one log line naming the verb |
| Pages are not prompted over | A `page` charm is not placed on the tier ladder and generates no trust prompt, signed or not. This is a no-change check: it fails if registration makes an included page noisier than it is now |
| A tier is not stated before it is known | A registration’s `tier` is absent rather than guessed while nothing has observed an image |
| A license fails closed | A modified grant, an expired one, a wrong clock and an unknown key each leave that class locked and everything else working; expiry is checked at every decision, not at the write; a retired key still verifies a grant issued under it; a ban, a decline, a reservation and a revoked record each outrank a license |
| A license is signed over the bytes as sent | Whitespace inside the payload is preserved and verified as-is, and the signature is P1363 `r‖s` |

**Not checked, because it is not in this build:** the `placement` field’s absent and unknown behavior asserted across the whole shipping corpus, a single-action shorthand reaching a menu item, delivery for the MusicBee-side doors other than the menu, a renewed certificate keeping its tier, and what a person is shown about an `endpoint` charm’s reach. Each of those is described above as designed rather than done, and none of them is promised on a date.

**Built but not yet walked, which is a different thing and is worth saying separately:** the click on a real MusicBee *Tools* menu, and a launch attempted on an install whose allow-list has been narrowed. The decision paths behind both are pure and are pinned above; what has not been exercised is the last step that starts a process.

## Telling us it is wrong

The most useful thing you can send back is the endpoint your integration reads that is not on the frozen list, or the clause here you would not implement. A clause nobody adopts is worse than a missing one, because it looks like a contract.

Charms SDK — September 5, 2026. Describes the behavior of the current MBXHub build; anything marked *not in this build* is designed and not yet shipped, with no date attached. See also [the REST API](https://mbxhub.com/api.html) and [Charms](/features.html#charms).
