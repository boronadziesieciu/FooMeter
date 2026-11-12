# FooMeter Linux preview

An experimental Linux frontend based on FooMeter 0.10.29, intended for testing
on CachyOS with Hyprland/XWayland. This is an initial preview, not a feature-complete
Linux release.

- **[Download the Linux x64 app (43 MB)](https://github.com/boronadziesieciu/FooMeter/raw/refs/heads/linux-preview/linux-preview/FooMeter-linux-x64-preview.tar.gz)** — .NET is included.
- [Download the modified source](https://github.com/boronadziesieciu/FooMeter/raw/refs/heads/linux-preview/linux-preview/FooMeter-linux-preview-source.zip)
- **[Read the CachyOS setup guide](README-LINUX.md)** — required packages and dumpcap permissions.
- [SHA-256 checksums](SHA256SUMS.txt)

Extract the application archive, open a terminal in `FooMeter-linux-x64`, and run:

```sh
./run-foometer.sh --demo
```

Close the demo and run `./run-foometer.sh` for live capture after completing the
capture-permission setup in the guide. Start capture before entering your character.
Run the app as your normal desktop user.

Included: combat and skill views, observed GS/CP, event and boss timers, pet
progress and saved fight history. Not yet ported: loot-row OCR, click-through/Alt
interaction, global hotkeys, tray integration, appearance settings and export UI.

Validation: 555 core checks passed, Linux model and X11 UI smoke checks passed,
and real dumpcap adapter enumeration/failure handling passed. Live game traffic,
CachyOS capture permissions and Hyprland window stacking still require testing
on the user's machine.

The screenshot below uses synthetic demo data.

![FooMeter Linux preview showing synthetic combat](FooMeter-linux-preview.png)
