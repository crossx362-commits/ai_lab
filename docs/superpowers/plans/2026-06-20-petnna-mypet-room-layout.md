# Petnna Mypet Room Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the Mypet room so butler-pet links feel natural, pet avatars do not overlap, and the stage layout feels more polished.

**Architecture:** Keep the existing vanilla JavaScript rendering path in `projects/petnna/js/mypet.js`. Replace random circular placement with deterministic safe slots plus collision relaxation, and replace the dashed leash plus emoji with layered SVG connection paths and small inline connection nodes. Add focused CSS for the refined stage visuals.

**Tech Stack:** Vanilla JavaScript, SVG DOM APIs, Tailwind utility classes already present in templates, existing `projects/petnna/css/style.css`.

## Global Constraints

- Do not refactor unrelated Petnna modules.
- Do not introduce dependencies.
- Keep existing upload/click behavior for active pet and butler avatars.
- Preserve Korean UI copy and UTF-8 content.
- Verify touched JavaScript with `node --check`.

---

### Task 1: Collision-Safe Pet Stage Layout

**Files:**
- Modify: `projects/petnna/js/mypet.js`

**Interfaces:**
- Consumes: global `pets`, `activePetIndex`, `setActivePet(idx)`, `triggerPetPhotoUploadDirect()`.
- Produces: `renderPetStageList()` still renders into `#pet-stage-list` and `#leash-svg`.

- [ ] Replace random radius placement with deterministic stage slots selected by pet index and count.
- [ ] Apply a small relaxation loop that pushes pet positions apart when their label/avatar boxes would overlap.
- [ ] Keep all pet positions inside the stage bounds and away from the central butler avatar.
- [ ] Keep active pet click-to-upload and inactive pet click-to-select behavior unchanged.
- [ ] Run `node --check projects/petnna/js/mypet.js`.

### Task 2: Premium Connection Lines And Icons

**Files:**
- Modify: `projects/petnna/js/mypet.js`
- Modify: `projects/petnna/css/style.css`

**Interfaces:**
- Consumes: the safe positions from Task 1.
- Produces: SVG paths with classes `room-connection-line`, `room-connection-glow`, and `room-connection-node`.

- [ ] Draw each connection as a soft background glow plus a foreground curve.
- [ ] Use active/inactive classes instead of inline dashed styling.
- [ ] Replace the floating leash emoji with a small centered heart/paw node embedded into the line.
- [ ] Add CSS animation and reduced-motion handling for connection lines.
- [ ] Run `node --check projects/petnna/js/mypet.js`.

### Task 3: Stage Polish And Responsive Spacing

**Files:**
- Modify: `projects/petnna/js/templates/mypet.js`
- Modify: `projects/petnna/css/style.css`

**Interfaces:**
- Consumes: existing IDs `#leash-svg`, `#butler-graphic-container`, `#pet-stage-list`, and `#pet-speech-bubble`.
- Produces: a room stage with better vertical space, softer butler ring, and no speech bubble collision with top pet slots.

- [ ] Increase stage height slightly on desktop and mobile.
- [ ] Move the speech bubble upward but keep it readable and within the stage.
- [ ] Add CSS for stage depth, butler aura, pet hover, active state, and dark theme connection colors.
- [ ] Run syntax checks for touched JavaScript files.
