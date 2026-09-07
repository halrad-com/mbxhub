# hello-spout

An MBXHub charm that **receives** Spout through `mbxspout.dll` and **sends** Spout by linking the
Spout SDK directly.

Two instances of it — one sending, one receiving — is a complete, self-contained demo with no
third-party application anywhere in it.

```cmd
build.cmd
build\Release\hello-spout.exe --send        :: window 1: a moving picture, sent as "HelloSpout"
build\Release\hello-spout.exe --recv        :: window 2: the same picture, received through the DLL
```

---

## The two halves are not the same kind of thing

This is the point of the sample, and the reason to read it before copying from it.

| | Goes through | Supported surface? |
|---|---|---|
| **Receive** | `mbxspout.dll`, over the published C ABI in [`mbxspout/include/mbxspout.h`](../../../mbxspout/include/mbxspout.h) | **Yes.** Versioned, and the consumer asserts the major at load. |
| **Send** | the vendored `spoutDX` sources, linked straight into this exe | **No.** There are no send exports and there are not meant to be. |

**Sending is deliberately outside the contract.** `mbxspout.dll` has six exports and every one of
them is receive-side. The spec rules sending out
([§1](../../../mbxspout/docs/2026-09-06-mbxspout-dll-spec.md)) because MBXHub-as-a-source is a different
feature with a different frame-rate floor — so a sender links the Spout SDK like any other Spout
application, and this sample does exactly that.

Read `RunSend()` for the SDK call sequence if you are writing a sender. Do **not** read this sample
as evidence that the ABI can send. It cannot, and asking it to is a spec change, not a bug report.

---

## What to copy if you are writing a receiver

`LoadApi()` and `RunReceive()`, in that order. Between them they are the whole reference
consumption path:

1. **`LoadLibrary`, then six `GetProcAddress` lookups.** If any fails, the file is not
   `mbxspout.dll` and you say so — you do not proceed and crash later.
2. **Assert the ABI major.** This is the entire reason the ABI carries a version. A pin bump that
   changed the contract fails *there*, once, legibly, rather than at the first `present` with a
   frame that is the wrong shape.
3. **`open` on a window you own, then `connect` to a sender name.** Connecting to a sender that is
   not running is **accepted** — the state becomes `NO_SENDER`, which is a normal state, not a
   failure. Start the receiver first and watch it pick the sender up on its own.
4. **`present` in a loop, off the UI thread in a real application.** It blocks up to a vblank by
   design; that block is the pacing, not cost. The `cpuMs` it reports is work only, and here it
   runs at **~0.27 ms** a frame at 720p.
5. **Say the state out loud.** The DLL owns the swapchain, so a state with no picture — waiting,
   handle failed — has to be shown by the caller. This sample puts it in the title bar; MBXHub's
   own view says *"Waiting for IKANDY…"*.
6. **Recover from `DEVICE_LOST` yourself.** The DLL never re-creates a lost device: you own the
   window and have to re-create the swapchain against it anyway, and a hidden re-create is how a
   recovery loop becomes an infinite one. `RunReceive` shows the caller's half — `close`, `open`,
   `connect`, carry on.

---

## The charm half

`--register` POSTs [`manifest.json`](manifest.json) to `/charms/register` and prints the exchange
verbatim. `--launched` is what MBXHub itself runs: register, then open the receiver window.

This is the *small* version of the charm contract on purpose. Registration here stops at the 200;
it does not wait for approval, collect a ticket, make gated calls or bind the event socket.
[`hello-charm`](https://github.com/halrad-com/mbxhub) in MBXHUB-Partners is the sample that walks
the full protocol, and duplicating it here would bury the thing this sample is actually about.

`/charms/register` answers only callers on the MusicBee machine, by design. Off that machine you
get a `403`; with no hub running you get a legible "is MBXHub running?" rather than a hang.

---

## Building, and which DLL it loads

`build.cmd` needs Visual Studio 2022 (MSVC, C++ desktop workload) and uses the CMake it bundles.
Static CRT (`/MT`), same as the DLL — a sample that needs a redistributable installed before it
starts teaches the wrong lesson on someone else's machine.

`mbxspout.dll` is resolved **at runtime**, never linked:

1. beside `hello-spout.exe` — how it would ship;
2. otherwise `..\..\build\Release\mbxspout.dll`, this repo's own build output, so the sample runs
   straight after the repo-root `build-spout.cmd`;
3. or wherever `--dll <path>` says.

> ⚠ **As of 2026-09-06 that fallback is an *unsigned* development build.** `publish/1.0.0/` does not
> exist yet — the DLL has not been signed. Nothing should ship against the build output: once
> `publish/<version>/mbxspout.dll` exists, that signed file is the one to put beside the exe, and
> MBXHub verifies hash and signer before it loads its own copy.

---

## Options

```
hello-spout --send [name] [w] [h] [fps] [seconds]     default: HelloSpout 1280 720 30 120
hello-spout --recv [name] [seconds]                   default: HelloSpout, runs until Esc
hello-spout --register                                register with MBXHub, print the exchange
hello-spout --launched                                what MBXHub runs: register, then receive

--dll <path>    mbxspout.dll to load
--host <ip>     MBXHub host for --register (default 127.0.0.1)
--port <n>      MBXHub port for --register (default 8080)
```

---

## Two notes on things that look like bugs

**The sent frame rate is counted here, not read from the SDK.** `spoutDX::GetFps()` is
frame-count-derived, and Spout's frame counting is off unless `HKCU\Software\Leading Edge\Spout\
Framecount` has been written by Spout's own settings app — which nobody who merely runs a Spout
application has done. Asked for 30 fps it reports 60.0. The same measurement is why the DLL's
`present` never gates on `IsFrameNew()`; see spec §3.1.

**The picture is drawn on the CPU.** Diagonal bars, a colour-cycling band and a walking square,
written into BGRA and pushed with `UpdateSubresource`. No shader, no `.fx` file, no compile step —
a sample should build with the toolchain it already has, and the subject here is the Spout path,
not rendering.

---

## Where this lives — moving

**Moved here from `mbxspout/samples` on 2026-09-07** — operator ruling: every sample except the
vendor `ikandy` one belongs in the `mbxhub` repo.

The move's one real consequence is `SPOUT_ROOT` in [`CMakeLists.txt`](CMakeLists.txt). It used to
reach a parent directory, because the sample lived inside the repo that vendors Spout; from here it
reaches a **sibling checkout** (`../../../mbxspout/third_party/Spout2/SPOUTSDK`), the way MBXH
already reaches `../fireants/src/Firebug`. CMake now *checks* that path and stops with a sentence
naming it, rather than letting the compiler emit a wall of missing-include errors; pass
`-DSPOUT_ROOT=<path>` for any other layout. Nothing else in the sample cares where it lives:
`mbxspout.dll` is resolved at runtime, not linked, so only the SDK path is positional.
