using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealSphere.Breathing;
using HealSphere.Tripo;
using UnityEngine;

namespace HealSphere.Creation
{
    /// <summary>One AudioSource, no clip stacking. Personalized audio is memory-only and discarded with the session.</summary>
    public sealed class RabbitVoiceNarrator : MonoBehaviour
    {
        [SerializeField] private TripoModelLoader backend;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioManager ambience;
        [SerializeField, Range(0, 1)] private float voiceVolume = .65f;
        [SerializeField] private bool muted;
        [SerializeField, Range(2, 30)] private float voiceTimeoutSeconds = 8;
        public bool IsSpeaking => voiceSource != null && voiceSource.isPlaying;
        public bool IsMuted => muted;
        public float VoiceVolume => voiceVolume;
        public bool IsConfigured => backend != null && voiceSource != null && ambience != null;
        public event Action<string> SubtitleChanged;
        private readonly Dictionary<string, AudioClip> cached = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, string> transcripts = new Dictionary<string, string>();
        private CancellationTokenSource speech;
        private bool recording;
        private bool serviceUnavailable;
        [Serializable] private sealed class Request { [SerializeField] private string cue, plan_id; public Request(string cue, string plan) { this.cue = cue; plan_id = plan; } }
        [Serializable] private sealed class Reply { [SerializeField] private string id, text; public string Id => id; public string Text => text; }
        public async Task PrepareBreathingAsync(CancellationToken token)
        {
            if (!backend.LiveConnectionRequested || muted) return;
            foreach (var cue in new[] { "breathe_in", "breathe_out" }) await GetClip(cue, "", token);
        }
        internal async Task PrepareEndingAsync(CancellationToken token)
        {
            if (!IsConfigured || muted) return;
            try
            {
                foreach (string cue in new[] { "animal_final_breath", "animal_awakes", "animal_wonder" })
                    await GetClip(cue, "", token);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { /* The timed ending continues with captions. */ }
        }
        private async Task<AudioClip> GetClip(string cue, string plan, CancellationToken token)
        {
            var key = plan + ":" + cue; if (cached.TryGetValue(key, out var clip)) return clip;
            if (serviceUnavailable || !backend.HasBackendConnection || !backend.LiveConnectionRequested) return null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(voiceTimeoutSeconds));
            try {
            var bytes = await backend.SendReflectionRequestAsync("/api/speech/narration", JsonUtility.ToJson(new Request(cue, plan)), timeout.Token);
            var reply = JsonUtility.FromJson<Reply>(Encoding.UTF8.GetString(bytes));
            if (reply == null || !System.Text.RegularExpressions.Regex.IsMatch(reply.Id ?? "", "^[a-f0-9]{64}$")) throw new InvalidOperationException("Invalid narration response.");
            var wav = await backend.SendReflectionRequestAsync("/api/speech/audio/" + reply.Id, null, timeout.Token);
            token.ThrowIfCancellationRequested(); clip = SpeechWavCodec.Decode(wav); cached.Add(key, clip); transcripts[key] = reply.Text; return clip;
            } catch { if (!token.IsCancellationRequested) serviceUnavailable = true; throw; }
        }
        public async Task SpeakAsync(string cue, string subtitle, string plan, CancellationToken token, bool cachedOnly = false)
        {
            StopSpeech(); if (recording) return;
            var current = CancellationTokenSource.CreateLinkedTokenSource(token); speech = current;
            SubtitleChanged?.Invoke(subtitle);
            try {
                if (muted || !IsConfigured) return;
                AudioClip clip;
                if (cachedOnly) cached.TryGetValue(plan + ":" + cue, out clip);
                else clip = await GetClip(cue, plan, current.Token);
                if (clip == null) return;
                current.Token.ThrowIfCancellationRequested();
                if (recording) return;
                if (transcripts.TryGetValue(plan + ":" + cue, out var spokenText)) SubtitleChanged?.Invoke(spokenText);
                voiceSource.clip = clip; voiceSource.volume = voiceVolume; voiceSource.Play(); ambience.SetNarrationActive(true);
                while (voiceSource.isPlaying) { current.Token.ThrowIfCancellationRequested(); await Task.Yield(); }
            } catch (OperationCanceledException) { }
              catch { /* Subtitles remain usable when speech is unavailable; never log private text/audio. */ }
            finally {
                if (ReferenceEquals(speech, current)) { speech = null; if (voiceSource != null) voiceSource.Stop(); ambience?.SetNarrationActive(false); }
                current.Dispose();
            }
        }
        public void SpeakBreath(BreathPhase phase)
        {
            if (recording || muted || !IsConfigured || IsSpeaking) return;
            string cue = phase == BreathPhase.Inhale ? "breathe_in" : "breathe_out";
            if (!cached.TryGetValue(":" + cue, out var clip)) return; // Late network speech never arrives in the wrong phase.
            voiceSource.clip = clip; voiceSource.volume = voiceVolume; voiceSource.Play();
            SubtitleChanged?.Invoke(transcripts.TryGetValue(":" + cue, out var text) ? text : phase == BreathPhase.Inhale ? "A gentle breath in, if comfortable." : "And an easy breath out.");
        }
        public void SetRecording(bool value) { recording = value; if (value) StopSpeech(); }
        public void ToggleMute() { muted = !muted; if (muted) StopSpeech(); else serviceUnavailable = false; }
        public void CycleVolume() { voiceVolume = voiceVolume >= .99f ? .25f : Mathf.Min(1, voiceVolume + .25f); if (voiceSource != null) voiceSource.volume = voiceVolume; }
        public void StopSpeech() { speech?.Cancel(); speech = null; if (voiceSource != null) voiceSource.Stop(); ambience?.SetNarrationActive(false); }
        public void ClearPrivateSession()
        {
            StopSpeech(); var remove = new List<string>();
            serviceUnavailable = false;
            foreach (var item in cached) if (!item.Key.StartsWith(":")) { if (item.Value != null) Destroy(item.Value); remove.Add(item.Key); }
            foreach (var key in remove) { cached.Remove(key); transcripts.Remove(key); }
        }
        private void Update() { if (ambience != null) ambience.SetNarrationActive(IsSpeaking); }
        private void OnDisable() { StopSpeech(); foreach (var clip in cached.Values) if (clip != null) Destroy(clip); cached.Clear(); transcripts.Clear(); }
    }
}
