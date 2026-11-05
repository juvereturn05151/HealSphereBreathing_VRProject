using System;

namespace HealSphere.Breathing
{
    public enum BreathPhase { Rest, Inhale, Exhale, Calibrating, Unavailable }

    public readonly struct BreathSignal
    {
        public BreathPhase Phase { get; }
        public float Progress { get; }
        public BreathSignal(BreathPhase phase, float progress) { Phase = phase; Progress = progress; }
    }

    public interface IBreathingProvider
    {
        event Action<BreathSignal> Signal;
        event Action ExhaleCompleted;
        bool IsAvailable { get; }
        void Begin();
        void Stop();
        void SetPaused(bool paused);
    }

    /// <summary>Deterministic clock with no hold phase, scoring, or failed cycles.</summary>
    public sealed class GuidedBreathingClock
    {
        public BreathPhase Phase { get; private set; } = BreathPhase.Inhale;
        public float Progress => elapsed / (Phase == BreathPhase.Inhale ? inhale : exhale);
        private readonly float inhale;
        private readonly float exhale;
        private float elapsed;

        public GuidedBreathingClock(float inhaleSeconds, float exhaleSeconds)
        {
            inhale = Math.Max(0.5f, inhaleSeconds);
            exhale = Math.Max(0.5f, exhaleSeconds);
        }

        public bool Advance(float delta)
        {
            // A stalled frame never awards multiple cycles or demands catch-up breathing.
            elapsed += Math.Max(0, Math.Min(delta, 0.25f));
            if (elapsed < (Phase == BreathPhase.Inhale ? inhale : exhale)) return false;
            bool completed = Phase == BreathPhase.Exhale;
            elapsed = 0;
            Phase = completed ? BreathPhase.Inhale : BreathPhase.Exhale;
            return completed;
        }
    }

    /// <summary>Expansion/contraction hysteresis for a motion proxy; never a respiration measurement.</summary>
    public sealed class MotionBreathGate
    {
        private bool expanded;
        private float phaseStarted;
        public bool IsExpanded => expanded;
        public void Reset() { expanded = false; phaseStarted = 0; }
        public bool Advance(float displacement, float time, float threshold, float minimumPhaseSeconds)
        {
            if (!expanded && displacement >= threshold && time - phaseStarted >= minimumPhaseSeconds)
            { expanded = true; phaseStarted = time; return false; }
            if (expanded && displacement <= -threshold && time - phaseStarted >= minimumPhaseSeconds)
            { expanded = false; phaseStarted = time; return true; }
            return false;
        }
    }
}
