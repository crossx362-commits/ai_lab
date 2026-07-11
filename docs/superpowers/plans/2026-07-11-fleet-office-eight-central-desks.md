# Fleet Office Eight Central Desks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mixed FleetView seating layout with eight aligned central workstations and place every agent at one of them.

**Architecture:** Edit the existing office bitmap while preserving its room shell, then use one shared 4-by-2 workstation geometry for seat anchors and monitor overlays. Regenerate furniture foreground masks from the new desk rows and verify placement in the live browser.

**Tech Stack:** PNG raster assets, Pillow-based foreground generation, vanilla JavaScript canvas, Python unittest, in-app browser.

## Global Constraints

- Preserve the existing office resolution, camera angle, warm illustrated style, meeting room, cafe, server corner, lounge, walls, and plants.
- Render exactly eight central workstations in two rows of four.
- Keep one chair and one monitor per workstation.
- Keep all walking and resting anchors on open floor.

---

### Task 1: Generate The Eight-Desk Background

**Files:**
- Create: `projects/ai-team/skills/예원_CEO/tools/office_bg_8desks_chibi.png`
- Modify: `projects/ai-team/skills/예원_CEO/tools/office_bg_chibi.png`

**Interfaces:**
- Consumes: current `office_bg_chibi.png` as the visual edit target.
- Produces: a same-size PNG with two centered rows of four desks.

- [ ] **Step 1: Inspect the source background and record its dimensions.**
- [ ] **Step 2: Generate a non-destructive edited PNG with the current room shell preserved.**
- [ ] **Step 3: Confirm the edited PNG has the same dimensions and all eight desks are fully visible.**
- [ ] **Step 4: Promote the inspected image to `office_bg_chibi.png`.**

### Task 2: Map Eight Seats And Monitors

**Files:**
- Modify: `projects/ai-team/skills/예원_CEO/tools/fleet_view.html`
- Test: `projects/ai-team/skills/예원_CEO/tools/test_chibi_sprites.py`

**Interfaces:**
- Consumes: desk centers measured from the new PNG.
- Produces: unique `SEATS` and `MONITORS` entries for all eight agent keys.

- [ ] **Step 1: Add a failing regression assertion for eight unique central seats and monitors.**
- [ ] **Step 2: Run the focused test and confirm it fails against the old mixed layout.**
- [ ] **Step 3: Replace seat and monitor fractions with the measured 4-by-2 grid.**
- [ ] **Step 4: Move rest anchors into the remaining open aisles.**
- [ ] **Step 5: Run the focused test and confirm it passes.**

### Task 3: Regenerate Furniture Occlusion

**Files:**
- Modify: `projects/ai-team/skills/예원_CEO/tools/gen_office_foreground.py`
- Modify: `projects/ai-team/skills/예원_CEO/tools/office_fg_chibi.png`

**Interfaces:**
- Consumes: the new background and desk front rectangles.
- Produces: a foreground PNG that covers only the lower desk lips.

- [ ] **Step 1: Replace obsolete desk masks with eight new desk-front masks.**
- [ ] **Step 2: Regenerate `office_fg_chibi.png`.**
- [ ] **Step 3: Confirm the overlay has transparency and does not cover character heads or torsos.**

### Task 4: Live Verification

**Files:**
- Test: `projects/ai-team/skills/예원_CEO/tools/test_chibi_sprites.py`

**Interfaces:**
- Consumes: the new background, foreground, seat mappings, monitor mappings, and sprite sheets.
- Produces: a verified live FleetView at `http://127.0.0.1:8765/`.

- [ ] **Step 1: Run the full sprite and office regression test.**
- [ ] **Step 2: Compile the Python entry points.**
- [ ] **Step 3: Reload FleetView and inspect all eight seated agents and animated monitors.**
- [ ] **Step 4: Inspect a second frame after movement begins to verify aisles and furniture collision.**
