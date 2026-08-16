# IcomRigControl — Hardware Testing Checklist

Everything built in the 2026-08-15 session that needs verification against **real
hardware** (your IC-7300 and IC-705, plus a Raspberry Pi for the remote-audio server).
The unit test suite (323 tests) covers the logic; this list is the stuff only a real
radio, real audio, and a real network can confirm. Check items off as you go; note the
symptom next to anything that misbehaves so we can fix it.

> **In a nutshell:** connect a real radio, work down each section, and confirm each item
> does what it says. The two biggest unknowns are **IC-705 meter scaling** and **remote
> audio latency/quality** — spend the most time there.

---

## 1. Audit fixes (mostly test-covered; confirm on a real rig)

- [ ] **Echo does not kill reception** — with the radio connected and CI-V transceive on, turn the VFO and confirm the app keeps updating (before the fix, an echoed read crashed the receive loop). Do a Memory Editor "Read All" and confirm it completes.
- [ ] **UI doesn't crash on live data** — spin the dial and let meters/scope stream; the dashboard must update smoothly with no crash.
- [ ] **Stuck-PTT safety** — send an APRS beacon; confirm PTT always releases even if you yank the audio device mid-transmit.
- [ ] **S-meter reading** — confirm the S-meter and the CSV log's dBm look sane (S9 ≈ −73 dBm).

## 2. IC-705 support (the one radio not yet hardware-verified)

- [ ] **Connect** — Settings → Radio Model → **IC705**, Serial mode, correct COM port, Save, restart. Confirm it connects and reads frequency/mode.
- [ ] **Address** — confirm control works (the app addresses the 705 at `0xA4`).
- [ ] **Meter scaling (IMPORTANT)** — check each meter against reality on the 705: S-meter on a known signal, **Po at a known power (e.g. 5 W / 10 W)**, SWR into a known load, ALC, Vd, Id. The 705 currently shares the IC-7300 decoder — if any meter reads wrong, note the raw-vs-expected so we can add a 705-specific scaling.
- [ ] **Everything else** — frequency/mode/PTT, waterfall, memories, logging, APRS — confirm they work on the 705.

## 3. Remote Power ON/OFF

- [ ] **Set the radio's menu first** — Menu → SET → Function → "Power OFF Setting (for Remote Control)" → **Standby/Shutdown** (not "Shutdown only").
- [ ] **Power Off** — click Power Off; radio powers down to standby.
- [ ] **Power On** — click Power On; radio wakes (only works from standby with CI-V alive).

## 4. CW keyer & voice memory (Keyer window)

- [ ] Put the radio in a **CW mode**, connect an antenna/dummy load.
- [ ] **Send a CW macro** — edit M1, click Send; the radio should key and send the text as Morse. Try `^` for a prosign.
- [ ] **Stop CW** — click Stop mid-message; transmission aborts.
- [ ] **Save Messages** — edit macros, Save, reopen the window; they persist.
- [ ] **Voice memory** — record a voice memory on the radio, then fire **T1–T8** from the window; radio transmits it. Stop works.

## 5. FT8 Setup button

- [ ] Click **FT8 Setup**; confirm the radio switches to **USB-D** with a wide filter. (NB/NR/AGC you still set manually, as with Icom's own preset.) Confirm WSJT-X still decodes normally.

## 6. DX Cluster window

- [ ] **Connect** — pick a cluster (e.g. NC7J or a Reverse Beacon Network node), enter your callsign, Connect; spots start appearing.
- [ ] **Click-to-tune** — click **Tune** on a spot; the radio QSYs to it.
- [ ] **Post a spot** — enter a call, "use radio" to fill the frequency, "Spot It"; confirm it appears on the cluster (check another client or the RBN/cluster web view).
- [ ] Try 2–3 different clusters from the dropdown to confirm login works across nodes.

## 7. Linux/Pi audio (new — the APRS beacon on Linux)

- [ ] On a **Raspberry Pi / Linux** desktop build, send an **APRS beacon** and confirm audio actually plays (this used to be Windows/macOS only). Requires ALSA `aplay`.

## 8. Remote Audio (Phase 12) — the big one

**Setup:** Pi at the radio running the headless server; a Windows (or Linux) client elsewhere.

- [ ] **Start the Pi server with audio** —
  `IcomRigControl.UI --headless-server --port /dev/ttyUSB0 --tcpport 7300 --token <secret> --model IC705 --audioport 7301 --audiocapture plughw:1,0 --audioout plughw:1,0`
  (use `arecord -l` / `aplay -l` on the Pi to find the radio's ALSA device numbers).
- [ ] **Client CI-V** — Settings → Remote mode, the Pi's host/port/token, restart; confirm remote frequency/mode control works (Phase 9).
- [ ] **RX audio** — open **Remote Audio**, enter the Pi host + audio port 7301, Connect. Confirm you **hear the radio's receive audio**.
- [ ] **Audio quality** — is the Opus audio clear? Note any dropouts/robotic artifacts (→ jitter buffer depth), or lag (→ latency).
- [ ] **TX / push-to-talk** — hold **Transmit**; confirm the radio **keys up** (PTT) and your **mic audio goes out** (watch ALC/Po, or have someone confirm on another receiver). Release → returns to receive.
- [ ] **Stuck-PTT check** — disconnect / close the window while "transmitting"; confirm the radio un-keys (must never be left transmitting).
- [ ] **Latency feel** — is round-trip latency acceptable for operating? Note it; we can tune buffer depth / sample rate (milestone 6).
- [ ] **Device routing** — confirm RX plays on your chosen speakers and TX uses your chosen mic.

---

## Tuning knobs we can turn after testing (milestone 6)

- Jitter buffer depth (default 3 frames ≈ 60 ms) — raise for stability, lower for latency.
- Opus sample rate (default 16 kHz) / frame size (default 20 ms).
- ALSA device strings and Windows output-device selection.
- Po/ALC meter-scaling breakpoints (also the deferred audit item — verify byte-for-byte vs the radio's meter table).

*Bring the notes from this checklist back and we'll fix/tune whatever misbehaved.*
