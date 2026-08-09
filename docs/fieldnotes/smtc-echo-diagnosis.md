# One press, two deliveries: diagnosing the SMTC play/pause echo

*A bug story from MBXHub 0.5.4.7 development, 2026-08. Kept because the diagnosis method
matters more than the bug.*

## Symptom

With the hub enabled, play/pause from a BT media controller got "stuck": pressing play
would play for about a second, then snap back to paused. Skip forward/back un-stuck it.
With the hub disabled, the same controller behaved perfectly. Only MusicBee was affected —
other players on the same machine were fine.

## The mechanism (measured, not guessed)

A single physical press was reaching MusicBee **twice**, by two paths with very different
latencies:

1. **Native path (instant):** the controller's press arrives as an ordinary media
   keystroke; MusicBee handles it itself and toggles immediately.
2. **SMTC path (~500 ms later):** MBXHub's shell registers a System Media Transport
   Controls session on MusicBee's behalf (that's a feature — it's what makes the Windows
   media overlay work). Windows routes the *same press* to that session too. And SMTC has
   no "toggle": it has separate Play and Pause buttons, and **Windows decides which one
   your press meant using the playback status reported *before* the press**. The shell
   forwards that command to the hub as a REST call.

The hub's play/pause endpoints were state-guarded — they check actual player state on
arrival and no-op if already there. The guard can't defend against one thing: a
*wrong-direction* command. By the time the SMTC-routed command arrived, the native toggle
had already flipped the state, making the stale-direction command look valid. Result: play
(native), then pause (echo), net stuck — with the REST hop's latency stretching the
double-toggle into an audible one-second blip.

Why skip forward/back "fixed" it: Next/Previous carry no direction, so a stale status
can't invert them — and they force playback, realigning everything.

## How it was pinned

- **A/B with the hub disabled** proved the controller and its keystroke path were clean.
- **The controlled variable that settled the routing question:** MusicBee has no SMTC
  integration of its own — the hub's shell provides it. With the hub off, SMTC routing
  couldn't exist, yet the controller still worked → the commands arrive as plain
  keystrokes. With the hub on, both paths exist → double delivery.
- **The log had the smoking gun.** Every press produced a *pair* of opposite play-state
  transitions plus one REST POST completing ~500 ms later — including a wrong-direction
  `play` arriving one second after a native pause, resuming against the user's intent.
- **The on-screen overlay was the free instrument:** its play/pause glyph blipped in sync,
  proving the state genuinely double-flipped (rather than a stale display).

## The fix, and what the first version got wrong

v1 suppressed an "echo": a state-guarded command arriving shortly after an opposite
native transition. Correct idea — but review caught that it armed on *every* transition
regardless of origin and never disarmed after firing, so a genuine SMTC-only press (the
overlay button — which has **no** native keystroke, SMTC is its *only* delivery) could be
swallowed by an unrelated state change. The hardened version:

- **Origin-aware arming** — transitions caused by the hub's own REST commands never arm
  the suppressor (the handler notes the command before executing, and the resulting
  transition is attributed to it).
- **One-shot consumption** — a suppression consumes the arm; an echo pair has exactly one
  echo, so a second press is treated as a human.
- **Window sized from measurement** — widened after logs showed echoes arriving up to ~1s
  on a slow box; safe only *because* arming became precise. The two knobs trade off:
  fix precision before widening windows.
- **Every suppression is logged** — a silently dropped command is indistinguishable from a
  no-op in exactly the log you'll be using to debug the next problem.

## Lessons

1. **Find the controlled variable.** The hub-off test and the no-native-SMTC fact turned
   a mystery into a two-path race you could reason about.
2. **Latency turns races audible.** The bug was always there; ~500 ms of REST latency made
   it a one-second music blip instead of an invisible 50 ms flicker.
3. **Dedupe logic must know who caused what.** Time-window heuristics without origin
   attribution swallow legitimate input — the fix for a double-delivery bug can create a
   swallowed-press bug.
4. **Log the decision, not just the action.** "Dropped as echo" and "no-op, already in
   state" look identical from outside unless you write the line.
