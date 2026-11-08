# Personalized animal reflection

The current launch scene is `Assets/HealSphere/Creation/Scenes/HealSphereCreation.unity`. It reuses the meadow, rabbit, XR Origin, stationary desktop fallback, spatial keyboard, glTFast loader, mesh-surface formation, music and chimes. Packages remain unchanged: Unity 6000.4.6f1, URP 17.4.0, XRI 3.4.1, OpenXR 1.16.1, Meta OpenXR 2.5.0, Input System 1.19.0 and glTFast 6.20.0.

## Play and restart

1. Open the scene, wait for import, then press Play. Without an XR display the desktop camera takes over after approximately three seconds. Click the Game view and use the existing world-space buttons and input field.
2. Choose **Begin gently**. Optional introduction: two configurable 4-second inhales / 6-second exhales, without holds. **Skip breathing** proceeds immediately. These are suggestions, not measured respiration.
3. Enter one animal, then two optional thoughts. **Speak answer** explicitly starts recording; **Stop recording** sends the recording for transcription. Correct the recognized text before confirming. Empty thoughts are skipped. **Back** restores earlier answers for editing.
4. Review the answers, then **Imagine friends**. Connected OpenAI interpretation returns tentative themes and three same-animal concepts: Reflection, Support and Hope. No diagnosis or promise is requested. Skipped thoughts use a neutral direction.
5. Suitable saved library models are reused. Missing models pause at a choice: wait for separately authorized operator generation, edit answers, or **Use illustrations**. Unity only polls this plan; it never submits a paid generation.
6. Each model uses the original seed charging, mesh-surface swirling motes, gradual material reveal and anchored settling/chime. Three charging cycles and three formation cycles are inherited from creation settings. Rest or pause whenever comfortable.
7. A closing reflection appears with subtitles. **Begin again** removes the three session models, clears input/thoughts and private audio, and restarts. **Just breathe** opens the preserved 120-second wellness scene. The reflection flow has variable duration; it is not a fixed two-minute exercise.

Offline illustrations support fox, cat, rabbit, dog, elephant and gorilla, with three genuinely different poses per animal. They are original simple pastel GLB placeholders, not personalized AI generations. Live structured planning also supports bird, turtle, deer, panda, bear, otter, horse, dolphin and butterfly. Arbitrary species and freeform animal descriptions are intentionally outside this first bounded catalogue. Offline illustrations remain available after network failure; microphone and AI voice require a backend.

## Backend and OpenAI setup

Use Node 24+. Existing dependencies are Express 5, dotenv, gltf-validator and image-size. Built-in fetch, FormData, Blob and SQLite handle the new features; no new npm dependency is needed.

```powershell
cd Backend
npm ci --ignore-scripts
# Copy only if .env does not already exist:
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
npm start
```

Edit `.env` privately. Preserve existing keys. Configure `OPENAI_API_KEY`, `OPENAI_MODEL=gpt-4.1-mini`, `OPENAI_SPEECH_ENABLED=true`, `OPENAI_TTS_MODEL=gpt-4o-mini-tts`, `OPENAI_TTS_VOICE=marin`, `OPENAI_STT_MODEL=gpt-transcribe`, and a separate random `BACKEND_ACCESS_TOKEN` (32+ characters). Keep `ENABLE_GENERATION=false`. Keys and backend data are Git-ignored; neither provider key belongs in Unity.

`GET http://127.0.0.1:3000/health` tests the server without provider calls. Authenticated `GET /api/capabilities` reports configuration without generating anything. The existing balance check is read-only. OpenAI planning/voice/transcription can incur charges when the player explicitly begins a connected session; no live OpenAI calls were used for implementation testing.

Device-local `healsphere-backend.json` configures Unity. On this Windows Editor: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/HealSphere_VR/`. It contains only the separate backend capability:

```json
{"url":"https://your-private-backend.example","token":"YOUR_LOCAL_BACKEND_CAPABILITY","live":true}
```

Use the exact existing JSON field names above. Remove the file for completely offline play. Do not copy provider keys into this file, StreamingAssets, scenes or PlayerPrefs. The bearer capability is also sensitive: keep this prototype server private and use HTTPS for thoughts/audio over a network. The project retains its existing Android HTTP restriction. A plain LAN HTTP URL will need an explicitly chosen development build setting; the delivered Android configuration does not silently enable insecure HTTP.

## Voice, subtitles and microphone

One dedicated 2D voice AudioSource avoids stacked narration. Ambient music ducks gently while speaking. Breathing cues preload and play only from cache at phase boundaries; a late response never becomes a late inhale instruction. Narration has a configurable eight-second client deadline and falls back to subtitles after failure. Restart or unmute allows a fresh attempt. Speak, transcribe, processing and voice states appear in the authored guidance panel. Mute and stepped volume controls are always available. AI-generated voice is disclosed.

Reusable scripted WAV narration is cached in ignored `Backend/data/speech`. Personalized reveal/closing audio is held in RAM for up to five minutes and discarded on plan cancellation. Private plans expire after thirty minutes. Recordings are bounded to twenty seconds and sent as mono PCM WAV; no continuous listening, background capture or permanent microphone file is created. Recording stops voice playback and blocks narration through transcription. Android microphone permission is requested only after pressing the microphone button. Denial, dismissal, no device, silence or provider failure leaves typed input usable. Real platform permission behavior and physical recording remain manual tests.

Current [OpenAI speech documentation](https://developers.openai.com/api/docs/guides/text-to-speech) supports the implemented WAV speech adapter, but documents retirement of that Speech API/model on **January 6, 2027**. Migrate the backend adapter to its recommended Realtime successor before then; changing the model environment variable alone will not migrate endpoints. [Transcription setup](https://developers.openai.com/api/docs/guides/speech-to-text) uses the current file-transcription endpoint with `gpt-transcribe`. Voice quality, account access and first-request latency have only been simulated, not checked with a live paid request.

## Privacy and library matching

The structured planner validates three distinct poses and a single chosen animal. Public generation descriptions use only controlled animal, pose, expression, visual detail and style values. Raw thoughts, inferred private themes and personalized narration never become reusable model names, filenames, task prompts or library metadata. Unity private answers are nonserialized RAM fields; restart clears the editable field too. Provider processing follows your [OpenAI account data controls](https://developers.openai.com/api/docs/guides/your-data); this application policy is not a claim that providers retain no data.

Library matching conservatively checks animal, pose, expression, appearance, anatomy/accessories and unrecognized requirements. It does not reuse a sleeping animal for a standing request or a hostile expression for a calm one. A mismatch stays missing. Current owner choice `MODEL_REVIEW_MODE=none` still bypasses visual/manual approval after bounded technical GLB validation; metadata says `skipped_by_owner`, not visually approved. Prompt safety remains. Use `openai` or `manual` if visual review is desired again.

## Separate paid authorization

This milestone authorizes **zero new live Tripo tasks**. Global generation remains disabled. Existing historical reservations and models are preserved. The new player flow cannot issue paid POST requests.

A local operator may inspect pending public concepts without spending:

```powershell
cd Backend
node tools/generate-reflection.mjs
node tools/generate-reflection.mjs --plan PLAN_UUID
```

Only after the owner separately authorizes an exact maximum number of tasks AND credit budget, and the operator verifies an upper credit cost for the selected Tripo endpoint/options, may the operator use the CLI's `--confirm-paid-generation`, `--max-tasks`, `--credit-budget` and `--verified-task-credit-ceiling` options. No authorization receipt was issued during this implementation. The CLI writes a short-lived, owner/plan/proposal-bound local receipt in ignored `data/reflection-authorization.json`; no HTTP route issues one. It refuses to overwrite an existing receipt. Inspect/retire an old receipt locally before issuing a new authorized grant; never change grant IDs merely to evade a consumed budget.

SQLite atomically reserves both task count and the verified credit ceiling before submitting each paid POST. Reservations survive restart and are not refunded merely because a request times out or is ambiguous. Same-job polling is GET-only; POSTs are never automatically retried. Unknown submission state means inspect the durable journal/provider account, not resubmit. Global legacy limits still apply. A scoped receipt permits only the authorized reflection proposals even while the global legacy flag stays off.

**Credit limitation:** this is a conservative local reservation budget using an operator-verified per-task upper price, not a provider-enforced spending cap or live price quotation. If the provider changes price beyond that ceiling, the first task could cost more; verify current official pricing before any future authorization. The previous fox's 40 credits are historical, not a price guarantee. Authorized models are discovered through library polling and use the existing validation/review setting before reveal.

## Scene and Inspector assignments

All new references are assigned in the saved scene; no manual UI construction is pending. The original creation root now also contains:

| Component | Inspector dependencies / settings |
| --- | --- |
| ReflectionExperienceDirector | Existing CreationSessionDirector, CreationSessionPresenter, TripoModelLoader, GuidedBreathingProvider, DreamSeedController, EnvironmentReactionController, FloatingCompanionController; new controls, narrator and microphone. Intro inhale 4, exhale 6, cycles 2. |
| ReflectionControlsPresenter | Existing authored `MomentCanvas/MomentPanel/MicrophoneButton`, `VoiceMuteButton`, `VoiceVolumeButton`. |
| RabbitVoiceNarrator | Backend loader, dedicated non-looping AudioSource (Play On Awake off), existing AudioManager; volume .65, voice timeout 8 seconds. |
| ReflectionMicrophoneInput | Backend loader, narrator; bounded recording duration and sample rate. |

Existing `PauseButton`, `RestartButton`, `WellnessButton`, `MomentMessage`, request field and spatial keyboard are reused. MomentPanel was visually enlarged to 1000×640; message rectangle to 920×420. No UI layout is constructed by runtime or editor C#. If references are intentionally changed, **HealSphere → Reflection → Bind Existing Authored Controls** rebinds named existing controls and saves the scene; it refuses missing UI. Adjust world-space positioning visually for seated comfort; never reposition the tracked camera to compensate.

## Routes

All routes below require `X-Backend-Token`; the normal existing request/rate limits apply.

| Method / route | Behavior |
| --- | --- |
| POST `/api/reflection/plans` | `{animal,thought1,thought2,offline_demo?}` → validated plan, three safe public concepts, saved matches and missing count. Thoughts may be empty; all fields required. |
| GET `/api/reflection/plans` | Owner's active public plan summaries; no thoughts or closing text. |
| GET `/api/reflection/plans/:id` | Private session plan, refreshed saved-library matches. |
| POST `/api/reflection/plans/:id/cancel` | Discard private context, unused proposals and private audio. |
| GET `/api/reflection/demo/:animal/:role/model` | Whitelisted, validated prebuilt GLB. |
| POST `/api/speech/narration` | Fixed cue and optional plan ID → bounded audio ID/text; arbitrary speech text is not accepted. |
| GET `/api/speech/audio/:id` | Bounded WAV, scoped private audio expiry. |
| POST `/api/speech/transcribe` | Raw `audio/wav` (maximum 2 MiB / 20 seconds) → editable recognized text. |

## Quest installation and manual playtest

1. Install Unity 6000.4.6f1 with Android Build Support, SDK/NDK and OpenJDK. Open the project without upgrading packages. Build Android using the preserved OpenXR/Meta features, IL2CPP, ARM64, Vulkan, existing performance URP and both creation/wellness scenes (creation first). Minimum API remains 32. Microphone use adds Android `RECORD_AUDIO`.
2. Enable developer mode and personally authorize USB debugging on Quest. Connect USB and run `adb devices`.
3. Install the new reflection build: `adb install -r Builds/HealSphereReflection/HealSphereReflection.apk`. Launch **HealSphere_VR** from Unknown Sources. Offline play needs neither backend nor JSON.
4. For connected voice/planning, start the private backend, provide a reachable HTTPS URL and separate capability in device-local JSON, then copy it using `adb push YOUR_PRIVATE_JSON /sdcard/Android/data/com.DefaultCompany.VRTemplate/files/healsphere-backend.json`. Restart the app. A PC LAN address belongs in this file, not Quest localhost. Configure private firewall/network access yourself. USB `adb reverse tcp:3000 tcp:3000` alone does not override Android's preserved cleartext policy.
5. Sit comfortably, recenter, check controller rays/keyboard and panel readability. Run animal + two thoughts with both typing and explicit microphone input. Review/edit transcripts. Test deny permission, retry, stop at twenty seconds, and no recording while narration plays.
6. Test skip/back/edit, neutral offline illustrations, three visibly different poses, smooth formation, textures/original materials, all anchors, music/chime/voice levels, mute and volume. Pause/suspend/resume, complete, restart and confirm old models/input disappear. Verify no unexpected motion, flashes or frame-time spikes. Try **Just breathe** and its 120-second ending/restart.

Physical Quest comfort, microphone hardware, OS permission dialogs, controller interaction, sustained frame rate and live provider voice quality remain unverified without that manual test. Pink materials: retain glTFast URP shader resources, verify supported imported shaders/textures, and avoid replacing every material with a flat shader. Speech failure: check backend credentials/model access, speech flag, HTTPS reachability, mute/volume and narration timeout; subtitles remain available. Authentication errors: use the backend capability in Unity, provider keys only in `.env`, then restart backend after configuration changes.

## Costs and reproducible checks

[Current OpenAI pricing](https://developers.openai.com/api/docs/pricing) is account/model dependent. At documentation check, `gpt-transcribe` is $0.0045/minute: three maximum twenty-second recordings total about $0.0045 before retries. `gpt-4o-mini-tts` is token-priced ($0.60/M input text tokens, $12/M output audio tokens); no fixed per-session speech price is promised. Script cache hits avoid new synthesis. Personalized audio and structured planning add usage. Tripo charges only separately authorized missing models; saved reuse/offline assets do not submit tasks.

Run `cd Backend; npm test` for fully mocked provider tests. For explicit Unity integration verification, start `node Backend/tools/reflection-mock-server.mjs` from the project root, then choose **HealSphere → Reflection → Verify Full Mock Reflection And Voice** outside Play Mode with the saved creation scene open. This uses a disposable mock database, synthetic WAV tone fixtures, test transcriptions and local models; it mutes output and never starts a physical microphone or loads real provider keys. It includes saved-model reuse, private-input reset, disconnected offline recovery, and the preserved wellness timeline. Stop the mock process afterward. See [validation results](AnimalReflectionValidation.md) for actual evidence and remaining checks.
