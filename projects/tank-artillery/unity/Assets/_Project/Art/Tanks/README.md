# TANKFALL native 3D assets

Editable source: `art/blender/tankfall_roster.blend` in the project root.
`FBX/` contains the imported Blender files; `Materials/` contains Unity materials.
`Concepts/CharacterSheet.png` is the design target, not a screenshot of these meshes.

Use `Assets/_Project/Resources/TankModels/Tanks/` for the 13 playable tank prefabs,
and `TankModels/Shells/` for 26 projectile prefabs. Each tank has a `NativeTankRig`
with serialized turret, barrel, firing point, material roles and rotating wheels.

Rebuild via `Tankfall/Rebuild Native 3D Assets` after running the Blender generator.
The editor saves persistent meshes, prefab assets and the TankArtGallery scene.
It preserves any untitled user scene under Scenes/EditorRecovery before opening
an additive gallery. `Tankfall/Render Native Gallery` refreshes gallery and portraits.

Visual approval is pending. Passing mesh and gameplay checks does not establish
90% likeness to the concept sheet. Use output/native-models/compare.html to review.
