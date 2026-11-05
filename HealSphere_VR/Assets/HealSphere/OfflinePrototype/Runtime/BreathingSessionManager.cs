using UnityEngine;
using UnityEngine.InputSystem;

namespace HealSphere.Breathing
{
    public enum SessionPhase { NotReady, Greeting, Choosing, Breathing, Settling, Reflection, Ended }

    public sealed class BreathingSessionManager : MonoBehaviour
    {
        [Header("Required Scene Components")]
        [SerializeField] private BreathingInputController breathingInput;
        [SerializeField] private CreationManager creations;
        [SerializeField] private DreamSeedController dreamSeed;
        [SerializeField] private CompanionDialogueController companion;
        [SerializeField] private AudioManager audioManager;
        [Header("Session")]
        [SerializeField, Range(1, 12)] private int cyclesPerCreation = 3;
        [SerializeField] private bool beginOnStart = true;
        [SerializeField] private bool enableEditorKeyboard = true;
        public SessionPhase Phase { get; private set; } = SessionPhase.NotReady;
        public bool IsPaused { get; private set; }
        public int CompletedCycles { get; private set; }
        private bool subscribed;

        private void OnEnable()
        {
            if (breathingInput == null || creations == null || companion == null) return;
            breathingInput.ExhaleCompleted += Exhale; breathingInput.Signal += Signal;
            breathingInput.ModeChanged += companion.SetMode; creations.CreationCompleted += CreationFinished;
            companion.ContinueRequested += Continue; companion.ChoiceRequested += ChooseCreation;
            companion.PauseRequested += TogglePause; companion.GuidedRequested += breathingInput.UseGuided;
            companion.MotionRequested += breathingInput.UseMotionProxy; companion.CalibrationRequested += breathingInput.Recalibrate;
            companion.RestartRequested += Restart; companion.EndRequested += EndSession;
            subscribed = true;
        }
        private void Start() { if (beginOnStart) BeginSession(); }
        private void OnDisable()
        {
            if (!subscribed) return;
            breathingInput.ExhaleCompleted -= Exhale; breathingInput.Signal -= Signal;
            breathingInput.ModeChanged -= companion.SetMode; creations.CreationCompleted -= CreationFinished;
            companion.ContinueRequested -= Continue; companion.ChoiceRequested -= ChooseCreation;
            companion.PauseRequested -= TogglePause; companion.GuidedRequested -= breathingInput.UseGuided;
            companion.MotionRequested -= breathingInput.UseMotionProxy; companion.CalibrationRequested -= breathingInput.Recalibrate;
            companion.RestartRequested -= Restart; companion.EndRequested -= EndSession;
            breathingInput.Stop(); creations.SetPaused(true); subscribed = false;
        }
        [ContextMenu("Session/Begin Offline Session")]
        public void BeginSession()
        {
            if (!Application.isPlaying || Phase != SessionPhase.NotReady) return;
            if (!subscribed || dreamSeed == null || !creations.IsConfigured || !breathingInput.IsConfigured || !companion.IsConfigured)
            {
                Debug.LogError("Breathe Into Being: required scene/UI references are missing. Author the world-space panel visually and follow OfflinePrototype/README.md; no fallback UI is created.", this);
                return;
            }
            Phase = SessionPhase.Greeting; IsPaused = false; CompletedCycles = 0;
            dreamSeed.Hide(); creations.SetPaused(false);
            companion.Show("Hello, I’m your little dream companion. Let’s make something gentle together. You can pause or stop whenever you like. Your breath can follow its own comfortable pace.");
            companion.SetMode("Offline guided experience • no microphone, sensors, AI service or health assessment.");
            companion.ShowBreath(new BreathSignal(BreathPhase.Rest, 0));
            audioManager?.StartAmbience(); Refresh();
        }
        public void Continue()
        {
            if (Phase == SessionPhase.Greeting) ShowChoices();
            else if (Phase == SessionPhase.Reflection) EndSession();
        }
        public void ChooseCat() => ChooseCreation(0);
        public void ChooseTree() => ChooseCreation(1);
        public void ChooseMoon() => ChooseCreation(2);
        public void ChooseCreation(int index)
        {
            if (Phase != SessionPhase.Choosing || !creations.BeginCreation(index)) return;
            CompletedCycles = 0; IsPaused = false; Phase = SessionPhase.Breathing;
            dreamSeed.Show(); breathingInput.Begin();
            companion.Show("Let’s give this little dream some shape. The rhythm is only a suggestion. Rest or pause whenever you need; nothing is lost."); Refresh();
        }
        private void Signal(BreathSignal signal)
        {
            if (Phase != SessionPhase.Breathing || IsPaused) return;
            dreamSeed.SetBreath(signal); companion.ShowBreath(signal);
        }
        private void Exhale()
        {
            if (Phase != SessionPhase.Breathing || IsPaused) return;
            CompletedCycles = Mathf.Min(cyclesPerCreation, CompletedCycles + 1);
            creations.SetProgress((float)CompletedCycles / cyclesPerCreation);
            if (CompletedCycles == cyclesPerCreation)
            {
                breathingInput.Stop(); Phase = SessionPhase.Settling;
                companion.Show("There it is. Let it settle into your little world. Breathe naturally.");
            }
            Refresh();
        }
        private void CreationFinished()
        {
            if (Phase != SessionPhase.Settling) return;
            dreamSeed.Hide(); audioManager?.PlayCreationChime();
            if (creations.CompletedCount < 3) { ShowChoices(); return; }
            Phase = SessionPhase.Reflection;
            companion.Show("A cat, a tree, and a moon — three small wonders you gave time to. Perhaps one can remind you of warmth, steadiness, or a little space to dream. Only you decide what they mean. Thank you for this quiet moment together.");
            companion.ShowBreath(new BreathSignal(BreathPhase.Rest, 0)); Refresh();
        }
        private void ShowChoices()
        {
            Phase = SessionPhase.Choosing;
            companion.Show(creations.CompletedCount == 0 ? "Choose a cat, tree, or moon. We’ll make all three, in any order you like." : "Your creation can stay here with us. Which little dream would you like to make next?");
            companion.ShowBreath(new BreathSignal(BreathPhase.Rest, 0)); Refresh();
        }
        public void TogglePause()
        {
            if (Phase != SessionPhase.Breathing && Phase != SessionPhase.Settling) return;
            IsPaused = !IsPaused; breathingInput.SetPaused(IsPaused); creations.SetPaused(IsPaused); dreamSeed.SetPaused(IsPaused); Refresh();
        }
        public void EndSession()
        {
            if (Phase == SessionPhase.NotReady || Phase == SessionPhase.Ended) return;
            breathingInput.Stop(); creations.CancelActive(); dreamSeed.Hide(); IsPaused = false;
            Phase = SessionPhase.Ended;
            companion.Show("Our quiet moment is complete. Your finished creations are here to enjoy. Take your time, and return whenever you wish.");
            companion.ShowBreath(new BreathSignal(BreathPhase.Rest, 0)); audioManager?.FadeAmbience(); Refresh();
        }
        public void Restart()
        {
            if (Phase != SessionPhase.Ended) return;
            creations.ResetCreations(); Phase = SessionPhase.NotReady; BeginSession();
        }
        private void Refresh()
        {
            companion.SetProgress(CompletedCycles, cyclesPerCreation, creations.CompletedCount);
            companion.SetControls(Phase == SessionPhase.Greeting, Phase == SessionPhase.Choosing,
                Phase == SessionPhase.Breathing || Phase == SessionPhase.Settling, IsPaused,
                Phase == SessionPhase.Reflection, Phase == SessionPhase.Ended, Phase == SessionPhase.Breathing,
                creations.HasCreated(0), creations.HasCreated(1), creations.HasCreated(2));
        }
        private void OnApplicationPause(bool paused) { if (paused && !IsPaused) TogglePause(); }
        private void Update()
        {
            if (!Application.isEditor || !enableEditorKeyboard || Keyboard.current == null) return;
            var keys = Keyboard.current;
            if (keys.enterKey.wasPressedThisFrame) Continue();
            if (keys.digit1Key.wasPressedThisFrame) ChooseCat();
            if (keys.digit2Key.wasPressedThisFrame) ChooseTree();
            if (keys.digit3Key.wasPressedThisFrame) ChooseMoon();
            if (keys.spaceKey.wasPressedThisFrame && Phase == SessionPhase.Breathing) breathingInput.DebugCompleteExhale();
            if (keys.pKey.wasPressedThisFrame) TogglePause();
            if (keys.gKey.wasPressedThisFrame) breathingInput.UseGuided();
            if (keys.mKey.wasPressedThisFrame) breathingInput.UseMotionProxy();
            if (keys.cKey.wasPressedThisFrame) breathingInput.Recalibrate();
            if (keys.escapeKey.wasPressedThisFrame) EndSession();
            if (keys.rKey.wasPressedThisFrame) Restart();
        }
        private void OnValidate() => cyclesPerCreation = Mathf.Clamp(cyclesPerCreation, 1, 12);
    }
}
