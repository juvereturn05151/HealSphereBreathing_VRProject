# Phase 1 validation — 4 November 2026

## Verified

- Inspected `AGENTS.md`, Unity/project/package versions, source scenes, XR prefab setup, existing anchors, audio and coding conventions before implementation.
- Changes are confined to the new `Assets/HealSphere/OfflinePrototype` folder and its metadata. No existing scene, prefab, input action, package manifest/lock, build profile or project setting was overwritten. Earlier Tripo/backend code is preserved and not referenced by this scene.
- Final runtime and Editor assemblies compiled with Unity 6000.4.6f1's Roslyn compiler using this project's real generated response files/references: **both exit code 0**, no diagnostics.
- The actual C# guided clock and motion hysteresis logic also executed successfully independently of Unity: no early cycle, no catch-up cycles on a stalled frame, no chatter/held-position duplicates.
- A separate Unity 6000.4.6f1 batch authoring project using the same package manifest/lock generated `Authored/BreathingExperience.unity`, three placeholder prefab assets and nine materials. New assets were copied back with their original generated GUIDs. Its source scene was a copy of the existing `BasicScene`, preserving the XR rig, input references and EventSystem.
- The authoring process exited **0**. It found and assigned the tracked `Right Controller` Transform.
- A second Unity batch run compiled the final code and executed `OfflinePrototypeChecks.Run`, exiting **0**. Its report confirms: guided 4/5 timing; stalled-frame handling; motion hysteresis and duplicate suppression; translated/scaled/rotated mesh surface sampling; hard sample cap; unreadable-mesh fallback; restoration of original material and renderer property block; scene references; exactly one XR Origin; no missing scripts; disabled locomotion providers. These checks create no UI and contact no APIs.
- `git diff --check` passed. Source inspection found no runtime/editor construction of Canvas, Buttons, TMP elements or sliders, and no mutable public serialized data fields.

## Pending — do not claim a fully playable build yet

- **Required UI:** author `ExperienceCanvas` and the documented panel/text/buttons visually in the Unity Editor, bind existing components through the supplied menu or Inspector, and save. The companion presenter is intentionally unassigned in the scene. `BreathingSessionManager` reports the missing dependencies and refuses to begin; it never builds UI as a fallback.
- The agent could not access the existing project window through the Windows automation tool. A recovery launch confirmed another instance already had the project open, so that instance was left intact. This was not worked around by generating UI assets through code or YAML.
- No complete Play Mode session was exercised through actual player-facing UI. Session sequencing, mouse/XR button interaction, audio timing, visual particle quality, reveal completion, reflection and restart require the README playtest checklist after UI hookup.
- No Quest APK, on-device shader rendering, sustained performance, controller-motion validity, suspend/resume behavior, seated comfort or UI readability was verified. The motion option is explicitly an unvalidated proxy and not actual respiration detection.
- The sky/island/companion/cat/tree/moon are simple pastel placeholders. No generative AI, spoken AI companion, external service, breathing sensor, clinical interpretation or diagnostic claim is implemented.

## Environment notes

The first temporary authoring run on C: exhausted free space during package extraction. Automatic review rejected recursive deletion of that temporary project. Moving the same known task directory to E: succeeded and recovered its C: space. Subsequent scene creation and deterministic checks ran successfully there. The retained, ignored sandbox and its logs are under `Library/OfflinePrototypeAuthoringCheck`; compiler outputs are under `Library/OfflinePrototypeCompilerCheck`. They are not runtime dependencies or tracked assets. Package UI-style/test-resource import warnings in the sandbox did not prevent final script compilation, authoring, or the successful checks. No project settings were changed to suppress them.

## Mesh formation refinement

The reveal now uses staggered, cached surface targets, a rounded cloud of orbiting motes, spiral attraction, smoothly damped progress, a base-up surface frontier and a single-pass premultiplied core/halo shader. Default pool remains 256, capped at 512. No bloom, extra lights, trail systems, repeated mesh sampling or per-frame particle emission was added. Existing scene/prefab references and original material assets are retained.

Additional deterministic checks passed in Unity: staggered arrival; continued orbit at fixed progress; exact convergence for all tested arrival orders; paused progress; monotonic/bounded smoothing; exactly one completion event; material restoration at completion and cancellation; renderer-level and material-slot property-block restoration. Both runtime and Editor assemblies compiled successfully against the existing project's references.

The actual 3D effect was rendered as four stages through Unity's desktop graphics pipeline, and the montage was visually inspected. Both formation shaders compiled without shader errors. `FormationPreview` renders in a transient preview scene and writes only to `Library/FormationPreview`; it saves no scene assets and constructs no UI. The standalone Play Mode context menus provide an effect preview while session UI remains pending. These Editor controls do not enable the incomplete session or appear in player builds.

Quest rendering cost is bounded by the fixed pool and simple shaders, but on-device stereo correctness, overdraw, sustained frame timing and comfort have **not** been measured. The temporary surface shading retains URP base colors/textures but simplifies metallic/normal/specular/custom features until original materials are restored. Existing UI authoring and full-session playtesting remain pending as above.
