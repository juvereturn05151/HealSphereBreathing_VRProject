# HealSphere reflection and creation backend

Current milestone: animal + two optional thoughts → structured same-animal Reflection/Support/Hope concepts, conservative saved-model reuse, and separately authorized missing models. OpenAI narration and explicit WAV transcription run only through the backend. Provider keys remain in ignored `.env`; private thoughts and recordings never enter public library metadata. See [current setup, routes, privacy, audio models and paid authorization](../Documentation/AnimalReflection.md) and [validation](../Documentation/AnimalReflectionValidation.md).

**Global generation is disabled. No live Tripo task is authorized by this milestone.** Unity never submits paid reflection tasks. A local operator can preview public concepts with `node tools/generate-reflection.mjs`; the spending flag requires separate owner approval of maximum tasks AND credits plus a verified per-task price ceiling. SQLite reservations and duplicate protection persist. Current owner review mode remains `none` (technical validation only); the new milestone did not reenable slow visual review. Reuse does not incur generation charges.

Node 24+, existing Express/dotenv/gltf-validator/image-size dependencies, `npm ci --ignore-scripts`, configure `.env` from its example only if absent, then `npm start`. New speech settings are in `.env.example`. Run `npm test` for mock-only verification. No new provider SDK is required. Scripted audio caches in ignored data; private personalized audio and recordings remain memory-only.

The sections below retain earlier library/creation API and recovery references. Historical statements about paid counts, enabled generation or manual review describe previous milestones; the current settings and the guide above take precedence.

### Recovering a visually rejected existing asset

Automatic visual review can return a false rejection. The current boolean reviewer does not record a detailed reason; do not infer that Tripo failed or submit a replacement automatically. Inspect the saved local preview/model first. An authenticated development operator can explicitly approve a reviewed rejected asset through `POST /api/creations/{job_id}/review` with `decision: APPROVE`, `confirm: APPROVE_REVIEWED_EXISTING_ASSET`, and a nonempty `note` (maximum 256 characters). This revalidates and registers existing bytes, preserves the previous status/review/error in the private review history, and never calls Tripo. Ordinary approval without this confirmation/note remains blocked for rejected assets. This is a trusted local operator capability, not a production multi-user moderation console. No new Unity UI or Inspector assignments are required.

The gorilla task `ea866475-302f-4324-9ee9-cc55d78feedc` completed and its GLB passed validation (5,010 triangles, one material, 3,163,224 bytes). After local preview inspection, the existing asset was explicitly approved with a recorded note and registered as `6a443999-3fa6-46fb-b269-bbabbf98abdb`. Gorilla category matching was added. Authenticated saved-model search and validated model retrieval passed; paid reservations remained **2 before and after recovery**. All 26 backend tests pass, including guarded review override/audit and gorilla pose matching. No new Unity/headset runtime test was performed for this particular gorilla recovery.

**Current milestone:** the real sleeping fox is validated, approved and stored persistently. Keep `ENABLE_GENERATION=false`; the single lifetime paid reservation has been used. Follow [Creation Library setup, routes, limits, management and Quest instructions](../Documentation/CreationLibrary.md). Do not run another fixed-cat or fox paid test. [Live validation results](../Documentation/CreationLibraryValidation.md) supersede the older test instructions below.

The backend now supports structured OpenAI conversation, server-issued proposals, explicit confirmation, dynamic Tripo v3 tasks, asynchronous polling, bounded GLB validation and an asset-review gate. The original fixed-cat API remains available for development. The integrated creation scene and offline GLB demonstration are documented in [the creation README](../Assets/HealSphere/Creation/README.md). The fixed-cat instructions below are optional and are not required to play that scene.

## Backend setup

Use Node.js 24 or newer (tested with 24). Dependencies: Express 5, dotenv, Khronos `gltf-validator`, and `image-size`. `package-lock.json` pins the installed dependency graph.

From the project root in PowerShell:

```powershell
cd Backend
npm ci --ignore-scripts
# Only if .env does not already exist:
Copy-Item .env.example .env
```

Existing provider keys were preserved; a distinct private backend capability was securely configured. Edit it locally; do not paste its contents into chat, Unity, or logs:

```dotenv
TRIPO_API_KEY=your_real_key_from_the_tripo_developer_console
BACKEND_ACCESS_TOKEN=a_distinct_random_local_access_token_of_at_least_32_characters
HOST=127.0.0.1
PORT=3000
ENABLE_GENERATION=false
MODEL_DOWNLOAD_HOSTS=tripo3d.ai,openapi-dl.data.tripo3d.com
```

Generate a random local token if needed with `node -e "console.log(require('node:crypto').randomBytes(32).toString('hex'))"`. The local backend token is a development access capability, **never** the Tripo API key. Keep it out of committed scenes and public builds. `.gitignore` excludes `.env` variants, `node_modules`, logs, and downloaded data; `.env.example` is explicitly included. Check with `git check-ignore Backend/.env` from the project root. Ignoring does not remove secrets already committed in history.

```powershell
npm start
```

The server resolves `.env` relative to `Backend`, regardless of the shell working directory. It fails closed if the local token is missing/placeholder, shorter than 32 characters, or equal to the Tripo key. It can start without a Tripo key for health checks. Startup does not query Tripo or generate anything. Run only one backend process against this data directory.

If npm reports `UNABLE_TO_VERIFY_LEAF_SIGNATURE` on this machine, use Node 24's system CA support: `$env:NODE_USE_SYSTEM_CA='1'`, then repeat `npm ci --ignore-scripts`. Do not disable TLS verification.

## Free connection and account checks

```powershell
Invoke-RestMethod http://127.0.0.1:3000/health
# Enter the distinct local access token, not the Tripo key:
$localToken = Read-Host 'Backend access token'
$headers = @{ 'X-Backend-Token' = $localToken }
Invoke-RestMethod http://127.0.0.1:3000/api/balance -Headers $headers
```

`/health` tests the local backend only. `/api/balance` authenticates to Tripo and returns only numeric `balance` (available credits) and `frozen` (reserved credits). Neither creates a task. Positive balance does not guarantee enough credits for this model; Tripo's submission response is authoritative. There is no fixed price hardcoded in this integration.

## Unity Inspector hookup (manual, no new UI required)

1. Let Unity Package Manager resolve `com.unity.cloud.gltfast` **6.20.0**, added to `Packages/manifest.json`. Let Unity update its package lock normally; do not replace the existing package graph. Confirm no Console compilation errors before proceeding.
2. Open `Assets/HealSphere/Meadow/BakedMeadow/BreathingMeadow.unity`. Its existing `CreationAnchor_01` is at local position `(-1.15, 0.35, 1.85)`. Leave the XR Origin and anchor hierarchy intact. Verify the anchor is comfortable and visible from the seated player position.
3. Create an empty gameplay GameObject named `TripoModelLoader` through the Editor hierarchy and add **HealSphere.Tripo.TripoModelLoader**. This object is a controller, with no Canvas or UI children.
4. Assign **Creation Anchor** to the existing `CreationAnchor_01` Transform. Keep its world scale uniform and ideally `(1,1,1)`; display bounds are measured in anchor space.
5. Set **Backend URL** to `http://127.0.0.1:3000` for the Editor. Enter only **BACKEND_ACCESS_TOKEN** in **Backend Access Token**. For local Editor testing, assign the token during Play Mode so it is not saved into the scene. Never enter `TRIPO_API_KEY` in Unity.
6. Set **Maximum Size** to e.g. `(0.5, 0.5, 0.5)` meters. Uniform scaling fits every axis within that box, preserves proportions, centers X/Z, and places the model base at the anchor. Adjust **Model Rotation** if its forward direction needs correction. Imported materials and textures are retained.
7. Optional: assign an existing, reviewed 3D prefab to **Offline Fallback Prefab**. Prefer a small static URP asset without runtime scripts. **Use Fallback On Error** displays it when a load fails and no model is already shown. Use the context menu to test it without the backend. No fallback prefab is fabricated by code.
8. Leave **Load Approved Model On Start** disabled for the first test. Even if enabled later for Quest, it only downloads a previously approved model; it never generates. Request timeout defaults to 30 seconds, whole client operation to 660 seconds, and client polling to 3 seconds.

No UI is needed for the Editor test. Open the component's three-dot menu (or right-click its header) during Play Mode to find **Tripo/** commands. `Status`, `Progress`, `LastError`, `IsBusy`, and `StateChanged` are available to a future presenter referencing visually authored UI. If headset buttons are wanted later, manually author a world-space Canvas and Buttons in the Editor and bind their OnClick events to these public methods; no such UI is created here.

## Archived fixed-cat paid test (do not run for this milestone)

1. Start the backend with generation disabled. Enter Play Mode, assign the local access token, then invoke **1 Check Backend Connection (Free)** and **2 Check Authentication And Balance (Free)**. Read the Unity Console. Play Mode itself submits nothing.
2. When ready for one charge, set `ENABLE_GENERATION=true` in `.env` and restart the backend. Invoke **3 Generate ONE Test Cat (PAID, Explicit)** once. Alternatively, use this explicit command instead of the Unity command:

   ```powershell
   Invoke-RestMethod http://127.0.0.1:3000/api/test-cat -Method Post -Headers $headers -ContentType 'application/json' -Body '{"confirm":"GENERATE_ONE_CAT"}'
   ```

3. The backend submits the exact requested orange cat prompt with `model: P1-20260311`, `face_limit: 5000`, `texture: true`, and `pbr: true`. The endpoint accepts no user-defined prompt: the reviewed, fixed family-friendly prompt is the input safety allowlist for this test. No silent model substitution or paid retry occurs.
4. Backend polling queries only `GET /v3/tasks/{task_id}` every 2.5 seconds, independent of Unity. It immediately downloads `output.model_url` when successful. Unity polls only its backend. To observe from PowerShell:

   ```powershell
   Invoke-RestMethod http://127.0.0.1:3000/api/test-cat -Headers $headers
   ```

5. Wait for `awaiting_review`. The downloaded file is `Backend/data/test-cat.glb`; metadata and task ID are in `Backend/data/test-cat.json`. Inspect the GLB locally in a trusted 3D viewer/Blender. Verify it is the requested friendly cat and that geometry/textures are suitable. Validation cannot judge visual content, so the Unity-serving endpoint remains blocked until this human review. Do not upload it to an online viewer merely to review it.
6. After visual review, approve or reject explicitly:

   ```powershell
   # Only after inspecting the downloaded asset:
   Invoke-RestMethod http://127.0.0.1:3000/api/test-cat/review -Method Post -Headers $headers -ContentType 'application/json' -Body '{"decision":"APPROVE"}'
   # To reject instead, use {"decision":"REJECT"}.
   ```

7. Invoke **5 Load Reviewed And Approved Model (No Submission)** in Unity. The loader downloads the backend's authenticated `/api/test-cat/model`, imports it asynchronously using glTFast and places it at the assigned anchor. It skips a second import of the same displayed task. Only one displayed model is retained; replacing it releases the previous glTFast-owned resources. Disabling the component or exiting Play Mode cancels local work and cleans up its model.
8. Confirm base color, cream belly, normals, roughness, textures, scale, position, and orientation in the Editor. Set `ENABLE_GENERATION=false` and restart the backend after your single test. Existing polling/download/approval continue to work with generation disabled.
9. Complete shader inclusion and Quest network setup below, then test in the headset. A real cat generation, material check, and Quest check remain manual; the automated tests use fake upstream responses and a tiny fixture GLB.

## Recovery and duplicate-charge protection

`Backend/data/test-cat.json` is written **before** the paid POST. All subsequent generation calls return that same job or a conflict; they never submit another job, including after backend restart. There is intentionally no reset/new-generation endpoint in this first-test implementation.

- **Unity cancelled, timed out or exited:** backend work continues. Use **4 Resume Polling Existing Task** on reconnect. Cancelling local work does not cancel/refund a Tripo task.
- **Backend restart:** a saved polling/downloading task resumes automatically using its original task ID and original ten-minute deadline. Completed/rejected jobs are preserved. This restart behavior only polls; it never submits.
- **`submission_unknown`:** the upstream may have accepted a charge even if its response was lost. Check the Tripo developer console/task history before doing anything else. Never blindly delete the journal and resubmit. To recover a confirmed existing task, stop the backend, back up the journal, insert the verified `task_id`, set `status` to `polling` and `deadline` to `Date.now()+600000` (Unix milliseconds), then restart. This is an explicit operator repair, not automatic retry.
- **`failed` / `TASK_TIMEOUT`:** inspect the sanitized error and original task in the Tripo console. The backend never silently starts a replacement. For a verified recoverable download/poll failure, the same operator repair can resume the original task. Signed upstream URLs expire; another task query is used to obtain a fresh URL.
- **`INSUFFICIENT_CREDITS`:** add credits/check reserved credits in the Tripo console. If submission was attempted, preserve the journal until you verify no task was charged.
- **`TRIPO_AUTH_FAILED`:** check the key in `Backend/.env`, its active status and permissions in the API developer console, then restart. Do not use a Studio session token or copy the key into Unity. Errors never echo upstream messages or authorization headers.
- **HTTP 401 from backend:** local access token mismatch/missing. This is separate from Tripo authentication.
- **`MODEL_HOST_REJECTED`:** check the output host independently against official Tripo documentation/console. The default permits `tripo3d.ai` and its subdomains over HTTPS. Add only a verified public CDN host to `MODEL_DOWNLOAD_HOSTS`, restart, and recover the same task. Never allow private hosts, arbitrary user URLs or broad shared domains. CDN requests omit the API key and refuse redirects.
- **`MODEL_REJECTED`:** the asset is malformed or exceeds the fixed Quest test budget. Limits: 32 MiB GLB, 1 MiB JSON, 20,000 instantiated triangles, 60,000 mesh vertices, 128 nodes, 8 materials/images, at most 2048 pixels per image axis and 16 megapixels total. Only embedded PNG/JPEG and uncompressed triangle geometry are accepted. External resources, animation/skins, Draco/Meshopt/KTX and unapproved extensions are rejected. The generation requests 5,000 faces, but output is independently checked. Do not relax limits merely to display an unreviewed asset. Review/optimize it through a separate asset workflow if needed.

Downloaded data remains on disk so paid results survive restart. For this single test it is bounded to one model plus one journal. Back it up before intentional cleanup; do not delete the journal as a routine retry mechanism.

## Quest 2 networking and materials

The Quest must reach your PC, rather than its own loopback address. Use the PC's private Wi-Fi/LAN IPv4 address, e.g. `http://192.168.1.25:3000`. Both devices must be on the same trusted network without client isolation. Set backend `HOST=0.0.0.0`, restart, and allow inbound TCP 3000 on your private-network firewall profile. Do not port-forward this development service or expose it publicly.

For a local HTTP development build, manually set **Player Settings > Other Settings > Configuration > Allow downloads over HTTP** to **Development Only**, make a Development Build, and set Android **Internet Access** to **Require** if necessary. The project's current HTTP policy is disabled; it has deliberately not been changed. Prefer an HTTPS reverse proxy with a certificate trusted by the headset for broader use. The local backend token travels in an HTTP header on LAN and is extractable from a test build; this is not a production authentication scheme. A deployed product needs HTTPS and real user authorization/quotas, with generation disabled for ordinary model-viewing clients. Never ship the Tripo key.

For Quest verification, set **Backend URL** to the PC's LAN address, provide the distinct local test token locally, enable **Load Approved Model On Start**, and leave backend generation disabled. Build/run with the existing Android/OpenXR/Quest configuration. Verify from a seated position, with no forced movement; check model visibility and readable scale, both-eye rendering, textures, shader errors, memory, frame timing and stability. The complete meadow's performance is not guaranteed by this model's budget alone. An offline prefab can be loaded if the PC is unavailable.

If the model is pink, white, black or untextured:

- Read the glTFast Console/log output and confirm the asset has embedded images/materials. Do not overwrite all imported materials with a generic material; that discards the intended appearance.
- Use the project's existing URP assets. glTFast selects its URP material generator. Do not switch render pipelines or XR settings to fix materials.
- Runtime-only materials/shader variants can be stripped from Android builds even when the Editor looks correct. Load the reviewed model in Editor Play Mode, then follow the installed glTFast **Project Setup > Materials and Shader Variants** guide: save tracked variants as a ShaderVariantCollection and add it to Graphics **Preloaded Shaders**. Include the required glTFast shader assets if Unity reports them missing. Retest the actual Android build; preloading alone does not guarantee stripped variants are included.
- If there are no renderers, invalid bounds, unsupported compression/extensions or excessive textures, the loader/backend reports an error and preserves an existing displayed model or uses the optional fallback. It does not synthesize missing materials or UI.

## Validation and official references

`npm test` exercises the backend using mocked upstream responses, with no real key or charge: free health/balance, access controls, exact v3 payload, polling without resubmission, valid GLB download/review/serving, persistent reuse after restart, ambiguous POST lockout, credits/auth errors, terminal status/timeouts, malformed/external/overbudget GLBs, and unsafe output hosts.

- [Tripo quick start](https://developers.tripo3d.ai/en/docs/quick-start)
- [Text-to-model P Series and P1-20260311](https://developers.tripo3d.ai/en/docs/generation-text-to-model/p)
- [Account balance](https://developers.tripo3d.ai/en/docs/account)
- [Task query](https://developers.tripo3d.ai/en/docs/task-query)
- [Tripo error codes](https://developers.tripo3d.ai/en/docs/error-handling)
- [Unity glTFast 6.20 project setup](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/ProjectSetup.html)

Implementation verification results are recorded in `VALIDATION.md`. Real authentication, visual review, real generated model import, and Quest 2 behavior require the manual steps above.
