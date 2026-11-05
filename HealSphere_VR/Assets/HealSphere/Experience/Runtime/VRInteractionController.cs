using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace HealSphere.Wellness
{
    /// <summary>Optional proximity only. It never estimates respiration or gates experience progress.</summary>
    public sealed class VRInteractionController : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private ExperienceDirector director;
        [SerializeField, Min(0.1f)] private float placementDwellSeconds = 0.4f;
        public event Action<bool> BellyPlacementChanged;
        public bool IsNearBelly { get; private set; }
        public bool IsConfigured => head != null && leftController != null && rightController != null && director != null;
        private float dwell;
        private bool menuHeld;
        private bool restartHeld;
        private float finishHold;
        private void Update()
        {
            bool near = Near(leftController, XRNode.LeftHand) || Near(rightController, XRNode.RightHand);
            dwell = Mathf.MoveTowards(dwell, near ? placementDwellSeconds : 0, Time.unscaledDeltaTime);
            bool detected = near ? dwell >= placementDwellSeconds : dwell > 0;
            if (detected != IsNearBelly) { IsNearBelly = detected; BellyPlacementChanged?.Invoke(detected); }
            if (director == null) return;
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool menu = left.TryGetFeatureValue(CommonUsages.menuButton, out bool pressed) && pressed;
            bool primary = right.TryGetFeatureValue(CommonUsages.primaryButton, out bool a) && a;
            if (menu && !menuHeld) director.TogglePause();
            if (primary && !restartHeld && director.Phase == ExperiencePhase.Completed) director.RestartExperience();
            menuHeld = menu; restartHeld = primary;
            bool secondary = right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool b) && b;
            finishHold = secondary ? finishHold + Time.unscaledDeltaTime : 0;
            if (finishHold > 2) { director.FinishGently(); finishHold = 0; }
            var keyboard = Keyboard.current;
            if (Application.isEditor && keyboard != null)
            {
                if (keyboard.pKey.wasPressedThisFrame) director.TogglePause();
                if (keyboard.rKey.wasPressedThisFrame && director.Phase == ExperiencePhase.Completed) director.RestartExperience();
                if (keyboard.escapeKey.wasPressedThisFrame) director.FinishGently();
            }
        }
        private bool Near(Transform controller, XRNode node)
        {
            if (head == null || controller == null) return false;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked) return false;
            var offset = controller.position - head.position;
            return offset.y < -0.18f && offset.y > -0.85f && offset.sqrMagnitude < 0.8f * 0.8f;
        }
    }
}
