# Petgame Sprite Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add simple idle and walking animation to the Petnna My Pet growth game.

**Architecture:** Keep game logic in `PetGameCore` untouched. Add render-state helpers to `projects/petnna/js/petgame/game-stage.js`, add CSS motion classes to `projects/petnna/css/style.css`, and save generated walk-frame PNG assets under `projects/petnna/images/petgame/pet/`.

**Tech Stack:** Vanilla JavaScript, CSS animation, PNG game assets, existing Petnna static app.

## Global Constraints

- Do not modify `projects/ai-team/harness/`.
- Keep `PetGameCore` DOM-free.
- Existing static pet images must remain valid fallback assets.
- Bump `index.html` script version for `game-stage.js` and `sw.js` cache version when browser-facing behavior changes.
- Verify touched JavaScript with `node --check`.

---

### Task 1: Runtime animation state

**Files:**
- Modify: `projects/petnna/js/petgame/game-stage.js`

**Interfaces:**
- Consumes: `Items.stageForLevel(level)`, `Core.ensureGame(pet)`, existing `wanderTick()`.
- Produces: `.pg-pet-img` with `data-idle-src`, `data-walk-srcs`, and movement classes on `#pg-pet`.

- [ ] **Step 1: Add sprite metadata**

Add helper functions that derive idle and walk frame URLs from pet type and stage:

```javascript
function spriteMeta(p) {
    const g = Core.ensureGame(p);
    const st = Items.stageForLevel(g.level);
    const type = ['dog', 'cat', 'rabbit', 'hamster'].includes(p.type) ? p.type : 'dog';
    const fallback = { dog: '🐶', cat: '🐱', rabbit: '🐰', hamster: '🐹' }[type];
    const base = `images/petgame/pet/${type}_${st.stage}`;
    return { type, stage: st.stage, fallback, idle: `${base}.png`, walk: [`${base}_walk_1.png`, `${base}_walk_2.png`] };
}
```

- [ ] **Step 2: Render image data attributes**

Update `petSprite()` so the image carries idle and walk frame paths and falls back to the idle image if walk frames are missing.

- [ ] **Step 3: Animate during wander**

Update `wanderTick()` so it sets direction, starts a short walk-frame loop, moves the pet, and returns to idle after the CSS transition.

- [ ] **Step 4: Keep feed animation compatible**

Ensure `throwFoodFx()` uses the same movement helper so eating still works and the pet returns to idle.

### Task 2: CSS motion polish

**Files:**
- Modify: `projects/petnna/css/style.css`

**Interfaces:**
- Consumes: `.pg-idle`, `.pg-moving`, `.pg-facing-left`, `.pg-facing-right`, `.pg-pet-img`.
- Produces: subtle idle breathing and slightly livelier walking motion.

- [ ] **Step 1: Add CSS classes**

Add CSS near the existing petgame block:

```css
#pg-pet.pg-idle .pg-pet-img { animation: pgIdleBreath 2.8s ease-in-out infinite; }
#pg-pet.pg-moving .pg-pet-img { animation: pgWalkBob .34s ease-in-out infinite; }
#pg-pet.pg-facing-left .pg-pet-img { transform: scaleX(-1); }
```

- [ ] **Step 2: Respect reduced motion**

Extend the existing reduced-motion rule so `.pg-idle .pg-pet-img` and `.pg-moving .pg-pet-img` have no animation.

### Task 3: Assets, cache, and verification

**Files:**
- Create: `projects/petnna/images/petgame/pet/dog_1_walk_1.png`
- Create: `projects/petnna/images/petgame/pet/dog_1_walk_2.png`
- Create: `projects/petnna/images/petgame/pet/cat_1_walk_1.png`
- Create: `projects/petnna/images/petgame/pet/cat_1_walk_2.png`
- Modify: `projects/petnna/index.html`
- Modify: `projects/petnna/sw.js`

**Interfaces:**
- Consumes: generated PNG assets and existing script loading.
- Produces: browser-visible animation that avoids stale service-worker cache.

- [ ] **Step 1: Generate and save walk-frame assets**

Generate transparent PNG walk poses for baby dog and baby cat, consistent with existing 512x512 pet assets. Save the final files in `projects/petnna/images/petgame/pet/`.

- [ ] **Step 2: Bump browser versions**

Change `js/petgame/game-stage.js?v=2` to the next version in `projects/petnna/index.html`, and increment the service-worker cache name in `projects/petnna/sw.js`.

- [ ] **Step 3: Verify**

Run:

```bash
node --check projects/petnna/js/petgame/game-stage.js
node --check projects/petnna/sw.js
```

Expected: both commands exit successfully.
