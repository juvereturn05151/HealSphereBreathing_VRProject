using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HealSphere.Creation
{
    /// <summary>Binds only existing Editor-authored controls. Missing controls are an explicit configuration error.</summary>
    public sealed class ReflectionControlsPresenter : MonoBehaviour
    {
        [SerializeField] private Button microphoneButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private Button volumeButton;
        public bool IsConfigured => microphoneButton != null && muteButton != null && volumeButton != null;
        public event Action MicrophoneRequested;
        public event Action MuteRequested;
        public event Action VolumeRequested;
        private void OnEnable()
        {
            if (!IsConfigured) return;
            microphoneButton.onClick.AddListener(Microphone); muteButton.onClick.AddListener(Mute); volumeButton.onClick.AddListener(Volume);
        }
        public void Present(bool entering, MicrophoneInputState state, bool muted, float volume)
        {
            if (!IsConfigured) return;
            microphoneButton.gameObject.SetActive(entering);
            microphoneButton.interactable = state != MicrophoneInputState.Transcribing && state != MicrophoneInputState.Permission;
            Label(microphoneButton, state == MicrophoneInputState.Recording ? "Stop mic" : state == MicrophoneInputState.Transcribing ? "Transcribing…" : "Speak answer");
            Label(muteButton, muted ? "Unmute rabbit" : "Mute rabbit"); Label(volumeButton, "Voice " + Mathf.RoundToInt(volume * 100) + "%");
        }
        private static void Label(Button button, string text) { var label = button.GetComponentInChildren<TMP_Text>(true); if (label != null) label.text = text; }
        private void Microphone() => MicrophoneRequested?.Invoke(); private void Mute() => MuteRequested?.Invoke(); private void Volume() => VolumeRequested?.Invoke();
        private void OnDisable()
        {
            if (microphoneButton != null) microphoneButton.onClick.RemoveListener(Microphone);
            if (muteButton != null) muteButton.onClick.RemoveListener(Mute);
            if (volumeButton != null) volumeButton.onClick.RemoveListener(Volume);
        }
    }
}
