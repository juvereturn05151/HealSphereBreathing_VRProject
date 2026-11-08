using System;
using System.Text;
using System.Threading;
using HealSphere.Tripo;
using UnityEngine;

namespace HealSphere.Creation
{
    public enum MicrophoneInputState { Idle, Permission, Recording, Transcribing, Unavailable }
    public sealed class ReflectionMicrophoneInput : MonoBehaviour
    {
        [SerializeField] private TripoModelLoader backend;
        [SerializeField] private RabbitVoiceNarrator narrator;
        [SerializeField, Range(5, 20)] private int maximumSeconds = 20;
        public MicrophoneInputState State { get; private set; }
        public string Status { get; private set; } = "";
        public bool IsConfigured => backend != null && narrator != null;
        public bool InputAllowed { get; set; }
        public event Action Changed;
        public event Action<string> Transcribed;
        private AudioClip recording;
        private string device;
        private float started;
        private CancellationTokenSource operation;
        private int revision;
        [Serializable] private sealed class Transcription { [SerializeField] private string text; public string Text => text; }
        public void ToggleRecording()
        {
            if (!InputAllowed || !IsConfigured || State == MicrophoneInputState.Transcribing || State == MicrophoneInputState.Permission) return;
            if (State == MicrophoneInputState.Recording) { StopAndTranscribe(); return; }
            if (!backend.HasBackendConnection || !backend.LiveConnectionRequested) { SetState(MicrophoneInputState.Unavailable, "Voice input needs a connected backend. You can type instead."); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) {
                SetState(MicrophoneInputState.Permission, "Microphone permission requested. Recording has not started.");
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                int version = revision;
                callbacks.PermissionGranted += _ => { if (version == revision && InputAllowed && isActiveAndEnabled) BeginRecording(); };
                callbacks.PermissionDenied += _ => { if (version == revision) SetState(MicrophoneInputState.Unavailable, "Microphone permission was declined. Typing still works."); };
                callbacks.PermissionRequestDismissed += _ => { if (version == revision) SetState(MicrophoneInputState.Unavailable, "Microphone permission was dismissed. You can type instead."); };
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks); return;
            }
#endif
            BeginRecording();
        }
        private void BeginRecording()
        {
            if (!InputAllowed || !isActiveAndEnabled) return;
            try {
                if (Microphone.devices.Length == 0) throw new InvalidOperationException();
                device = Microphone.devices[0]; narrator.SetRecording(true);
                recording = Microphone.Start(device, false, maximumSeconds, 16000);
                if (recording == null) throw new InvalidOperationException();
                started = Time.realtimeSinceStartup; SetState(MicrophoneInputState.Recording, "Recording — tap Stop mic when you finish. Nothing is submitted yet.");
            } catch { ReleaseRecording(); SetState(MicrophoneInputState.Unavailable, "Microphone unavailable. You can type your answer."); }
        }
        private async void StopAndTranscribe()
        {
            if (State != MicrophoneInputState.Recording || recording == null) return;
            var source = new CancellationTokenSource(TimeSpan.FromSeconds(45)); operation = source; int version = revision;
            try {
                int frames = Microphone.GetPosition(device); if (frames <= 0 && Time.realtimeSinceStartup - started >= maximumSeconds - .1f) frames = recording.samples;
                Microphone.End(device);
                var bytes = SpeechWavCodec.EncodeMono(recording, frames); Destroy(recording); recording = null;
                SetState(MicrophoneInputState.Transcribing, "Transcribing… You will be able to edit the words before continuing.");
                var result = await backend.TranscribeSpeechAsync(bytes, source.Token);
                source.Token.ThrowIfCancellationRequested();
                if (version != revision || !InputAllowed) return;
                var reply = JsonUtility.FromJson<Transcription>(Encoding.UTF8.GetString(result));
                if (reply == null || string.IsNullOrWhiteSpace(reply.Text)) throw new InvalidOperationException();
                SetState(MicrophoneInputState.Idle, "Check or edit the recognized text, then choose Next."); Transcribed?.Invoke(reply.Text);
            } catch (OperationCanceledException) { }
              catch { if (version == revision) SetState(MicrophoneInputState.Unavailable, "Voice input could not finish. Please try again or type your answer."); }
            finally { if (ReferenceEquals(operation, source)) operation = null; source.Dispose(); if (version == revision) ReleaseRecording(); }
        }
        public void Cancel()
        {
            revision++; operation?.Cancel(); operation = null; ReleaseRecording(); SetState(MicrophoneInputState.Idle, "");
        }
        private void ReleaseRecording()
        {
            if (!string.IsNullOrEmpty(device)) { try { Microphone.End(device); } catch { } }
            if (recording != null) Destroy(recording); recording = null; narrator?.SetRecording(false);
        }
        private void SetState(MicrophoneInputState value, string status) { State = value; Status = status; Changed?.Invoke(); }
        private void Update() { if (State == MicrophoneInputState.Recording && Time.realtimeSinceStartup - started >= maximumSeconds) StopAndTranscribe(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        private void OnDisable() => Cancel();
    }
}
