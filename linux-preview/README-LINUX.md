# FooMeter Linux · expanded UI

Based on the uploaded FooMeter 0.10.29 source. The Linux application now has a
compact overlay and separate settings, capture, history, skill, event and pet
windows. It uses the original core parser and storage with an Avalonia frontend.
It is a Linux rebuild, not the Windows executable running under Wine.

## Install / update on CachyOS

Close the running copy before replacing its folder. Download and extract the new
`FooMeter-linux-x64-preview.tar.gz` into a fresh directory. Your Linux profile
under `~/.local/share/FooMeter` (or `$XDG_DATA_HOME/FooMeter`) is retained.

Required packages:

```fish
sudo pacman -Syu --needed wireshark-cli xorg-xwayland libx11 libxext libice libsm libxrandr libxi libxcursor fontconfig freetype2 libnotify
sudo usermod -aG wireshark (whoami)
```

The group command uses fish syntax. In bash use `sudo usermod -aG wireshark "$(whoami)"`.
Log out and back in after changing group membership. Check `dumpcap -D` as your
ordinary user. The package supplies dumpcap's capture permissions; do not grant
capabilities to FooMeter or dotnet, and do not launch the GUI with sudo.

From the extracted `FooMeter-linux-x64` folder:

```fish
./run-foometer.sh --demo
```

Close the demo and run `./run-foometer.sh` for live capture. The first launch opens
Capture so you can pick an adapter. For the supplied PC, Ethernet is likely
`enp4s0`; `any` also works. Start before entering your character. Once selected,
the saved adapter starts automatically on subsequent launches. The default TCP
port is 13328; Capture settings can override it. A VPN must expose decrypted game
traffic on the selected adapter. .NET is bundled in the download.

## Compact HUD update

The default combat window now follows the supplied A2tools HUD reference:
380 pixels wide, a target toolbar, thin HP strip, 22-pixel ranked blue damage
rows and a small footer. At 100% scale the single-player HUD is 380 × 88 pixels,
two players are 380 × 112, and strip mode is 380 × 31. Larger parties scroll within
a bounded row area. Capture details, settings and diagnostics open separately.
Unknown HP stays unknown; the HUD does not fabricate latency or game data.

Events use a 300-pixel single-line list (262 pixels tall with the default timers).
Pets use the Windows source's 350-pixel portrait layout with a 235-pixel scrolling
list, search, pins and a small footer. Both windows size to their visible content.

On Hyprland, FooMeter uses `hyprctl` to float and resize **its own overlay clients**
when they appear or change size. It matches both the process ID and window title,
and does not change your global configuration or other windows. This prevents
the compositor from tiling the HUD into half of the screen. Actual compositor
behavior still needs verification on your CachyOS session. If automatic sizing
fails, configure floating rules for `FooMeter Linux`, `FooMeter · Events` and
`FooMeter · Pets` using your installed Hyprland version's rule syntax.

The HUD visual reference is the supplied screenshot and
[taengu/A2Tools-DPS-Meter](https://github.com/taengu/A2Tools-DPS-Meter).
The pet/event panel dimensions and spacing follow the original Windows FooMeter
XAML. The implementation continues to use FooMeter's existing core and Avalonia.

## Interface

Drag the target title to move the combat HUD, or the dotted grip on the pet/event panel. The **☰ menu** opens:

- **Capture:** adapters, port, refresh, start/stop and detailed capture status.
- **Settings:** General, Capture, Appearance, Events, Pets, Share, Scores and Linux.
  Includes import/export, independent overlay scale/opacity, colors, privacy,
  capture scope, encounter duration, dummy limits, server and timer preferences.
- **History:** search, multi-select comparison, deletion confirmation, CSV/JSON
  exports, chat copy and player details. Ctrl-click two fights to compare them.
- **Events:** separate movable window with schedules, observed field bosses and
  local reminders. Unknown game values remain unknown.
- **Pets:** separate portrait window, search, pins, hide-maxed and pinned-only.
  Soul pickup cards appear from received game packets.
- **Healing view:** changes the combat rows to HP-based estimated healing/HPS.
- **One-line strip:** switches the combat overlay to a small summary.
- **Copy / export:** shares the current encounter; name privacy applies to chat.
- **Click-through:** passes clicks through the combat overlay. Hold Alt to
  interact temporarily. Escape restores input while the window has focus.
- **Linux integration:** command bindings, optional OCR, reminders, diagnostics,
  profile folder and the Linux update page.

Click a combat row to open its complete skill breakdown: sorting by damage,
healing, hits, crits or maximum hit, plus a per-second damage timeline. Boss HP,
estimated defeat time and berserk information are shown only when supported by
observed data and the bundled catalogue. Manual personal GS/CP fallback entries
must match your exact character name and server; they are marked with `*`.

Appearance changes apply live and persist. Use **Save and apply** in Settings.
Saved positions, settings, pet data and history are separate from Windows data.
Settings import preserves the previous Linux settings as a timestamped backup.
No Windows profile is automatically modified.

## Hyprland, input and tray

The app currently uses XWayland. Keep it enabled. Your compositor controls
floating/pinning and whether windows can appear above fullscreen games. Borderless
windowed game mode is the recommended starting point. The combat, pet and event
windows have drag grips and position-lock settings.

X11 click-through and restoring input are tested. Alt key visibility while a
native Wayland game has focus depends on XWayland/Hyprland. Use the command below
as an independent recovery binding so input can always be restored:

```ini
# Replace the path with your actual extracted application directory.
bind = CTRL SHIFT, F12, exec, /home/fooyer/Downloads/FooMeter-linux-x64/run-foometer.sh --command clickthrough
bind = CTRL SHIFT, F10, exec, /home/fooyer/Downloads/FooMeter-linux-x64/run-foometer.sh --command toggle
bind = CTRL SHIFT, F9, exec, /home/fooyer/Downloads/FooMeter-linux-x64/run-foometer.sh --command reset
bind = CTRL SHIFT, F11, exec, /home/fooyer/Downloads/FooMeter-linux-x64/run-foometer.sh --command copy
bind = CTRL SHIFT, F6, exec, /home/fooyer/Downloads/FooMeter-linux-x64/run-foometer.sh --command strip
bind = CTRL SHIFT, F7, exec, /home/fooyer/Downloads/FooMeter-linux-x64/run-foometer.sh --command events
```

Additional commands: `show`, `hide`, `pets`, `settings`, `quit`. These communicate
with the running live instance over a user-only local socket. Demo mode does not
register a command socket. A second live launch shows the existing instance.

The tray icon offers show/hide, settings, reset, copy, events, pets and quit.
Enable a tray module in your Waybar/panel for it to be visible. Notifications use
`notify-send`; your desktop must run a notification daemon. Settings controls
are not registered as global X11 grabs; compositor bindings work with the game
focused and avoid grabbing unrelated desktop keys.

## Optional loot-row recognition

```fish
sudo pacman -S --needed grim slurp tesseract tesseract-data-eng
```

Open **Linux integration → Choose loot region**, select only the game's loot
text, then **Start loot reader**. The opt-in reader takes temporary regional
screenshots with `grim` and uses offline English Tesseract TSV output to locate
`Soul: ...` rows. Counters follow the rows in a transparent, input-pass-through
window beside the selected region. OCR never adds souls or invents collection
progress. A `*` marks saved/unconfirmed progress. Stop the reader from the same
menu. Choose the region again if the game, monitor layout or scale changes.

Temporary screenshots are user-readable only and removed when the reader stops.
The live screencopy path requires Hyprland's screencopy support. Multi-monitor
fractional scaling and native Wayland input/placement still need testing on the
actual desktop; the cloud test display cannot establish those behaviors.

## Build and checks

Install .NET SDK 10, then run `bash build-linux.sh`. It builds the Linux-only
solution, runs the existing 555 core checks plus Linux model/persistence/export
and OCR checks, and publishes the self-contained linux-x64 package.

With an X11 display, run:

```sh
dotnet run --project src/FooMeter.Linux -c Release -- --smoke-ui /tmp/foometer-ui.json
```

The smoke run visits all settings tabs, changes and saves an accent, opens the
separate windows, checks history comparison, X11 input-shape restoration and
command IPC, then writes JSON and screenshots. All combat/history used by it is
labelled synthetic and it never writes your real profile.

Other diagnostics:

```sh
./run-foometer.sh --devices
./run-foometer.sh --self-test-linux
./run-foometer.sh --replay /path/to/recording.pcap
./FooMeter.Linux --ocr-image /path/to/loot-screenshot.png
```

Replay accepts classic pcap and does not write live history. Convert pcapng with
`editcap -F pcap input.pcapng output.pcap`. Keep captures and screenshots private.

Windows-specific APIs and the Windows installer/updater are replaced by Linux
capture, X11 input shapes, compositor commands, screenshot tools and manual Linux
package updates. Live Aion 2, actual Hyprland stacking, tray hosting, notifications
and screencopy must be tested on your PC. Passing synthetic checks is not proof
of compatibility with future game patches. The Windows frontend and solution
remain in the source archive for reference.

Additional compositor-isolation check (requires an X11 display):

```sh
python3 tests/test-hyprland-overlays.py artifacts/FooMeter-linux-x64/FooMeter.Linux
```

This supplies a mock `hyprctl` executable, verifies scoped floating/resizing and
confirms that a foreign client is never targeted. It does not contact your actual
compositor. The UI smoke test checks actual rendered window bounds and headers.
