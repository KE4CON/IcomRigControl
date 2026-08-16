# IcomRigControl Installation Guide

*Install the app, connect your Icom radio, and set up every integration — step by step, in plain language.*

*Generated August 15, 2026 · Markdown is the living source of truth.*


---


# 1. Before You Start

*What IcomRigControl is, what you need to run it, and the one safety rule.*

> **In a nutshell** — Install the app, plug your radio into the computer with one USB cable, change a handful of settings on the radio itself, tell the app which radio and which port to use, and press Save. That is the whole job. Each of those steps is spelled out, click by click, in the chapters that follow. If you only do the least possible, do this: **install, connect the USB cable, set the radio's CI-V settings, pick the radio and port in the app, restart the app.**


## What IcomRigControl Is

IcomRigControl is a desktop program that controls an Icom radio from your computer. It shows and changes your frequency and mode, reads your meters, draws a spectrum **waterfall** (a live picture of the band), logs your contacts, and — when you set it up on purpose — keys the transmitter, sends a beacon, decodes CW and RTTY, and lets you operate the radio from another room, from a phone, or from across the internet.

It runs on Windows, macOS, Linux, and the Raspberry Pi. It is free. Your contact log is kept on your own computer, so it keeps working even if every other program you use is turned off.

> **Jargon, in plain words.** — **CI-V** (Computer-to-transceiver, version 5) is Icom's name for the language the radio and the computer speak to each other over the USB cable. You do not need to understand it — but you will set two or three CI-V options on the radio, and this guide tells you exactly which.


## The Three Radios It Supports

IcomRigControl supports exactly three radios, and only these three. This is on purpose: these are the radios that can be properly tested against real hardware, so the app's control of them is trustworthy. If you own one of these, you are in the right place.

| Radio | Its CI-V address | Notes |
| --- | --- | --- |
| Icom IC-7300 | `0x94` (the factory default) | The most common of the three. |
| Icom IC-7300MK2 | `0xB6` (the factory default) | Differs from the IC-7300 essentially only in this address. |
| Icom IC-705 | `0xA4` (the factory default) | The portable model; connects the same way. |

> **What is a CI-V address?** — Every Icom radio answers to a number, like a name tag, so the computer knows it is talking to the right rig. The three radios above ship with different numbers. You will confirm this number in two places — on the radio's menu and in the app — and they must match. The app already knows the right default for each model, so in most cases you just pick your model and you are done.

> **Other Icom radios are not supported.** — IcomRigControl will not properly control an IC-7610, IC-9700, IC-718, IC-706, or any other model. Do not try to use it with them — it has only been built and tested for the three radios above.


## What You Need

Gather these before you begin. Every item is explained in detail later; this is the shopping list.

- **One of the three supported radios** — an IC-7300, IC-7300MK2, or IC-705.
- **One USB cable.** For the IC-7300 and IC-7300MK2 this is a standard **USB-A-to-USB-B** cable (the flat rectangular end goes into the computer; the squarer end goes into the radio). For the IC-705 it is a **USB-A-to-USB-C** cable. The exact cable and where it plugs in are covered in the 'Connecting Your Radio Over USB' chapter.
- **A computer** running Windows, macOS, Linux, or a Raspberry Pi (see the table below).
- **About 300 MB of free disk space** for the program, plus room for your logs and any audio recordings.
- **Internet access is optional** — you need it only for online callsign lookup, uploading to Logbook of the World, and the like. The radio control itself works with no internet at all.


## Which Computers It Runs On

IcomRigControl is a 64-bit desktop application built on Microsoft's .NET 10 runtime. Here is exactly what it runs on.

| Platform | Minimum version | Runs on |
| --- | --- | --- |
| Windows | Windows 10 or Windows 11 (64-bit) | 64-bit PCs and laptops |
| macOS | macOS 12 (Monterey) or later | Apple Silicon (M1-M4) and Intel Macs |
| Linux | Ubuntu 22.04+, Debian 12+, Fedora, or RHEL 8+ (64-bit) | x64 or ARM64, with a desktop |
| Raspberry Pi | Raspberry Pi OS 64-bit (Bookworm) | Pi 4 or Pi 5 recommended; 8 GB for the full waterfall display |

> **Running on a Raspberry Pi?** — The Pi can run the full app with the waterfall (a Pi 5 with 8 GB is best), or it can run 'headless' — no screen, just a quiet server at the radio that your laptop or phone connects to over the network. Both are covered later. Use the **64-bit** Raspberry Pi OS; the 32-bit version will not run the app.


## The One Safety Rule

IcomRigControl can key your transmitter. That is a real radio putting a real signal on the air, so treat it with the same care you would the microphone's PTT (push-to-talk) button.

> **You are the licensed operator.** — When you enable transmitting, you are responsible for legal amateur-radio operation — your callsign, your identification every ten minutes, your frequency privileges, and your local rules are yours to get right. The app helps (it has a 10-minute ID reminder and an SWR safety cut-out), but the responsibility is yours.

Two things keep you safe while you learn. First, **the app does not transmit on its own** — it only listens and controls until you deliberately press a transmit control. Second, there is a master **Receive-Only** switch on the main screen. When it is on, a red banner reads **TRANSMIT INHIBITED — RECEIVE ONLY**, and nothing — not PTT, not a beacon, not the keyer, not the remote — can key the radio. Turn it on while you set everything up.

> _[Figure: The main dashboard with the red 'TRANSMIT INHIBITED — RECEIVE ONLY' banner across the top, and the Receive-Only toggle button that controls it]_


## How to Use This Guide

You can read this guide straight through, or jump to the chapter you need. The order is deliberate: install the app, connect the radio's cable, set the radio's own menu, connect in the app, and then — only if you want them — set up remote operation and the other-program integrations.

Along the way you will see four kinds of colored boxes:

> **IMPORTANT** — Something you must not skip — usually about safety or about a setting that has to match on both sides.

> **NOTE** — A helpful clarification, or a plain-words explanation of a technical term.

> **TIP** — A shortcut, or a faster way once you are comfortable.

> **WARNING** — An action that can put a signal on the air, or otherwise have a real-world consequence.

Dashed boxes marked **SCREENSHOT** show where a picture of the screen will appear in the finished guide; each one describes exactly what it shows, so you can follow along even before the pictures are added.


# 2. Installing IcomRigControl

*Getting the program onto your computer — Windows, macOS, Linux, or a Raspberry Pi.*

> **In a nutshell** — Go to the official releases page, download the file for your computer, and run it. On Windows that is a Setup program; on macOS a disk image you drag into Applications; on Linux and the Raspberry Pi a package you install with one command. If your computer shows a one-time security caution, that is expected for now — this chapter tells you the exact clicks to get past it.


## Where to Download

Always download IcomRigControl from its official releases page. Do not get it from anywhere else.

```
https://github.com/KE4CON/IcomRigControl/releases
```

On that page, open the most recent release and look under **Assets** for the list of downloads. Each file is named for the computer it is for. Pick the one that matches yours from the table below, then follow the matching section.

| Your computer | Download this file |
| --- | --- |
| Windows 10 / 11 (64-bit) | `IcomRigControl-windows-x64-Setup.exe` |
| Mac with Apple Silicon (M1-M4) | `IcomRigControl-...-macos-arm64.dmg` |
| Mac with an Intel processor | `IcomRigControl-...-macos-x64.dmg` |
| Windows / Mac / Linux PC or laptop (Intel or AMD) | the `...-x64` file for your system |
| Raspberry Pi or other ARM computer | `icomrigcontrol_..._arm64.deb` |
| Debian / Ubuntu / Raspberry Pi OS | the `.deb` file |
| Fedora / RHEL / openSUSE | the `.rpm` file |

> **Not sure whether your Mac is Apple Silicon or Intel?** — Click the Apple menu at the top-left of the screen and choose **About This Mac**. If it says **Chip: Apple M1** (or M2, M3, M4), download the **arm64** file. If it says **Processor: Intel**, download the **x64** file.


## Installer or Portable — Which to Pick

Most downloads come in two forms, and you only need one:

- **The installer** (the Setup program, the `.dmg`, or the `.deb`/`.rpm` package) is the simplest choice. It puts the program in the normal place, adds a menu shortcut, and can be uninstalled cleanly later. Pick this unless you have a reason not to.
- **The portable archive** (a `.zip`) does not install anything — you unpack it to a folder and run the program in place. Handy for a USB stick or for trying it without touching the rest of your system.


## Windows


### Installer (recommended)

1. Download **`IcomRigControl-windows-x64-Setup.exe`** from the releases page.
2. Double-click it to run.
3. You may see one of two things — both are fine. If the build is **signed**, a **User Account Control** box may appear showing a verified publisher; click **Yes**. If instead you see **"Windows protected your PC,"** this copy is unsigned — click **More info**, then **Run anyway** (this is safe; see 'Signed vs. Unsigned' below).
4. The setup window opens. Click **Next** through the Welcome and License pages, leave the install folder at its default (`C:\Program Files\IcomRigControl`), and click **Install**.
5. When it finishes, click **Finish**. You now have an **IcomRigControl** shortcut on your Desktop and in the Start menu.

> _[Figure: Side by side: the signed User Account Control prompt showing the verified publisher, and the unsigned 'Windows protected your PC' prompt with the More info / Run anyway buttons]_

To remove it later: **Settings → Apps → IcomRigControl → Uninstall**, or use the Uninstall shortcut in the Start-menu folder.


### Portable (no installation)

1. Download the Windows `.zip`, right-click it, choose **Extract All**, and pick a folder.
2. Open the extracted folder and double-click **`IcomRigControl.UI.exe`**. If a 'Windows protected your PC' prompt appears, click **More info → Run anyway**.


## macOS


### Installer (.dmg, recommended)

1. Download the disk image for your Mac — the **arm64** `.dmg` for Apple Silicon, or the **x64** `.dmg` for an Intel Mac.
2. Double-click the `.dmg` to open it. A window appears showing the **IcomRigControl** app and a shortcut to your **Applications** folder.
3. Drag **IcomRigControl** onto the **Applications** folder.
4. Open **Applications** and start IcomRigControl. If the build is **signed and notarized**, it opens normally (you may get a one-time 'downloaded from the Internet — are you sure you want to open it?' — click **Open**). If instead macOS says it **"cannot be opened because Apple cannot check it,"** this copy is unsigned — **right-click** (or Control-click) the app, choose **Open**, then click **Open** again. macOS remembers your choice and it opens normally afterward.

> **The right-click step is only for an unsigned copy.** — A signed, notarized build opens with a normal double-click. The right-click → Open step is the standard one-time approval macOS asks for when a program is not signed. Apple signing for IcomRigControl is being set up; until it is finished, the Mac downloads are unsigned and you use the right-click → Open step once.


### Portable (.zip)

Unzip the archive. Then open **Terminal**, type `xattr -cr ` (with a trailing space), drag the unzipped folder into the Terminal window so its path fills in, and press **Return** — this clears the download-quarantine flag. Then run the `IcomRigControl.UI` program inside that folder.


## Linux

First pick the download that matches your computer's processor:

- **amd64 / x64** — a standard PC or laptop with an Intel or AMD processor.
- **arm64 / aarch64** — a Raspberry Pi or other ARM-based computer.

> **About the version number in these commands.** — The commands below show an example version like `1.0.0`. Type the exact name of the file you actually downloaded — your version number may differ. In a terminal you can type the first few letters of the file name and press **Tab** to complete the rest for you.


### Debian, Ubuntu, Mint, or Raspberry Pi OS  (.deb)

Open a terminal in the folder where the file downloaded, then run the command for your computer.

**On a PC or laptop** (amd64):

```
sudo dpkg -i icomrigcontrol_1.0.0_amd64.deb
```

**On a Raspberry Pi or ARM computer** (arm64):

```
sudo dpkg -i icomrigcontrol_1.0.0_arm64.deb
```


### Fedora, RHEL, or openSUSE  (.rpm)

**On a PC or laptop** (x86_64):

```
sudo rpm -i icomrigcontrol-1.0.0-1.x86_64.rpm
```

**On a Raspberry Pi or ARM computer** (aarch64):

```
sudo rpm -i icomrigcontrol-1.0.0-1.aarch64.rpm
```

An installed package puts the program at **`/opt/icomrigcontrol/`**, adds an **`icomrigcontrol`** command you can run from a terminal, and creates an entry in your application menu.

> **Using a USB radio on Linux or macOS?** — Your user account must be allowed to open serial ports. On Linux, run `sudo usermod -aG dialout $USER`, then log out and back in. This is covered fully in the 'Connecting Your Radio Over USB' chapter.


## Raspberry Pi

1. Install the **64-bit** Raspberry Pi OS (the tested version is 'Bookworm'). The 32-bit OS will not run IcomRigControl.
2. Download the **arm64** `.deb` and install it: `sudo dpkg -i icomrigcontrol_1.0.0_arm64.deb`.
3. Launch it from the application menu, or by running **`icomrigcontrol`** in a terminal.

> **Headless (no-screen) Pi.** — You can also run the Pi with no monitor as a server sitting at the radio, controlled from your laptop or phone over the network. That uses the same `icomrigcontrol` command with extra options and is covered in the 'Remote / Network Mode' chapter.


## Signed vs. Unsigned: What You'll See

'Code signing' is a digital seal that tells your computer who made a program and proves it has not been altered. When a program is signed, Windows and macOS trust it and show the author's name. When it is not, they show a one-time caution the first time you run it — not because anything is wrong, but because they cannot confirm the author automatically.

IcomRigControl's Windows builds are set up for code signing; macOS signing is being finished. Depending on the build and the date, a download may be **signed** or **unsigned**. Either way it is the identical program. Here is exactly what to expect and what to do.

| Your computer | If the copy is signed | If the copy is unsigned — and what to do |
| --- | --- | --- |
| Windows | Installs normally; a User Account Control box may show a verified publisher — click Yes. | A 'Windows protected your PC' box appears. Click **More info**, then **Run anyway**. Safe — it only means this copy is not signed. |
| macOS | Opens with a normal double-click; you may get a one-time 'downloaded from the Internet — open?' — click Open. | macOS says it 'cannot be opened because Apple cannot check it.' **Right-click** (or Control-click) the app → **Open** → **Open**. |
| Linux / Raspberry Pi | No warning — Linux does not gate apps this way. | No warning either; install and run normally. |


## Building from Source (Optional)

If you would rather build the program yourself — for example to run the very latest changes before a release is posted — install the **.NET 10 SDK** from Microsoft, then run:

```
git clone https://github.com/KE4CON/IcomRigControl.git
cd IcomRigControl
dotnet run --project IcomRigControl.UI/IcomRigControl.UI.csproj
```

The first build downloads the required packages and takes a few minutes; after that it starts quickly.

With IcomRigControl installed, the next chapter connects your radio to the computer with the USB cable.


# 3. Connecting Your Radio Over USB

*The exact cable, the exact jack, and how each computer sees the radio once it's plugged in.*

> **In a nutshell** — Plug one USB cable from the radio's USB port into the computer. The radio then shows up on the computer as a **serial port** (a name like `COM3` on Windows, or `/dev/tty.usbserial-XXXX` on a Mac). Find that name and write it down — you will type it into the app in a later chapter. The same cable also gives the computer the radio's **audio**, which is what makes the waterfall's audio features, CW/RTTY decode, and remote audio possible.


## The One Cable You Need

You control the radio with a single USB cable between the radio and the computer. Which cable depends on your radio:

| Radio | Cable | Plain description |
| --- | --- | --- |
| IC-7300 / IC-7300MK2 | USB-A to USB-B | The flat rectangular plug (USB-A) goes into the computer; the squarish plug (USB-B, the kind on the back of a printer) goes into the radio. |
| IC-705 | USB-A to USB-C | The flat rectangular plug (USB-A) goes into the computer; the small oval reversible plug (USB-C) goes into the radio. |

> **If your computer only has USB-C sockets.** — Many newer laptops and all recent Macs have only the small oval USB-C sockets. That is fine — use a **USB-C to USB-B** cable for the IC-7300/MK2, or a **USB-C to USB-C** cable for the IC-705, or a simple USB-A-to-USB-C adapter with the cable you already have. The radio does not care which shape is on the computer end.

> **Use a good, short cable — and a ferrite helps.** — A short, good-quality USB cable makes for a more reliable connection and less radio-frequency noise. A clip-on **ferrite choke** near the radio end can quiet down 'RF getting into the USB' problems that show up as dropped control or stuttering audio when you transmit. This is optional, but worth it on a busy station.


## Where It Plugs In — IC-7300 and IC-7300MK2

On the **back** of the IC-7300 (and IC-7300MK2), find the small squarish socket labeled **[USB]**. It is a USB type-B connector, the same shape as the socket on the back of a typical printer. Push the USB-B end of your cable firmly into it, and the other end into a USB socket on your computer.

> _[Figure: The rear panel of the IC-7300 with the square [USB] type-B socket clearly circled and labeled, and the USB-B cable plugged into it]_

> **This is not the microphone jack or the ACC jack.** — Use the rear **[USB]** socket only. Do not use the front microphone connector or the round **[ACC]** accessory socket for computer control — those are for other things. The single rear USB port carries both the control link and the audio you need.


## Where It Plugs In — IC-705

On the **side** of the IC-705, behind the rubber cover, find the oval **USB-C** socket (marked with the USB symbol; it sits near the DC-power and headphone jacks). Plug the USB-C end of your cable in there, and the other end into your computer.

> _[Figure: The side of the IC-705 with the rubber port cover open, the USB-C socket circled and labeled, and a USB-C cable plugged in]_

> **The IC-705 also charges over USB-C.** — The USB-C port can also power and charge the radio. For steady control and operating, run the IC-705 from its own DC power or a well-charged battery — do not rely on the computer's USB port alone to keep it running while you operate.


## What the Radio Becomes: a COM Port Plus a Sound Card

This is worth understanding because it explains why one cable does so much. When you plug in the radio, the computer sees **two** new devices at once:

- **A serial port** (also called a COM port or a 'CAT' port). This is the control channel — the app sends CI-V commands and reads the frequency, mode, and meters through it. This is the port name you will type into the app.
- **A USB audio device** (a sound card named for the radio). This carries the received audio into the computer and your transmit audio back out. IcomRigControl uses it for the audio-based features: CW and RTTY decode, the APRS beacon, the Band DVR, remote audio, and 'Listen' on the phone remote.

You configure the **serial port** in this chapter and the next. The **audio device** is picked later, only for the features that need it — most people can ignore it at first.


## Installing the USB Driver (Windows)

The IC-7300, IC-7300MK2, and IC-705 all use a common **Silicon Labs CP210x** USB chip to create the serial port. Windows 10 and 11 usually install the driver automatically the first time you plug the radio in — wait a minute after connecting and you may be done.

1. Plug the radio in (with the radio powered on) and wait about a minute for Windows to finish 'Setting up a device.'
2. Open **Device Manager** (press the **Windows key**, type `Device Manager`, and press **Enter**).
3. Expand **Ports (COM & LPT)**. If you see **Silicon Labs CP210x USB to UART Bridge (COM#)**, the driver is installed and you are done — note the `COM#` number.
4. If instead the radio appears under **Other devices** with a yellow warning triangle, the driver is missing. Download the **Silicon Labs CP210x VCP driver** (Icom links to it on the radio's download page), run it, then unplug and replug the radio.

> _[Figure: Windows Device Manager with 'Ports (COM & LPT)' expanded, showing 'Silicon Labs CP210x USB to UART Bridge (COM3)' highlighted]_

> **macOS and Linux usually need no driver.** — Recent versions of macOS and Linux already include the CP210x driver, so the serial port appears on its own when you plug the radio in. If it does not on an older macOS, install Silicon Labs' macOS VCP driver.


## Finding the Port on Windows

You need the port's name — `COM3`, `COM5`, and so on. From the Device Manager step above, read the number in parentheses after **Silicon Labs CP210x USB to UART Bridge**. That whole name in parentheses, for example **`COM3`**, is your port.

> **Which COM number is the radio?** — If several COM ports are listed, unplug the radio, watch which entry disappears, then plug it back in and watch which one returns — that is the radio. IcomRigControl's own port picker (next chapter) also lists the available ports for you, so you can simply pick from the list.


## Finding the Port on macOS

1. Open **Terminal** (press **Command-Space**, type `Terminal`, press **Return**).
2. Type this and press **Return**:
3. Look for a line like `/dev/tty.usbserial-0001` or `/dev/tty.SLAB_USBtoUART`. That whole path is your port name.

```
ls /dev/tty.*
```

> **Too many lines?** — Run `ls /dev/tty.*` once with the radio unplugged and once plugged in, and compare — the line that appears only when the radio is connected is the one you want. Note it exactly, including the `/dev/tty.` at the front.


## Finding the Port on Linux and Raspberry Pi

1. Open a terminal.
2. Plug in the radio, then run:
3. The newest line usually names the radio's port, typically `/dev/ttyUSB0`. You can also just list them with `ls /dev/ttyUSB*`.

```
dmesg | tail -20
```

> **One extra step on Linux — permission to use the port.** — By default your user is not allowed to open serial ports, so the app would fail to connect. Fix it once: run the command below, then **log out and log back in** (or reboot) for it to take effect. On the Raspberry Pi this is often already done, but running it does no harm.

```
sudo usermod -aG dialout $USER
```


## Write Down Your Port Name

Before moving on, write down the exact port name for your computer. You will type it into IcomRigControl in the 'First Launch' chapter. Concrete examples of what a real port name looks like:

| Computer | A real example | Where you found it |
| --- | --- | --- |
| Windows | `COM3` | Device Manager → Ports (COM & LPT) |
| macOS | `/dev/tty.usbserial-0001` | Terminal: `ls /dev/tty.*` |
| Linux / Raspberry Pi | `/dev/ttyUSB0` | Terminal: `ls /dev/ttyUSB*` |

> **Don't type these examples literally.** — `COM3` and `/dev/ttyUSB0` are only examples. Use the real name your own computer showed you. If yours is `COM5` or `/dev/ttyUSB1`, use that.


## Troubleshooting

- **"Nothing new appeared when I plugged in the radio."** Make sure the radio is powered **on**, try a different USB cable (some cheap cables are charge-only, with no data wires), and try a different USB socket on the computer. On Windows, check Device Manager for a yellow warning triangle, which means the driver is missing (see the Windows driver section).
- **"Windows shows the device under 'Other devices' with a warning triangle."** The CP210x driver is not installed. Download and run the Silicon Labs CP210x VCP driver, then unplug and replug the radio.
- **"I see a COM port but which one is the radio?"** Unplug the radio and watch which entry disappears; plug it back in and it reappears. That is the radio's port.
- **"On Linux the app says permission denied when it connects."** You are not in the `dialout` group yet. Run `sudo usermod -aG dialout $USER`, then log out and back in.
- **"Control works but drops out when I transmit."** This is usually radio-frequency energy getting into the USB cable. Use a shorter, better-shielded cable, add a clip-on ferrite near the radio end, and check your antenna system's SWR.
- **"I don't see a sound card for the radio."** The audio device is only needed for the audio features. It appears under your system's sound settings named for the radio's USB codec; you will select it later, only if you use those features.

With the cable connected and the port name written down, the next chapter sets the matching options **on the radio itself** so it will answer the computer.


# 4. Setting Up Your Radio's CI-V Menu

*The handful of settings to change on the radio itself so it answers the computer.*

> **In a nutshell** — On the radio, go to **Menu → SET → Connectors → CI-V** and confirm five things: the **CI-V USB Baud Rate** is `115200` (or `Auto`), the **CI-V Address** matches your model, **CI-V USB Echo Back** is `OFF`, **CI-V Transceive** is `ON`, and — for the IC-7300/MK2 — the **CI-V USB Port** is set to **Unlink from [REMOTE]**. Save and back out. That is the whole radio side.


## Why This Chapter Matters

The USB cable is only half the connection. The radio also has to be told to listen and answer over that cable, at a speed and under a name the computer expects. These live in the radio's **CI-V** settings. There are only a few, you set them once, and this chapter walks through every one — what it is, what to set it to, and why.

> **You will not break anything.** — These are just menu settings. If you change the wrong one, you can change it back. Nothing here transmits or affects your radio's receive performance — they only govern how the radio talks to the computer.


## Opening the CI-V Menu (IC-7300 and IC-7300MK2)

1. Press the **MENU** button on the radio's front panel.
2. Touch **SET** on the screen.
3. Scroll down and touch **Connectors**.
4. Touch **CI-V**. You are now looking at the list of CI-V settings you will adjust below.

> _[Figure: The IC-7300 touchscreen showing the CI-V settings list under Menu → SET → Connectors → CI-V, with the items named in the steps visible]_

> **How to change a value on the IC-7300 screen.** — Touch the setting's name, then turn the main dial (or touch the up/down arrows) to change its value. Touching **EXIT** backs out one level and keeps your change. There is no separate 'Save' button — the setting is kept as soon as you change it.


## The Settings to Check, One by One

Go through these in order. For most people the factory defaults are already correct; the point is to confirm each one so nothing is a surprise later.


### CI-V USB Baud Rate

This is the speed of the control link over USB. Set it to **`115200`**, which is the speed IcomRigControl uses. If you prefer, you may leave it at **`Auto`** — the radio then matches whatever the computer uses — but a fixed `115200` is the most predictable.

> **In plain words.** — Baud rate is how fast the two sides talk. Both ends must agree, or they hear gibberish. Setting the radio to `115200` makes it agree with the app; `Auto` lets the radio figure it out.


### CI-V Address

This is the radio's 'name tag' — the number the computer uses to address it. Confirm it matches the factory default for your model, because that is what IcomRigControl expects when you pick that model in the app:

| Your radio | CI-V Address should read |
| --- | --- |
| IC-7300 | `94h` |
| IC-7300MK2 | `B6h` |
| IC-705 | `A4h` |

> **If you ever changed this, change it back.** — If your CI-V Address shows anything other than the value above for your model, set it back to that value — unless you know exactly why you changed it. The address in the radio and the model you pick in the app must agree, or the app cannot reach the radio.


### CI-V USB Echo Back

Set this to **`OFF`** (the factory default). When it is ON, the radio repeats every command back to the computer, which is unnecessary here. IcomRigControl is built to ignore those echoes if they occur, so ON will still work — but `OFF` is the clean choice.


### CI-V Transceive

Set this to **`ON`** (the factory default). With Transceive ON, the radio automatically tells the computer whenever you turn the dial or change mode on the front panel, so the app's display follows what you do at the radio. Turn it OFF only if you have a specific reason.


### CI-V USB Port (IC-7300 / IC-7300MK2 only)

Set this to **`Unlink from [REMOTE]`** (the default). This keeps the USB control port independent of the round [REMOTE] jack on the back, which is what you want when controlling over USB.


## A Quick Table You Can Copy

Here is the whole radio side on one line each. Set these and you are done.

| Setting | Set it to | Why |
| --- | --- | --- |
| CI-V USB Baud Rate | `115200` (or `Auto`) | Matches the app's speed |
| CI-V Address | `94h` / `B6h` / `A4h` for your model | Must match the app's model |
| CI-V USB Echo Back | `OFF` | No echoes needed |
| CI-V Transceive | `ON` | Front-panel changes update the app |
| CI-V USB Port (7300/MK2) | `Unlink from [REMOTE]` | Use the USB port for control |


## IC-705 Differences

The IC-705 has the same CI-V settings, reached almost the same way. Press **MENU**, touch **SET**, then **Connectors**, then **CI-V**, and set the same values as the table above — with the CI-V Address confirmed as **`A4h`**. The wording on screen is essentially identical; the IC-705 simply has a smaller touchscreen and adds features (like GPS and D-STAR) that IcomRigControl does not use.

> **Bluetooth on the IC-705.** — The IC-705 can also do CI-V over Bluetooth, but IcomRigControl connects over the USB-C cable, which carries both control and audio. Use the USB cable; you do not need to set up Bluetooth.


## A Note on Transmit Audio

IcomRigControl keys the transmitter over the CI-V link itself, so you do **not** need to set up any 'USB SEND' or RTS/DTR keying for push-to-talk to work — that is handled for you.

The only time the radio needs extra setup is for features that send **audio** to the radio to transmit — the APRS beacon, RTTY transmit, and remote-audio transmit. For those, the radio must be told to take its transmit audio from the USB port (typically by operating in a **USB / DATA** mode and setting the modulation input to **USB**). This is an operating detail rather than an install step, and the User Manual covers it for each of those features. If you are only controlling the radio and listening, you can ignore it entirely for now.

> **Keep Receive-Only on until you're ready.** — While you are still setting things up, leave the app's **Receive-Only** switch on (the red 'TRANSMIT INHIBITED' banner). That way nothing can key the radio by accident before you have deliberately set up transmitting.


## The Remote Power-On Setting (Optional)

IcomRigControl can power the radio on and off over the link (the **Power On** and **Power Off** buttons on the dashboard). Powering **off** always works. Powering back **on** only works if the radio was left in a low-power standby where its control brain stays awake — and that is governed by one radio setting.

1. If you want remote power-on to be possible, find the radio's **Power OFF Setting** for remote control (in the radio's SET menu) and set it to allow **Standby** (often shown as **Standby/Shutdown**) rather than **Shutdown only**.
2. With that set, a remote **Power Off** puts the radio in standby (not fully off), so a later **Power On** can wake it.

> **The honest limits of remote power-on.** — Remote power-on can wake a radio from standby, but it cannot power on a radio that is fully unplugged, hard-switched off, or whose USB connection has dropped. This is the same limitation Icom's own remote software has — it is not specific to IcomRigControl. If remote power is important to you, leave the radio in standby (not fully off) when you step away.


## Troubleshooting

- **"I set everything but the app still won't connect."** Double-check the **CI-V Address** matches your model, and that the **CI-V USB Baud Rate** is `115200` (or `Auto`). Then confirm you picked the right **port** in the app (previous chapter) and the right **model** (next chapter).
- **"The app connects but the frequency doesn't update when I turn the dial."** Turn **CI-V Transceive** to `ON`.
- **"I can't find the CI-V menu."** On the IC-7300/MK2 it is **Menu → SET → Connectors → CI-V**. If your radio's firmware is older, the wording may differ slightly, but it is always under **SET → Connectors**.
- **"Remote Power On does nothing."** The radio must have been in **standby** (not fully off), and the **Power OFF Setting** must allow Standby. A fully powered-off or unplugged radio cannot be woken.

The radio is now ready to answer. The next chapter opens the app, points it at your radio, and confirms the connection.


# 5. First Launch — Connecting in the App

*Open IcomRigControl, point it at your radio, and confirm it's really talking.*

> **In a nutshell** — Open the app, click **Settings**. Under **Radio Connection**, set **Radio Model** to your radio, set **Connection Mode** to **Serial**, click **Refresh** and pick your **Serial Port** from the list, then click **Save Settings** and restart the app. When it reopens it connects automatically and shows your radio's real frequency.


## Starting the App for the First Time

Start IcomRigControl the normal way — the Desktop or Start-menu shortcut on Windows, the app in your Applications folder on macOS, or the application-menu entry on Linux and the Raspberry Pi. The main **dashboard** opens: a frequency readout, mode buttons, meters, a spectrum waterfall area, a row of transmit controls, and a strip of buttons that open the other features.

> _[Figure: The IcomRigControl main dashboard as it first opens in Demo mode, with the frequency readout, meters, waterfall area, and the button strip labeled]_


## Trying It in Demo Mode

Out of the box, IcomRigControl starts in **Demo** mode. Demo needs no radio and no cable — it makes up a frequency, meters, and a moving waterfall so you can click around safely and learn where things are. Nothing in Demo mode touches a real radio or puts anything on the air.

> **Demo is a great first stop.** — Before you connect real hardware, spend a minute in Demo mode. Click the feature buttons, open a window or two, and get a feel for the layout. When you are ready for the real radio, follow the rest of this chapter.


## Opening Settings

1. On the dashboard, find and click the **Settings** button.
2. The **Settings** window opens. The very first section at the top is **Radio Connection (Phase 9)** — that is the only section you need for now.

> _[Figure: The Settings window open, scrolled to the top, with the 'Radio Connection (Phase 9)' section and its Radio Model, Connection Mode, and Serial Port controls visible]_


## The Radio Connection Settings

This section has three controls that matter for connecting, in this order: **Radio Model**, **Connection Mode**, and **Serial Port**. Set them top to bottom.


## Picking Your Radio Model

Click the **Radio Model** dropdown and choose the entry for your radio. The three choices read exactly like this in the list:

| Choose this entry | For this radio |
| --- | --- |
| `IC7300` | Icom IC-7300 |
| `IC7300MK2` | Icom IC-7300MK2 |
| `IC705` | Icom IC-705 |

The helper line under the dropdown reminds you which CI-V address each model uses — **IC-7300 (CI-V 0x94), IC-7300MK2 (0xB6), or IC-705 (0xA4)** — and notes that the change 'takes effect after an app restart.' Picking the right model here is what makes the address in the app match the address you confirmed on the radio in the previous chapter.

> **Why the names have no hyphen.** — The list shows `IC7300` rather than `IC-7300`. That is just the internal spelling; it is the same radio. Match it to yours: `IC7300` for a 7300, `IC7300MK2` for the MK2, `IC705` for the 705.


## Choosing Serial Mode and Your Port

Next, set **Connection Mode**. Click its dropdown and choose **`Serial`** — that means 'a radio connected to this computer by USB cable.' (The other choices are **`Demo`**, the no-hardware simulator you just tried, and **`Remote`**, for connecting to a radio over the network, which has its own chapter.)

Now set the **Serial Port (Serial mode)** box to the port you wrote down in the USB chapter:

1. Click the **Refresh** button next to the Serial Port box. This asks the computer for the list of ports available right now.
2. Click in the **Serial Port** box and pick your radio's port from the list that drops down — for example `COM3` on Windows, or `/dev/tty.usbserial-0001` on a Mac.
3. If your port is not in the list, you can type it in directly. The box even hints at the format: 'Pick a port, or type COM3 / /dev/tty.usbserial-XXXX.'

> **Not sure which port is the radio?** — Click **Refresh** with the radio unplugged, note the list, then plug the radio in and click **Refresh** again — the port that just appeared is your radio. Pick that one.


## Saving and Restarting

1. Scroll to the bottom of the Settings window and click **Save Settings**.
2. **Close and reopen IcomRigControl.** The radio model and connection mode only take effect after a restart, so this step is required, not optional.

> **A restart is required after changing the model or mode.** — IcomRigControl builds its connection to the radio when it starts up. So after you switch to Serial mode (or change the radio model), you must fully close the app and open it again for the change to take hold. Just clicking Save is not enough on its own.


## Confirming the Connection

When the app reopens, it connects to the radio automatically — there is no separate 'Connect' button to press. Here is how to know it worked:

- **The frequency readout shows your radio's real frequency.** Tune the radio's dial by hand; the number on screen should follow along (this is the CI-V Transceive setting doing its job).
- **The mode matches** what the radio is set to (USB, LSB, CW, FM, and so on).
- **The meters move** when a signal comes in.
- **The status shows Connected.** If something is wrong, it shows **Connection failed** instead — see Troubleshooting below.

> _[Figure: The dashboard connected to a real radio: the frequency readout showing an on-air frequency, the mode indicator, and a moving S-meter]_

> **A quick two-way test.** — Type a nearby frequency into the frequency box and click **Set Freq**. The radio should jump to it. If the radio follows both your dial (radio → app) and your Set Freq (app → radio), control is working in both directions and you are fully connected.


## Troubleshooting

- **"It says Connection failed."** Work back through the chain: is the right **port** selected (try Refresh and pick again)? Is the **radio model** right? On the radio, is the **CI-V Address** correct for the model and the **CI-V USB Baud Rate** `115200` or `Auto`? Is the radio powered **on**?
- **"The port isn't in the Refresh list."** The computer isn't seeing the radio's serial port. Go back to the USB chapter — check the cable, that the radio is on, and (Windows) that the CP210x driver is installed with no yellow warning in Device Manager.
- **"It connects but the frequency never changes when I turn the dial."** Turn **CI-V Transceive** to `ON` on the radio.
- **"It connected once but not after I moved the cable."** Windows can assign a different COM number on a different USB socket. Re-open Settings, click **Refresh**, pick the new port, Save, and restart. Using the same USB socket each time keeps the number stable.
- **"On Linux it fails with a permission error."** Add yourself to the `dialout` group (`sudo usermod -aG dialout $USER`), then log out and back in.

Your radio is connected. If you only wanted local control, you are finished — the User Manual covers operating every feature. The remaining chapters set up the extras: operating over the network, from a phone, and alongside your other logging programs.
