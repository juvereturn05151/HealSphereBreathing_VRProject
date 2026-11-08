using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealSphere.Breathing;
using HealSphere.Tripo;
using HealSphere.Wellness;
using UnityEngine;

namespace HealSphere.Creation
{
    public enum ReflectionPhase { Welcome, IntroBreathing, Animal, Thought1, Thought2, Review, Processing, MissingModels, Revealing, Closing, Complete, Error }
    [DefaultExecutionOrder(-50)]
    public sealed class ReflectionExperienceDirector : MonoBehaviour
    {
        [Header("Existing scene systems")]
        [SerializeField] private CreationSessionDirector creation;
        [SerializeField] private CreationSessionPresenter presentation;
        [SerializeField] private TripoModelLoader backend;
        [SerializeField] private GuidedBreathingProvider breathing;
        [SerializeField] private DreamSeedController seed;
        [SerializeField] private EnvironmentReactionController environmentReaction;
        [SerializeField] private FloatingCompanionController companion;
        [Header("Authored voice controls")]
        [SerializeField] private ReflectionControlsPresenter controls;
        [SerializeField] private RabbitVoiceNarrator narrator;
        [SerializeField] private ReflectionMicrophoneInput microphone;
        [Header("Gentle optional introduction")]
        [SerializeField, Min(.5f)] private float inhaleSeconds = 4;
        [SerializeField, Min(.5f)] private float exhaleSeconds = 6;
        [SerializeField, Range(2, 3)] private int introductionCycles = 2;
        public ReflectionPhase Phase { get; private set; }
        public string Animal => animal; public string Thought1 => thought1; public string Thought2 => thought2;
        public int CompletedModels => creation != null ? creation.CompletedCount : 0;
        public bool IsConfigured => creation != null && creation.IsConfigured && presentation != null && controls != null && controls.IsConfigured && narrator != null && narrator.IsConfigured && microphone != null && microphone.IsConfigured && backend != null && breathing != null && seed != null && environmentReaction != null && companion != null;
        [NonSerialized] private string animal = "", thought1 = "", thought2 = "", subtitle = "", error = "";
        [NonSerialized] private ReflectionPlan plan;
        private CancellationTokenSource session = new CancellationTokenSource(), operation;
        private int introCompleted, designIndex, revision;
        private BreathPhase lastBreath = BreathPhase.Rest;
        private float breathAmount;
        private bool introReady, advancing, lastSpeaking;
        private static readonly string[] OfflineAnimals = { "fox", "cat", "rabbit", "dog", "elephant", "gorilla" };
        [Serializable] private sealed class PlanRequest
        {
            [SerializeField] private string animal, thought1, thought2;
            [SerializeField] private bool offline_demo;
            public PlanRequest(string animal, string one, string two, bool demo) { this.animal = animal; thought1 = one; thought2 = two; offline_demo = demo; }
        }
        private void Awake() { if (creation != null) creation.SetExternalControl(true); }
        private void Start()
        {
            if (!IsConfigured) { Debug.LogError("HealSphere reflection: assign the existing scene systems and Editor-authored microphone/mute/volume controls. No UI is created.", this); return; }
            backend.ReloadLocalConnection(); presentation.PrimaryRequested += Primary; presentation.SecondaryRequested += Secondary; presentation.WellnessRequested += Wellness;
            controls.MicrophoneRequested += microphone.ToggleRecording; controls.MuteRequested += Mute; controls.VolumeRequested += Volume;
            narrator.SubtitleChanged += Subtitle; microphone.Changed += Render; microphone.Transcribed += Transcribed;
            breathing.Signal += OnBreath; breathing.ExhaleCompleted += OnCycle;
            Phase = ReflectionPhase.Welcome; Render();
        }
        public void Primary()
        {
            if (!IsConfigured || microphone.State == MicrophoneInputState.Recording || microphone.State == MicrophoneInputState.Transcribing || microphone.State == MicrophoneInputState.Permission) return;
            switch (Phase) {
                case ReflectionPhase.Welcome: StartIntroduction(); break;
                case ReflectionPhase.IntroBreathing: SkipBreathing(); break;
                case ReflectionPhase.Animal:
                    animal = presentation.RequestText.Trim().ToLowerInvariant();
                    if (animal == "bunny") animal = "rabbit";
                    if (!ValidAnimal(animal)) { error = "Please name one animal, for example fox, cat, rabbit, dog, elephant or gorilla."; Render(); return; }
                    EnterInput(ReflectionPhase.Thought1); break;
                case ReflectionPhase.Thought1: thought1 = presentation.RequestText.Trim(); EnterInput(ReflectionPhase.Thought2); break;
                case ReflectionPhase.Thought2: thought2 = presentation.RequestText.Trim(); SetPhase(ReflectionPhase.Review); break;
                case ReflectionPhase.Review: Interpret(false); break;
                case ReflectionPhase.MissingModels: Interpret(true); break;
                case ReflectionPhase.Revealing: creation.TogglePause(); narrator.StopSpeech(); Render(); break;
                case ReflectionPhase.Closing: case ReflectionPhase.Complete: RestartExperience(); break;
                case ReflectionPhase.Error: Interpret(false); break;
            }
        }
        public void Secondary()
        {
            if (!IsConfigured) return;
            if (Phase == ReflectionPhase.IntroBreathing) { SkipBreathing(); return; }
            if (Phase == ReflectionPhase.Animal) { microphone.Cancel(); narrator.StopSpeech(); SetPhase(ReflectionPhase.Welcome); return; }
            if (Phase == ReflectionPhase.Thought1) { thought1 = presentation.RequestText.Trim(); EnterInput(ReflectionPhase.Animal); return; }
            if (Phase == ReflectionPhase.Thought2) { thought2 = presentation.RequestText.Trim(); EnterInput(ReflectionPhase.Thought1); return; }
            if (Phase == ReflectionPhase.Review || Phase == ReflectionPhase.MissingModels) { CancelPlan(); EnterInput(ReflectionPhase.Thought2); return; }
            if (Phase == ReflectionPhase.Error) { if (Array.IndexOf(OfflineAnimals, animal) < 0) EnterInput(ReflectionPhase.Animal); else Interpret(true); return; }
            RestartExperience();
        }
        private async void StartIntroduction()
        {
            CancelOperation(); int version = revision; var source = CancellationTokenSource.CreateLinkedTokenSource(session.Token); operation = source;
            SetPhase(ReflectionPhase.IntroBreathing); introCompleted = 0; introReady = false; lastBreath = BreathPhase.Rest;
            try {
                await narrator.SpeakAsync("welcome", "Hello there. I'm happy you're here. We can take things slowly.", "", source.Token);
                try { await narrator.PrepareBreathingAsync(source.Token); } catch { /* Timed visuals and subtitles remain available. */ }
                source.Token.ThrowIfCancellationRequested(); if (version != revision) return;
                introReady = true; seed.Show(); breathing.ConfigureTiming(inhaleSeconds, exhaleSeconds); breathing.Begin();
            } catch (OperationCanceledException) { }
            finally { if (ReferenceEquals(operation, source)) operation = null; source.Dispose(); }
        }
        public void SkipBreathing()
        {
            if (Phase != ReflectionPhase.IntroBreathing) return;
            CancelOperation(); introReady = false; breathing.Stop(); seed.Hide(); EnterInput(ReflectionPhase.Animal);
        }
        private void EnterInput(ReflectionPhase phase)
        {
            microphone.Cancel(); error = ""; SetPhase(phase);
            presentation.SetRequestText(phase == ReflectionPhase.Animal ? animal : phase == ReflectionPhase.Thought1 ? thought1 : thought2);
            string cue = phase == ReflectionPhase.Animal ? "animal" : phase == ReflectionPhase.Thought1 ? "thought1" : "thought2";
            Speak(cue, Question(phase));
        }
        private async void Speak(string cue, string text, string id = "") { await narrator.SpeakAsync(cue, text, id, session.Token); Render(); }
        private async void Interpret(bool demo)
        {
            CancelOperation(); CancelPlan(); microphone.Cancel();
            if (demo && Array.IndexOf(OfflineAnimals, animal) < 0) { error = "Offline illustrations support fox, cat, rabbit, dog, elephant and gorilla. Go back to edit the animal."; SetPhase(ReflectionPhase.Error); return; }
            SetPhase(ReflectionPhase.Processing); var source = CancellationTokenSource.CreateLinkedTokenSource(session.Token); source.CancelAfter(TimeSpan.FromSeconds(60)); operation = source; int version = revision;
            try {
                await narrator.SpeakAsync("generation", "Thank you. We can give these little friends time to take shape.", "", source.Token);
                source.Token.ThrowIfCancellationRequested();
                if (demo || !backend.LiveConnectionRequested || !backend.HasBackendConnection) {
                    if (Array.IndexOf(OfflineAnimals, animal) < 0) throw new InvalidOperationException("That animal needs the backend. Offline choices: fox, cat, rabbit, dog, elephant or gorilla.");
                    plan = ReflectionPlan.Offline(animal);
                } else {
                    var bytes = await backend.SendReflectionRequestAsync("/api/reflection/plans", JsonUtility.ToJson(new PlanRequest(animal, thought1, thought2, demo)), source.Token);
                    source.Token.ThrowIfCancellationRequested(); plan = JsonUtility.FromJson<ReflectionPlan>(Encoding.UTF8.GetString(bytes));
                    if (plan == null || !plan.IsValid) throw new InvalidOperationException("The reflection plan was incomplete. You can use offline illustrations.");
                }
                if (version != revision) return;
                if (plan.Missing > 0) { SetPhase(ReflectionPhase.MissingModels); WatchAuthorizedModels(plan.Id); return; }
                creation.ResetPreparedCreations(); designIndex = 0; BeginDesign();
            } catch (OperationCanceledException) { if (version == revision) { error = "The request took too long. Your answers stay here; you can try again or use offline illustrations."; SetPhase(ReflectionPhase.Error); } }
              catch (Exception e) { if (version == revision) { error = e is InvalidOperationException ? e.Message : "The reflection service is unavailable. Typed answers and offline illustrations still work."; SetPhase(ReflectionPhase.Error); } }
            finally { if (ReferenceEquals(operation, source)) operation = null; source.Dispose(); }
        }
        private async void BeginDesign()
        {
            advancing = true; lastBreath = BreathPhase.Rest; var design = plan.Designs[designIndex]; int version = revision;
            SetPhase(ReflectionPhase.Revealing);
            await narrator.SpeakAsync(design.Role, design.Narration, plan.Id, session.Token);
            if (version != revision || Phase != ReflectionPhase.Revealing) return;
            creation.BeginPreparedCreation(design.Endpoint, animal, design.Role, design.Name, string.IsNullOrEmpty(plan.Id), design.IsPrebuilt);
            advancing = false;
        }
        private async void CompleteDesign()
        {
            advancing = true;
            await Task.Yield();
            if (Phase != ReflectionPhase.Revealing) return;
            designIndex++;
            if (designIndex < 3) { BeginDesign(); return; }
            SetPhase(ReflectionPhase.Closing);
            await narrator.SpeakAsync("closing", plan.Closing, plan.Id, session.Token);
            if (Phase == ReflectionPhase.Closing) SetPhase(ReflectionPhase.Complete);
        }
        private async void WatchAuthorizedModels(string id)
        {
            int version = revision;
            using var watch = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
            try {
                while (version == revision && Phase == ReflectionPhase.MissingModels && plan?.Id == id) {
                    await Task.Delay(TimeSpan.FromSeconds(3), watch.Token);
                    if (version != revision || Phase != ReflectionPhase.MissingModels || plan?.Id != id) return;
                    var bytes = await backend.SendReflectionRequestAsync("/api/reflection/plans/" + id, null, watch.Token);
                    if (version != revision || Phase != ReflectionPhase.MissingModels || plan?.Id != id) return;
                    var updated = JsonUtility.FromJson<ReflectionPlan>(Encoding.UTF8.GetString(bytes));
                    if (updated == null || !updated.IsValid) return;
                    plan = updated;
                    if (plan.Missing == 0) { creation.ResetPreparedCreations(); designIndex = 0; BeginDesign(); return; }
                    Render();
                }
            } catch { /* No POST or paid retry. The player can choose local illustrations or edit answers. */ }
        }
        private void OnBreath(BreathSignal signal)
        {
            if (Phase != ReflectionPhase.IntroBreathing && Phase != ReflectionPhase.Revealing || signal.Phase == BreathPhase.Rest) return;
            if (Phase == ReflectionPhase.IntroBreathing && !introReady) return;
            breathAmount = signal.Phase == BreathPhase.Inhale ? signal.Progress : 1 - signal.Progress;
            if (Phase == ReflectionPhase.IntroBreathing) seed.SetBreath(signal);
            if (lastBreath != signal.Phase) {
                lastBreath = signal.Phase; narrator.SpeakBreath(signal.Phase); Render();
            }
        }
        private void OnCycle() { if (Phase == ReflectionPhase.IntroBreathing && introReady && ++introCompleted >= introductionCycles) SkipBreathing(); }
        private void Subtitle(string text) { subtitle = text; Render(); }
        private void Transcribed(string text) { if (IsInput) presentation.SetRequestText(text); Render(); }
        private void Mute() { narrator.ToggleMute(); Render(); }
        private void Volume() { narrator.CycleVolume(); Render(); }
        private bool IsInput => Phase == ReflectionPhase.Animal || Phase == ReflectionPhase.Thought1 || Phase == ReflectionPhase.Thought2;
        private static bool ValidAnimal(string value) => System.Text.RegularExpressions.Regex.IsMatch(value ?? "", @"^(fox|cat|rabbit|bunny|dog|elephant|gorilla|bird|turtle|deer|panda|bear|otter|horse|dolphin|butterfly)$");
        private static string Question(ReflectionPhase phase) => phase == ReflectionPhase.Animal ? "What animal would you like to see today?" : phase == ReflectionPhase.Thought1 ? "What's something that's been on your mind lately?" : "Thank you for sharing. Is there anything else you've been thinking about?";
        private void SetPhase(ReflectionPhase value)
        {
            Phase = value; subtitle = "";
            if (value != ReflectionPhase.IntroBreathing && value != ReflectionPhase.Revealing) {
                breathAmount = 0; environmentReaction?.SetReaction(0, .15f); companion?.SetReaction(0, .15f);
            }
            Render();
        }
        private void Render()
        {
            if (!IsConfigured) return;
            // Cancellation clears private plan data before the next phase is entered. Microphone/voice
            // events can run synchronously during that transition; keep the last stable presentation.
            if (plan == null && (Phase == ReflectionPhase.MissingModels || Phase == ReflectionPhase.Revealing)) return;
            microphone.InputAllowed = IsInput;
            string body, primary = "", secondary = "";
            switch (Phase) {
                case ReflectionPhase.Welcome: body = "A little moment for you and three animal friends.\nYou choose what to share; personal thoughts are optional.\nWhen connected, answers and recordings are sent to OpenAI."; primary = "Begin gently"; break;
                case ReflectionPhase.IntroBreathing:
                    body = introReady ? (lastBreath == BreathPhase.Inhale ? "An easy breath in, if comfortable…" : "Soften into a gentle breath out…") + "\nYou can return to your normal breathing at any time." : "Take a moment to settle. You can skip the breathing guide.";
                    primary = "Skip breathing"; break;
                case ReflectionPhase.Animal: case ReflectionPhase.Thought1: case ReflectionPhase.Thought2:
                    body = Question(Phase) + (Phase == ReflectionPhase.Animal ? "\nType one animal or use Speak answer." : "\nType, speak, or leave this empty to skip sharing."); primary = Phase == ReflectionPhase.Animal ? "Confirm animal" : "Next / Skip"; secondary = "Back";
                    if (!string.IsNullOrEmpty(microphone.Status)) body += "\n" + microphone.Status;
                    break;
                case ReflectionPhase.Review: body = "Your animal: " + animal + "\nFirst thought: " + (thought1.Length > 0 ? "shared — editable with Back" : "skipped") + "\nSecond thought: " + (thought2.Length > 0 ? "shared — editable with Back" : "skipped") + "\nReady to imagine Reflection, Support and Hope?"; primary = "Imagine friends"; secondary = "Edit answers"; break;
                case ReflectionPhase.Processing: body = "Considering your words gently…\nYour thoughts stay separate from reusable model metadata."; secondary = "Cancel"; break;
                case ReflectionPhase.MissingModels: body = plan.Missing + " design(s) need new models. Paid work needs separate task/credit approval.\nAuthorized models appear when ready, or use neutral " + animal + " illustrations."; primary = "Use illustrations"; secondary = "Edit answers"; break;
                case ReflectionPhase.Revealing:
                    body = (plan.Source == "offline_demo" ? "OFFLINE ILLUSTRATIONS — not personalized AI models\n" : "Your three animal friends\n") + (designIndex + 1) + " / 3 — " + plan.Designs[designIndex].Role + "\n" +
                        (creation.IsPaused ? "Rest in your own rhythm. Resume when ready." : lastBreath == BreathPhase.Inhale ? "An easy breath in…" : "A gentle breath out…");
                    primary = creation.IsPaused ? "Resume" : "Pause / Rest"; secondary = "End session"; break;
                case ReflectionPhase.Closing: case ReflectionPhase.Complete: body = plan?.Closing ?? "Thank you for this little moment."; primary = "Begin again"; break;
                default: body = error + "\nNo replacement paid task was submitted."; primary = "Try again"; secondary = Array.IndexOf(OfflineAnimals, animal) < 0 ? "Edit animal" : "Use illustrations"; break;
            }
            if (!string.IsNullOrEmpty(error) && IsInput) body += "\n" + error;
            string status = (microphone.State == MicrophoneInputState.Recording ? "Listening — recording is active" : microphone.State == MicrophoneInputState.Transcribing ? "Transcribing" : narrator.IsSpeaking ? "Speaking" : "Subtitles available") + " • AI-generated voice";
            string escaped = (body ?? "").Replace("<", "‹").Replace(">", "›");
            string caption = string.IsNullOrEmpty(subtitle) ? "" : "\n<size=24>“" + subtitle.Replace("<", "‹").Replace(">", "›") + "”</size>";
            presentation.Present("<size=29>" + escaped + "</size>" + caption + "\n<size=20>" + status + "\nTimed guidance is not measured breathing.</size>", primary, secondary, IsInput);
            controls.Present(IsInput, microphone.State, narrator.IsMuted, narrator.VoiceVolume);
        }
        public void RestartExperience()
        {
            CancelOperation(); microphone.Cancel(); CancelPlan(); session.Cancel(); session.Dispose(); session = new CancellationTokenSource(); narrator.ClearPrivateSession();
            breathing.Stop(); seed.Hide(); creation.ResetPreparedCreations(); presentation.ClearRequest(); animal = thought1 = thought2 = error = ""; introReady = advancing = false; SetPhase(ReflectionPhase.Welcome);
        }
        private void CancelOperation() { revision++; operation?.Cancel(); operation = null; narrator?.StopSpeech(); }
        private async void CancelPlan()
        {
            var id = plan?.Id; plan = null; narrator?.ClearPrivateSession();
            if (string.IsNullOrEmpty(id) || backend == null) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await backend.SendReflectionRequestAsync("/api/reflection/plans/" + id + "/cancel", "{}", timeout.Token); } catch { }
        }
        private void Wellness() { RestartExperience(); creation.EnterWellness(); }
        private void Update()
        {
            if (!IsConfigured) return;
            if (Phase == ReflectionPhase.IntroBreathing) { environmentReaction.SetReaction(breathAmount, introReady ? 1 : .15f); companion.SetReaction(breathAmount, introReady ? 1 : .15f); }
            if (Phase == ReflectionPhase.Revealing && !advancing) {
                if (creation.State == CreationSessionState.CreationComplete && creation.CompletedCount == designIndex + 1) CompleteDesign();
                if (creation.State == CreationSessionState.ErrorOrOfflineFallback) { error = "That friend could not load. You can retry the saved plan or use offline illustrations."; SetPhase(ReflectionPhase.Error); }
            }
            if (lastSpeaking != narrator.IsSpeaking) { lastSpeaking = narrator.IsSpeaking; Render(); }
        }
        private void OnApplicationPause(bool paused) { if (paused) { microphone?.Cancel(); narrator?.StopSpeech(); if (Phase == ReflectionPhase.IntroBreathing) SkipBreathing(); } }
        private void OnDisable()
        {
            CancelOperation(); microphone?.Cancel(); CancelPlan(); session.Cancel(); session.Dispose();
            if (presentation != null) { presentation.PrimaryRequested -= Primary; presentation.SecondaryRequested -= Secondary; presentation.WellnessRequested -= Wellness; }
            if (controls != null && microphone != null) { controls.MicrophoneRequested -= microphone.ToggleRecording; controls.MuteRequested -= Mute; controls.VolumeRequested -= Volume; }
            if (narrator != null) narrator.SubtitleChanged -= Subtitle;
            if (microphone != null) { microphone.Changed -= Render; microphone.Transcribed -= Transcribed; }
            if (breathing != null) { breathing.Signal -= OnBreath; breathing.ExhaleCompleted -= OnCycle; }
        }
        private void OnValidate() { inhaleSeconds = Mathf.Max(.5f, inhaleSeconds); exhaleSeconds = Mathf.Max(.5f, exhaleSeconds); introductionCycles = Mathf.Clamp(introductionCycles, 2, 3); }
    }
}
