# hello-endpoint

A charm **MBXHub calls**, rather than one that calls MBXHub.

`hello-charm` is a `proc` charm: it holds a WebSocket open and its events arrive on it.
This is the other shape. An `endpoint` charm declares an **address**, and when a person activates it
the hub POSTs the event there. Nothing is held open, and the charm need not be running when the
person clicks — if it is not, the delivery fails and the hub log says so.

```
dotnet build
dotnet run
```

Run it on the MusicBee machine. `--help` lists the flags.

## What it does, in order

1. **Opens the address first**, `http://127.0.0.1:9099/charm/` by default. It has to be real before
   it is declared: a hub delivering to an address nobody is on gets a connection refused, which
   reads like a broken charm rather than a sample that had not opened its socket yet.
2. `GET /ping` — is this MBXHub.
3. `POST /charms/register` with `kind: "endpoint"` and that address.
4. Waits for a person to approve it in the Charm Manager, then serves.

## The rule this sample exists to show

**Registration is loopback-only. The address you declare is not.**

The announcement must come from the MusicBee machine. What answers may be anything on the same
private network — the hub checks only that your `endpoint` is `http` or `https` on a private or
loopback host, and refuses a public one. That is the local-half / remote-half split in the SDK: an
application with a service elsewhere registers *through* something local — its installer, its
launcher, a small agent — and that local half is what makes the announcement.

This sample runs both halves in one process because that is the smallest thing that demonstrates it.

**An endpoint charm's remote half cannot hold a ticket**, because a ticket is bound to where it was
issued and is refused off the machine. Its privileged reach is whatever its local half does on its
behalf; everything else it reads is the permanently open surface, which needs no ticket at all.

## Why it still needs an approval

Not for a credential — this sample never makes a gated call and never collects a ticket. Being
called is not a privilege. It needs the approval because **nothing unapproved is drawn on a charm
surface**, so until a person approves it there is no menu entry to click and nothing to deliver.

## What arrives

A JSON POST, `Content-Type: application/json`:

```json
{ "charmId": "com.halrad.samples.hello-endpoint", "event": "activated",
  "placement": "menu", "entryIndex": 0, "entryLabel": "Ping the endpoint",
  "atUtc": "2026-09-06T04:15:22.1234567Z" }
```

Three properties of that call are worth writing your handler against:

- **Two seconds, for the whole call.** Answer first and do your work after. A handler that finishes
  its job before replying is a handler that times out, and a timed-out delivery is reported as
  undelivered even though it arrived.
- **The status code is all that is read.** Any `2xx` is delivered; anything else is not, and the hub
  logs the code. Your response body is never read.
- **No redirects are followed.** The answer has to come from the host you declared.

## The manifest

```json
{
  "id": "com.halrad.samples.hello-endpoint",
  "schemaVersion": 2,
  "kind": "endpoint",
  "label": "Hello Endpoint",
  "publisher": "HALRAD LLC (sample)",
  "version": "0.1.0",
  "endpoint": "http://127.0.0.1:9099/charm/",
  "expand": [
    { "label": "Ping the endpoint", "placement": "menu" }
  ]
}
```

- `endpoint` is filled in at startup from the flags this run was given, the same way `hello-charm`
  fills in its `launch`. It is your word either way: the hub checks the shape of the address, never
  that anything is actually there.
- The entry declares **no action**, deliberately. A menu click delivers the activation *by kind*
  whatever the entry's verb is, so an entry with no verb is a clean way to say "the activation is
  the whole point". A `webapp` or a `post` there would do its own thing as well.
- `placement: "menu"` puts it under MusicBee's Tools menu. Menus are read when MusicBee starts, so
  the entry appears after the next restart — the registration answer says so in `restartRequired`.
- No `scopes`, so no `scopeReasons`: this sample asks for nothing. `hello-charm` covers permissions.

## If nothing arrives

- **No menu entry** — MusicBee has not restarted since you registered.
- **The entry is there and nothing happens** — this process is not running, or is on a different
  port than the one it registered. Register again after changing `--listen-port`; the address is
  part of the manifest.
- **`Access is denied` on startup** — Windows reserves non-loopback URL prefixes. Keep `127.0.0.1`,
  or grant the one you want once from an elevated prompt:
  `netsh http add urlacl url=http://<host>:<port>/charm/ user=%USERNAME%`
