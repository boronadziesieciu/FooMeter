# AscendAPP 0.10.3 — Square minimap

## Install
Close the earlier app. Extract the complete **Compact-Windows** ZIP into a fresh folder and run AscendAPP.exe. Keep the optional **Compact-Source** ZIP separate; do not combine the two. The existing .NET 10 Desktop Runtime and Npcap prerequisites remain. Shared settings load automatically. Ctrl+Shift+F6 toggles the radar; its gear opens PvP radar settings.

## New look
The radar uses a rounded square, a world-anchored grid, a metric scale bar, a central own-position arrow, clearer corner framing and a slimmer status area. The square includes contacts in its corners, rather than clipping to the old circle. Range is now the distance from the centre to each edge before rotation; corner distances can exceed that number.

Cyan rings identify confirmed party members, gold rings the explicit force list, green dots allies and coral triangles enemies. Faded rings remain last-seen observations. Click a player to track it; hover for details. Names/privacy, own facing source, manual rotation, zoom, retention, death removal, independent window scale/opacity and position locking remain.

## Put an actual map underneath
No game artwork or automatically verified terrain pack is included. A plain grid is the correct default until you import a map. Screenshots in the release preview use clearly labelled synthetic contacts.

1. Open gear → PvP radar → **Add / align zone map**.
2. Choose a full-zone PNG or JPEG you are allowed to use. Prefer a clean map crop without menus. Use a fixed image covering the entire area you want; avoid perspective views or a screenshot with changing camera projection.
3. Stand at a clear landmark. When the dialog says your position is fresh, click your exact location on the imported image.
4. Walk to two more landmarks, clicking each after a fresh position report. Spread them widely across the zone, at least 10 m apart, forming a broad triangle.
5. Walk to a fourth independent landmark and click it. The app checks its world-coordinate error against the transform derived from the first three. Save is enabled only when error is at most 15 m. If it fails, reset and use clearer, more widely spaced landmarks.
6. Name the map and click **Save zone map**. The overlay pans, zooms and rotates the image with the markers.

Keep the alignment window open while moving in game; switch back to it for each click. Position older than five seconds, defeat, an unknown zone, or a teleport during the point collection prevents mixing observations. Move or use a skill to obtain a new position report. The four-point check is a local consistency check; it does not prove that all image locations are accurate. Walk around several landmarks in game before relying on it.

**Show terrain map** toggles the layer; **Map brightness** changes its contrast independently from radar-window opacity. Remove current zone map removes that zone's registration. Original files are never modified. Old normalized copies are left in the local map folder when a map is removed or replaced.

One map is stored for each exact numeric zone, without guessing region from surrounding NPCs. A different or unknown zone falls back to the grid. Returning to the saved zone restores its map. Separate floors that share one zone may need a different image/alignment; this release does not automatically select floors.

## Persistence and performance
Map registrations, four observed world/image anchors and normalized PNGs are saved under **%LOCALAPPDATA%\AscendAPP\radar-maps**. They persist when installing another version alongside the existing shared profile. Imported images are local user data and are not included in exports or release archives. Removing a registration leaves its stored PNG in this folder.

The image is decoded at most once for the selected zone and frozen for WPF rendering. Its longest edge is capped at 3,072 pixels; there is no image download, per-frame disk read, terrain guessing or map-texture extraction. Input is capped at 32 MB, each dimension 64–16,384 pixels, under 100 megapixels. A corrupt or missing map falls back to the grid.

Both terrain and contacts use the same observed origin and affine projection. No inferred travel or interpolation of unseen players is added. Last-seen origin warnings remain visible. Camera-only continuous heading remains unverified: Movement follows travel, and Character facing is the existing intermittent own-skill proxy with its observation age. The +Y indicator labels a protocol axis, not verified geographic north.

## Validation
Release build: zero warnings and errors. **470 core checks and 149 native WPF checks** passed. Added checks cover affine fitting/inversion, reflected/skewed image axes, malformed and degenerate landmarks, independent fourth-point rejection, actual rendered terrain pixels through rotation/zoom, square-corner contacts, map visibility, exact zone matching, teleport/epoch guards, corrupt-image fallback, cached image reuse and map persistence in isolated workspace profiles. Existing party/damage/timer/player lifecycle checks still pass.

No real map image, new live packet capture, or in-game accuracy/performance measurement was supplied for this change. Synthetic fixtures verify the implementation, not a particular game's map alignment. To configure an actual map, supply/import a clean full-zone PNG/JPEG and collect four fresh own-position landmarks using the dialog. No new packet fixture is required for this optional local-image feature.

## References and licensing
The project consulted the MIT reference implementation's [map model](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/8c14501afabb109f9c30963e1c6259a3479b5189/Client/Aion2/Protocol/Aion2Maps.cs) and [map renderer](https://github.com/SkeeveAN/Aion-DPS-Meter/blob/8c14501afabb109f9c30963e1c6259a3479b5189/Client/Ui/MapTiles.cs). Native tile metadata does not establish the calibration of an arbitrary user image; numeric tile offsets were not applied to screenshots. This release uses its own affine landmark calibration and includes no game textures or extracted map assets. Existing protocol attribution and dependency licences remain in THIRD_PARTY_NOTICES.
