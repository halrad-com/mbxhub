# Windows volume steps: why 2%, and every real way to do better

*Research notes, 2026-08. Windows 10/11. Motivating case: a desktop rotary knob whose full
rotation swept ~half the volume range — we wanted ~20% per turn.*

## The mechanism

Hardware volume inputs (keyboard media keys, BT knobs and D-pads) emit HID Consumer
Control usages (Volume Increment/Decrement). Windows translates each detent into a
`VK_VOLUME_UP`/`VK_VOLUME_DOWN` keystroke, and the shell's handler for that keystroke bumps
master volume by a **hardcoded 2 units on the 0–100 scale**. Hence 50 steps, on every
machine, regardless of driver.

Two things follow:

- **There is no setting.** No Settings page, no Group Policy — and the `VolumeIncrement`
  registry value circulating on SEO sites is a myth; Microsoft Q&A answers state the
  increment is hardcoded.
- **The audio stack itself is not the limit.** `IAudioEndpointVolume::SetMasterVolumeLevelScalar`
  takes a float 0.0–1.0. The coarseness lives only in the built-in keystroke handler, which
  means any software that intercepts the keystroke can substitute a finer delta.

## The options, measured

| Option | Verdict |
|---|---|
| **AutoHotkey v2** | Best minimal: `Volume_Up::SoundSetVolume "+1"` (and `-1`). In-process, smooth under fast knob spins, and uniquely supports fractional steps (`"+0.8"`). Mind the version: that syntax is v2; under v1 it fails. |
| **Volume²** | Best no-code: open source, maintained, hooks the keys with a configurable step, own OSD. |
| **3RVX** | Purpose-built and works, but unmaintained for years. |
| **Volumouse** | Wrong tool for a knob — it binds *mouse wheel* events and never sees keystrokes. |
| **NirCmd per detent** | Works but spawns a process per event; a fast-spun knob fires dozens per second. Prefer in-process. |
| **PowerToys Keyboard Manager** | Can't do it — remaps keys to keys, no "volume ±N%" action. |

## Two failure modes to test before trusting any interceptor

1. **Is the device in the keystroke path at all?** Some devices adjust volume via the
   audio stack directly and no keyboard hook ever sees them. Sixty-second test: an AHK
   script containing only `Volume_Up::MsgBox "caught"` — spin the knob. Box pops =
   interceptable. Volume moves with no box = no hook-based tool can ever work.
   (A useful real-world discriminator we hit: if a device controls an app that has *no*
   SMTC integration of its own, with no helper software running, the commands must be
   arriving as plain keystrokes.)
2. **Does the endpoint hold fine values?** Some render endpoints quantize API sets to
   their driver step table. Thirty-second test: set master volume to 43 and read it back.
   Sticks at 43 = fine-grained, 1% interception will hold. Snaps to 42/44 = no
   interceptor can beat the driver on *master* volume — do your fine control at a
   different stage (see below).

## The four volume stages (they multiply, and they don't sync)

1. **Player-internal** (e.g. MusicBee's own slider) — applied inside the app before audio
   reaches Windows.
2. **App session** (the Volume Mixer) — a per-app multiplier (`ISimpleAudioVolume`).
   Independent of the app's own slider; they match only by coincidence. The mixer UI
   draws app sliders scaled to master, which hides the independence.
3. **System master** (`IAudioEndpointVolume`) — the volume keys' stage, the 2% one.
4. **Device/amp hardware** (a network speaker's own volume) — after Windows entirely.

Effective loudness is the product of all four. Practical rule: park all but one at 100%
and do real control at one deliberate stage — a forgotten 60% × 60% is 36% and a
debugging session. Bonus: WASAPI-exclusive/ASIO output bypasses stages 2–3 completely.

## UX math

A typical 24-detent knob at stock 2% = 48%/rotation. At 1% ≈ 24%/rotation; AHK's
fractional steps let you hit arbitrary targets (0.8%/detent ≈ 19%/rotation).
