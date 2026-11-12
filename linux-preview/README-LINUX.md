# FooMeter Linux preview (based on 0.10.29)

This is an initial Linux port of the uploaded FooMeter 0.10.29 source, with an
Avalonia desktop frontend. It reuses the actual combat parser, admission filters,
HP-based healing reconciliation, timers, pet collection tracking and history
storage. It is not a feature-complete replacement for the Windows application.

## CachyOS / Arch / Hyprland

Install the native dependencies in a terminal (these commands also work in fish):

```sh
sudo pacman -Syu --needed wireshark-cli xorg-xwayland libx11 libice libsm libxrandr libxi libxcursor fontconfig freetype2
```

Enable capture for your user using Arch's Wireshark group:

```sh
sudo usermod -aG wireshark (whoami)
```

The command above uses **fish** syntax. In bash use `sudo usermod -aG wireshark "$(whoami)"`.
Log out of your desktop session and log back in for group membership to take
effect. The Arch `wireshark-cli` package supplies `dumpcap` and its capture
permissions; do not grant capabilities to FooMeter or the shared dotnet runtime.

Check as your ordinary user:

```sh
dumpcap -D
```

If you get a permission error, inspect `id -nG`, `ls -l /usr/bin/dumpcap` and
`getcap /usr/bin/dumpcap`. Expected packet capture capabilities are
`cap_net_admin,cap_net_raw`; use the distribution's Wireshark setup instructions
to restore missing package permissions. Do not run the GUI with sudo.

Extract `FooMeter-linux-x64-preview.tar.gz`, open a terminal in the extracted
`FooMeter-linux-x64` folder, and run:

```sh
./run-foometer.sh --demo
```

The demo uses **synthetic** combat and never writes a profile. To capture, close
the demo and run:

```sh
./run-foometer.sh
```

Choose the actual Ethernet interface (for the supplied screenshot, likely
`enp4s0`) or `any`, then press **Start before entering your character**. Interface
names vary; use the list rather than hard-coding a name. Choose your VPN tunnel
if it carries decrypted game traffic. Capture filters TCP port **13328**, as used
by this source version; game updates or a different regional protocol may need
parser changes. No process-name detection is required for Wine/Proton.

The UI runs through **XWayland**, not native Wayland. Keep XWayland enabled in
Hyprland. A normal floating window works first; use **Keep above** and your
compositor's rules to position it over a borderless game. Exclusive fullscreen
and compositor policies may prevent an overlay from appearing above the game.

Run `hyprctl clients` to confirm the window title (`FooMeter Linux`). If desired,
add floating/pinned window rules matching that title, using the syntax documented
for your installed Hyprland version. Compositor rules and game stacking are not
verified by the X11 smoke test. No existing Hyprland configuration is edited by
this package.

The Linux x64 download includes .NET; a .NET installation is not required to run
it. This build was tested on a Linux X11 dummy display, not your CachyOS desktop.

## What is included

- Live packet input from `dumpcap`, including Linux cooked SLL/SLL2 capture.
- Self/confirmed party combat against recognized bosses and training dummies.
- Damage, DPS, observed GS/CP, HP-based healing estimates and skill details.
- Three recurring event schedules and observed field-boss timers.
- All 207 bundled pet names, search and packet-derived soul progress. Unknown
  totals remain unknown. Pet portraits are not included in this frontend.
- Completed encounter history, stored separately under
  `${XDG_DATA_HOME:-~/.local/share}/FooMeter` along with pets and boss observations.
  Existing Windows profiles are not modified or automatically imported.
- A normal resizable desktop window with a Keep above toggle.

The initial port ends encounters after 20 seconds without admitted activity or
observed boss death. It does not yet reproduce every Windows lifecycle setting.

Not ported: loot-row OCR/counters, global hotkeys, click-through/Alt interaction,
tray integration, appearance preferences, CSV/export UI, automatic Windows
updater, and the full Windows overlay layout. No game process injection or
memory access is used. No raw packet files are recorded by live capture.

## Diagnostics and replay

The **Info** tab shows packets, TCP payloads, frames, events and identity status.
Packets alone do not mean game events were recognized. Capturing after login
can miss the identity snapshot; capture before entering the character and zone.

```sh
./run-foometer.sh --devices
./run-foometer.sh --self-test-linux
./run-foometer.sh --replay /path/to/recording.pcap
```

Replay accepts classic pcap, with little/big endian and micro/nanosecond times.
For pcapng, convert locally using Wireshark's `editcap -F pcap input.pcapng output.pcap`.
Offline replay prints JSON and does not alter your live profile. Recordings may
contain character names and other private traffic; keep them local.

## Build from source

Install .NET SDK 10 on your development machine, then:

```sh
bash build-linux.sh
```

This builds the Linux-only solution, runs the existing core suite plus Linux
capture checks, runs the Linux model smoke check, and publishes a self-contained
linux-x64 package under `artifacts/`. The original `AscendAPP.slnx` and Windows
frontend are retained. With an X11 display, an additional GUI smoke check is:

```sh
dotnet run --project src/FooMeter.Linux -c Release -- --smoke-ui /tmp/foometer-ui.json
```

It checks rendered synthetic combat, writes JSON and a screenshot, then exits.
Successful synthetic tests do not establish current live game protocol support.
Live game capture, CachyOS group permissions and Hyprland stacking require testing
on the user's machine.
