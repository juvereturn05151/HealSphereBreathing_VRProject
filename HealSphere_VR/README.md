# HealSphere — Create an Animal

Open `Assets/HealSphere/Creation/Scenes/HealSphereCreation.unity` in Unity **6000.4.6f1** and press Play. Pick a preset or type a single animal description such as **A little sleeping fox**. Select **Create and Breathe**. The Dream Seed and meadow respond to a guided four-second inhale and six-second exhale while the model loads. After a cycle, glowing motes gather on the actual mesh and reveal the animal. **Create Another** returns to the picker. **Relax Here** hides the panel and leaves the animal in place.

For connected play, start the backend with `npm start` from the Backend folder and configure the device-local connection. The picker reuses matching saved models automatically. New paid generation requires a separate authorization that names the request, sets a task limit and reserves a verified credit ceiling. The local catalogue covers Cat, Dog, Fox, Rabbit, Deer, Owl, Elephant and Gorilla. A fallback is labeled in the scene.

[Animal creation setup, authorization, verification and Quest installation](Documentation/CreateAnAnimal.md)

The separate **HealSphereMoment** scene remains available for the automatic two-minute wellness session. The earlier private reflection and voice workflow is documented in [Animal Reflection](Documentation/AnimalReflection.md).

## Open and play

1. Open this project in **Unity 6000.4.6f1** (the existing project version). In Unity Hub install that version with Android Build Support, Android SDK & NDK Tools, and OpenJDK when building for Quest.
2. Let imports and shader compilation finish. Do not upgrade packages for this prototype. Installed versions: URP 17.4.0, OpenXR 1.16.1, XR Interaction Toolkit 3.4.1, Input System 1.19.0, XR Core Utilities 2.6.0, XR Management 4.5.4, Meta OpenXR 2.5.0.
3. For wellness only, open `Assets/HealSphere/Experience/Scenes/HealSphereMoment.unity`. It is the second enabled build scene; creation mode is first. The original SampleScene build entry is retained, disabled; the original meadow scene is preserved.
4. Press Play. Without a running XR display a stationary desktop camera takes over after approximately three seconds. Click the Game view to give keyboard controls focus. No camera movement or locomotion is required.
5. On Quest, sit comfortably or stand within your boundary and face the scene's arrival direction. Recenter using the headset's normal recenter control before starting. The world-space panel stays in the environment, rather than following your head.

| Active time | Experience |
| --- | --- |
| 0–15 s | Arrival greeting, music fades in, companion idles |
| 15–25 s | Optional hand-near-belly introduction |
| 25–110 s | Repeating 4-second inhale / 6-second exhale suggestions; tree glow, warmth, motes and companion follow the rhythm |
| 110–120 s | Gentle settling, one completion chime, ten-second music fade |
| After 120 s | Calm resting scene, completion message and Begin again button |

Pauses exclude time from the 120-second timeline. Removing/suspending the headset pauses the experience; resume intentionally. Holding a controller near the belly is optional proximity detection with dwell/hysteresis, **not validated respiration sensing**. It never controls timing or blocks progress. The later master task supersedes the earlier object-selection/three-creation sequence: these prepared models are already present in the final experience. The earlier mesh-surface formation prototype remains available separately under OfflinePrototype.

## Controls

| Action | Desktop Editor | Quest controllers |
| --- | --- | --- |
| Pause / resume | Click Pause / Rest or Resume; P | Point at authored button and press UI select; left menu button |
| Finish gently | Escape | Hold right B for two seconds |
| Restart after completion | Click Begin again; R | Point at Begin again and press UI select; right A |

Restart resets the timeline, guided clock, reactions and music without reloading or duplicating models. The existing XR ray/UI select bindings are retained. Headset controller ray targeting must still be playtested on physical Quest 2.

## Scene and Inspector assignments

All references are assigned in the saved scene. No manual UI construction or hookup is required to run it. UI was created and arranged visually in the Unity Editor; scripts only present text, control visibility, and bind existing buttons.

- `BreathingExperience`: preserved meadow, single XR Origin Hands rig, existing EventSystem, AudioRoot, lighting and terrain. Locomotion providers are disabled only in the final experience scene.
- `A Little Moment — Experience`: ExperienceDirector, GuidedBreathingProvider, EnvironmentReactionController, FloatingCompanionController, ExperiencePresentationController, VRInteractionController and VRComfortSettings.
- Prepared children: Magical Tree, Orange Cat, Gentle Moon, Rabbit Guide Anchor / Rabbit Companion, Breathing Glow Motes.
- `Stationary Desktop Camera`: inactive until desktop fallback; one active camera/listener after fallback.
- `MomentCanvas` (World Space, Tracked Device Graphic Raycaster and Graphic Raycaster): `MomentPanel` (Image + CanvasGroup), containing `MomentMessage` (TMP), `PauseButton` and initially inactive `RestartButton` (both with TMP labels).

Director: configuration = `MomentConfiguration`; breathing/environment/companion/presentation/interaction = components on the experience root; audioController = AudioRoot's AudioManager; Start Automatically enabled.

Presentation: World Canvas = MomentCanvas; Message Text = MomentMessage; Panel Group = MomentPanel; Pause Button = PauseButton; Restart Button = RestartButton. Comfort: XR Origin and XR Camera = preserved rig; Desktop Camera = Stationary Desktop Camera; World Space Canvas = MomentCanvas; Guidance Input Module = existing EventSystem's XRUIInputModule. Force Desktop Simulation is normally off.

Environment: Tree Renderers = prepared tree renderer; Key Light = meadow DirectionalLight; Breathing Motes = Breathing Glow Motes; Configuration = MomentConfiguration. Companion: Companion Visual = Rabbit Companion; Viewer = XR Main Camera; Configuration assigned; Facing Offset corrects the imported model's forward direction. Interaction: Head/Left Controller/Right Controller = preserved XR rig transforms; Director = experience root. AudioManager: existing Music and Birds AudioSources reused as ambience/chime sources; Dreamy Pasture MP3 and Creation Complete Chime WAV assigned. Original MeadowAmbience is disabled in this final scene to avoid overlapping playback.

If UI references are deliberately changed, open and save the final scene, then run **HealSphere > A Little Moment > Bind Authored Guidance UI And Set Launch Scene**. This only binds existing named objects, saves this scene and sets its build entry; it never creates or positions UI. Do not run the assembly command on an existing experience: it intentionally refuses to overwrite it.

## Adjust the experience

Select `Assets/HealSphere/Experience/MomentConfiguration.asset` to change phase durations, inhale/exhale timing, companion rise, idle float, tree emission, warmth, mote count/size and master volume. Defaults total 120 seconds. Use small amplitudes and no holds. Re-enter Play Mode after changing configuration. All Inspector-assigned runtime fields are private serialized fields.

The panel is 1.2 m wide at approximately 1.9 m forward and 1 m above the floor, aligned with the existing 120-degree arrival yaw. Adjust it visually in the Editor if the intended seated/standing eye height changes. Keep text world anchored and within comfortable downward gaze; verify readability through the headset lenses. Do not move a tracked camera to compensate.

Rendering uses the original textured URP materials, one scene-referenced emissive tree variant, no new real-time model shadows, and 128 cached soft motes (configuration cap 256). No bloom, dynamic particle lights or per-frame particle spawning is required. The four prepared models total approximately 200,000 triangles before meadow geometry. This is a prototype budget, not a measured Quest frame-rate guarantee. Profile on-device before a pitch; consider imported lower-detail models/LODs if needed.

## Build and run on Quest 2

1. In File > Build Profiles select Android and Switch Platform. Keep the existing URP/OpenXR configuration. This project uses IL2CPP, ARM64, minimum Android API 32, automatic target API, and its existing Vulkan graphics configuration. Do not replace the XR Origin or switch XR providers.
2. Confirm XR Plug-in Management > Android uses OpenXR and the existing Meta Quest feature configuration. Run the OpenXR project validation checks and address actual build errors without changing unrelated settings.
3. Confirm the Scene List has only `HealSphereMoment` enabled and first. If a custom Build Profile overrides the global Scene List, put that same scene first in its list.
4. Use a Development Build for profiling; select APK rather than App Bundle for sideloading. Build to `Builds/HealSphereMoment/HealSphereMoment.apk` (generated output, not source control).
5. Enable developer mode on your Quest through your own Meta setup, connect USB, and accept the headset's USB debugging prompt yourself. Select the connected Quest under Run Device, then Build And Run. Alternatively install the generated APK using your normal authorized sideloading workflow.
6. Launch **HealSphere_VR** from the headset's Unknown Sources/apps list. The existing Android package identity com.DefaultCompany.VRTemplate is retained. Recenter, check the panel and surroundings seated, and complete the checklist below. For the pitch build disable Development Build after profiling.

On this machine the bundled Java initially reported a PKIX certificate-chain error during SDK lookup. The successful SDK probe used the existing Windows root store through child-process JAVA_TOOL_OPTIONS values `-Djavax.net.ssl.trustStoreType=Windows-ROOT -Djavax.net.ssl.trustStore=NONE`. If rebuilding encounters that error, launch the build process with those process-local options or repair the Java trust chain through your normal environment administration. Keep certificate validation enabled.


To install the already-built APK without rebuilding, open PowerShell in this project folder, connect and authorize your Quest, then run:

```powershell
& 'E:/Unity/Editor/6000.4.6f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe' devices
& 'E:/Unity/Editor/6000.4.6f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe' install -r 'Builds/HealSphereMoment/HealSphereMoment.apk'
```

Confirm the device is listed as `device` before installation; accept USB debugging in the headset yourself. Open **HealSphere_VR** under Unknown Sources, recenter while comfortably seated, and let the automatic experience begin.

No physical headset is assumed connected. A successful APK build does not verify stereo rendering, comfort, controller rays, audio loudness or sustained performance.

## Verification and playtest checklist

Run **HealSphere > A Little Moment > Verify Full Experience In Play Mode** with this saved scene open, outside Play Mode. The opt-in check runs the real scene at accelerated time, temporarily forces desktop fallback and mutes output, invokes the authored pause/restart buttons, then exits Play Mode and restores time scale/audio volume. It checks 120 active seconds, all five phases, inhale/exhale events, music playback state, tree emission, companion motion, missing scripts, configured foundations, one active camera/listener, pause/resume and restart. Report: `Library/HealSphereMomentPlaytest.txt`. This is a behavioral test; it does not replace visual/audio/headset inspection.

- [ ] Automatic greeting and correct launch scene; no selection required.
- [ ] Tree, cat, moon and rabbit textured, correctly facing the player, no pink materials.
- [ ] Comfortable stable camera, safe seated/standing boundary; no forced locomotion.
- [ ] Guidance visible/readable without leaning; UI ray and mouse targeting work.
- [ ] 15/25/110/120-second transitions; inhale 4 s, exhale 6 s; permission to breathe naturally.
- [ ] Subtle tree glow, companion rise/descent and motes; no flashes or abrupt lighting.
- [ ] Music fade and quiet completion chime; subjective loudness checked in headset.
- [ ] Pause freezes timeline/reactions; resume works; app suspension pauses safely.
- [ ] Ending remains peaceful; Begin again and A/R restart once without duplicate objects.
- [ ] Quest stereo, controller rays, recentering, readable text and sustained frame rate verified physically.

For missing UI/references, use the saved final scene and inspect the assignments above; no fallback UI is generated. Pink/missing textures: use the existing URP pipeline assets and original imported materials, allow shader compilation to finish, and confirm the scene-referenced tree emissive material is included. Do not convert the whole project to another render pipeline. No glTF/Tripo loading is used by this experience. Unity AI subscription/entitlement warnings are editor-service messages and are unrelated to local gameplay.

The Android Development APK has been built and its ARM64 content, Quest 2 manifest and signature verified. Unity BuildReport still records two nonfatal URP Editor probe-volume asset-type diagnostics; gameplay tests passed without runtime errors. See `Documentation/HealSphereMomentValidation.md` for the exact messages, APK hash, validation results and remaining hardware checks.



