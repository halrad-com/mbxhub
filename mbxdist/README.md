# MBXDist

Local distribution & update client for the MBXHub family (`halrad.mbxhub.*`).

One signed `mbxdist.exe`, two faces:

- **Run it directly** — interactive console UX: shows installed vs recommended per package, prompts to apply.
- **Invoke it** (ARIA / Task Scheduler / scripts) — verbs + flags + exit codes, no prompts.

```
mbxdist                      interactive UX (real console)
mbxdist check [--json]       diff vs the recommendation      exit 0 up-to-date / 10 update available
mbxdist update [--yes]       download + verify + apply       exit 0 / 20 pending-restart / 30 verify-fail / 1
mbxdist status               installed versions
mbxdist list                 the embedded catalog
mbxdist init <root> <path>   map an install target root
mbxdist version
flags: --feed <url>  --json  --yes  --no-self-update
```

**Trust model:** two checks before anything touches disk, fail closed (exit 30). First-party binaries are
Authenticode-signed; the client accepts them only if the signature is valid **and** the signer is the
certificate pinned in the client. Third-party binaries (ffmpeg, essentia — not signed by us) are instead
verified by **SHA-256 against the manifest**, and that manifest is trustworthy because it is **embedded in
the signed client** — so the recommendation itself inherits the client's signature. Promoting a release =
shipping a new signed `mbxdist.exe` + bumping the `mbxdist/latest.txt` marker, which clients pick up via
verified self-update. Every download is SHA-256-checked regardless. Locked targets (e.g. a plugin DLL held
by a running app) are staged as `<target>.pending` and swapped on a later run.

Layout: `src/MBXDist.Core` (cross-platform engine, fully unit-tested) · `src/MBXDist.Platform.Windows`
(WinVerifyTrust) · `src/MBXDist.App` (the dual-mode EXE). Build: `dotnet build mbxdist/MBXDist.sln` ·
Test: `dotnet test mbxdist/MBXDist.sln`.

Status: WIP — engine + client functional end-to-end against a local feed; awaiting real pinned thumbprint,
published feed content, and release signing.
