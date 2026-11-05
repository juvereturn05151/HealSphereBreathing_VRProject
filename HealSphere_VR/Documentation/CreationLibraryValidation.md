# Creation Library milestone verification — 5 November 2026

## Real provider result

Exactly one authorized paid sleeping-fox generation succeeded. No further paid Tripo tasks were submitted during the continuation or reuse tests.

| Item | Actual result |
| --- | --- |
| Tripo task | `bb8f30f5-df59-4120-86c3-5e8bfd56ccd3` |
| Provider model | `P1-20260311`, requested 5,000 faces, textures/PBR enabled |
| Backend job | `932163c4-57fe-434f-9652-386c541a530a` |
| Permanent creation | `539a8902-999b-4494-98d6-0a8add25e4a0` |
| Actual cost | 40 credits (`credits_consumed`); available balance 2,000 before, 1,960 afterward |
| GLB | 2,122,716 bytes; 4,942 triangles; one material; embedded textures |
| Review | OpenAI visual moderation/review approved; downloaded preview also inspected locally |
| Current paid settings | `ENABLE_GENERATION=false`; durable paid count 1; lifetime limit 1 |

The API did not provide a pre-submission quote for this exact P1 configuration through the checked account operations; the recorded 40-credit cost is the completed task's actual result. No paid retry was made. Download recovery used the existing task only. The authenticated provider's exact CDN host was added to the HTTPS download allowlist; binary-advertised previews now require bounded PNG/JPEG sniffing. TLS validation remained enabled using Node's system CA support.

The actual fox is a cute, seated orange fox with closed sleeping eyes and a fluffy tail. It is not verified as curled-up or lying down; those requested poses are not automatically substituted.

## Automated backend checks

`npm test`: **26 passed, 0 failed**. Providers are injected mocks; tests do not read real credentials or create paid tasks. Existing 18 tests remain passing. Added coverage includes exact/equivalent wording, nap synonym, pose/color/anatomy/style/unknown-feature exclusions, SQLite restart persistence, file deduplication, private/shared request isolation, content tampering/path rejection, retirement, durable session/lifetime/idempotency guards, rate/storage/provider limits, and recovery of equivalent in-progress session requests without another provider POST.

The read-only `tools/check-saved-library.mjs` also passed against the **actual persisted fox** after backend restart. It checked exact, peaceful-sleeping and taking-a-nap queries; standing, winged blue fox, dragon and unverified curled-pose exclusions; authenticated GLB SHA-256/geometry; approved preview retrieval; disabled generation and one durable paid submission. It makes no OpenAI or Tripo requests.

## Unity Editor integration

The opt-in `LibraryExperienceVerification` ran the existing authored scene and controls with the actual GLB and real OpenAI interpretation. Its backend was hard-pinned to localhost:3337 with paid generation disabled and every Tripo POST blocked.

Passed checks:

- All required serialized scene references assigned.
- Sleeping-fox request automatically reuses the saved model without paid confirmation.
- **Can you make a cute fox taking a nap?** reuses the same permanent creation.
- glTFast imports the real 4,942-triangle mesh; surface sampling succeeds.
- Three charge cycles precede formation; the existing particles/reveal complete.
- Original supported glTF material and embedded base-color texture restored; bounded model scale.
- Two independently requested completed foxes retained, with no duplicate import instances; formation particles cleared.
- Standing-fox request excludes this model and reaches the unavailable-new-creation/edit/demo choice, without submitting a paid task.
- The paid budget remains exactly one before and after reuse/different-request checks.
- A disconnected backend reaches the existing error/fallback UI; explicitly choosing the offline cat completes the same importer/reveal flow as the third retained creation.
- The full preserved 120-second wellness scene and restart pass; no runtime errors captured during the tested flow.

Report: `Library/HealSphereLibraryPlaytest.txt`. Local completion screenshot: `Library/HealSphereRealFoxCompleted.png`. Guided timing was accelerated and audio muted for this behavioral test; perceived audio levels, comfort and stereo appearance are not established by it. Unity compiles these scripts; its existing deprecated API warnings and AI Assistant entitlement diagnostics are separate from runtime test results.

The saved creation scene hash remained `B980461431AA409DD0FF8A52EC3ACF61D343B4809360DC8E96C30570A07A1D20`; the wellness scene remained `AB4895930CC05349185D118CAA94ED59F8502DEC4D3141391A3DFC1604E1A29A`. Temporary test-only connection/comfort settings are not saved into scene assets. No new UI was constructed.

## Changed files for this milestone

- Backend: `src/config.js`, `limits.js`, `matching.js`, `library.js`; changes to `app.js`, `server.js`, `conversation.js`, `creations.js`; `test/library.test.js` and preview-fixture updates in `test/creation.test.js`.
- Tools: guarded one-time `live-fox.mjs`, protected `library-verification-server.mjs`, read-only `check-saved-library.mjs`, local operator `library.mjs`, and preview-fixture correction in the existing mock service.
- Unity: library reuse/session/capability handling in `CreationSessionDirector.cs`; route support in `TripoCreationImport.cs`; opt-in `LibraryExperienceVerification.cs` and its Unity meta file.
- Configuration/docs: `.env.example`, Node engine/start scripts and lock metadata, root/Backend/creation READMEs, `CreationLibrary.md` and this report.
- Private ignored data: authorization journal, original job/model/preview, SQLite approved fox/cat records and content-hashed assets. Provider keys were preserved; the distinct backend capability stays private. Existing unrelated project work was preserved.

## Remaining headset checks

The updated development APK built successfully from the isolated Android checkout with current library runtime scripts. `apksigner verify` passed (v2 signature). Output: `Builds/HealSphereCreation/HealSphereCreation.apk`, **145,596,123 bytes**, SHA-256 `057EBC082959FED344F395BB3841EFD2D5E14BFD9D3DB8FAD9B4B3956D1BD7BA`. Manifest checks: `com.DefaultCompany.VRTemplate`, ARM64, minimum API 32, target API 36, VR head-tracking feature. The successful BuildReport still counts two existing nonfatal URP Editor asset diagnostics, as in the earlier build; no C# compilation error occurred. Existing source XR/HTTP settings were not deliberately changed. This APK preserves HTTP-disabled settings: use trusted HTTPS for backend access, or explicitly make a separate LAN development build allowing HTTP for development only.

The protected verification service was stopped after testing. The normal backend is running on localhost:3000, generation disabled. An exclusive-create device-local Editor JSON was configured under `AppData/LocalLow/DefaultCompany/HealSphere_VR`; it contains only the separate local capability and URL, outside the project. No existing file was overwritten. The Unity Editor is back on the saved creation scene outside Play Mode. Press Play, Create a dream, type the fox/nap request, then Ask rabbit. Stop and restart `npm start` yourself after closing this session if necessary.

Final integrity checks: SQLite `quick_check` returned `ok`; the ledger contains one paid reservation; configured secrets had zero occurrences in checked Git-included source/text assets. A copy of the passing Editor report is preserved as `Documentation/CreationLibraryPlaytest.txt`.

`adb devices` found no connected headset. No physical Quest 2 result is claimed. Verify both-eye rendering, controller typing/rays, seated panel readability, model orientation and scale, music/chime loudness, pause/suspend/resume, repeated-session memory and sustained frame time on the device. Follow [the exact run/build/configuration steps](CreationLibrary.md). Keep generation disabled and test the saved fox only.

Typed input, conservative vocabulary matching, private-LAN capability access and static reviewed GLBs are the current prototype boundaries. Voice input, production multi-user identity, public generative-service authorization and physical-headset performance certification remain outside this milestone. Retiring an asset hides it but does not reclaim storage or reset paid limits.
