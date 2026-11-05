using System;
using UnityEngine;

namespace HealSphere.Breathing
{
    public sealed class BreathingInputController : MonoBehaviour
    {
        [SerializeField] private GuidedBreathingProvider guided;
        [SerializeField] private ControllerMotionBreathingProvider controllerMotion;
        public event Action<BreathSignal> Signal;
        public event Action ExhaleCompleted;
        public event Action<string> ModeChanged;
        public bool IsConfigured => guided != null;
        public bool UsesMotionProxy => ReferenceEquals(provider, controllerMotion) && provider != null;
        private IBreathingProvider provider;
        private bool running;
        private bool paused;

        public void Begin() { running = true; paused = false; UseGuided(); }
        public void Stop() { running = false; Disconnect(); }
        public void SetPaused(bool value) { paused = value; provider?.SetPaused(value); }
        public void UseGuided()
        {
            if (!running || guided == null) return;
            Switch(guided); ModeChanged?.Invoke("Guided suggestion — follow your own comfortable pace. No breath holds.");
        }
        public void UseMotionProxy()
        {
            if (!running) return;
            if (controllerMotion == null || !controllerMotion.IsAvailable)
            { UseGuided(); ModeChanged?.Invoke("Motion proxy unavailable. Guided mode is available; breathe comfortably."); return; }
            Switch(controllerMotion);
            ModeChanged?.Invoke("UNVALIDATED MOTION PROXY — not respiration detection. Rest one controller near your belly for calibration; breathe normally.");
        }
        public void Recalibrate() { if (running && UsesMotionProxy) Switch(controllerMotion); }
        public void DebugCompleteExhale() { if (running && !paused) ExhaleCompleted?.Invoke(); }
        private void Switch(IBreathingProvider next)
        {
            Disconnect(); provider = next;
            provider.Signal += Forward; provider.ExhaleCompleted += Complete;
            provider.Begin(); provider.SetPaused(paused);
        }
        private void Disconnect()
        {
            if (provider == null) return;
            provider.Signal -= Forward; provider.ExhaleCompleted -= Complete; provider.Stop(); provider = null;
        }
        private void Forward(BreathSignal value) => Signal?.Invoke(value);
        private void Complete() { if (running && !paused) ExhaleCompleted?.Invoke(); }
        private void Update() { if (running && !paused && UsesMotionProxy && !provider.IsAvailable) UseGuided(); }
        private void OnDisable() => Stop();
    }
}
