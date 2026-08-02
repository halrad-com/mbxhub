# DynaPort — a well-behaved listener port

A short pattern (and a few library calls) that stop a LAN HTTP/media server from
ever stranding a user on a busy port. Both halves live in **Firebug** (Apache-2.0),
so this is just the calls — no source to vendor.

## The idea

Many network media receivers are **handed** the full media URL — host *and* port —
by the sender and simply fetch it; they never discover or guess the port. So a
server's listener port is its own business, and any fixed value (8080, 8081,
8000…) eventually collides with something else on the machine. Instead of
hardcoding: **pick a free port, persist it, and authorize that exact port** with the
firewall / URL-ACL.

The two halves, both in Firebug:

| Concern | Firebug API |
|--------|-------------|
| Pick a free port | `Firebug.PortPicker` — `Resolve` / `Pick` / `PickPair` / `IsFree` |
| Authorize the port (URL ACL + firewall) | `Firebug.FirebugManager` — `AddUrlAcl` / `AddTcpInboundRule`, or the `firebug` CLI |

## The calls

```csharp
var fb = new Firebug.FirebugManager();

// 1. Pick — reuse the saved port if still free, else ladder up from 8000.
int port = Firebug.PortPicker.Resolve(settings.Port, preferred: 8000, log: Log);

// 2. Persist — keeps the URL ACL / firewall rule / bookmarks aligned to one number.
settings.Port = port;
settings.Save();

// 3. Authorize that exact port (needs elevation — see note).
fb.AddUrlAcl(port);                       // non-admin process may then bind a LAN prefix
fb.AddTcpInboundRule("MyApp", port);      // peers on the LAN can reach it

// 4. Bind, then hand the receiver the URL. It never guesses the port.
listener.Prefixes.Add($"http://*:{port}/");
string mediaUrl = $"http://{GetLocalIP()}:{port}/";
```

Prefer the CLI? Same effect, from an elevated shell:

```
firebug add --name "MyApp" --port <boundPort> --urlacl
```

## Two things to get right

- **Authorize the *bound* port, not a settings value.** The firewall/URL-ACL and the
  listener must agree on one number — feed step 3 the port step 1 returned and step 2
  saved, never a stale UI value.
- **Persist, don't re-roll.** The URL ACL and firewall rule are keyed to the port, so
  a port that changes every launch needs re-authorizing (admin) every launch. Pick
  once, keep it. Only re-pick when the saved port is genuinely busy.

## Windows footnote

Serving a receiver on the LAN means binding a non-loopback prefix (`localhost` won't
reach another device). That needs a **URL-ACL** reservation (or an elevated process)
plus a firewall rule — which is exactly what `AddUrlAcl` + `AddTcpInboundRule` do.
Run them from a small elevated helper (`ProcessStartInfo { Verb = "runas" }`), not
from inside a non-elevated host process.

## Firebug

Both APIs ship in [Firebug](https://github.com/halrad-com/firebug) — a small,
dependency-free (BCL-only) utility that consolidates the firewall / URL-ACL /
free-port patterns from several projects into one tried-and-true place. Reference the
library, or shell out to `firebug.exe`.
