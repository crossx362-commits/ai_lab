# Fleet Office Eight Central Desks Design

## Goal

Rebuild the FleetView office background so all eight agents have one clearly aligned workstation in the center of the room.

## Layout

- Place eight matching desks in a centered 4-column by 2-row grid.
- Keep enough aisle space between rows for walking characters and labels.
- Front row, left to right: Bomi, Teo, Baekho, Suri.
- Back row, left to right: Yewon, Youngsuk, Mio, Namu.
- Each workstation contains exactly one desk, one forward-facing office chair, and one monitor.
- Remove or repurpose the old top workstations so they do not read as additional assigned seats.
- Preserve the meeting room, cafe, server corner, lounge, walls, plants, and the existing warm illustrated office style.

## Character Alignment

- Define all eight seat anchors from the new desk centers in `fleet_view.html`.
- A working or away agent at their seat uses the rear-view seated sprite.
- The character torso overlaps the chair back naturally while the monitor remains visible above the shoulders.
- Standing and walking agents use open floor aisles only and never cross desks, chairs, sofas, plants, or the server area.
- Labels remain above the head and must not overlap the monitor or neighboring labels at the normal viewport.

## Monitor Animation

- Map one animated screen region to each of the eight physical monitors.
- Draw animation only inside the monitor glass; do not add a second monitor body or border.
- Screen animation is visible only while the assigned agent is working.

## Asset Workflow

- Edit the existing office bitmap rather than replacing the entire visual identity.
- Use the current office background as the edit target and preserve its resolution and camera angle.
- Generate a new background version non-destructively, inspect it, then promote the approved version to the FleetView background.
- Regenerate the foreground occlusion overlay so desk fronts and chair edges layer correctly around seated characters.

## Verification

- Add a regression check for exactly eight seat and monitor mappings.
- Confirm every seat anchor is unique and every working agent is rendered seated at its assigned desk.
- Confirm no old furniture coordinates remain in the navigation obstacle and foreground overlays.
- Reload `http://127.0.0.1:8765/` and visually inspect the full office at the current desktop viewport.
- Verify all eight characters, chairs, monitors, labels, walking aisles, and foreground occlusion in a browser screenshot.
