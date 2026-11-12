AscendAPP 0.10.3 — Square minimap
================================
INSTALL
1. Close the earlier AscendAPP.
2. Extract the WHOLE Compact-Windows ZIP into a new folder.
3. Run AscendAPP.exe from that folder.
Keep Compact-Source separate; you do not need it to run the app.
The existing .NET 10 Desktop Runtime and Npcap requirements remain.
Your shared settings, placement and shortcuts carry over automatically.

RADAR
Ctrl+Shift+F6 toggles it. The square area includes corner contacts.
Zoom with the mouse wheel or + / -. Follow/manual rotation still work.
Cyan rings: party; gold rings: manual force; green: allies; coral: enemies.
Faded rings are last-seen positions. Click a player to track it.

ACTUAL MAP BACKGROUNDS
No game map artwork is bundled. The grid is shown until you add a map.
Open gear > PvP radar > Add / align zone map.
Choose a full-zone PNG/JPEG, walk to three widely separated landmarks and
click each exact spot while your position is fresh. Use a fourth landmark
to check the alignment, then Save zone map.
Map brightness controls the terrain contrast; radar opacity controls the
whole window. Different zones use their own saved maps or the grid.
Images/alignment persist in the shared profile across releases.
See RADAR_MINIMAP_0.10.3.md for the full setup guide.

CHECKED
470 core checks and 149 native WPF checks passed; Release build has no warnings.
Terrain pixels were checked through rotation and zoom using synthetic images.
No new live capture or real map was supplied. In-game terrain alignment still
needs your image and landmarks. Camera-only continuous rotation remains unverified.
Developer previews/tests used isolated workspace profiles, not your saved settings.
