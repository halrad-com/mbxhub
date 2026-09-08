# MBXDist

Local distribution and update client for the MBXHub family (`halrad.mbxhub.*`).

Status: WIP. This is unreleased code; there is no deployed-client compatibility or migration contract. The embedded core recommendation is still sample metadata. A real release requires final package artifacts, valid embedded hashes, signing, and explicit publication.

The distribution artifact is a self-contained Windows x64 single-file `mbxdist.exe`. Its embedded catalog and manifests travel inside the file that the operator signs. A normal `dotnet build` output is for development; distribute the **published** EXE.

## Use

Run without arguments in a console for a check followed by an apply prompt. With a verb or redirected output, use the noninteractive CLI.

| Command | Behavior |
|---|---|
| `list` | Show the embedded recommendations. |
| `init <root> <absolute-path>` | Map an install root; `init` alone shows mappings. |
| `check` | Inspect installed packages against the recommendations and report pending or damaged installations. Catalog members not installed are informational. |
| `update [id...]` | Install explicitly selected packages and their dependencies. Without IDs, update only installed packages or interrupted installations. |
| `verify [id...]` | Check installed files using trusted embedded hashes, sizes and signature policy. Without IDs, inspect recorded packages. |
| `repair [id...]` | Restore damaged files when this client carries trusted metadata for the installed version. Otherwise report the unavailable metadata. |
| `status` | Show recorded packages, interrupted operations and target mappings. |
| `version` / `help` | Show client version or usage. |
| `validate-feed --feed-root <absolute-path>` | Validate embedded metadata and the local feed's actual bytes before release staging. |

Options: `--config <state-file>`, `--feed <http-or-https-url>`, `--json`, `--yes`, `--no-self-update`. Options may precede the verb. Unknown verbs, flags and package IDs are errors.

`--dry-run` works with `check`, `update`, and `repair`. It does not self-update, download, apply files, create state, take a file lock, or write diagnostics. It can read installed files to plan the work. `--json` produces machine-readable output; self-update progress goes to stderr and the caller receives the replacement process's exit code.

For an initial installation, choose a package deliberately:

```powershell
.\mbxdist.exe list
.\mbxdist.exe init musicbee-plugins 'C:\Path\To\MusicBee\Plugins'
.\mbxdist.exe update halrad.mbxhub.core --dry-run
.\mbxdist.exe update halrad.mbxhub.core
.\mbxdist.exe verify
```

The sample metadata currently makes the selected-package validation fail; do not replace a placeholder with a guessed hash.

## Trust and recovery

Package files are checked for size and SHA-256 before installation. Resources marked `authenticode: true` also require valid Authenticode plus a pinned **SHA-256 certificate hash**. Unsigned third-party files use `authenticode: false`; their authenticity comes from the hash embedded in the signed client. The pin itself is never supplied by a runtime flag or editable state file.

An apply operation prepares and verifies all resources of a package before changing its targets. Dependencies precede their dependents; a failed or pending dependency blocks the dependent. Targets may not overlap. Replacement files are staged beside their destination so the final replacement stays on one volume.

Locked targets produce `<target>.pending`. Close the application holding the file and repeat `update`. Pending bytes are verified again and can finish offline. A partial installation records intent before any target changes; retries reconcile already-correct targets and pending files against embedded metadata. A package is recorded as installed only after all resources apply. This is recoverable application, not an all-or-nothing transaction across packages.

State defaults to `%LOCALAPPDATA%\MBXDist\mbxdist-state.json`. Each config has its own `.lock`, `.bak`, and `.work` paths. Concurrent operations using the same config are refused. Saves use atomic replacement; an unreadable primary can be recovered from its backup. Bounded structured diagnostics stay under `<config>.work\operations.jsonl`. Remote telemetry is not enabled.

A published client checks for a newer signed client before ordinary `check`/`update`/interactive runs. An unreachable feed leaves it using its current embedded recommendation. Use `--no-self-update` when deliberately working offline. Ordinary development builds do not replace themselves.

Installed versions newer than the recommendation are `ahead` and are **never automatically downgraded**. Publishing a newer client that recommends an older package changes the recommendation; it does not roll back an existing installation. There is no rollback command.

## Exit codes

`0`: success/no actionable installed-package changes. `10`: installed packages need updating, repair, or pending completion. `20`: resource staged pending. `30`: verification failure or trusted metadata unavailable. `1`: argument, dependency, I/O, or other failure. If a pending dependency blocks a requested parent, the invocation reports the blocked dependency as an error rather than claiming that the parent was installed.

## Development and verification

From this directory:

```powershell
dotnet build MBXDist.sln -c Release
dotnet test MBXDist.sln -c Release
```

Core tests cover the engine; Windows tests cover command parsing, executable invocation, handoff, and the update/verify/repair lifecycle. Synthetic feed tests isolate signing from installation behavior. The real positive signature test requires `MBXDIST_SIGNED_FIXTURE` and `MBXDIST_SIGNED_THUMBPRINT` (SHA-256); without them it is skipped. Passing synthetic tests is not proof of a signed public release.

See [PLAYBOOK.md](PLAYBOOK.md) for artifact publishing, promotion, signing and release checks. No package is added merely by making it visible in the catalog: the user selects what to install.
