# Breathing Meadow

Environment-only scene for Breathe Into Being. No AI, breathing sensing, Tripo integration, Dream Seed effect, or new UI is implemented.

## Open the environment

Open `Assets/HealSphere/Meadow/BakedMeadow/BreathingMeadow.unity` by itself in Unity 6000.4.6f1. The original SampleScene and BasicScene remain intact. Build Profiles still contains the original scene: explicitly select the meadow scene in your development build profile when ready to test on Quest. Do not load both scenes together during Play mode, because each contains its preserved XR rig.

The meadow scene is a copy of the saved SampleScene. Its XR Origin Hands prefab, hand/controller references, camera tracking, input actions, and permissions manager are retained. Original demo UI, geometry, lighting, teleport surface and interactables are grouped under `PreservedTemplateContent (Inactive)`. Existing locomotion provider components are disabled only in this scene copy to keep the experience stationary; their input bindings remain assigned. The XR Origin world transform is preserved, and the anchors follow its initial yaw while the Terrain stays unrotated. Nothing moves the camera at runtime.

## Editable scene hierarchy

- `BreathingExperience/XR Origin Hands (XR Rig)`: existing tracked player.
- `EnvironmentRoot/MeadowTerrain`: 768 m square editable Unity Terrain, 257 height samples, three painted grass layers. The center is flat, with rolling relief beyond the interaction area. Terrain Collider supplies the ground.
- `EnvironmentRoot/DistantHills`: twelve editable, overlapping rounded silhouettes. Reposition, rotate and scale each mesh normally.
- `EnvironmentRoot/Grass`: static 10 m mesh tiles with randomized short blades and vertex colors. LOD groups cull distant tiles. No per-frame wind simulation or spawning.
- `EnvironmentRoot/Flowers`: static 12 m mesh tiles with clustered white, yellow, red and occasional pink flowers. A 3.5 m radius around the player remains flower-free.
- `EnvironmentRoot/Trees`: 22 instances of three reusable tree prefabs, primarily around the perimeter. Each prefab includes two geometry LODs and distance culling.
- `EnvironmentRoot/Clouds (Baked Into Skybox)`: organizational marker. Clouds are in the skybox cubemap rather than world geometry.
- `LightingRoot/DirectionalLight`: warm daylight, gentle shadow strength. Ambient color, skybox and horizon fog are saved in the scene Render Settings.
- `GameplayAnchors`: five empty transforms, aligned with the preserved player's initial facing direction.
- `AudioRoot`: the ambience controller and three assigned AudioSources.

## Inspector customization

Select `Assets/HealSphere/Meadow/MeadowRecipe.asset` to configure the source scene, seed, terrain width, hill height, grass density, flower count, tree count, meadow green, sky blue and cloud white. All serialized fields are private; required references are assigned through the Inspector. Choose **HealSphere > Meadow > Bake New Scene From Selected Recipe** to bake another version. Every bake creates a new unique `BakedMeadow` folder; existing generated scenes and assets are never overwritten. Save the source scene before baking. The tool reads its saved state, validates exactly one XR Origin, and closes its temporary additive scene after generation.

For an existing bake:

- Sculpt/paint `MeadowTerrain` with the standard Terrain Inspector. Vegetation was fitted to the baked heightmap; move or rebake affected mesh tiles after changing their ground heights. Horizon meshes can be scaled independently.
- Adjust the shared `Meadow Vegetation` material's Tint and Soft ambient floor. Grass and flower colors are also stored in mesh vertex colors.
- Reusable assets live in `BakedMeadow/Prefabs`: GrassTuft, FlowerWhite, FlowerYellow, FlowerRed, FlowerPink, Tree1, Tree2 and Tree3. Tree instances retain prefab links. Grass/flower tiles are baked meshes for fewer renderers, rather than thousands of GameObjects.
- Assign an alternative sky material in **Window > Rendering > Lighting > Environment**. The generated sky material exposes tint, exposure and rotation; its cloud pattern is baked into a 512 px cubemap.
- Adjust the directional light's color/intensity/shadow strength in its Inspector; adjust fog and ambient lighting in the Lighting window. The existing URP asset's shadow distance is only 2.5 m. This phase does not change it. Distant vegetation does not cast realtime shadows.
- Move any gameplay anchor using its Transform. Coordinates below are relative to the player's initial floor position and facing, not to the tracked headset.
- Select `AudioRoot` to assign the actual wind, birds and optional music AudioClips, and set master/per-track volume. Its `windSource`, `birdsSource`, `musicSource` references are already wired to the three child sources. Clips are intentionally unassigned. Sources are non-spatial, looping, and start only when an assigned clip exists. No placeholder audio is generated. Use seamlessly loopable recordings.

## Anchor layout

| Anchor | Local position in meters | Purpose |
|---|---|---|
| CreationAnchor_01 | (-1.15, 0.35, 1.85) | Left object, 2.18 m horizontal distance |
| CreationAnchor_02 | (0, 0.35, 2.30) | Center object, 2.30 m distance |
| CreationAnchor_03 | (1.15, 0.35, 1.85) | Right object, 2.18 m horizontal distance |
| DreamSeedAnchor | (0, 1.15, 1.75) | Future seed effect |
| AICompanionAnchor | (-1.25, 1.35, 1.55) | Future companion |

These are placement suggestions for later content, not tracking-height compensation. Check companion/seed comfort seated and standing on the actual headset. The three creation anchors have no pedestals, renderers or colliders.

## Performance design and limits

The terrain uses URP Terrain/Lit with three layers in a single layer pass, instanced terrain patches, no Terrain detail system and a distant basemap. Grass and flowers use one opaque, vertex-colored, stereo-aware URP shader with instancing variants and SRP Batcher-compatible material constants. Geometry is grouped into spatial tiles marked for static batching, with LOD culling. Trees share prefab meshes and material. The cloud sky is a static cubemap with no volumetric pass, transparent cloud particles, depth sampling or animation. No runtime environment generation occurs. New environment meshes do not cast realtime shadows. The soft vegetation shader supplies inexpensive stylized directional shading rather than full physically based lighting.

The 72 FPS requirement is a device target, not a measured result. Existing URP, OpenXR, input actions, packages, graphics settings and Android configuration are preserved. Desktop rendering does not establish Quest stereo correctness or mobile performance. No lightmap bake is required for the current ambient-lit layout.

## Validation and remaining work

The editor bake checks exactly one XR Origin, missing scripts, vegetation materials/shader errors and creation-anchor distances. It exports seated (1.25 m) and standing (1.70 m) desktop camera previews to `BakedMeadow/Previews`. The preview camera is temporary and is not saved into the scene. Build diagnostics are written to `Library/MeadowBuildReport.txt`.

Before accepting on device:

1. Open only the meadow scene. Enter Play mode and confirm tracked hands/controllers, camera tracking and the original permissions workflow still work. Check that no demo content or locomotion activates.
2. Inspect all directions at seated and standing heights. Confirm ground coverage, no horizon seams or visible terrain edges, comfortable anchors, and acceptable LOD transitions.
3. Make an Android development build using the existing Quest configuration. Check both eyes for terrain, cloud and foliage rendering, and confirm shader variants are retained in the player build.
4. Profile on Quest 2 at 72 Hz. Measure CPU/GPU frame times, draw calls, triangle counts, memory and thermal stability with room for future generated models and particles. Reduce recipe grass/tree counts or adjust LOD thresholds if needed.
5. Assign real ambient audio and verify quiet, seamless playback and master-volume control.

All environment art is original procedural geometry/texture data generated inside this project. No external art package is required. Real wind/bird/music recordings remain to be supplied. More detailed tree/ground art can replace the reusable assets later if desired. No UI hierarchy or UI Inspector assignment is pending for this environment-only phase.


## Changed files

All implementation assets are under `Assets/HealSphere/Meadow/`:

- `Editor/MeadowBuilder*.cs`: reusable bake, mesh/terrain/sky authoring and validation, compiled only in the Editor.
- `Editor/MeadowRecipe.cs` and `MeadowRecipe.asset`: private serialized authoring controls and source-scene reference.
- `Editor/MeadowReview.cs`: validates and renders a saved generated meadow without saving scene changes. Select a generated scene asset, close that scene if loaded, then use **HealSphere > Meadow > Validate and Render Selected Meadow Scene**.
- `Runtime/MeadowAmbience.cs`: optional playback of assigned real clips through existing sources.
- `Shaders/MeadowSoft.shader`: opaque, inexpensive vegetation shading with stereo and instancing macros.
- `BakedMeadow/`: the finished scene, TerrainData/layers, meshes, materials, eight reusable plant prefabs, textures and review previews. The editor tooling is not needed in a player build.

`Documentation/MeadowEnvironment.md` contains the setup instructions; `Documentation/MeadowValidation.txt` records this implementation's checks. The validator also exports left, rear and right views. Preview rendering waits for shader compilation and uses a temporary render layer restored before closing the review scene; no preview cameras or temporary layers are saved.
