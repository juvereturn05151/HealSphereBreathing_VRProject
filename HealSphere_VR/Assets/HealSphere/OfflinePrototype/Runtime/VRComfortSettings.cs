using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HealSphere.Breathing
{
    /// <summary>Selects a stationary desktop camera when no XR display starts; never moves a tracked camera.</summary>
    public sealed class VRComfortSettings : MonoBehaviour
    {
        [SerializeField] private GameObject xrOrigin;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private Camera desktopCamera;
        [SerializeField] private Canvas worldSpaceCanvas;
        [SerializeField] private XRUIInputModule guidanceInputModule;
        [SerializeField] private bool forceDesktopSimulation;
        [SerializeField, Range(1, 10)] private float xrStartupWaitSeconds = 3f;
        private IEnumerator Start()
        {
            if (xrOrigin == null || xrCamera == null || desktopCamera == null) yield break;
            desktopCamera.gameObject.SetActive(false);
            float timeout = Time.realtimeSinceStartup + xrStartupWaitSeconds;
            while (!forceDesktopSimulation && !XRSettings.isDeviceActive && Time.realtimeSinceStartup < timeout) yield return null;
            bool desktop = forceDesktopSimulation || !XRSettings.isDeviceActive;
            xrOrigin.SetActive(!desktop); desktopCamera.gameObject.SetActive(desktop);
            if (worldSpaceCanvas != null) worldSpaceCanvas.worldCamera = desktop ? desktopCamera : xrCamera;
            // The rig's InputActionManager disables its shared action asset on deactivation.
            // Re-enable the existing module afterwards so desktop UI keeps its pointer actions.
            if (desktop && guidanceInputModule != null)
            {
                guidanceInputModule.enableMouseInput = true;
                guidanceInputModule.enabled = false;
                guidanceInputModule.enabled = true;
            }
        }
    }
}
