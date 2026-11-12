# AscendAPP 0.10.17 — Overlay input and movement

## Install

Close the older AscendAPP instance. Extract the entire **Compact-Windows ZIP** into a fresh folder and launch **AscendAPP.exe**. Keep the optional Source ZIP separate. Existing settings carry over under the same Windows account; Windows still shows its normal administrator prompt. Existing .NET 10 Windows Desktop Runtime and Npcap prerequisites apply.

## Using the overlays

| Action | Result |
| --- | --- |
| Click an overlay's padlock | Locks that overlay's position and passes ordinary mouse clicks through it. Meter, timers and radar retain independent locks. |
| Hold **Alt**, then click | Temporarily interact with and focus the protected overlay. Its buttons, player details and radar controls work normally. |
| Hold **Alt** and drag the top-left six-dot grip | Reposition a locked overlay without changing its saved lock setting. |
| Drag the grip or title bar while unlocked | Move that overlay normally. The title bar now has a continuous hit area, including gaps around its title. |
| Hold **Alt** and drag plain background | Move from an additional background area. Buttons, player-detail rows and the radar map retain their normal interactions. |
| Release **Alt** | Click-through resumes after any click or drag already started on the overlay completes. |
| **Ctrl+Shift+F12**, while protected | Restore clicks and unlock all overlays. Pressing it again enables global click-through. |
| **Ctrl+Shift+F10**, or tray **Show overlay** | Show the meter, disable global click-through and unlock all overlays as a recovery action. |

The shortcut rows above are the defaults; configured keybinds still apply. Alt is a held override, so press it before clicking a protected overlay. The check runs at a 25 ms interval while protection is visible. Alt alone does not activate a window; an actual click focuses it. Ctrl+Alt / AltGr and Windows-key combinations do not enable the override.

The six-dot grips have a visible background, a move cursor, a larger hit box and a hover highlight. The compact meter exposes its header while Alt interaction is active, including during combat focus mode. Existing window positions continue to be saved by the normal position handlers.

Global click-through remains available in Settings and More actions. It covers all three overlays, including the timer window, which previously missed this handling. An individual padlock can protect just one overlay even when the global switch is off. Recovery deliberately clears all three locks so it cannot leave a window inaccessible. Hiding an event/radar window still preserves its own lock; automatically bringing the meter back after a game restart preserves its configured input state.

## Implementation and verification

A shared overlay input service now owns the actual layered-window click-through style. The old independent main-window and radar style writers were removed so a settings refresh cannot override an Alt interaction in progress. A click begun on the overlay can complete after Alt is released, and native window movement holds interaction until it exits. Cancelled movement clears that latch.

Only one modifier sampler is used. It runs while at least one protected overlay is visible and stops when none needs it. It reads current modifier/button state; it does not install a keyboard hook, record keystrokes, inject input or access the game process. Closing windows removes their registrations. Native movement uses WPF's existing DragMove rather than estimating screen movement from overlay scaling.

- Release build: **0 warnings, 0 errors**.
- **841 core checks** passed, retaining the automatic GS/CP, capture, combat, radar and timer coverage.
- **355 native WPF/interface checks** passed with no binding warnings. New checks inspect real overlay HWND styles, independent locks, temporary Alt overrides, modifier conflicts, click/drag completion, cancelled drags, all-window recovery, drag-grip layout and shared sampler start/stop/cleanup.
- Native style/sampler checks use off-screen fixture windows; they do not send input to the running game. Meter, timer and radar previews were rendered and visually reviewed. Preview combat/contact values are synthetic DEMO data.
- The dependency versions and protocol readers are unchanged. Dependencies were restored from the existing local cache; no fresh online vulnerability audit is claimed.

The user reported the 0.10.16 automatic-score app working in game. This release changes overlay input and movement; its Alt behavior still needs a short check over the user's game window.

## Quick in-game check

1. Unlock an overlay and move it with its top-left grip and title bar.
2. Lock it, then click a game control beneath it. The click should reach the game without moving or activating the overlay.
3. Hold Alt, click settings or a normal overlay control, then Alt-drag the grip. Release Alt after the click/drag: protection should return while the padlock remains closed.
4. Repeat with the independent timers and radar. Release Alt during a drag and confirm it can finish normally.
5. Use the recovery shortcut or tray Show overlay once to confirm all three become interactive and unlocked.

If a game or keyboard layout prevents the Alt override, use the recovery shortcut or tray action and report the affected overlay, lock/global-switch state and modifier combination. No new packet capture is needed for an input-only issue.

## Primary Windows references

- [GetAsyncKeyState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate): current state uses the high bit; the unreliable historical pressed bit is ignored. Modifier queries can be limited by desktop/integrity conditions.
- [Layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows): hit testing passes to windows beneath when WS_EX_TRANSPARENT is set on a layered window.
- [Extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles): WS_EX_TRANSPARENT and the existing layered-window style.

Earlier release notes are historical. The automatic own/party GS/CP readers from 0.10.16 remain in this version; no manual score entry is introduced.
