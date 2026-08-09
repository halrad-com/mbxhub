# Bench tools

The short list that keeps earning its place. Criteria: works offline, scriptable or
inspectable, no account required, does one thing credibly.

## Music

- **[MusicBee](https://getmusicbee.com/)** — first, obviously. The player everything here
  orbits: serious library management, a real plugin API, and the host MBXHub extends.
- **[Mp3tag](https://www.mp3tag.de/)** — bulk tag surgery with scriptable actions; the
  editor you want before a 70k-track library learns your mistakes.
- **[MediaInfo](https://mediaarea.net/en/MediaInfo)** — the truth about what's actually
  inside a media file: codecs, bit depth, sample rate, container quirks.

## Audio & media CLI

- **[ffmpeg](https://ffmpeg.org/)** — the universal converter/prober. `ffprobe` alone
  justifies the install; if audio bytes need moving, inspecting, or transcoding, it's this.
- **[curl](https://curl.se/)** — ships with Windows now; the bench's lingua franca for
  poking device APIs (`-k` for the self-signed LAN appliances, `-s` for scripts).

## Automation & input

- **[AutoHotkey v2](https://www.autohotkey.com/)** — intercept and remap anything the
  input stack can see; the fastest path from "Windows hardcodes this" to "not anymore."
  Mind the v1/v2 syntax split.
- **[NirSoft suite](https://www.nirsoft.net/)** — dozens of tiny single-purpose utilities
  (NirCmd, SoundVolumeView, USBDeview...). Old-school, portable, indispensable.

## Network & devices

- **[Wireshark](https://www.wireshark.org/)** — when a device won't say what it's doing,
  the wire will. Essential for reverse-engineering appliance protocols.
- **SSDP/UPnP eyes** — Windows' own device enumeration (Explorer → Network) plus a
  ten-line M-SEARCH script covers most discovery questions; see the WiiM notes for the
  pattern.

## Windows internals

- **[Sysinternals](https://learn.microsoft.com/en-us/sysinternals/)** — Process Explorer,
  Process Monitor, Autoruns. When you need to know what a process is *actually* touching.
- **PowerShell** — already installed, talks to everything (.NET, COM, registry, UDP
  sockets), and a `.ps1` beats an unreproducible sequence of clicks every time.
