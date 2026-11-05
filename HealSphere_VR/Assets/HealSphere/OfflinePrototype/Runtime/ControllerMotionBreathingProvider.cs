using System;
using UnityEngine;
using UnityEngine.XR;

namespace HealSphere.Breathing
{
    /// <summary>Unvalidated controller-motion proxy. Head/hand movement can produce false cycles.</summary>
    public sealed class ControllerMotionBreathingProvider : MonoBehaviour, IBreathingProvider
    {
        [Header("Tracked Poses")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform bellyController;
        [SerializeField] private XRNode controllerNode = XRNode.RightHand;
        [Header("Calibration And Hysteresis")]
        [SerializeField, Min(1f)] private float calibrationSeconds = 2f;
        [SerializeField, Range(0.002f, 0.03f)] private float halfTravelMeters = 0.005f;
        [SerializeField, Min(0.1f)] private float smoothingSeconds = 0.25f;
        [SerializeField, Min(0.5f)] private float minimumPhaseSeconds = 0.8f;
        [SerializeField] private Vector2 distanceBelowHead = new Vector2(0.15f, 0.85f);
        [SerializeField, Min(0.2f)] private float maximumHeadDistance = 0.85f;
        public event Action<BreathSignal> Signal;
        public event Action ExhaleCompleted;
        public bool IsAvailable => isActiveAndEnabled && ValidPose();
        public bool IsCalibrated { get; private set; }
        private readonly MotionBreathGate gate = new MotionBreathGate();
        private bool running;
        private bool paused;
        private float elapsed;
        private float depthSum;
        private int sampleCount;
        private float baseline;
        private float smoothed;
        private float time;

        public void Begin()
        {
            running = true; paused = false; IsCalibrated = false;
            elapsed = depthSum = smoothed = time = 0; sampleCount = 0; gate.Reset();
            Signal?.Invoke(new BreathSignal(BreathPhase.Calibrating, 0));
        }
        public void Stop() { running = false; IsCalibrated = false; }
        public void SetPaused(bool value) { paused = value; }
        private bool ValidPose()
        {
            if (head == null || bellyController == null) return false;
            var handDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
            var headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!handDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool handTracked) || !handTracked ||
                !headDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool headTracked) || !headTracked) return false;
            var offset = bellyController.position - head.position;
            float below = -offset.y;
            return below >= distanceBelowHead.x && below <= distanceBelowHead.y && offset.magnitude <= maximumHeadDistance;
        }
        private void Update()
        {
            if (!running || paused) return;
            if (!ValidPose()) { Signal?.Invoke(new BreathSignal(BreathPhase.Unavailable, 0)); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            // Yaw-relative depth avoids treating head pitch as controller expansion.
            Vector3 forward = Quaternion.Euler(0, head.eulerAngles.y, 0) * Vector3.forward;
            float depth = Vector3.Dot(bellyController.position - head.position, forward);
            if (!IsCalibrated)
            {
                elapsed += dt; depthSum += depth; sampleCount++;
                Signal?.Invoke(new BreathSignal(BreathPhase.Calibrating, Mathf.Clamp01(elapsed / calibrationSeconds)));
                if (elapsed >= calibrationSeconds) { baseline = depthSum / sampleCount; smoothed = 0; IsCalibrated = true; }
                return;
            }
            time += dt;
            smoothed = Mathf.Lerp(smoothed, depth - baseline, 1f - Mathf.Exp(-dt / smoothingSeconds));
            bool complete = gate.Advance(smoothed, time, halfTravelMeters, minimumPhaseSeconds);
            var phase = gate.IsExpanded ? BreathPhase.Exhale : BreathPhase.Inhale;
            float progress = Mathf.Clamp01(0.5f + smoothed / (halfTravelMeters * 4f));
            Signal?.Invoke(new BreathSignal(phase, gate.IsExpanded ? 1 - progress : progress));
            if (complete) ExhaleCompleted?.Invoke();
        }
        private void OnDisable() => Stop();
        private void OnValidate()
        {
            calibrationSeconds = Mathf.Max(1, calibrationSeconds); smoothingSeconds = Mathf.Max(0.1f, smoothingSeconds);
            halfTravelMeters = Mathf.Clamp(halfTravelMeters, 0.002f, 0.03f); minimumPhaseSeconds = Mathf.Max(0.5f, minimumPhaseSeconds);
            distanceBelowHead.x = Mathf.Max(0.05f, distanceBelowHead.x);
            distanceBelowHead.y = Mathf.Max(distanceBelowHead.x + 0.05f, distanceBelowHead.y);
        }
    }
}
