using HealSphere.Breathing;
using UnityEngine;

namespace HealSphere.Wellness
{
    public sealed class ExperienceDirector : MonoBehaviour
    {
        [SerializeField] private ExperienceConfiguration configuration;
        [SerializeField] private GuidedBreathingProvider breathing;
        [SerializeField] private EnvironmentReactionController environmentReaction;
        [SerializeField] private FloatingCompanionController companion;
        [SerializeField] private AudioManager audioController;
        [SerializeField] private ExperiencePresentationController presentation;
        [SerializeField] private VRInteractionController interaction;
        [SerializeField] private bool startAutomatically = true;
        public ExperiencePhase Phase => timeline?.Phase ?? ExperiencePhase.Ready;
        public bool IsPaused => timeline?.Paused ?? false;
        public float ElapsedSeconds => timeline?.ElapsedSeconds ?? 0;
        public bool IsConfigured => configuration != null && breathing != null && environmentReaction != null && environmentReaction.IsConfigured &&
            companion != null && companion.IsConfigured && audioController != null && audioController.IsConfigured && presentation != null && presentation.IsConfigured && interaction != null && interaction.IsConfigured;
        private ExperienceTimeline timeline;
        private float normalizedBreath;
        private bool bound;
        private void Start() { if (startAutomatically) StartExperience(); }
        public void StartExperience()
        {
            if (!Application.isPlaying || Phase != ExperiencePhase.Ready) return;
            if (!IsConfigured)
            {
                Debug.LogError("HealSphere: assign all scene references and the visually authored MomentCanvas before starting. No UI fallback is created.", this); return;
            }
            Bind();
            timeline = new ExperienceTimeline(configuration.ArrivalSeconds, configuration.IntroductionSeconds, configuration.BreathingSeconds, configuration.EndingSeconds);
            RestartExperience();
        }
        private void Bind()
        {
            if (bound) return;
            breathing.Signal += OnBreath; presentation.PauseRequested += TogglePause;
            presentation.RestartRequested += RestartExperience; interaction.BellyPlacementChanged += presentation.SetNearBelly;
            bound = true;
        }
        public void RestartExperience()
        {
            if (timeline == null) return;
            breathing.Stop(); breathing.ConfigureTiming(configuration.InhaleSeconds, configuration.ExhaleSeconds);
            timeline.Restart(); normalizedBreath = 0;
            environmentReaction.ResetReaction(); companion.RestartMotion(); ApplyPaused(false);
            audioController.SetMasterVolume(configuration.MasterVolume); audioController.StartAmbience();
            ChangePhase();
        }
        public void TogglePause()
        {
            if (timeline == null || Phase == ExperiencePhase.Completed || Phase == ExperiencePhase.Ready) return;
            timeline.SetPaused(!timeline.Paused); ApplyPaused(timeline.Paused);
        }
        private void ApplyPaused(bool paused)
        {
            breathing.SetPaused(paused); environmentReaction.SetPaused(paused); companion.SetPaused(paused);
            audioController.SetPaused(paused); presentation.SetPaused(paused);
        }
        public void FinishGently()
        {
            if (timeline == null || Phase == ExperiencePhase.Ending || Phase == ExperiencePhase.Completed) return;
            timeline.EndEarly(); ApplyPaused(false); ChangePhase();
        }
        private void Update()
        {
            if (timeline == null) return;
            if (timeline.Advance(Mathf.Min(Time.deltaTime, 0.25f))) ChangePhase();
            if (timeline.Paused) return;
            float activity = Phase == ExperiencePhase.Breathing ? Mathf.SmoothStep(0, 1, Mathf.Min(1, timeline.PhaseProgress * configuration.BreathingSeconds / 3)) :
                Phase == ExperiencePhase.Ending ? 1 - Mathf.SmoothStep(0, 1, timeline.PhaseProgress) : Phase == ExperiencePhase.Completed ? 0 : 0.12f;
            environmentReaction.SetReaction(normalizedBreath, activity); companion.SetReaction(normalizedBreath, activity);
            audioController.SetActivityLevel(0.94f + normalizedBreath * 0.06f);
        }
        private void ChangePhase()
        {
            presentation.ShowPhase(Phase);
            if (Phase == ExperiencePhase.Breathing) breathing.Begin();
            else if (Phase == ExperiencePhase.Ending)
            { breathing.Stop(); audioController.FadeAmbience(configuration.EndingSeconds); audioController.PlayCreationChime(); }
            else if (Phase == ExperiencePhase.Completed)
            { normalizedBreath = 0; breathing.Stop(); audioController.FadeAmbience(); }
        }
        private void OnBreath(BreathSignal signal)
        {
            if (Phase != ExperiencePhase.Breathing || IsPaused) return;
            normalizedBreath = signal.Phase == BreathPhase.Inhale ? signal.Progress : signal.Phase == BreathPhase.Exhale ? 1 - signal.Progress : normalizedBreath;
            presentation.ShowBreath(signal.Phase == BreathPhase.Inhale);
        }
        private void OnApplicationPause(bool paused) { if (paused && timeline != null && !IsPaused && Phase != ExperiencePhase.Completed) TogglePause(); }
        private void OnDisable()
        {
            if (!bound) return;
            breathing.Signal -= OnBreath; presentation.PauseRequested -= TogglePause;
            presentation.RestartRequested -= RestartExperience; interaction.BellyPlacementChanged -= presentation.SetNearBelly;
            breathing.Stop(); bound = false;
        }
    }
}
