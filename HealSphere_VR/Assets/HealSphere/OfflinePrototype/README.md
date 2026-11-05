# Breathe Into Being — Phase 1 offline prototype

This milestone uses local cat, tree and moon placeholder prefabs, scripted companion dialogue, and guided breathing. It does not contact Tripo, an LLM, a microphone, or any external service. The earlier `Tripo` and `Backend` folders are preserved and unused by this scene.

**Handoff status:** runtime/controller code and non-UI scene authoring are implemented. The world-space UI must be authored visually and assigned as described below. The session deliberately refuses to start if UI is missing. Until that hookup and a playtest are complete, this is not a fully playable pitch build. Do not present the controller-motion option as breathing detection or a medical feature.

## Installation and existing packages

1. Open this existing project through Unity Hub with **Unity 6000.4.6f1**. Do not create a new project or upgrade it.
2. Through Hub's Add Modules for this Editor, install **Android Build Support**, **Android SDK & NDK Tools**, and **OpenJDK** if not already installed. They are required for a Quest APK; they are not assumed present.
3. Let Package Manager finish resolving the project's committed manifest/lock. Phase 1 adds no new package dependency. Inspected versions: URP **17.4.0**, Input System **1.19.0**, XR Core Utilities **2.6.0**, XR Interaction Toolkit **3.4.1**, XR Plug-in Management **4.5.4**, OpenXR **1.16.1**, Meta OpenXR **2.5.0**. Existing glTFast **6.20.0** is unused by this milestone. TMPro and uGUI are supplied by the installed Unity packages.
4. If Unity prompts to import TMP Essential Resources, do so through **Window > TextMeshPro > Import TMP Essential Resources**. Existing project TMP resources may already suffice.
5. Open the new `Authored/BreathingExperience.unity` beneath this folder when present. To create another isolated copy, use **HealSphere > Offline Prototype > Create New 3D Scene (UI Pending)**. The tool copies the existing `Assets/Scenes/BasicScene.unity` XR rig into a uniquely named output folder and authors only 3D gameplay content. It does not create or arrange any UI and never overwrites an existing scene.

## Scene hierarchy

The authored scene contains the following; the XR Origin keeps its original template prefab/input references:

```text
XR Origin (preserved source prefab; locomotion providers disabled in this scene)
XR Interaction Manager
EventSystem (preserved source input module)
FloatingIslandEnvironment
  Floating Island
  Distant Soft Cloud 0…5
Gentle Key Light
Floating Companion
  Companion Body / Ears / Eyes
  GentleFloat
  CompanionDialogueController
DreamSeedAnchor                  (0, 1.05, 1.65)
  Dream Seed Visual
Formation Particles
CreationAnchor_01                (-0.75, 0.70, 1.85)
CreationAnchor_02                (0, 0.65, 2.25)
CreationAnchor_03                (0.75, 0.70, 1.85)
Offline Session Controllers
  GuidedBreathingProvider
  ControllerMotionBreathingProvider
  BreathingInputController
  DreamSeedController
  ParticleModelReveal
  CreationManager
  BreathingSessionManager
  VRComfortSettings
Audio Manager
  AudioManager / two AudioSources
Stationary Desktop Camera         (0, 1.15, 0; initially inactive)
ExperienceCanvas                  MANUAL — hierarchy below
```

All gameplay anchors should have scale `(1,1,1)`. The scene contains no Tripo loader, networking component, forced camera motion, active locomotion provider, real-time shadows, or animated flashing/noise. The floating island and companion are basic pastel primitives; the sky is a soft solid pastel camera background. The moon placeholder is a round moon, rather than a detailed crescent. Placeholder faces/materials are deliberately simple and may be replaced with reviewed lightweight prefabs.

## REQUIRED: author world-space UI visually in Unity

`AGENTS.md` forbids code-created UI. Do all creation and layout in the Editor hierarchy/Inspector. The optional binding menu below only assigns existing references; it creates no components.

1. Through **GameObject > UI > Canvas**, create `ExperienceCanvas`. Set **Render Mode = World Space**, scale `(0.001, 0.001, 0.001)`, RectTransform size approximately **1200 × 1100**, position approximately **(0, 1.65, 2.8)**, rotation `(0,0,0)`. Use this as a starting point; verify legibility and occlusion from the actual seated HMD. Keep it world-fixed and away from the face. Do not parent it to the camera.
2. In its Inspector, add **Tracked Device Graphic Raycaster**. Keep/configure the Canvas Graphic Raycaster for desktop pointer use as appropriate. Assign the XR Camera to the Canvas Event Camera. Ensure the preserved EventSystem has an enabled **XR UI Input Module**, mouse input and XR input enabled; do not add a second EventSystem. The rig's XR ray interactors must have UI interaction enabled. Fix these through the Editor if validation identifies a missing component.
3. Under the Canvas, manually create a UI Panel named `ExperiencePanel`. Stretch it inside the Canvas with generous padding. Use a calm, mostly opaque cream/lavender background that provides adequate contrast. Add **Vertical Layout Group** in the Inspector; use spacing around 10 px, child width controlled/expanded, and no forced child height expansion. Avoid transparent layers over the model's focal area. Adjust panel position or width if the model obscures controls.
4. Create these four **UI > Text - TextMeshPro** children. Use a dark cocoa text color, approximately 40–44 px font for dialogue/prompts, 32–36 px for the mode disclosure, wrapping enabled. Give each a **Layout Element** with a starting Preferred Height as below. Increase panel size or reduce copy/layout density if necessary; never shrink below readable headset text merely to fit it.

   | Object name | Preferred height | Purpose |
   | --- | ---: | --- |
   | `DialogueText` | 280 | Greeting, choices, creation narration, reflection, ending |
   | `BreathingText` | 95 | Gentle inhale/exhale/rest/pause suggestion |
   | `ProgressText` | 65 | Cycles and number of completed creations |
   | `ModeText` | 180 | Guided suggestion or full motion-proxy disclosure |

5. Manually create these **UI > Button - TextMeshPro** children under the same panel. Name each button GameObject exactly as shown, set its child TMP label, and add a Layout Element with Preferred Height approximately **65**. The presenter shows only the controls relevant to the current state, so authoring all ten does not mean they will all be displayed together. Set transition to a restrained color tint; no flashing animation. The panel may require scrolling or a wider two-column button arrangement authored in the Editor for your preferred text sizes.

   | Button GameObject | Label |
   | --- | --- |
   | `ContinueButton` | Continue |
   | `CatButton` | Make a cat |
   | `TreeButton` | Make a tree |
   | `MoonButton` | Make a moon |
   | `PauseButton` | Pause / Resume |
   | `GuidedButton` | Use gentle guided rhythm |
   | `MotionProxyButton` | Try unvalidated motion proxy |
   | `CalibrateButton` | Recalibrate motion proxy |
   | `RestartButton` | Begin another quiet moment |
   | `EndButton` | Finish for now |

6. Select **HealSphere > Offline Prototype > Bind Existing Visually Authored UI** while this new scene is active. This checks exact object/component names and assigns references to the existing presenter and comfort controller. If anything is missing, it stops with the precise required object; it does not fabricate a fallback. Inspect the assignments and **save the new scene yourself**. Alternatively drag each field manually as listed next.
7. Keep each button's Inspector OnClick list empty when using this presenter: it binds/removes listeners at runtime, so extra manual duplicate listeners would invoke actions twice. Optional: manually create a volume Slider and bind `AudioManager.SetMasterVolume(float)` to its dynamic float event. Optional: drag the Canvas/Panel from the hierarchy into a new prefab folder through the Editor to make it reusable.

The UI authoring and its headset readability have **not** been completed by the coding agent because the Unity project window was unavailable to the automation tool. Do not skip this step or interpret a Console log as a substitute for player-facing UI.

## Inspector assignments and controls

The scene builder assigns gameplay fields. Check these after opening the scene:

| Component | Required assignments / settings |
| --- | --- |
| BreathingSessionManager | Breathing Input, Creations, Dream Seed, Companion presenter, Audio Manager; Cycles Per Creation = **3**, Begin On Start enabled, Editor Keyboard enabled |
| BreathingInputController | Guided provider and Controller Motion provider |
| GuidedBreathingProvider | Inhale Seconds = **4**, Exhale Seconds = **5**; no holds |
| ControllerMotionBreathingProvider | Head = XR Origin's tracked Main Camera; Belly Controller = tracked **Right Controller** transform (verify manually); Controller Node = RightHand; calibration 2 s; smoothing 0.25 s; half-travel 0.005 m; minimum phase 0.8 s |
| CreationManager | Local Cat/Tree/Moon placeholder prefab assets; Staging Anchor = DreamSeedAnchor; three Creation Anchors in order; Reveal = ParticleModelReveal; maximum model size 0.45 m per axis; settle 1.5 s |
| ParticleModelReveal | Formation Particles; Gentle Reveal material; 256 particles, hard cap **512**; transition 1.8 s |
| DreamSeedController | Dream Seed Visual Transform; inhale size 0.17 m, exhale size 0.12 m |
| CompanionDialogueController | Panel Root = ExperiencePanel; four TMP fields; Continue/Cat/Tree/Moon/Pause/Guided/Motion Proxy/Calibrate/Restart/End buttons by matching names above |
| VRComfortSettings | Existing XR Origin and XR Camera; Stationary Desktop Camera; **World Space Canvas = ExperienceCanvas**; Force Desktop Simulation optional for Editor |
| AudioManager | Two existing AudioSources, supplied local Dreamy Pasture ambience and creation-complete chime; master volume 0.35; fade 2 s. Missing audio is silent, not a session error |

There is no microphone permission or API setup. `IBreathingProvider` emits `BreathSignal` and `ExhaleCompleted`; the input controller forwards them, the session increments progress, and creation/reveal communicate through completion events. Presenters only update existing components.

### Desktop simulation

Open Game view and enter Play Mode after UI hookup. Without an active XR display, `VRComfortSettings` waits up to three seconds, then selects the stationary desktop camera and deactivates this scene's XR Origin. Set **Force Desktop Simulation** to bypass the wait when desired. It never changes a tracked camera's pose, render pipeline, or global XR settings. To switch back to XR, exit Play Mode and start again with the headset connected.

- Mouse: click the visible world-space buttons in Game view. Confirm XR UI Input Module mouse input and the Canvas event camera are correct.
- **Enter:** continue greeting or finish reflection.
- **1 / 2 / 3:** choose cat / tree / moon.
- **P:** pause or resume breathing/settling without losing progress.
- **G:** guided mode; **M:** try motion proxy; **C:** recalibrate proxy.
- **Space:** explicitly simulate one completed exhale for rapid Editor testing. This debug shortcut is disabled in player builds.
- **Esc:** graceful ending; **R:** restart from the ending.

Guided cycles are suggested timings, not measurements of the player's breath. They continue on the clock even if the player breathes differently. Pausing freezes the breathing clock and reveal; resuming preserves progress. A stalled frame does not award catch-up cycles. Completed objects remain until an explicit new session/restart; ending early removes only the unfinished model. Choices already made become unavailable, so all three preset objects are created once in any order.

### Optional controller-near-belly mode

Use the explicit **Try unvalidated motion proxy** button during breathing. Rest the assigned controller comfortably near the belly; breathe naturally during the two-second calibration. The proxy uses controller position relative to HMD yaw, a near-belly envelope, exponential smoothing, two-sided displacement hysteresis, and a minimum phase duration. It requires actual tracked HMD/controller devices, so it intentionally falls back to guided mode in ordinary desktop simulation.

The displayed label says **UNVALIDATED MOTION PROXY — not respiration detection**. Hand/head motion, clothing/posture, or drift can produce false positives. Do not ask players to squeeze, strain, hold their breath or exaggerate movement to make it work. Use **Recalibrate** after a posture change, or return to **Use gentle guided rhythm**. Tracking loss/out-of-envelope pose returns to guided mode without removing earned progress. Only choose motion mode again when comfortable; it does not automatically switch back and forth.

## Particle reveal and limitations

The sampler gathers area-weighted triangle samples once per creation, with child transforms applied. It supports readable MeshFilters and a single baked pose for practical SkinnedMeshRenderers (up to 60,000 vertices); it does not resample animated skinning per frame. It caps mesh sampling at 20,000 triangles and 512 samples. Unreadable, non-triangle or excessive meshes skip sampling and use the glowing dissolve instead.

The ParticleSystem holds one fixed particle array. Motes orbit slowly around the fitted model's center, follow spiral paths into cached surface samples, and settle in staggered groups from the base upward. Unsettled motes continue drifting between breathing cycles. Critically damped progress eases each new contribution without overshooting; pausing freezes both motion and progress. Near completion, motes shrink/fade and the glowing surface frontier settles before the original model is restored. There are no per-frame emission bursts, trails, collision particles, extra lights or sub-emitters.

Glow comes from a premultiplied core/halo billboard shader in one pass, with no bloom, postprocessing or real-time particle lights. Its radial falloff uses simple polynomial arithmetic. The temporary surface shader uses a stable world-space frontier across child meshes, reads each material slot's original URP base-color texture/tint/UV tiling, and fades its rim emission out before completion. Original material arrays and both renderer-level and per-slot property blocks are restored at completion or cancellation. No prefab asset or shared material is edited at runtime.

The supplied placeholders use lightweight local primitive meshes and flat pastel URP materials. The temporary surface shading is simplified diffuse shading; normal maps, metallic/specular features, transparency and arbitrary custom material properties become fully available when original materials are restored. Skinned animated production assets, complex custom shaders, particle-containing prefabs and expensive high-poly models need separate review. Surface samples are cached once in ParticleSystem local space: keep the model and particle anchor stationary relative to each other during formation. Use static reviewed gameplay prefabs for this pitch. Real Quest performance and stereo shader rendering remain unverified.

### Formation tuning and preview (no new UI required)

Existing scene references remain valid. On `ParticleModelReveal`, start with **Particle Count 256** (hard cap 512), **Particle Size 0.012**, **Transition Seconds 1.8**, **Orbit Radius 0.24**, **Swirl Radians Per Second 0.55**, and **Drift Height 0.025**. The particle size is the luminous core scale; the billboard includes a halo about 2.5 times wider. Warm/Cool Mote colors provide a stable pastel palette. Reduce particle count/size first if fill rate is high on Quest. Do not add bloom or trail systems merely to increase glow.

To inspect the effect while session UI is still pending, enter Play Mode and use `CreationManager`'s **Formation Preview > Begin Cat Without Session** context menu. This is available only while the session is NotReady, and instantiates a single local cat. On `ParticleModelReveal`, choose **Progress To One Third**, **Progress To Two Thirds**, then **Complete** to inspect the motion between steps. **Clear Preview Objects** on CreationManager resets the standalone preview. These controls are Editor-only and do not bypass session readiness for the actual experience.

**HealSphere > Offline Prototype > Render Formation Preview Frames** renders four actual 3D effect stages and a montage into `Library/FormationPreview` without creating UI or saving scene changes. It first runs the deterministic checks. This desktop render is not a Quest performance or stereo test.

## Quest 2 Android build

1. Finish and save the new scene/UI. In **File > Build Profiles**, create/use an **Android** profile and explicitly add this new BreathingExperience scene as the launch scene. The existing project's build scene list is intentionally unchanged (it currently launches SampleScene).
2. In **Project Settings > XR Plug-in Management**, inspect the **Android** tab: preserve the existing OpenXR provider and Meta Quest feature configuration. Enable the Quest Touch Controller interaction profile and resolve relevant OpenXR project validation errors through the Editor. Do not replace the project's OpenXR/XRI packages or switch to an unrelated SDK.
3. Inspect Player Settings. The project already specifies **IL2CPP**, **ARM64**, Input System, minimum Android API **32**, automatic target API, and Vulkan for Android. Retain these as the starting point. Use the SDK/NDK/JDK bundled with this Editor. No internet, microphone, camera, or health-sensor permission is needed by this milestone. Platform/store policy requirements are separate from this local prototype check.
4. Use the existing URP asset. Start with a modest render scale and 2× MSAA only if headset profiling supports it; avoid extra postprocessing, bloom and real-time shadows. Do not globally change graphics settings merely to make the Editor preview look brighter.
5. Make a **Development Build**, connect an authorized Quest 2 through USB or your normal deployment workflow, then Build And Run. The person using the device must complete its developer-mode/device authorization steps; the agent does not bypass them.
6. Verify both-eye rendering, controller rays/UI clicks, seated height/comfort, text legibility, audio, suspend/resume, shader compatibility, memory and sustained frame time. Profile on-device before making any mobile-VR performance claim. If the custom reveal shader is pink or missing, inspect Android shader compilation/variant inclusion; it is referenced by a scene material and should be included, but Editor success alone does not prove device support.

## Playtest checklist

- [ ] UI is visually authored and all references assigned; there is one EventSystem and one active camera/AudioListener.
- [ ] Greeting is shown on start; no API/network request occurs.
- [ ] Mouse/keyboard desktop fallback works with no HMD; all three choices can be made in any order.
- [ ] Default guide uses 4-second inhale and 5-second exhale, with no holds or failure messages.
- [ ] Three completed guided cycles reveal one model; Space works only as an Editor debug shortcut.
- [ ] Pause/resume preserves clock, visual progress and settle position; no catch-up cycle after a long stall.
- [ ] Switching guided/proxy/recalibration preserves progress and does not award an extra cycle.
- [ ] Tracking loss falls back to guided mode, and the unvalidated-proxy disclosure is readable before attempting it.
- [ ] Particles visibly gather toward child-mesh surfaces; unreadable meshes use a gentle dissolve rather than throw.
- [ ] Completed objects retain original materials, sit at distinct world anchors and are not duplicated on repeated clicks.
- [ ] A soft chime plays once per completed creation; volume can be reduced/muted.
- [ ] After all three creations, reflection is supportive, tentative and non-diagnostic; Continue ends gracefully.
- [ ] Ending early retains finished creations, discards the unfinished one, and fades ambience; Restart clears the old set.
- [ ] Quest: no forced locomotion/camera movement, readable world-fixed UI, both eyes correct, no flashing, comfortable audio.
- [ ] Quest: leaving/resuming the app pauses rather than silently progressing; sustained performance is acceptable.

Run **HealSphere > Offline Prototype > Run Deterministic Checks** for clock timing, stalled-frame handling, motion hysteresis, transformed sampling, particle cap and unreadable-mesh fallback checks. These create no UI and use no APIs. See `VALIDATION.md` for what was actually verified and what remains pending.
