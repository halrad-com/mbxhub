# fosi-probe — first-contact recon for an unknown network audio device

A dependency-free PowerShell sweep that answers, in one pass, "what *is* this thing and how do I
talk to it?" — written for a Fosi Audio S3 streamer, but nothing in it is Fosi-specific. Point it
at any network audio device and it will tell you more in forty seconds than an afternoon of
guessing.

**It is strictly read-only.** Every request is a GET. Nothing is written to the device, no state is
changed, no firmware surface is exercised. Safe to run repeatedly, safe to run against hardware
someone else is listening to.

## What it does

| Stage | What it answers |
| --- | --- |
| **0. SSDP sweep** | What is on the network, what does each device claim to be, and where is its UPnP description? Run it before *and* after plugging the new device in — the diff is your answer. |
| **1. Port plan** | Which of the ports that matter for audio appliances are open. Each port carries a written reason it is on the list, because a bare port list rots the moment you forget why. |
| **2. Platform test** | Does the LinkPlay `httpapi.asp` surface exist, over HTTP or HTTPS? On white-label audio hardware this single question decides which half of the documentation applies to you. |
| **3. Vendor web surface** | Fetches `/`, `settings.fcgi` and friends, then regex-extracts every linked and endpoint-looking reference out of whatever HTML comes back — the fastest way to find the pages a vendor did not document. |
| **4. UPnP description** | Parses the device description into friendly name, manufacturer, model, and the full service list, then states plainly whether `AVTransport` and `RenderingControl` are present — i.e. whether standards-based transport and volume control are available to you at all. |

Every response is saved verbatim to `out/`, and a summary lands in `out/fosi-probe-report.md` so
findings can be re-read later without re-probing the device.

## Run

```powershell
# discovery only - what is on this network?
powershell -NoProfile -ExecutionPolicy Bypass -File fosi-probe.ps1

# full sweep against one device
powershell -NoProfile -ExecutionPolicy Bypass -File fosi-probe.ps1 -Ip 192.168.1.123
```

`-DiscoverSeconds` changes how long the SSDP listen runs (default 5).

## Reading the results

The interesting output is usually a *contrast*, not a single value. For example, measured against
two stock LinkPlay devices (a WiiM Ultra and a WiiM Sound Lite, identical results): port 80 closed,
443/8443/8819/49152/8008/8009 open, `httpapi.asp` answering over **HTTPS only**, UPnP description on
`:49152` carrying `AVTransport` + `ConnectionManager` + `RenderingControl`.

That profile becomes a yardstick. A device that serves plain HTTP on port 80 and has 8819 closed is
running something other than stock LinkPlay firmware, whatever its module lineage — and that single
observation redirects the entire integration effort. See
[the Fosi S3 field note](fosi-s3-field-notes.md) for how that reasoning is being used.

## Notes

- Uses `curl.exe` (bundled with Windows 10+) with `-k`, because LAN appliances use self-signed
  certificates. Treat those certs as necessary, not trusted.
- HTTP status `000` means no response at all — the script deliberately blanks the body in that case,
  because reporting curl's own error text as a response body reads as "the device answered" when it
  did not.
- Everything is plain PowerShell — no modules, no installs, works offline.
- A negative result here means *this probe did not find it*, which is not the same as *it is not
  there*. Packet capture is the next step, not a conclusion.
- **If you are probing a device with a firmware-update page, do not exercise it.** Read it, learn
  from it, leave it alone.
