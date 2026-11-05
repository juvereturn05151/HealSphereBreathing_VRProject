using UnityEngine;

namespace HealSphere.Breathing
{
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource chimeSource;
        [SerializeField] private AudioClip ambienceClip;
        [SerializeField] private AudioClip creationChime;
        [SerializeField, Range(0, 1)] private float masterVolume = 0.35f;
        [SerializeField, Min(0.5f)] private float fadeSeconds = 2f;
        private float gain;
        private float target;
        private float currentFadeSeconds;
        private float activityLevel = 1;
        private bool paused;
        public bool IsConfigured => ambienceSource != null && chimeSource != null && ambienceClip != null && creationChime != null;
        public void StartAmbience()
        {
            target = 1;
            currentFadeSeconds = fadeSeconds;
            if (ambienceSource == null || ambienceClip == null) return;
            ambienceSource.clip = ambienceClip; ambienceSource.loop = true;
            if (!ambienceSource.isPlaying) { gain = 0; ambienceSource.volume = 0; ambienceSource.Play(); }
        }
        public void PlayCreationChime()
        { if (chimeSource != null && creationChime != null) chimeSource.PlayOneShot(creationChime, masterVolume * 0.65f); }
        public void SetMasterVolume(float value) => masterVolume = Mathf.Clamp01(value);
        public void FadeAmbience() => target = 0;
        public void FadeAmbience(float duration) { currentFadeSeconds = Mathf.Max(0.5f, duration); target = 0; }
        public void SetActivityLevel(float value) => activityLevel = Mathf.Clamp(value, 0.8f, 1);
        public void SetPaused(bool value)
        {
            if (paused == value) return; paused = value;
            if (ambienceSource != null) { if (value) ambienceSource.Pause(); else ambienceSource.UnPause(); }
            if (chimeSource != null) { if (value) chimeSource.Pause(); else chimeSource.UnPause(); }
        }
        private void Update()
        {
            if (paused) return;
            gain = Mathf.MoveTowards(gain, target, Time.deltaTime / Mathf.Max(0.5f, currentFadeSeconds > 0 ? currentFadeSeconds : fadeSeconds));
            if (ambienceSource != null) { ambienceSource.volume = gain * masterVolume * activityLevel; if (gain == 0 && target == 0 && ambienceSource.isPlaying) ambienceSource.Stop(); }
        }
        private void OnDisable() { if (ambienceSource != null) ambienceSource.Stop(); if (chimeSource != null) chimeSource.Stop(); gain = target = 0; }
    }
}
