# 0.9.0 — separate compact event timers

Added an independent Obsidian event overlay, toggled from the main bell, tray, Event tracking settings or Ctrl+Shift+F7. It includes Shugo Festival and Spacetime Rift UTC schedule estimates, optional possible invasion slots, and the four major Verteron field bosses from the user's screenshot. Settings select groups, calibrate rift phase, enable optional alerts, lock/reset window position and forget boss snapshots.

Boss data comes from validated server-bound 0x0191 map-list snapshots; no screenshot countdown, fixed respawn cycle or distance is guessed. The UI distinguishes unknown, seen alive, countdown and due/refresh states. Cached snapshots are server-separated and bounded. Schedule rows remain estimates and expose the uncertainty around Global rift phase.

Existing strict self/party + boss/dummy capture and teleport refresh remain included. Validation: 232 core checks, 45 native UI checks, zero build warnings/errors. Live Global timer packet calibration still needs real game verification. See EVENT_TIMERS.md.
