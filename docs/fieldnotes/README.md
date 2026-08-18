# Field Notes

Bench-measured findings from MBXHub development. The rule here: everything stated was
verified against real hardware or real logs; anything inferred is labeled as such, and
"unknown" is a valid finding. These are working notes, not polished manuals — they exist
because the next person (often us, six months later) shouldn't have to rediscover any of it.

| Note                                                | One-liner                                                                                                  |
| --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| [WiiM Ultra field notes](wiim-ultra-field-notes.md) | Owning a WiiM Ultra over its HTTP API — no vendor app required, including an undocumented timezone command |
| [WiiM HTTP API reference](wiim-http-api-reference.md) | Every command as measured — verified/accepted/false-OK/dead, undocumented EQ band read+write, enum tables |
| [Devialet Phantom field notes](devialet-phantom-field-notes.md) | The Phantom's local REST API — source switching by playing, firmware field drift, and the transient-mute trap |
| [Fosi Audio S3 field notes](fosi-s3-field-notes.md) | Not the LinkPlay box the press said — a StreamUnlimited StreamSDK device that serves its own API client and a self-describing settings tree, unauthenticated |
| [Telling devices apart safely](device-identification.md) | Which family a discovered box belongs to — identify from the description you already fetched, why a timeout is not a negative, and what never to send |
| [Windows volume steps](windows-volume-steps.md)     | Why volume keys move in 2% jumps, why the registry hack is a myth, and every real way to get finer control |
| [Bench tools](bench-tools.md)                       | The tools that keep earning their place on the bench                                                       |

Runnable companions live beside the notes — see
[`wiim-samples/`](wiim-samples/) for the live device watcher used in the
WiiM notes, and [`fosi-samples/`](fosi-samples/) for `fosi-probe`, a read-only
first-contact sweep for an unknown network audio device (SSDP census, port plan,
platform test, vendor web surface, UPnP service list). It is device-agnostic; its
first real target, the Fosi S3, is written up above — and the probe's one-glance
LinkPlay-or-not discriminator called it correctly on the first run.


