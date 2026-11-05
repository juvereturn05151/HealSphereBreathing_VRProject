using UnityEngine;

namespace HealSphere.Wellness
{
    [CreateAssetMenu(menuName = "HealSphere/A Little Moment Configuration")]
    public sealed class ExperienceConfiguration : ScriptableObject
    {
        [Header("Active Experience Seconds (Pauses Excluded)")]
        [SerializeField, Min(1)] private float arrivalSeconds = 15;
        [SerializeField, Min(1)] private float introductionSeconds = 10;
        [SerializeField, Min(1)] private float breathingSeconds = 85;
        [SerializeField, Min(1)] private float endingSeconds = 10;
        [Header("A Suggestion, Not A Breathing Measurement")]
        [SerializeField, Min(0.5f)] private float inhaleSeconds = 4;
        [SerializeField, Min(0.5f)] private float exhaleSeconds = 6;
        [Header("Subtle Reactions")]
        [SerializeField, Range(0, 0.12f)] private float companionRise = 0.055f;
        [SerializeField, Range(0, 0.03f)] private float idleFloat = 0.012f;
        [SerializeField, Range(0, 0.5f)] private float treeEmission = 0.18f;
        [SerializeField, Range(0, 0.1f)] private float lightWarmth = 0.04f;
        [SerializeField, Range(32, 256)] private int moteCount = 128;
        [SerializeField, Range(0.005f, 0.02f)] private float moteSize = 0.009f;
        [SerializeField, Range(0, 1)] private float masterVolume = 0.28f;
        public float ArrivalSeconds => arrivalSeconds;
        public float IntroductionSeconds => introductionSeconds;
        public float BreathingSeconds => breathingSeconds;
        public float EndingSeconds => endingSeconds;
        public float InhaleSeconds => inhaleSeconds;
        public float ExhaleSeconds => exhaleSeconds;
        public float CompanionRise => companionRise;
        public float IdleFloat => idleFloat;
        public float TreeEmission => treeEmission;
        public float LightWarmth => lightWarmth;
        public int MoteCount => moteCount;
        public float MoteSize => moteSize;
        public float MasterVolume => masterVolume;
        public float TotalSeconds => arrivalSeconds + introductionSeconds + breathingSeconds + endingSeconds;
        private void OnValidate()
        {
            arrivalSeconds = Mathf.Max(1, arrivalSeconds); introductionSeconds = Mathf.Max(1, introductionSeconds);
            breathingSeconds = Mathf.Max(1, breathingSeconds); endingSeconds = Mathf.Max(1, endingSeconds);
            inhaleSeconds = Mathf.Max(0.5f, inhaleSeconds); exhaleSeconds = Mathf.Max(0.5f, exhaleSeconds);
            moteCount = Mathf.Clamp(moteCount, 32, 256);
        }
    }
}
