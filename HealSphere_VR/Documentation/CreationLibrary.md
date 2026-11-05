# Persistent Creation Library

**Latest visual-review override:** at the owner's explicit request, `MODEL_REVIEW_MODE=none` now skips both automatic visual review and manual approval. Validated models proceed directly to ready/library persistence and the existing Unity reveal. `skipped_by_owner` records the absence of visual review honestly. Prompt checks, paid confirmation and geometry/download/storage limits still apply. Existing waiting downloads are revalidated/released on restart without another provider submission. Failed/cancelled/rejected jobs remain in their existing states. This supersedes the earlier manual-review configuration described below. No new UI is required. The Editor feedback now says the model is ready without claiming it was reviewed; rebuild the APK to include that wording update if desired.

**Latest owner-requested settings:** lifetime and per-session paid-count quotas are removed: `MAX_LIFETIME_PAID_GENERATIONS=-1`, `MAX_GENERATIONS_PER_SESSION=-1`. Paid generation remains enabled, manual review remains selected. The backend reports unlimited count quotas while preserving the two existing paid reservations. Every new paid task still requires explicit confirmation and Tripo credits. Concurrency, request-rate, storage/model-size, safety and duplicate-submission protections remain. The finite/disabled settings below describe earlier verification and safe defaults. There is no new Unity UI or Inspector hookup; restart Play Mode to submit a fresh proposal after the backend restart.

**Later owner-requested configuration update:** the local backend now has `ENABLE_GENERATION=true`, lifetime budget **2**, and **1** existing paid reservation, allowing one additional explicitly confirmed generation. One-per-session and one-concurrent limits remain. No task was submitted by this configuration change. Restart Play Mode and submit a fresh request; backend restarts expire old unconfirmed proposals. The disabled-state instructions below describe the earlier verification/default-safe setup.

The authorized real sleeping-fox generation has already succeeded. **Keep `ENABLE_GENERATION=false` and `MAX_LIFETIME_PAID_GENERATIONS=1`. Do not submit another paid test or reset its ledger.** Saved creations work with generation disabled.

## Run the saved fox

On this workstation the normal backend and private Editor connection have already been configured. Start at step 5 while that backend remains running. The APK was rebuilt and signature-verified; physical Quest testing remains pending. See [the current validation report](CreationLibraryValidation.md) for its hash and results.

1. Use Unity **6000.4.6f1**, the installed packages and `Assets/HealSphere/Creation/Scenes/HealSphereCreation.unity`. No new UI or Inspector hookups are required. Preserve XR Origin and the authored keyboard/panel/buttons.
2. In `Backend`, run `npm ci --ignore-scripts` if dependencies are absent, then `npm start` with **Node 24+**. Dependencies are Express, dotenv, gltf-validator and image-size; SQLite uses Node's built-in module. `.env` already contains private configuration; do not replace it. For a fresh installation, copy `.env.example` only when `.env` is absent and configure provider keys there.
3. Check `/health` and authenticated `/api/capabilities`. Capabilities should report generation disabled and budget used **1**. `/api/balance` checks Tripo authentication/credits without generating. OpenAI interpretation can incur OpenAI usage; library search/download does not call OpenAI or Tripo.
4. Configure the existing loader through the **device-local** `healsphere-backend.json` described in the [creation README](../Assets/HealSphere/Creation/README.md). Use only the separate backend access capability, never either provider key. No capability is committed in scenes. On Windows Editor the directory is `%USERPROFILE%/AppData/LocalLow/DefaultCompany/HealSphere_VR`.
5. Press Play, choose Create a dream, type **I want a cute sleeping fox.**, then Ask rabbit. A match automatically starts the same 4-second inhale / 6-second exhale, seed charge, actual mesh-surface formation, material reveal and settling sequence. There is no paid confirmation for a saved match. Create another and try **Can you make a cute fox taking a nap?**
6. **A standing fox**, **a blue fox with wings**, and **a small purple baby dragon** must not reuse this fox. With generation disabled, the existing panel offers editing or an explicitly labeled prepared demo cat. No paid request is sent. Just breathe still opens the independent 120-second experience.

Without a local connection file, the original offline cat demonstration remains available. Typed requests use the existing keyboard; voice recognition is not implemented.

## Storage, matching and access

`Backend/data/library/creations.sqlite` holds owner, original request, prompt, display name, category/attributes, source/provider/task ID/timestamp, content hash, relative GLB/preview paths, validation/review, visibility and reuse permission. `library/assets/<sha256>.glb` holds atomically written validated files; repeated registration deduplicates bytes. Approved preview files sit beside them. Private task journals remain under `data/creations`. These paths, `.env`, logs and generated connection files are ignored by Git. Clients receive protected endpoint URLs, never arbitrary disk paths or signed CDN URLs.

Only approved, valid assets enter the reusable collection. Prepared orange cat is `prebuilt`; fox is `AI-generated`. A downloaded but rejected/unreviewed asset stays outside approved search. GLB serving rechecks the content hash and geometry budget. Live models are limited to 20,000 triangles, eight materials and 32 MiB; the known reviewed cat has a separate 60,000-triangle allowance. The actual fox has 4,942 triangles, one material and embedded textures.

Matching first retrieves at most 64 category candidates using normalized metadata; then requires every recognized requested attribute and any unknown meaningful words. Color, pose, anatomy, accessories, style, material, texture, age and prominent size requirements matter. Negations, multiple objects and ambiguous color relationships refuse automatic reuse. This is conservative vocabulary matching, not unrestricted semantic understanding. Sleeping/asleep/napping/taking a nap are equivalent. The actual fox is seated with closed sleeping eyes: **curled-up** is not verified and is not inferred. Unknown descriptions safely fall through to the existing new-creation flow.

Ownership currently derives from a SHA-256 digest of the local capability. Keep that capability stable to retain private access; this is a trusted prototype boundary, not production account authentication. Shared access requires both `SHARED_COLLECTION_ENABLED=true` and explicit per-asset publication. New player creations remain private until an operator publishes them. Shared metadata omits original requests, prompts and unknown/private descriptive words. Do not distribute this development capability publicly.

## Protected development endpoints

All `/api/*` requests require `X-Backend-Token` with the distinct local capability.

| Operation | Route |
| --- | --- |
| Inspect approved collection | `GET /api/library` |
| Search | `POST /api/library/search`, body `{"idea":"a sleeping fox"}` |
| Metadata | `GET /api/library/{creation_id}` |
| Validated model / approved preview | `GET /api/library/{creation_id}/model` or `/preview` |
| Create an owner-bound session | `POST /api/sessions`, body `{}` |
| Interpret request | `POST /api/conversation` |
| Reuse automatically or explicitly confirm new generation | `POST /api/creations` |
| Poll / cancel a creation | `GET /api/creations/{job_id}` / `POST /api/creations/{job_id}/cancel` |

The server searches before generating. New paid requests require a live-enabled backend, valid server proposal, explicit confirmation, allowed provider, credit check and a durable reservation. Unity sends a session ID and stable proposal idempotency key. Equivalent in-progress requests in that session recover the existing job. SQLite records the reservation **before** the external POST. Ambiguous submission errors never automatically retry. Poll/download retries only use the same task. Local cancellation does not promise provider cancellation or a refund.

Defaults: one simultaneous generation, one per session, one lifetime paid reservation, 900-second generation timeout, 60 authenticated requests/minute, 32 MiB/model and 512 MiB retained backend storage. Configure corresponding values in `.env.example`; restart the backend after changes. The lifetime budget includes the already completed fox. **Changing it is not authorization to spend again.** Rate windows are process-local; paid reservations persist. Run only one backend against this data directory. Reuse handles are bounded to 64 in memory; the permanent library survives process/application restarts.

## Inspect and manage safely

From `Backend`:

```powershell
npm run library -- list
npm run library -- show 539a8902-999b-4494-98d6-0a8add25e4a0
# Only for an asset you have explicitly approved for the shared prototype collection:
npm run library -- publish CREATION_UUID --approve-shared
# Logical removal from search and downloads; preserves files, history and cost ledger:
npm run library -- retire CREATION_UUID --confirm-retire
```

The fox and prepared cat are marked approved for the prototype shared collection; shared serving stays opt-in. Retirement does not free disk space. For storage maintenance, stop all backends, back up the entire `Backend/data` directory (including SQLite state), inspect references and archive obsolete task artifacts under operator control. There is deliberately no destructive reset/purge command. Never delete the submissions table, paid authorization journal or whole data directory to bypass cost limits. Keep backups private because original player text is stored there.

## Verification

`npm test` uses fake credentials and injected providers, never `.env` or paid calls. For the actual already approved fox, run `node --use-system-ca tools/library-verification-server.mjs` from Backend. It binds localhost:3337, writes an ignored temporary connection file and **blocks every Tripo POST**, even if a caller attempts one. `node --use-system-ca tools/check-saved-library.mjs` performs read-only real-library/hash/pose checks.

With the saved creation scene open outside Play Mode, run **HealSphere > Creation > Verify Real Saved Fox Reuse (No Paid Generation)**. The opt-in Editor test temporarily pins this protected service, accelerates timing and mutes audio; it exercises existing authored controls and restores the saved scene afterward. Report: `Library/HealSphereLibraryPlaytest.txt`. It requires no existing device-local JSON so it cannot overwrite a real connection. This check uses real OpenAI interpretation and the real saved fox; it makes no new Tripo generation. Stop the test service when finished. The separate offline verification menu remains available.

## Quest 2

Rebuild the current source with Android Build Support, IL2CPP/ARM64, existing OpenXR/Quest features, Vulkan and both creation/wellness scenes enabled. Use a private LAN backend (`HOST=0.0.0.0`) and the PC's LAN IP in device JSON; Quest localhost refers to Quest. Prefer HTTPS; LAN HTTP requires the existing project's development HTTP allowance to be configured for that build. For USB development, `adb reverse tcp:3000 tcp:3000` can map localhost.

After personally authorizing USB debugging, install the APK with `adb install -r Builds/HealSphereCreation/HealSphereCreation.apk`, push local JSON to `/sdcard/Android/data/com.DefaultCompany.VRTemplate/files/healsphere-backend.json`, and launch HealSphere_VR under Unknown Sources. Keep generation disabled. Test both fox phrases, different pose, original textures, scale/orientation, controller typing/buttons, seated readability, music/chime volume, pause/suspend, repeated-session memory and frame time. No physical headset was available for this milestone's verification.

## Troubleshooting and terms

401 from this backend means the local capability is wrong. Provider authentication errors require correcting server `.env`, not entering provider keys into Unity. Empty credits cannot be fixed by polling. `GENERATION_BUDGET_EXHAUSTED` is expected after the one authorized test; reuse still works. `CREATION_NOT_FOUND` can mean wrong owner, retired model or unpublished shared access. Invalid hash/GLB means restore a trusted backup and investigate; never weaken validation.

For TLS-chain errors on this PC, Node 24 `npm start` uses the Windows system CA store; never disable TLS verification. The authenticated provider returned `openapi-dl.data.tripo3d.com`; it is explicitly allowed for credential-free HTTPS downloads. Preview bodies advertised as binary are accepted only after bounded PNG/JPEG sniffing. For pink materials verify URP/glTFast shader retention resources and the original embedded textures; do not replace all materials with one flat shader.

The [official quick start](https://developers.tripo3d.ai/en/docs/quick-start) documents asynchronous tasks, and the [P-series endpoint](https://developers.tripo3d.ai/en/docs/generation-text-to-model/p) still documents `P1-20260311`. [Tripo terms](https://developers.tripo3d.ai/en/terms), sections 3.2 and 5.2.2, describe output-use rights for paid users subject to their terms and third-party rights. They also require prior authorization for making the generative service available to end users. Persistent curated prototype reuse is consistent with those output-use provisions; the account's specific supplemental plan and permission for a public generative product must be confirmed with Tripo before public deployment. Metadata records rights as conditional, not unrestricted ownership.
