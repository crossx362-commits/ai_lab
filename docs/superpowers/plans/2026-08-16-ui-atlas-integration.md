# UI Atlas Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline execution selected by the owner) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render the supplied one-page pixel-art UI atlas in the playable Ashes to Stars Unity screens.

**Architecture:** Keep the atlas as one PNG under `Assets/Resources/ui/`; `UiAtlas` owns named pixel rectangles and GUI drawing helpers. `GameScreen` consumes those helpers for the shared header, five-button bottom navigation, standard action buttons, and framed content panels, so every existing play screen receives the visual update without scene edits.

**Tech Stack:** Unity 6000.5, C#, IMGUI, Unity Editor self-check.

**Spec:** `docs/GAME_DESIGN_ASHES_TO_STARS.md` §16 and `docs/GAME_ART_RESOURCES.md` §2-7.

## Global Constraints

- Preserve the user-supplied atlas as one RGBA PNG; do not regenerate or repaint it.
- Store UI art under `Assets/Resources/ui/` so runtime `Resources.Load` can resolve it.
- Use point filtering and alpha transparency.
- Do not modify Unity scene files; they are concurrently edited in the main checkout.
- Do not bake Korean text into images; existing IMGUI remains the text layer.

---

### Task 1: Define and verify atlas layout

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/UiAtlas.cs`
- Create: `Assets/_Game/Scripts/Editor/UiAtlasSelfCheck.cs`

**Interfaces:**
- Produces `UiAtlas.Icon`, `UiAtlas.Frame`, `UiAtlas.DrawIcon`, `UiAtlas.DrawFrame`, and `UiAtlas.IsReady`.
- Consumes `Resources/ui/ashes_to_stars_ui_atlas`.

- [ ] **Step 1: Write the failing editor self-check**

Add assertions that the atlas loads, all named rectangles have positive sizes, and every rectangle stays inside the 1448×1086 source image.

- [ ] **Step 2: Run the self-check to verify it fails**

Run Unity in batch mode with `-executeMethod AshesToStars.UiAtlasSelfCheck.Run`.
Expected: compilation/self-check failure because `UiAtlas` and the atlas asset do not exist.

- [ ] **Step 3: Add the atlas asset and minimal `UiAtlas` implementation**

Copy the supplied PNG to `Assets/Resources/ui/ashes_to_stars_ui_atlas.png`; implement named source rectangles for five navigation icons, three button states, the panel, portrait frame, HP frame, and five rarity frames. Draw icons with `GUI.DrawTextureWithTexCoords` and frames with `GUI.DrawTexture`.

- [ ] **Step 4: Run the self-check to verify it passes**

Run the same batch-mode method. Expected: log contains `[UiAtlasSelfCheck] PASS`.

### Task 2: Apply atlas chrome to all game screens

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/GameScreen.cs`
- Test: `Assets/_Game/Scripts/Editor/UiAtlasSelfCheck.cs`

**Interfaces:**
- Consumes `UiAtlas` methods from Task 1.
- Produces atlas-backed header, bottom navigation, standard action buttons and information panels.

- [ ] **Step 1: Extend the self-check with required semantic keys**

Assert that `territory`, `field`, `tower`, `worldmap`, `characters`, `button_normal`, `panel`, and `hp_frame` resolve to valid atlas rectangles.

- [ ] **Step 2: Run the self-check to verify it fails**

Run the batch-mode check. Expected: failure until all requested semantic keys exist.

- [ ] **Step 3: Replace shared plain chrome in `GameScreen`**

Draw the title accent with the atlas, draw bottom-bar icons above labels, use the atlas button normal/hover/pressed regions underneath existing `GUI.Button` hit testing, and draw atlas panels behind `Info` text. Keep color and text fallbacks when the atlas cannot load.

- [ ] **Step 4: Run the self-check and game compile**

Run `UiAtlasSelfCheck.Run`, then Unity batch-mode compile for the project. Expected: both exit 0.

### Task 3: Visual smoke verification

**Files:**
- No source changes required unless verification finds a defect.

- [ ] **Step 1: Build the existing playable target**

Run the project’s Unity batch-mode W2 build entry point.

- [ ] **Step 2: Capture a title and estate screen**

Run the existing automation entry point if available and inspect captured PNGs. Verify the atlas is visible in header, navigation and action controls while Korean labels remain legible.

- [ ] **Step 3: Commit intentional changes**

Stage only the atlas PNG, its Unity meta file, `UiAtlas.cs`, `UiAtlasSelfCheck.cs`, `GameScreen.cs`, and this plan; commit with a Korean message.
