# Current animal-reflection experience

Open `Scenes/HealSphereCreation.unity` and choose **Begin gently**. The current flow collects one animal and two optional thoughts (typed or push-to-talk), offers Back/edit/skip, then reveals three same-animal Reflection/Support/Hope designs. The saved scene already has the visually authored microphone, voice mute and volume buttons and assigned private serialized references. The existing XR rig, spatial keyboard, model loader, particles, original materials, music and chimes are reused.

Use [the current animal reflection guide](../../../../Documentation/AnimalReflection.md) for setup, Inspector assignments, backend/audio routes, privacy, costs and exact Quest installation. See [current verification](../../../../Documentation/AnimalReflectionValidation.md). Offline illustrations support six animals × three distinct poses. They are simple placeholders, not personalized generated models. Global generation remains disabled; missing live models require separate owner task AND credit authorization. No live paid generation was performed for this milestone.

The new APK is `Builds/HealSphereReflection/HealSphereReflection.apk`. **Begin again** clears private answers/audio and the three created objects. **Just breathe** preserves the old automatic two-minute wellness experience. The material below is the archived freeform creation workflow and low-level configuration reference; its old buttons and default-cat sequence do not describe the new launch flow.

---

# HealSphere creation mode

**Current update:** the real sleeping fox is saved in the persistent Creation Library and can be reused automatically through this scene's existing breathing/reveal pipeline. Use [the current library guide](../../../../Documentation/CreationLibrary.md) and [live verification report](../../../../Documentation/CreationLibraryValidation.md). Keep paid generation disabled: the one authorized test already succeeded. No new UI or Inspector hookups are required.

Open this project in **Unity 6000.4.6f1** with its existing packages. Android builds require that Editor's Android Build Support, SDK/NDK and OpenJDK modules. Installed: URP 17.4.0, XRI 3.4.1, OpenXR 1.16.1, Meta OpenXR 2.5.0, Input System 1.19.0, glTFast 6.20.0. No package upgrade is needed. XRI's installed Spatial Keyboard sample is copied under `Assets/Samples/XR Interaction Toolkit/3.4.1/Spatial Keyboard` with its existing authored prefabs.

## Play without credentials or backend

1. Open `Scenes/HealSphereCreation.unity` in this folder. Wait for imports, then press Play. Without an XR display the stationary desktop camera activates after about three seconds. Click the Game view once to focus it.
2. Choose **Create a dream**. Select the request field above the guidance panel. The placed spatial keyboard opens below the panel; it does not follow the head. Use pointer/controller UI select on keys, Backspace to edit, and Enter to close. On desktop, ordinary keyboard input also works while the field is focused.
3. Enter `I want a cute sleeping fox.` and choose **Ask rabbit**. A vague request such as `something` asks one clarifying question. Empty or unsupported input stays in the request step.
4. In default offline mode the companion is deterministic and the result is explicitly **Prebuilt demo cat**. Confirm **Form demo cat**. This never contacts the backend, OpenAI or Tripo, and does not create a new fox.
5. The Dream Seed and environment follow optional 4-second inhale / 6-second exhale guidance, without holds. Three exhalation events charge the seed. A model ready early stays hidden. Three further cycles assemble 256 swirling motes toward samples from the actual imported mesh and smoothly reveal its original textured materials. Breathe naturally; there is no respiration measurement or penalty.
6. Completion plays a chime and moves the model gently to an anchor. Choose **Create another** to repeat. The last three creations are retained; older glTFast resources are released when replaced. Pause/rest freezes guidance and formation. Cancel clears the pending model and motes. Left controller menu pauses; right A starts another request after completion.
7. **Just breathe** loads `HealSphereMoment`, the preserved automatic 120-second wellness scene. To return to creation on device, relaunch; in the Editor open creation mode again. Creation duration depends on model availability and participation cycles; it is not forced to end at 120 seconds. Slow generation continues gentle guidance after 120 seconds, with a 900-second overall operation timeout.

The demo uses `Assets/StreamingAssets/healsphere-cat-demo.glb`, a reviewed export of the existing orange-cat asset. It has 50,000 triangles, one material and embedded textures (about 4.1 MiB). Only this known local demo has a 60,000-triangle allowance. Live assets are requested at 5,000 faces and rejected above 20,000 triangles. Geometry, vertices, materials, image dimensions and download size are bounded. The backend validator is authoritative for live downloads; imported models are also checked in Unity. Unsupported surface sampling falls back to the existing glowing dissolve, preserving original materials at completion.

## Backend and live mode

Use Node.js 24+ (tested with 24). Dependencies are Express 5, dotenv, Khronos gltf-validator and image-size, pinned by `Backend/package-lock.json`.

```powershell
cd Backend
npm ci --ignore-scripts
# Only if .env is absent:
Copy-Item .env.example .env
```

Edit `Backend/.env` locally. Keep provider keys there only. Never paste them into Unity, a scene, logs or chat. Provider keys in the existing `.env` were preserved; Git excludes it and backend data. Set `TRIPO_API_KEY`, `OPENAI_API_KEY`, a distinct random `BACKEND_ACCESS_TOKEN` of at least 32 characters, `OPENAI_MODEL=gpt-4.1-mini`, and `MODEL_REVIEW_MODE=openai`. `ENABLE_GENERATION=false` is the safe default. Set it to `true` only after separate authorization for a future paid request and restart the backend. `HOST=127.0.0.1`, `PORT=3000` are desktop defaults. Leave an unavailable OpenAI key empty; `ALLOW_OFFLINE_CONVERSATION=true` permits clearly labeled deterministic backend interpretation. A configured OpenAI service failure is shown as an error rather than pretending AI answered.

```powershell
npm start
Invoke-RestMethod http://127.0.0.1:3000/health
$localToken = Read-Host 'Local backend access token (not provider key)'
$headers = @{ 'X-Backend-Token' = $localToken }
Invoke-RestMethod http://127.0.0.1:3000/api/capabilities -Headers $headers
Invoke-RestMethod http://127.0.0.1:3000/api/balance -Headers $headers
```

Health/capabilities do not call a paid provider. Balance checks authenticate to Tripo without submitting generation. OpenAI conversation itself can incur OpenAI usage, even before model confirmation. Tripo uses the documented v3 text-to-model route, `P1-20260311`, low-poly settings and the server-approved dynamic prompt. Provider errors are sanitized. No client response contains the provider key or signed CDN URL.

On the device, place `healsphere-backend.json` inside `Application.persistentDataPath`. On Windows Editor this project's path is `%USERPROFILE%/AppData/LocalLow/DefaultCompany/HealSphere_VR`; confirm company/product in Player Settings if changed. The file contains a **local backend capability**, never an OpenAI or Tripo key:

```json
{"url":"http://127.0.0.1:3000","token":"YOUR_DISTINCT_LOCAL_BACKEND_ACCESS_TOKEN","live":true}
```

Without that file (or with `live:false`) creation stays fully local. Never commit this file or distribute a personal capability in public builds. Before entering live mode, verify the local endpoints and credits. Confirm **Create (paid)** only when intentionally spending one generation. Polling never submits another paid task. An ambiguous paid POST is journaled and never automatically retried. Cancellation stops local polling/display; the provider task may continue and still incur charges. Check the backend journal before requesting a replacement after a timeout. Proposals expire after 15 minutes. The private journal holds at most 32 non-reuse jobs; approved reuse uses bounded transient handles and does not exhaust that paid-job capacity.

The backend checks idea and prompt safety before generation. After download it validates the GLB and reviews the generated preview with moderation and structured vision approval when available. Missing/failed preview review leaves the job `awaiting_review`; Unity does not receive the model. An operator must inspect the asset independently and explicitly approve/reject it:

```powershell
$jobId = 'UUID_FROM_AUTHENTICATED_JOB_STATUS'
Invoke-RestMethod "http://127.0.0.1:3000/api/creations/$jobId" -Headers $headers
# Only after visual review:
Invoke-RestMethod "http://127.0.0.1:3000/api/creations/$jobId/review" -Method Post -Headers $headers -ContentType application/json -Body '{"decision":"APPROVE"}'
```

Automatic preview review is a safety screen, not a guarantee about unseen model surfaces. For curated demonstrations, operator review remains appropriate. No unreviewed asset is served. Voice input is not implemented; this milestone uses typed requests.

## Authored hierarchy and Inspector assignments

No UI hierarchy is constructed by runtime or Editor C#. The saved scene contains the visually placed and arranged UI. No additional manual UI construction is needed to play it.

| Object/component | Saved assignments |
| --- | --- |
| Preserved BreathingExperience | Meadow, XR Origin, HMD/controller input, EventSystem, lighting, AudioRoot |
| A Little Moment — Experience / CreationSessionDirector | Presenter, loader, existing GuidedBreathingProvider, DreamSeedController, ParticleModelReveal, environment, rabbit, AudioManager, MomentConfiguration, three completed anchors |
| Same root / TripoModelLoader | Creation Staging Anchor; maximum dimensions 0.65 m; no provider key or committed access token |
| Same root / CreationSessionPresenter | MomentMessage, external request TMP_InputField, request Canvas root, placed XRKeyboard, PauseButton as primary, RestartButton as secondary, WellnessButton; input Canvases plus XR/desktop cameras |
| MomentCanvas / MomentPanel | Existing world-space message and three separated action buttons; labels and availability reflect session state |
| Input Field World Keyboard | Existing sample Canvas/input/XRKeyboardDisplay; Use Scene Keyboard enabled and reference assigned to the placed XRI Spatial Keyboard |
| XRI Spatial Keyboard | Existing keys, preview field, raycast components; KeyboardOptimizer disabled to avoid runtime UI regrouping; no GlobalNonNativeKeyboard instantiation manager |
| Creation Surface Motes | Existing ParticleSystem, capped at 256; Soft Particles material; Gentle Reveal dissolve on ParticleModelReveal |
| Creation Staging Anchor / Created Object Anchor 1–3 | Stable world positions; imported model fits before activation, original materials restored before settling |

The old wellness director/presenter/interaction controller are disabled only in the creation scene. Its XR rig, audio, environment and companion systems are reused. `HealSphere > Creation > Bind Authored Creation UI And Set Launch Scene` rebinds existing named components and saves; it never creates UI. Do not rerun the assembly menu over the existing scene: it intentionally refuses overwrite.

Adjust charge/reveal cycles, settle time, model bounds, generation timeout and demo load delay on the creation components. Adjust music volume using MomentConfiguration. Scene composition and UI layout belong in the Unity Editor. Keep models and panels stationary relative to the world; do not move the tracked camera or add forced locomotion.

### Position the animal

In `HealSphereCreation`, select **Creation Staging Anchor** and move it to the intended standing height. Its pivot marks the bottom center of the fitted animal. Move or rotate this anchor in Edit Mode and save the scene. The **Arrange Existing Creation Anchors** menu restores its preset position.

On **TripoModelLoader**, keep **Creation Staging Anchor** assigned to that object. Leave **Animal Placement Root** empty, or assign a direct child of the staging anchor if you want a separate authored placement socket. Keep the anchor and socket at unit scale. **Target Animal Height** scales the geometry uniformly, **Model Rotation Offset** corrects its imported axes, and **Model Position Offset** nudges the fitted geometry in local meters. Start with all offsets at zero and try a Y rotation of 90°, −90°, or 180° for an individual model that faces sideways or backward.

Both camera options default to off so the saved anchor pose controls the initial placement. Enable **Automatically Face Camera** to replace the initial anchor yaw with an upright turn toward the viewer. Enable **Use Camera-Relative Staging** to stage the animal 1.75 m ahead and 0.65 m below the eye position sampled when creation begins. Enable both for a centered, player-facing reveal. Head movement after that sample leaves the placement stationary. The brief awakening approach is applied in local space, so the animal remains attached to the anchor.

To test the active animal flow, use the offline animal in Play Mode, then repeat with an existing saved backend GLB. Check the reveal and awakening after moving and rotating the anchor with both camera options off. Repeat with both options on and move your head during the reveal. For automated coverage, the mock-library test checks that saved and offline GLBs remain parented to the anchor and respond to its translation and rotation.

## Tests and troubleshooting

Run `npm test` inside Backend. Provider tests use injected mocks and fake keys; they never load `.env` or spend credits. In Unity, save the creation scene and run **HealSphere > Creation > Verify Offline Creation And Wellness In Play Mode**. It temporarily forces desktop simulation, mutes audio and accelerates the guide; it uses existing buttons and the real demo GLB, checks surface sampling/material restoration/cancel/repeat/scene transition, then exits Play Mode. Results are written to `Library/HealSphereCreationPlaytest.txt`. The preserved wellness scene has its own full 120-second verification menu.

For an isolated provider integration check, run `node Backend/tools/mock-creation-server.mjs`. It listens only on localhost:3317 and intercepts all OpenAI/Tripo fetches. It uses an explicitly incomplete, in-memory 5,000-triangle fixture derived from the demo cat; it is not a generated fox and does not modify the shipped GLB. With no device-local backend JSON present, request the Editor verification with `mock` in `Library/HealSphereCreationPlaytest.request`. The test temporarily supplies fake credentials and never persists them in the scene. Stop that mock server after testing. This verifies transport/state integration, not real provider output quality.

Authentication errors: verify the **distinct local token** first, then provider credentials in `.env`. `OPENAI_AUTH_FAILED` and Tripo auth errors require correcting server configuration and restarting it. Never print raw provider responses or keys. `GENERATION_DISABLED` means opt-in is still false. Credit failures do not submit a replacement. `awaiting_review` requires review, not another paid generation.

GLB problems: verify StreamingAssets file exists for demo, backend job is ready for live, byte size/budgets pass, and glTFast is installed. Compressed/animated/skinned external assets outside the accepted static GLB subset may be rejected; use a small, textured static export. URP/pink materials: confirm URP is active, glTFast's URP shaders are included, and Soft Particles/Gentle Reveal materials remain referenced in the scene. Preserve embedded textures and imported original materials. Do not “fix” by assigning one flat material to every model. If regenerating the local demo using the export menu, then run `node Backend/tools/prepare-demo.mjs` to correct export accessor bounds and strictly validate it; production downloads are never repaired or validation-relaxed.

`Resources/CreationImportShaders` contains 30 small material references for the supported glTFast PBR/unlit variants (texture transforms, emission, occlusion and opaque/masked/transparent surfaces). They keep runtime-only shaders reachable by the Android build; they are not substituted onto imported models. The creation build helper prepares missing references without modifying global Graphics settings or overwriting existing materials. Regenerate missing references with **HealSphere > Creation > Prepare Runtime Import Shader References**. The dissolve presenter also recognizes glTFast's `baseColorTexture` and `baseColorFactor`, so imported coloring is retained during formation.

## Quest 2 build and installation

Keep the existing Android configuration: IL2CPP, ARM64, minimum API 32, automatic target SDK, Vulkan, OpenXR with existing Quest features/controller profiles and Performance URP renderer. Creation first and HealSphereMoment second must both be enabled in Build Profiles. Build Android APK through Unity Build Profiles; the optional batch method is `HealSphere.Creation.Editor.CreationAndroidBuild.Build`. Output defaults to `Builds/HealSphereCreation/HealSphereCreation.apk`.

Enable developer mode and authorize USB debugging on your own headset. Using the SDK bundled with this Unity installation:

```powershell
$adb = 'E:/Unity/Editor/6000.4.6f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
& $adb devices
& $adb install -r 'Builds/HealSphereCreation/HealSphereCreation.apk'
```

Start the app from Unknown Sources. The existing Android package ID is `com.DefaultCompany.VRTemplate`; this install replaces previous builds with that same ID. Default offline creation requires no computer or network.

For live LAN development, set backend `HOST=0.0.0.0`, use a private trusted network, and set the JSON URL to the computer's LAN address, e.g. `http://192.168.1.50:3000` (not the Quest's localhost). Allow Node through the appropriate private-network firewall rule yourself. Device JSON path for this existing package is `/sdcard/Android/data/com.DefaultCompany.VRTemplate/files/healsphere-backend.json`. Copy your local JSON using adb push. Remove it to return to fully offline mode. HTTP on LAN may require allowing insecure HTTP in Android Player Settings for that development build; prefer HTTPS for nonlocal deployments. Do not expose this development token endpoint to the public Internet. For a USB-only development session, `adb reverse tcp:3000 tcp:3000` can map headset localhost to the computer backend.

On the real Quest verify controller key selection, Backspace/Enter, readable text and comfortable keyboard angle, both eyes and HMD tracking, original textures, formation smoothness, music/chime balance, pause/suspend/resume, repeat/cancel, retained-model memory and sustained frame time. A successful APK or Editor test does not establish physical-headset performance or comfort. See the validation report in `Documentation` for actually performed tests and remaining limits.
