# hello-spout

An MBXHub charm that **receives** Spout through `mbxspout.dll` and **sends** Spout by linking the
Spout SDK directly.

Two instances are intended to demonstrate sending and receiving without another application.

**The sample requires the sibling `mbxspout` checkout** — it takes both the Spout SDK sources and
`mbxspout.h` from there. The header path and the runtime DLL fallback were repaired on 2026-09-07;
both had kept the relative paths from when this sample lived in the `mbxspout` repo. The Release
build and a bounded sender/receiver run were verified on 2026-09-07; details are below.

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
| **Send** | the vendored `spoutDX` sources, linked straight into this exe | Uses the Spout SDK directly; it does not exercise the DLL's sender API. |

**The sample and the current DLL expose different choices.** This sample retains its direct-SDK
sender. The current [`mbxspout.h`](../../../mbxspout/include/mbxspout.h) also declares DLL sender
operations, including `mbxspout_sender_open`, `mbxspout_sender_send_texture` and
`mbxspout_sender_send_pixels`. The older receive-only description no longer describes the full ABI.

Read `RunSend()` for this sample's direct SDK sequence. For the DLL's sender API, use
[`mbxspout.h`](../../../mbxspout/include/mbxspout.h) — it is the normative contract and documents
each export in place — with the summary table in
[mbxspout's README](../../../mbxspout/README.md). This sample does not demonstrate that API; its
source comments distinguish the sample's direct-SDK sender from the DLL's sender API.

---

## What to copy if you are writing a receiver

`LoadApi()` and `RunReceive()`, in that order. Between them they are the whole reference
consumption path:

1. **`LoadLibrary`, then six `GetProcAddress` lookups.** These are the six functions this receiver
   needs, not the DLL's entire export surface. If one is absent, report an incompatible DLL and stop.
2. **Assert the ABI major.** This is the entire reason the ABI carries a version. A pin bump that
   changed the contract fails *there*, once, legibly, rather than at the first `present` with a
   frame that is the wrong shape.
3. **`open` on a window you own, then `connect` to a sender name.** Connecting to a sender that is
   not running is **accepted** — the state becomes `NO_SENDER`, which is a normal state, not a
   failure. Start the receiver first and watch it pick the sender up on its own.
4. **`present` in a loop, off the UI thread in a real application.** It blocks up to a vblank by
   design; that block is the pacing, not cost. The `cpuMs` it reports is work only, and here it
   was previously measured at **~0.27 ms** a frame at 720p. That is a historical measurement,
   not a timing guarantee or a result from this documentation pass.
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
[`hello-charm`](../hello-charm/) in this repository is the sample that walks
the full protocol, and duplicating it here would bury the thing this sample is actually about.

`/charms/register` answers only callers on the MusicBee machine, by design. Off that machine you
get a `403`; with no hub running you get a legible "is MBXHub running?" rather than a hang.

---

## Building, and which DLL it loads

`build.cmd` needs Visual Studio 2022 (MSVC, C++ desktop workload) and uses the CMake it bundles.
Static CRT (`/MT`), same as the DLL — a sample that needs a redistributable installed before it
starts teaches the wrong lesson on someone else's machine.

`mbxspout.dll` is resolved **at runtime**, never linked:

1. `--dll <path>`, when supplied, overrides automatic lookup.
2. Otherwise, the sample looks beside `hello-spout.exe`.
3. If absent, it walks **five** directories up from the executable — to the directory holding
   both checkouts — and appends `mbxspout\build\Release\mbxspout.dll`, naming the sibling
   explicitly. This matches the default sibling layout in [`CMakeLists.txt`](CMakeLists.txt).
   A custom `MBXSPOUT_ROOT` does not change runtime lookup; use `--dll` for another DLL location.

Until 2026-09-07 step 3 walked four levels and appended `build\Release\mbxspout.dll`, which
resolved inside **mbxhub** — a path that has never existed — because the arithmetic was written
when this sample lived in the `mbxspout` repo.

Supply `--dll` with the DLL you intend to test, or place it beside the executable. The fallback
targets a developer build; this pass did not verify a packaged DLL or its signature.

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
`present` never gates on `IsFrameNew()` — it presents at display cadence instead, since
`IsFrameNew()` returned true on every call and the sender's frame number never advanced.

**The picture is drawn on the CPU.** Diagonal bars, a colour-cycling band and a walking square,
written into BGRA and pushed with `UpdateSubresource`. No shader, no `.fx` file, no compile step —
a sample should build with the toolchain it already has, and the subject here is the Spout path,
not rendering.

---

## Repository layout, and the paths the move broke

**Moved here from `mbxspout/samples` on 2026-09-07** — operator ruling: every sample except the
vendor `ikandy` one belongs in the `mbxhub` repo.

`SPOUT_ROOT` in [`CMakeLists.txt`](CMakeLists.txt) defaults to the sibling checkout at
`../../../mbxspout/third_party/Spout2/SPOUTSDK`. CMake checks that directory; a direct CMake
invocation can override it with `-DSPOUT_ROOT=<path>`.

`MBXSPOUT_ROOT` covers the header the same way, defaulting to the sibling checkout at
`../../../mbxspout` and overridable with `-DMBXSPOUT_ROOT=<path>`. CMake checks for
`include/mbxspout.h` under it and fails with that message rather than letting the compiler report
a missing `mbxspout.h`, which sends the reader looking for a missing file instead of a missing
checkout.

**What was wrong, and when.** Both paths were written when this sample lived in `mbxspout/samples`,
where `../../include` reached the contract and four levels up from `build\Release` reached that
repo's own build output. The move on 2026-09-07 carried the relative paths unchanged, so the
include pointed at an `mbxhub/include` that does not exist — the sample was not buildable as
checked out — and the DLL fallback resolved inside `mbxhub`. Both were repaired on 2026-09-07.

**Relocated build caches:** if CMake reports the old `mbxspout/samples/hello-spout` source path,
preserve or remove this sample's generated `build` directory and rerun `build.cmd`. A cache from
the old location cannot be reused in this checkout.

**Verified on 2026-09-07:** `build.cmd` passed in Release after preserving the relocated cache
and configuring a fresh build. An 8-second 640×360 sender run and a 5-second receiver run both
exited with code 0. The receiver loaded ABI `0x00010001`, reached `live`, and presented 303 frames.
No `--dll` override or DLL beside the executable was used, exercising the sibling-checkout fallback.
Presented frames include repeated presentations; this count is not a claim of unique source frames.

This verifies the local build and video path. MusicBee registration/approval, device-loss recovery,
custom checkout layouts, packaged DLL signatures and visual appearance were not tested in this run.
