# HealSphere Moment validation

Validation date: 4 November 2026, Unity 6000.4.6f1 on Windows.

## Completed scene

`Assets/HealSphere/Experience/Scenes/HealSphereMoment.unity` is the saved launch scene. It preserves the prepared meadow and its single XR Origin/EventSystem. The scene contains the real prepared tree, cat, moon and rabbit with original textured URP materials, gently responding lighting and 128 cached soft motes. The automatic flow lasts 120 active seconds (15 arrival + 10 introduction + 85 breathing + 10 ending), with 4-second inhale / 6-second exhale suggestions. Pause, resume, gentle ending and restart are implemented.

MomentCanvas, MomentPanel, MomentMessage, PauseButton and RestartButton were authored and arranged visually in the Unity Editor. Code binds and updates these existing components only. All required experience, controller and audio references are assigned. No UI construction fallback, API request, new package or new artwork is needed.

## Tests performed

- Unity compiled runtime and Editor assemblies successfully. No compilation errors were found in the tested final scene.
- Deterministic timeline checks passed the 15/25/110/120-second boundaries, pause exclusion and restart.
- Full Play Mode integration check passed all five phases and their guidance, 120 active seconds, text viewport visibility and overflow checks, desktop pointer actions after XR fallback, inhale/exhale signals, music playback state, tree emission, companion motion, pause/resume, ending, authored-button restart and gentle early ending.
- Scene check passed: assigned foundations, no missing scripts, one preserved XR Origin and EventSystem, one active camera and audio listener after desktop fallback, no runtime errors during the check.
- Normal-speed Editor run visually reached the completion message. Tree, cat, moon and rabbit rendered with textures and no pink materials. Model facing and world-space guidance alignment were corrected through iteration.
- Computer-use mouse tests passed Pause / Rest, Resume and Begin again returning to the arrival greeting. Give the Game view focus before testing input. Keyboard Escape also reached the gentle ending; all controller controls still require physical testing.
- Source/asset review confirmed private serialized Inspector references, a saved Editor-authored UI hierarchy, preserved source assets/materials, and the final launch scene entry. `git diff --check` passed.

The automated test is available at HealSphere > A Little Moment > Verify Full Experience In Play Mode. Its detailed result is written to `Library/HealSphereMomentPlaytest.txt`. It temporarily accelerates the actual scene and mutes output; it is not a headset performance or subjective audio test.

## Prepared asset budget

| Model | Triangles | Renderers |
| --- | ---: | ---: |
| Magical Tree | 50,042 | 1 |
| Orange Cat | 50,000 | 1 |
| Gentle Moon | 49,999 | 1 |
| Rabbit Companion | 50,000 | 1 |

Four prepared models total 200,041 triangles before the meadow. Mesh/texture/material content was retained. Model shadows are off in the final scene; the soft mote pool defaults to 128 and caps at 256. The tree material was corrected to retain the saved `_EMISSION` keyword with a non-black emissive GI flag, preventing Android shader stripping of its runtime glow. No bloom or per-particle lights are needed. Sustained Quest frame rate is not yet measured.

## Quest build and hardware

Existing Android settings were inspected and preserved: IL2CPP, ARM64, minimum API 32, automatic target API and existing Vulkan/OpenXR configuration. Meta Quest Support and Oculus Touch Controller Profile are enabled in the existing OpenXR settings. Android selects the existing Low quality profile and Performance URP asset (4x MSAA); Quest 2 is enabled in the Meta Quest supported-device list. No provider/package replacement was performed.

Built an Android Development APK using the installed Unity SDK/NDK/OpenJDK in an isolated build workspace, preserving the main Editor target and project settings. Vulkan shader compilation, IL2CPP ARM64 native compilation and Gradle packaging succeeded. The scene and emissive material in the build match the delivered source files byte for byte. The bundled Java trust-store issue was handled through process-local Windows trusted roots; certificate validation remained enabled.

**Final result: Succeeded.** APK: `Builds/HealSphereMoment/HealSphereMoment.apk`.

Verified with Android build tools: application label HealSphere_VR; package com.DefaultCompany.VRTemplate; version 1.0; ARM64 only; minimum API 32; target API 36; valid APK Signature Scheme v2 with one signer. The packaged manifest includes Quest 2 in supportedDevices and the Oculus VR launch category. ARM64 libil2cpp, libunity, libUnityOpenXR and libopenxr_loader are present.

**Outstanding package diagnostics:** BuildReport records two nonfatal Editor package messages (Build player and Preprocess Player): `Host type is not matching any asset type at Path Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/RenderingLayerMask/TraceRenderingLayerMask.urtshader.` They persisted after a targeted reimport and a graphics-enabled build. The APK still builds and verifies; this is not a zero-diagnostic build. Package versions were retained. These messages were not gameplay exceptions in the scene test. Existing unrelated Unity Editor service/package warnings may also appear in the Editor logs.
APK size: 104674557 bytes (99.8 MiB). SHA-256: `BEA65C4AF9C46252C6E6EF3109787BB1AD97A2AA9863883149A046616BC35E2B`.


ADB reported no connected devices. Physical Quest 2 checks remain: stereo rendering, tracked seated/standing eye height and recentering, comfort/readability through lenses, controller rays and buttons, proximity interaction, headset removal/resume, subjective music/chime loudness and sustained performance/thermal behavior. No claim of on-headset verification or 72 FPS is made.

## Changed files

- `Assets/HealSphere/Experience/Runtime/`: configuration, pure timeline, director, existing-UI presenter, environment reactions, companion motion and VR interaction.
- `Assets/HealSphere/Experience/Editor/`: preservation-first 3D assembler/reference binder, explicit 3D facing polish tool and opt-in scene verification.
- `Assets/HealSphere/Experience/Scenes/HealSphereMoment.unity`, configuration asset, scene-referenced tree emission material and associated Unity metadata.
- Extended existing `OfflinePrototype/Runtime/GuidedBreathingProvider.cs`, `AudioManager.cs` and `VRComfortSettings.cs` for configurable timing, audio transitions/pause and desktop UI input restoration.
- `ProjectSettings/EditorBuildSettings.asset`: final scene enabled first; original SampleScene retained disabled.
- Root `README.md` and this validation document: run/restart, exact hierarchy/assignments, configuration, build and playtest instructions.

Earlier OfflinePrototype/backend work and the pre-existing solution-file change were preserved. This milestone runs entirely offline; the prepared meshes and scripted text are the presentation content, and timed breathing is guidance rather than sensed respiration.


