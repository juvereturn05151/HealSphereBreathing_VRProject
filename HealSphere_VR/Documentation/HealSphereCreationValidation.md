# Creation integration validation — 2026-11-04

## Delivered implementation

`HealSphereCreation` is the first enabled launch scene. It reuses the existing XR rig, meadow, rabbit, prepared decorations, breathing/environment controllers and audio. The original `HealSphereMoment` remains the second enabled scene. Its scene SHA-256 is unchanged: `AB4895930CC05349185D118CAA94ED59F8502DEC4D3141391A3DFC1604E1A29A`.

The request input and XRI spatial keyboard prefabs were placed through the Unity Editor. The existing guidance panel was visually arranged with three separated action buttons, raised to avoid covering keyboard rows, and connected to the presenter. No UI hierarchy is created by gameplay or Editor C#. KeyboardOptimizer is disabled on the placed keyboard; no global keyboard-instantiation manager is used. All authored creation references are assigned. Inspector fields in the new runtime components use private serialized fields.

The session handles welcome, typed request, clarification, explicit confirmation, generation/polling, seed charging, hidden early-ready model, mesh-surface formation, original-material restoration, settling, cancellation, pause/rest and repeated creation. The last three completed models are retained with glTFast resource ownership. The guide uses 4-second inhale / 6-second exhale suggestions and is not a respiration sensor. Slow generation continues guidance rather than cutting off at 120 seconds. Just breathe loads the original wellness experience.

The default Prebuilt demo cat is an actual reviewed local GLB imported at runtime, not an object spawned in place of the import/reveal pipeline. It has 50,000 triangles, one material and embedded textures. Its demo allowance is explicit; live assets remain limited to 20,000 triangles and are requested at 5,000 faces. The demo never contacts a provider or backend and does not claim to generate the typed fox.

## Tests performed

| Check | Result |
| --- | --- |
| Unity 6000.4.6f1 compilation | Passed; no new C# compilation errors |
| Backend `npm test` | All 18 tests passed; fake credentials and injected provider mocks only |
| Backend review handling | Approve serves asset; reject blocks it; review failure leaves asset gated |
| Backend submission recovery | Paid POST deduplication/journal, ambiguous-response handling, transient polling recovery, cancellation during submission, failed/invalid assets and timeout tests passed |
| Backend demo restart | Pending local-demo journal resumes without a provider task ID or network call |
| Offline Unity integration | Passed: empty/vague requests, clarification, authored-button confirmation, actual GLB import, three-cycle hold, surface samples, pause/resume, original materials/textures, cancellation, two creations and particle cleanup |
| Texture during reveal | Confirmed populated glTFast texture is supplied to the dissolve shader, then original materials are restored; test excludes the subsequent settling phase where temporary property blocks are intentionally removed |
| Mock provider through Unity | Passed conversation → proposal → explicit submission → polling → visual approval → download → GLB import → formation → completion; two ready journal entries, no duplicate paid task calls |
| Mock geometry caveat | An explicitly incomplete in-memory 5,000-triangle fixture derived from the cat exercised the production live budget. It was not a real generated fox or shipped replacement asset |
| Wellness alternative | Passed actual scene transition, full 120 active seconds, completion and authored-button restart |
| Desktop input | Direct mouse selection opened request field and keyboard; spatial keys entered `fox`; desktop typing and Backspace edited a full request; Done closed the keyboard retaining text; Ask rabbit and explicit demo confirmation worked |
| Final normal-speed run | Completed actual textured-cat formation above the tree, settling at the separated right anchor and Create another; request cleared while completed cat stayed visible |
| Visual composition | Observed imported textured cat forming above tree; UI raised clear of keyboard; completed anchors separated from rabbit and prepared scenery |
| Android build | Succeeded in an isolated copy, with the same saved creation scene and preserved wellness scene |
| Shader retention | glTFast PBR/unlit shaders compiled for Vulkan; 30 Resources material references retain supported runtime variants without changing global Graphics settings |
| APK signature | `apksigner verify --verbose`: verifies, APK Signature Scheme v2, one signer |
| APK architecture/platform | `aapt dump badging`: arm64-v8a, minimum API 32, target API 36, existing package `com.DefaultCompany.VRTemplate` |
| Secrets | Existing Backend/.env untouched, ignored by Git; no provider keys added to Unity or serialized scenes; fake test capability was runtime-only |
| Physical headset | Not performed: ADB device list was empty |
| Real OpenAI/Tripo generation | Not performed; no paid provider calls or real-key generation attempted |

Machine-readable Editor reports are in `HealSphereCreationOfflineValidation.txt` and `HealSphereCreationMockValidation.txt` alongside this document. The test harness accelerates guide time and mutes output; a separate direct normal-speed Editor run exercised the polished input/formation path. AudioSource playback and chime triggers were checked in code; headset audio balance still requires listening on the device.

## Android artifact

- `Builds/HealSphereCreation/HealSphereCreation.apk`
- 120,506,991 bytes (about 115 MiB).
- SHA-256: `EE2DCE3642D54D177AC281A5E8DAA11043CDB35A2E613DB11CC1692F546BC668`.
- Development APK; installing replaces the previous app using the same package ID.
- Source and isolated build scene hashes matched: `B980461431AA409DD0FF8A52EC3ACF61D343B4809360DC8E96C30570A07A1D20`.

The successful BuildReport still counts two existing URP Editor asset diagnostics for `Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/RenderingLayerMask/TraceRenderingLayerMask.urtshader` (“Host type is not matching any asset type”). These are package/build diagnostics, not C# compilation failures or runtime exceptions observed in the tested flow. No package upgrade or existing Quest XR configuration change was made to suppress them. Unity's installed AI Assistant also emitted unrelated entitlement warnings in the Editor.

## Main files added or extended

- `Assets/HealSphere/Creation/Runtime/CreationSessionDirector.cs` and `CreationSessionPresenter.cs`.
- `Assets/HealSphere/Creation/Scenes/HealSphereCreation.unity`, Dream Seed material and Resources shader-reference materials.
- Creation Editor assembly/export, reference-binding, shader-retention, build and opt-in integration-verification tools.
- `Assets/HealSphere/Tripo/Runtime/TripoCreationImport.cs`, `RuntimeModelOwnership.cs`, and sanitized-error support in `TripoModelLoader.cs`.
- Existing `OfflinePrototype/Runtime/MeshSurfaceSampler.cs` samples bounded subsets of larger reviewed meshes; `ParticleModelReveal.cs` recognizes glTFast texture/color properties.
- `Assets/StreamingAssets/healsphere-cat-demo.glb` and the existing installed XRI Spatial Keyboard sample assets.
- `Backend/src/conversation.js`, `creations.js`, extended app/server/GLB validator, creation tests, demo preparation and isolated mock-provider tools.
- `Backend/.env.example`, Backend README, root README, creation README and launch-scene build entries.

The pre-existing solution-file modification and earlier Experience/OfflinePrototype work were preserved.

## Remaining manual checks

1. Authorize USB debugging on a real Quest 2, install the APK with `adb install -r`, launch HealSphere_VR from Unknown Sources, and recenter while seated.
2. Complete the offline cat path on the headset. Check both-eye rendering, controller key/button selection, input editing, readable text, keyboard angle, materials, music/chime levels, suspend/resume, repeated-session memory and sustained frame time.
3. For real AI generation, configure backend keys privately, verify health/capabilities and free Tripo balance, configure the device-local backend capability/URL, explicitly enable live generation, and intentionally confirm one paid request. Review the generated asset before it is served. These steps may incur provider costs and were not performed automatically.
4. LAN HTTP needs a development build that allows insecure HTTP; this APK preserves the existing project's setting. Use a trusted-network development build configured accordingly, or provide HTTPS. See the creation README for exact configuration, paths and commands.

Voice input, arbitrary animated/compressed GLBs, production multi-user authentication and real-headset performance certification are outside this milestone. Live provider output quality and current account/credit access remain unverified.
