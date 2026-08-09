# Field Notes

Bench-measured findings from MBXHub development. The rule here: everything stated was
verified against real hardware or real logs; anything inferred is labeled as such, and
"unknown" is a valid finding. These are working notes, not polished manuals — they exist
because the next person (often us, six months later) shouldn't have to rediscover any of it.

| Note | One-liner |
|------|-----------|
| [WiiM Ultra field notes](wiim-ultra-field-notes.md) | Owning a WiiM Ultra over its HTTP API — no vendor app required, including an undocumented timezone command |
| [Windows volume steps](windows-volume-steps.md) | Why volume keys move in 2% jumps, why the registry hack is a myth, and every real way to get finer control |
| [SMTC echo diagnosis](smtc-echo-diagnosis.md) | A play/pause "stuck toggle" bug: one button press, two delivery paths, and how logs pinned it |
| [Bench tools](bench-tools.md) | The tools that keep earning their place on the bench |

Runnable companions live under [`samples/`](../../samples/) — see
[`samples/wiim-bench/`](../../samples/wiim-bench/) for the live device watcher used in the
WiiM notes.
