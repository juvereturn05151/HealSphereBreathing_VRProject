using System;

namespace HealSphere.Wellness
{
    public enum ExperiencePhase { Ready, Arrival, Introduction, Breathing, Ending, Completed }

    /// <summary>Pure active-time sequencing. No score, accuracy check, required interaction or sensor input.</summary>
    public sealed class ExperienceTimeline
    {
        private readonly double arrival;
        private readonly double introduction;
        private readonly double breathing;
        private readonly double ending;
        private double elapsed;
        public ExperiencePhase Phase { get; private set; } = ExperiencePhase.Ready;
        public bool Paused { get; private set; }
        public float ElapsedSeconds => (float)elapsed;
        public float TotalSeconds => (float)(arrival + introduction + breathing + ending);
        public float PhaseProgress
        {
            get
            {
                double start = Phase == ExperiencePhase.Introduction ? arrival : Phase == ExperiencePhase.Breathing ? arrival + introduction :
                    Phase == ExperiencePhase.Ending ? arrival + introduction + breathing : 0;
                double duration = Phase == ExperiencePhase.Arrival ? arrival : Phase == ExperiencePhase.Introduction ? introduction : Phase == ExperiencePhase.Breathing ? breathing : ending;
                return Phase == ExperiencePhase.Completed ? 1 : (float)Math.Max(0, Math.Min(1, (elapsed - start) / duration));
            }
        }
        public ExperienceTimeline(float arrivalSeconds, float introductionSeconds, float breathingSeconds, float endingSeconds)
        {
            arrival = Math.Max(1, arrivalSeconds); introduction = Math.Max(1, introductionSeconds);
            breathing = Math.Max(1, breathingSeconds); ending = Math.Max(1, endingSeconds);
        }
        public void Restart() { elapsed = 0; Paused = false; Phase = ExperiencePhase.Arrival; }
        public void SetPaused(bool value) => Paused = value;
        public void EndEarly() { if (Phase == ExperiencePhase.Ready || Phase == ExperiencePhase.Completed) return; elapsed = arrival + introduction + breathing; Phase = ExperiencePhase.Ending; Paused = false; }
        public bool Advance(float deltaTime)
        {
            if (Paused || Phase == ExperiencePhase.Ready || Phase == ExperiencePhase.Completed) return false;
            var before = Phase;
            elapsed = Math.Min(TotalSeconds, elapsed + Math.Max(0, deltaTime));
            Phase = elapsed >= arrival + introduction + breathing + ending - 0.00001 ? ExperiencePhase.Completed :
                elapsed >= arrival + introduction + breathing - 0.00001 ? ExperiencePhase.Ending :
                elapsed >= arrival + introduction - 0.00001 ? ExperiencePhase.Breathing :
                elapsed >= arrival - 0.00001 ? ExperiencePhase.Introduction : ExperiencePhase.Arrival;
            return before != Phase;
        }
    }
}
