using System;
using UnityEngine;

namespace HealSphere.Breathing
{
    public sealed class GuidedBreathingProvider : MonoBehaviour, IBreathingProvider
    {
        [SerializeField, Min(0.5f)] private float inhaleSeconds = 4f;
        [SerializeField, Min(0.5f)] private float exhaleSeconds = 5f;
        public event Action<BreathSignal> Signal;
        public event Action ExhaleCompleted;
        public bool IsAvailable => isActiveAndEnabled;
        private GuidedBreathingClock clock;
        private bool running;
        private bool paused;
        /// <summary>Configure a new timed session without changing any scene asset.</summary>
        public void ConfigureTiming(float inhale, float exhale)
        {
            inhaleSeconds = Mathf.Max(0.5f, inhale); exhaleSeconds = Mathf.Max(0.5f, exhale);
        }
        public void Begin() { clock = new GuidedBreathingClock(inhaleSeconds, exhaleSeconds); running = true; paused = false; }
        public void Stop() { running = false; Signal?.Invoke(new BreathSignal(BreathPhase.Rest, 0)); }
        public void SetPaused(bool value) => paused = value;
        private void Update()
        {
            if (!running || paused) return;
            bool complete = clock.Advance(Time.deltaTime);
            Signal?.Invoke(new BreathSignal(clock.Phase, clock.Progress));
            if (complete) ExhaleCompleted?.Invoke();
        }
        private void OnDisable() => Stop();
        private void OnValidate() { inhaleSeconds = Mathf.Max(0.5f, inhaleSeconds); exhaleSeconds = Mathf.Max(0.5f, exhaleSeconds); }
    }
}
