# hello-charm

The smallest complete MBXHub charm: a console program that registers itself with the hub, waits for a
person to approve it, receives its ticket, makes gated calls, and listens for its own events. Every
request and every answer is printed as it happens.

It is a `proc` charm — a local executable — because that is the kind with a trust tier, a launch target
and a process. If you are writing a desktop app, this is your shape.

## Run it

On the machine where MusicBee and the MBXHub plugin are running:

```
cd samples/hello-charm
dotnet run
```

Options: `--port 8080` (the hub's REST port), `--once` (register and report, do not wait for approval),
`--forget` (discard a stored ticket and start over), `--proxy http://<a-lan-device>/path` (adds the
outward call that raises the runtime prompt).

## What you will see, step by step

**1. Find the hub.** `GET /ping`. The answer must say `service:"MBXHub"`. Anything else on that port is
not the hub, and the sample stops rather than register with it.

**2. Register.** `POST /charms/register` with `manifest.json` as the body. The sample fills in `launch`
with its own executable path first — the hub will only ever launch the image it watched register, so
that value is never hand-typed. The first answer is `status:"pending"` with a `charmId` and an
`apiVersion`. The route answers only callers on the MusicBee machine; from anywhere else it is `403`.

**3. An open route.** `GET /nowplaying` with no ticket. It works. Pending is not degraded: while nobody
has approved you, every open route answers you exactly as it answers anyone.

**4. Approval and the ticket.** A person opens **MusicBee → MBXHub settings → Charm Manager**, finds
*Hello Charm*, reads what it asks for and what it would launch, ticks the row and clicks **Approve**. The sample polls
`GET /charms/{id}` until `status:"active"`, then registers once more — the ticket travels on the first
registration after an approval, exactly once. It is stored under `%LOCALAPPDATA%\hello-charm\` and
reused on later runs; a stored ticket that answers `401` is discarded and the flow starts over.

**5. Gated calls.** With `X-MBXHub-Ticket` in the header:
- `GET /aria/status` — scope `automation:status`, which the manifest asked for → `200`.
- `GET /aria/presets` — scope `automation:presets`, which it did not → `403` with
  `error.state:"not-granted"` and `error.capability:"automation:presets"`. The refusal names the scope;
  asking for it in the manifest is the remedy.
- With `--proxy`, `POST /api/proxy` — scope `proxy:lan`, an outward-acting call. For an unsigned charm
  with prompts on, the person at the MusicBee machine is asked *Not now / Trust*, and the call waits for
  the answer. *Not now* comes back as a `403`; it is the person's answer, not an error.

**6. Receive events.** The sample connects to the hub's WebSocket on the same port and sends
`{"bind":"<ticket>"}`. A bound socket receives its own charm's `CharmActivated` events whatever else it
subscribed to. Click the charm's entry inside MusicBee and the event prints here.

## The manifest

```json
{
  "id": "com.halrad.samples.hello-charm",
  "schemaVersion": 2,
  "kind": "proc",
  "label": "Hello Charm",
  "publisher": "HALRAD LLC (sample)",
  "version": "0.1.0",
  "scopes": ["automation:status"],
  "scopeReasons": {
    "automation:status": "to check whether this hub can run automations before we offer to"
  },
  "launch": "<this executable> --launched",
  "expand": [
    { "label": "Say hello", "placement": "menu", "action": "launch" }
  ]
}
```

- `id` is yours, stable, and claimed: a second registration under a different `publisher` is refused.
- `scopes` are the capabilities you ask for. `GET /charms/capabilities` lists what exists and how
  mature each is. Asking is not being granted.
- `scopeReasons` is one sentence per scope, **in your words**, saying why you want it. The person
  approving you reads it verbatim beside the capability's own sentence on the approval row, and
  again on the runtime prompt labelled *They say:* — it is the only thing about your request they
  see in your words, so write it for them and make it true. One line, at most 200 characters, and
  the same set of names as `scopes`: a missing sentence, one that will not fit on a row, or one
  written for a scope you did not ask for is each refused by name at registration
  (`SCOPE_RATIONALE_MISSING` / `_INVALID` / `_UNKNOWN`), and nothing is written.
  **`schemaVersion: 2` is what says you are writing to this document**, and from there every scope
  needs its sentence. A manifest declaring `1`, or nothing, may omit `scopeReasons` and registers
  as it always did — its rows read *no reason given*. Rewording a sentence for a scope you already
  asked for takes your approval back to `pending` and clears your ticket, like a changed `launch`
  target does, so the first release in which an already-approved charm starts sending sentences
  costs it one re-approval.
- `launch` is what the hub starts when the person clicks your menu entry. An executable must be the
  image that registered — a charm may launch itself and nothing else. A URL scheme (`steam://run/…`) is
  handed to the shell as declared; script and shell schemes are refused. The person running the hub
  can also narrow which schemes launch at all, from the console only: a launch allow-list that ships
  as `*` (everything not refused) and, when it names schemes, allows only those — empty allows none.
  An executable target is judged by the image check above and is not affected. Nothing on the wire
  tells you the list was narrowed; your menu click starts nothing and the hub log says why, and the
  person sees *Refused: … not on the launch allow-list* on your approval row. So name the schemes
  your app relies on in your own README. The entry's `action` is the
  bare verb `launch`: the target always comes from this field, never from the action, so the string the
  person approved is the string that runs. A target written after the verb is refused at registration.
- `placement:"menu"` puts the entry under MusicBee's Tools menu. Menus are read when MusicBee starts, so
  a new entry appears after the next restart; the registration answer will say so in `restartRequired`.

## Which hub this sample expects

Everything here — registration, `restartRequired`, the ticket, gated calls, the socket bind, the menu entry
and `launch` — is in the current hub. Against an older hub the registration answer carries no
`restartRequired` and no menu entry appears; the sample says so at the point it notices rather than
failing. Update the hub rather than the sample.

## Handling refusals in your own app

| Answer | Meaning | Do |
|---|---|---|
| `200` `status:"active"`, no `ticket`, `ticketWithheld:"caller-unresolved"` | approved, but the hub could not identify the process that registered, so it kept the one-shot ticket | register again from the same executable; the next answer whose caller resolves delivers it. Do not revoke |
| `200` `status:"active"`, no `ticket`, no `ticketWithheld` | the ticket was already delivered once | use the one you stored; if it is lost, revoke and approve again in the Charm Manager |
| `401` on a gated call | ticket unknown or revoked | behave as unregistered; keep using the open routes; register again |
| `403` `state:"not-granted"` | the person has not granted that capability | ask for it in the manifest; do not retry in a loop |
| `403` `state:"declined"` | the person said no | stop asking; a new `version` may ask again |
| `403` `state:"reserved"` | not available yet | not a no; check `GET /charms/capabilities` for maturity |
| `403` `state:"banned"` | the one answer that means no | stop |
