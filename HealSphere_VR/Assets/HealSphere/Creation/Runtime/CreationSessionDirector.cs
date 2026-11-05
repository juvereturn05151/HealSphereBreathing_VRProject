using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealSphere.Breathing;
using HealSphere.Tripo;
using HealSphere.Wellness;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace HealSphere.Creation
{
    public enum CreationSessionState { Welcome, WaitingForRequest, UnderstandingRequest, AwaitingConfirmation, GeneratingModel, BreathingAndCharging, ModelReady, RevealingCreation, CreationComplete, ErrorOrOfflineFallback }
    public sealed class CreationSessionDirector : MonoBehaviour
    {
        [Header("Existing systems")]
        [SerializeField] private CreationSessionPresenter presentation;
        [SerializeField] private TripoModelLoader loader;
        [SerializeField] private GuidedBreathingProvider breathing;
        [SerializeField] private DreamSeedController seed;
        [SerializeField] private ParticleModelReveal reveal;
        [SerializeField] private EnvironmentReactionController environmentReaction;
        [SerializeField] private FloatingCompanionController companion;
        [SerializeField] private AudioManager audioController;
        [SerializeField] private ExperienceConfiguration configuration;
        [SerializeField] private Transform[] completedAnchors;
        [Header("Creation pacing")]
        [SerializeField, Min(1)] private int chargeCycles = 3;
        [SerializeField, Min(1)] private int revealCycles = 3;
        [SerializeField, Min(1)] private float standardGuidanceSeconds = 120;
        [SerializeField, Min(30)] private float generationTimeoutSeconds = 900;
        [SerializeField, Min(2)] private float pollSeconds = 3;
        [SerializeField, Min(0)] private float localDemoDelaySeconds = 3;
        [SerializeField, Min(.5f)] private float settleSeconds = 2;
        [SerializeField] private bool useBackendWhenConfigured = true;
        [SerializeField] private string wellnessSceneName = "HealSphereMoment";
        public CreationSessionState State { get; private set; }
        public bool IsPaused { get; private set; }
        public int CompletedCount { get; private set; }
        public int ChargedCycles => cycles;
        public bool UsesLiveBackend => live;
        public GameObject CurrentModel => candidate;
        public bool IsConfigured => presentation != null && presentation.IsConfigured && loader != null && breathing != null && seed != null && reveal != null && reveal.IsConfigured &&
            environmentReaction != null && environmentReaction.IsConfigured && companion != null && companion.IsConfigured && audioController != null && audioController.IsConfigured && configuration != null &&
            completedAnchors != null && completedAnchors.Length == 3 && completedAnchors[0] != null && completedAnchors[1] != null && completedAnchors[2] != null;
        private CancellationTokenSource operation;
        private int revision, cycles, formedCycles;
        private GameObject candidate;
        private readonly List<GameObject> completed = new List<GameObject>();
        private Proposal proposal;
        private string clarificationId = "", jobId = "", status = "", failure = "", breathText = "";
        private string activeProposalId = "", backendSessionId = "";
        private bool live, backendJob, modelReady, settling, menuHeld, restartHeld;
        private bool liveGenerationAvailable = true;
        private float elapsed, breathAmount;
        [Serializable] private sealed class IdeaRequest { [SerializeField] private string idea; [SerializeField] private string clarification_id; public IdeaRequest(string text, string id) { idea = text; clarification_id = id; } }
        [Serializable] private sealed class BackendSession { [SerializeField] private string session_id; public string Id => session_id; }
        [Serializable] private sealed class Capability { [SerializeField] private bool liveGenerationEnabled; public bool Enabled => liveGenerationEnabled; }
        [Serializable] private sealed class ConfirmRequest
        {
            [SerializeField] private string proposal_id, session_id, idempotency_key;
            [SerializeField] private string confirm = "CREATE_ONE_MODEL";
            [SerializeField] private string mode;
            public ConfirmRequest(string id, string session, bool reuse) { proposal_id = id; session_id = session; idempotency_key = id; mode = reuse ? "reuse" : "live"; }
        }
        [Serializable] private sealed class Proposal
        {
            [SerializeField] private bool accepted, needsClarification;
            [SerializeField] private string response, question, displayName, prompt, source, proposal_id, clarification_id, saved_creation_id;
            public bool Accepted => accepted; public bool Clarify => needsClarification; public string Response => response; public string Question => question;
            public string Name => displayName; public string Prompt => prompt; public string Source => source; public string Id => proposal_id; public string Clarification => clarification_id;
            public bool HasSavedCreation => !string.IsNullOrEmpty(saved_creation_id);
            public static Proposal Offline(string text)
            {
                text = (text ?? "").Trim();
                bool vague = text.Length < 4 || System.Text.RegularExpressions.Regex.IsMatch(text, @"^(something|anything|it|that|a thing)[.!?]*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return new Proposal { accepted = !vague, needsClarification = vague, source = "offline", displayName = "Prebuilt demo cat", response = vague ? "What little object are you imagining?" : "Let’s plant that little dream. This offline demo will form our Prebuilt demo cat.", question = vague ? "Could you name an animal, plant, or object?" : "", proposal_id = Guid.NewGuid().ToString() };
            }
        }
        [Serializable] private sealed class Job
        {
            [SerializeField] private string id, status, error, source, displayName, model_source;
            [SerializeField] private float progress;
            public string Id => id; public string Status => status; public string Error => error; public string Source => source; public string Name => displayName; public float Progress => progress;
            public bool IsReviewedPrebuilt => model_source == "prebuilt";
        }
        private void Start()
        {
            loader?.ReloadLocalConnection(); live = useBackendWhenConfigured && loader != null && loader.HasBackendConnection && loader.LiveConnectionRequested;
            if (!IsConfigured) { Debug.LogError("HealSphere creation: assign the authored input/keyboard/buttons and existing systems. No UI fallback is created.", this); return; }
            presentation.PrimaryRequested += Primary; presentation.SecondaryRequested += Secondary; presentation.WellnessRequested += Wellness;
            breathing.Signal += OnBreath; breathing.ExhaleCompleted += OnCycle; reveal.Revealed += OnRevealed;
            audioController.SetMasterVolume(configuration.MasterVolume); audioController.StartAmbience(); seed.Hide(); SetState(CreationSessionState.Welcome);
        }
        public void Primary()
        {
            switch (State)
            {
                case CreationSessionState.Welcome: case CreationSessionState.CreationComplete: case CreationSessionState.ErrorOrOfflineFallback: NewRequest(); break;
                case CreationSessionState.WaitingForRequest: SubmitIdea(); break;
                case CreationSessionState.AwaitingConfirmation: ConfirmCreation(); break;
                case CreationSessionState.GeneratingModel: case CreationSessionState.BreathingAndCharging: case CreationSessionState.ModelReady: case CreationSessionState.RevealingCreation: TogglePause(); break;
            }
        }
        public void Secondary()
        {
            if (State == CreationSessionState.ErrorOrOfflineFallback) UseOfflineFallback();
            else if (State == CreationSessionState.AwaitingConfirmation) { proposal = null; SetState(CreationSessionState.WaitingForRequest); }
            else CancelCreation();
        }
        public void NewRequest()
        {
            CancelWork(); proposal = null; clarificationId = ""; failure = ""; presentation.ClearRequest(); SetState(CreationSessionState.WaitingForRequest);
        }
        public void SubmitIdea()
        {
            if (State != CreationSessionState.WaitingForRequest) return;
            string idea = presentation.RequestText.Trim();
            if (idea.Length < 1 || idea.Length > 512 || System.Text.RegularExpressions.Regex.IsMatch(idea, @"[<>\x00-\x1f]|\b(porn|nude|sexual|gore|suicide|weapon|bomb|gun|swastika)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            { status = "Please enter one gentle, family-friendly object (up to 512 characters)."; Render(); return; }
            SetState(CreationSessionState.UnderstandingRequest);
            StartOperation(async token => {
                if (live)
                {
                    var capabilityBytes = await loader.SendCreationRequestAsync("/api/capabilities", null, token);
                    liveGenerationAvailable = JsonUtility.FromJson<Capability>(Encoding.UTF8.GetString(capabilityBytes)).Enabled;
                    if (string.IsNullOrEmpty(backendSessionId))
                    {
                        var sessionBytes = await loader.SendCreationRequestAsync("/api/sessions", "{}", token);
                        var session = JsonUtility.FromJson<BackendSession>(Encoding.UTF8.GetString(sessionBytes));
                        if (session == null || !Guid.TryParse(session.Id, out _)) throw new InvalidOperationException("Invalid backend session.");
                        backendSessionId = session.Id;
                    }
                    byte[] bytes = await loader.SendCreationRequestAsync("/api/conversation", JsonUtility.ToJson(new IdeaRequest(idea, clarificationId)), token);
                    token.ThrowIfCancellationRequested(); proposal = JsonUtility.FromJson<Proposal>(Encoding.UTF8.GetString(bytes));
                    if (proposal == null || string.IsNullOrWhiteSpace(proposal.Response)) throw new InvalidOperationException("Invalid companion response.");
                }
                else { await Task.Yield(); token.ThrowIfCancellationRequested(); proposal = Proposal.Offline(idea); }
                if (proposal.Clarify) { clarificationId = proposal.Clarification; status = proposal.Response + "\n" + proposal.Question; presentation.ClearRequest(); SetState(CreationSessionState.WaitingForRequest, false); }
                else if (!proposal.Accepted) { status = proposal.Response; SetState(CreationSessionState.WaitingForRequest, false); }
                else if (live && proposal.HasSavedCreation) BeginCreation(false, true);
                else SetState(CreationSessionState.AwaitingConfirmation);
            });
        }
        public void ConfirmCreation()
        {
            if (State != CreationSessionState.AwaitingConfirmation || proposal == null || !proposal.Accepted) return;
            BeginCreation(!live || !liveGenerationAvailable);
        }
        public void UseOfflineFallback() { BeginCreation(true); }
        private void BeginCreation(bool local, bool reuse = false)
        {
            CancelWork(); cycles = formedCycles = 0; elapsed = breathAmount = 0; modelReady = settling = false; failure = "";
            backendJob = !local; activeProposalId = local ? "" : proposal?.Id ?? "";
            environmentReaction.ResetReaction(); companion.RestartMotion(); seed.Show(); breathing.ConfigureTiming(4, 6); breathing.Begin();
            SetState(CreationSessionState.GeneratingModel);
            StartOperation(async token => {
                byte[] glb; bool reviewedPrebuilt = local;
                if (local)
                {
                    status = "Offline demo: the Prebuilt demo cat is loading. Your request is not being sent for generation.";
                    SetState(CreationSessionState.BreathingAndCharging, false);
                    await Task.Delay(TimeSpan.FromSeconds(localDemoDelaySeconds), token); glb = await loader.ReadLocalDemoAsync(token);
                }
                else
                {
                    var bytes = await loader.SendCreationRequestAsync("/api/creations", JsonUtility.ToJson(new ConfirmRequest(activeProposalId, backendSessionId, reuse)), token);
                    token.ThrowIfCancellationRequested(); var job = JsonUtility.FromJson<Job>(Encoding.UTF8.GetString(bytes));
                    if (job == null || string.IsNullOrEmpty(job.Id)) throw new InvalidOperationException("Invalid generation session.");
                    jobId = job.Id; SetState(CreationSessionState.BreathingAndCharging);
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        bytes = await loader.SendCreationRequestAsync("/api/creations/" + jobId, null, token);
                        job = JsonUtility.FromJson<Job>(Encoding.UTF8.GetString(bytes));
                        if (job == null) throw new InvalidOperationException("Invalid generation status.");
                        if (job.Status == "ready") { reviewedPrebuilt = job.IsReviewedPrebuilt; break; }
                        if (job.Status == "failed" || job.Status == "cancelled" || job.Status == "rejected" || job.Status == "submission_unknown")
                            throw new InvalidOperationException("Generation stopped: " + job.Error + ". A paid task is never automatically resubmitted.");
                        status = job.Status == "awaiting_review" ? "Your model is awaiting visual review on the backend. You can relax, cancel, or use an offline creation." : "Your little dream is growing (" + Mathf.RoundToInt(job.Progress) + "%).";
                        Render(); await Task.Delay(TimeSpan.FromSeconds(pollSeconds), token);
                    }
                    glb = await loader.SendCreationRequestAsync("/api/creations/" + jobId + "/model", null, token);
                }
                token.ThrowIfCancellationRequested();
                var imported = await loader.LoadCreationAsync(glb, local ? "Prebuilt demo cat — Offline Demo" : "Created " + proposal.Name, reviewedPrebuilt, token);
                if (token.IsCancellationRequested) { Destroy(imported); token.ThrowIfCancellationRequested(); }
                candidate = imported; modelReady = true; status = local ? "Prebuilt demo cat ready — offline demonstration." : "Your little dream is ready.";
                SetState(CreationSessionState.ModelReady, false); TryReveal();
            });
        }
        private async void StartOperation(Func<CancellationToken, Task> work)
        {
            operation?.Cancel(); operation?.Dispose(); var source = new CancellationTokenSource(); operation = source;
            source.CancelAfter(TimeSpan.FromSeconds(generationTimeoutSeconds)); int version = revision;
            try { await work(source.Token); }
            catch (OperationCanceledException) { if (version == revision && isActiveAndEnabled) Fail("The request timed out. A submitted paid task may still be running; use offline creation or check the backend before trying again."); }
            catch (Exception e) { if (version == revision && isActiveAndEnabled) Fail(e is InvalidOperationException ? e.Message : "The creation service is unavailable. You can continue offline."); }
            finally { if (ReferenceEquals(operation, source)) operation = null; source.Dispose(); }
        }
        private void Fail(string message)
        {
            failure = message; breathing.Stop(); SetPaused(false); seed.Hide(); reveal.Cancel(); if (candidate != null) Destroy(candidate); candidate = null; modelReady = false;
            SetState(CreationSessionState.ErrorOrOfflineFallback);
        }
        private void OnBreath(BreathSignal signal)
        {
            if (IsPaused || !IsCreating) return;
            seed.SetBreath(signal); breathAmount = signal.Phase == BreathPhase.Inhale ? signal.Progress : signal.Phase == BreathPhase.Exhale ? 1 - signal.Progress : 0;
            string next = signal.Phase == BreathPhase.Inhale ? "A gentle inhale, if comfortable…" : "Soften into an easy exhale…";
            if (next != breathText) { breathText = next; Render(); }
        }
        private void OnCycle()
        {
            if (IsPaused || !IsCreating || settling) return;
            cycles++;
            if (State == CreationSessionState.RevealingCreation) reveal.SetProgress((float)++formedCycles / revealCycles);
            else TryReveal();
            Render();
        }
        private bool IsCreating => State == CreationSessionState.GeneratingModel || State == CreationSessionState.BreathingAndCharging || State == CreationSessionState.ModelReady || State == CreationSessionState.RevealingCreation;
        private void TryReveal()
        {
            if (!modelReady || IsPaused || cycles < chargeCycles || candidate == null || State == CreationSessionState.RevealingCreation) return;
            foreach (var renderer in candidate.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            candidate.SetActive(true);
            if (!reveal.Prepare(candidate)) { Fail("The model cannot be revealed safely. You can use the offline creation."); return; }
            foreach (var renderer in candidate.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            formedCycles = 0; SetState(CreationSessionState.RevealingCreation);
        }
        private void OnRevealed()
        {
            if (settling || candidate == null || State != CreationSessionState.RevealingCreation) return;
            settling = true; breathing.Stop(); seed.Hide(); audioController.PlayCreationChime();
            var model = candidate; var anchor = completedAnchors[CompletedCount % completedAnchors.Length];
            if (completed.Count >= completedAnchors.Length) { Destroy(completed[0]); completed.RemoveAt(0); }
            StartOperation(async token => {
                Vector3 start = model.transform.position, offset = model.transform.localPosition; Quaternion startRotation = model.transform.rotation, endRotation = anchor.rotation * model.transform.localRotation;
                float time = 0;
                while (time < settleSeconds)
                {
                    token.ThrowIfCancellationRequested(); if (!IsPaused) time += Time.deltaTime;
                    float t = Mathf.SmoothStep(0, 1, time / settleSeconds); model.transform.SetPositionAndRotation(Vector3.Lerp(start, anchor.TransformPoint(offset), t), Quaternion.Slerp(startRotation, endRotation, t));
                    await Task.Yield();
                }
                token.ThrowIfCancellationRequested(); model.transform.SetParent(anchor, true); completed.Add(model); CompletedCount++; candidate = null;
                reveal.Cancel(); settling = modelReady = false; SetState(CreationSessionState.CreationComplete);
            });
        }
        public void TogglePause() { if (IsCreating) SetPaused(!IsPaused); }
        private void SetPaused(bool value) { IsPaused = value; breathing.SetPaused(value); seed.SetPaused(value); reveal.SetPaused(value); environmentReaction.SetPaused(value); companion.SetPaused(value); audioController.SetPaused(value); Render(); }
        public void CancelCreation() { CancelWork(); proposal = null; status = "Creation cancelled locally. Any submitted remote task may continue billing; it will not be automatically replaced."; SetState(CreationSessionState.WaitingForRequest, false); }
        private void CancelWork()
        {
            revision++; operation?.Cancel(); operation = null; CancelRemote(jobId, activeProposalId); jobId = activeProposalId = "";
            if (breathing != null) breathing.Stop(); if (reveal != null) reveal.Cancel(); if (seed != null) seed.Hide(); if (candidate != null) Destroy(candidate); candidate = null; modelReady = settling = false;
            if (IsConfigured) SetPaused(false);
        }
        private async void CancelRemote(string id, string proposalId)
        {
            if (loader == null || !backendJob || string.IsNullOrEmpty(id) && string.IsNullOrEmpty(proposalId)) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await loader.SendCreationRequestAsync(!string.IsNullOrEmpty(id) ? "/api/creations/" + id + "/cancel" : "/api/proposals/" + proposalId + "/cancel", "{}", timeout.Token); }
            catch { /* Local cancellation remains effective. No paid retries are sent. */ }
        }
        private void Wellness() { CancelWork(); SceneManager.LoadSceneAsync(wellnessSceneName); }
        private void SetState(CreationSessionState value, bool clear = true) { State = value; if (clear) status = ""; Render(); }
        private void Render()
        {
            if (presentation == null || !presentation.IsConfigured) return;
            string mode = live ? (proposal != null && proposal.Source == "offline" ? "Backend • deterministic companion (OpenAI unavailable)" : "Backend mode • paid generation only after confirmation") : "OFFLINE DEMO • deterministic companion • Prebuilt demo cat";
            string body, primary = "", secondary = "";
            bool entering = false;
            switch (State)
            {
                case CreationSessionState.Welcome: body = "Welcome to your little world of imagination!\nWhat would you like to create today?"; primary = "Create a dream"; break;
                case CreationSessionState.WaitingForRequest: body = string.IsNullOrEmpty(status) ? "Enter one little dream, then ask the rabbit.\nTry: I want a little sleeping fox." : status; primary = "Ask rabbit"; entering = true; break;
                case CreationSessionState.UnderstandingRequest: body = "Let me imagine that with you…"; secondary = "Cancel"; break;
                case CreationSessionState.AwaitingConfirmation:
                    body = live && !liveGenerationAvailable ? "That creation isn’t available right now. New creations are switched off.\nTry a sleeping fox, edit your dream, or choose the prepared demo cat." :
                        proposal.Response + (live ? "\nConfirming submits one paid Tripo task." : "\nThis demo forms the Prebuilt demo cat, not a generated version of your request.");
                    primary = live && liveGenerationAvailable ? "Create (paid)" : "Form demo cat"; secondary = "Edit request"; break;
                case CreationSessionState.CreationComplete: body = "Look! You made something wonderful.\nTake your time to enjoy it. Would you like to create another?"; primary = "Create another"; break;
                case CreationSessionState.ErrorOrOfflineFallback: body = failure; primary = "New request"; secondary = "Use offline cat"; break;
                default:
                    body = IsPaused ? "Take your time. Rest and breathe in your own way.\nResume whenever you’re ready." :
                        (elapsed >= standardGuidanceSeconds && !modelReady ? "Your little dream is still growing. Let’s enjoy another gentle breath.\n" : "Let’s plant your dream and help it grow with every breath.\n") + breathText + "\n" +
                        (State == CreationSessionState.RevealingCreation ? "Motes are finding your creation’s shape." : modelReady ? "Your model is ready. A few gentle cycles help the seed gather." : status);
                    primary = IsPaused ? "Resume" : "Pause / Rest"; secondary = "Cancel"; break;
            }
            body = (body ?? "").Replace("<", "‹").Replace(">", "›");
            presentation.Present("<size=30>" + body + "</size>\n\n<size=22>" + mode + "\nGuided cycles are participation events, not measured breathing.</size>", primary, secondary, entering);
        }
        private void Update()
        {
            if (!IsConfigured) return;
            if (IsCreating && !IsPaused) { elapsed += Time.deltaTime; TryReveal(); }
            float activity = IsCreating ? 1 : .12f;
            environmentReaction.SetReaction(breathAmount, activity); companion.SetReaction(breathAmount, activity); audioController.SetActivityLevel(.94f + breathAmount * .06f);
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand); bool menu = left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool pressed) && pressed;
            if (menu && !menuHeld) TogglePause(); menuHeld = menu;
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand); bool a = right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool button) && button;
            if (a && !restartHeld && State == CreationSessionState.CreationComplete) NewRequest(); restartHeld = a;
        }
        private void OnApplicationPause(bool paused) { if (paused && IsCreating && !IsPaused) TogglePause(); }
        private void OnDisable()
        {
            if (presentation != null) { presentation.PrimaryRequested -= Primary; presentation.SecondaryRequested -= Secondary; presentation.WellnessRequested -= Wellness; }
            if (breathing != null) { breathing.Signal -= OnBreath; breathing.ExhaleCompleted -= OnCycle; }
            if (reveal != null) reveal.Revealed -= OnRevealed; CancelWork();
        }
    }
}
