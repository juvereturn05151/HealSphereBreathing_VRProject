using UnityEngine;

namespace HealSphere.Meadow
{
    /// <summary>Plays user-supplied ambience through existing, Inspector-assigned sources.</summary>
    public sealed class MeadowAmbience : MonoBehaviour
    {
        [Header("Existing Audio Sources")]
        [SerializeField] private AudioSource windSource;
        [SerializeField] private AudioSource birdsSource;
        [SerializeField] private AudioSource musicSource;

        [Header("Assign Real Audio Assets (Optional)")]
        [SerializeField] private AudioClip windClip;
        [SerializeField] private AudioClip birdsClip;
        [SerializeField] private AudioClip musicClip;

        [Header("Comfort")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float birdsVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.15f;

        private void OnEnable()
        {
            Play(windSource, windClip, windVolume);
            Play(birdsSource, birdsClip, birdsVolume);
            Play(musicSource, musicClip, musicVolume);
        }

        private void OnDisable()
        {
            if (windSource != null) windSource.Stop();
            if (birdsSource != null) birdsSource.Stop();
            if (musicSource != null) musicSource.Stop();
        }

        /// <summary>Allows a future, visually authored settings panel to adjust all ambience.</summary>
        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        private void OnValidate() => ApplyVolumes();

        private void ApplyVolumes()
        {
            if (windSource != null) windSource.volume = masterVolume * windVolume;
            if (birdsSource != null) birdsSource.volume = masterVolume * birdsVolume;
            if (musicSource != null) musicSource.volume = masterVolume * musicVolume;
        }

        private void Play(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null || clip == null) return;
            source.clip = clip;
            source.loop = true;
            source.volume = masterVolume * volume;
            source.Play();
        }
    }
}
