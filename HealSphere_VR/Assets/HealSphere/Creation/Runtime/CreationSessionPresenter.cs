using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard;

namespace HealSphere.Creation
{
    /// <summary>Only presents and binds visually authored UI. Never constructs interface objects.</summary>
    public sealed class CreationSessionPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_InputField requestInput;
        [SerializeField] private GameObject requestEntryRoot;
        [SerializeField] private XRKeyboard keyboard;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private Button wellnessButton;
        [SerializeField] private Canvas[] inputCanvases;
        [SerializeField] private Camera xrCamera;
        [SerializeField] private Camera desktopCamera;
        public event Action PrimaryRequested;
        public event Action SecondaryRequested;
        public event Action WellnessRequested;
        public string RequestText => requestInput != null ? requestInput.text : "";
        public bool IsConfigured => messageText != null && requestInput != null && keyboard != null && requestEntryRoot != null && primaryButton != null && secondaryButton != null && wellnessButton != null;
        private void OnEnable()
        {
            if (!IsConfigured) return;
            primaryButton.onClick.AddListener(Primary); secondaryButton.onClick.AddListener(Secondary); wellnessButton.onClick.AddListener(Wellness);
            requestInput.characterLimit = 512; requestInput.richText = false; requestInput.shouldHideMobileInput = true;
            requestInput.shouldHideSoftKeyboard = true;
            requestInput.textComponent.richText = false;
        }
        private void LateUpdate()
        {
            var activeCamera = xrCamera != null && xrCamera.isActiveAndEnabled ? xrCamera : desktopCamera;
            if (inputCanvases == null || activeCamera == null) return;
            foreach (var canvas in inputCanvases)
                if (canvas != null && canvas.worldCamera != activeCamera) canvas.worldCamera = activeCamera;
        }
        public void Present(string message, string primary, string secondary, bool enteringRequest, bool wellness = true)
        {
            if (!IsConfigured) return;
            messageText.text = "<size=24><color=#805B79>HEALSPHERE · DREAM CREATION</color></size>\n\n" + message;
            SetButton(primaryButton, primary); SetButton(secondaryButton, secondary); SetButton(wellnessButton, wellness ? "Just breathe" : "");
            if (requestEntryRoot.activeSelf != enteringRequest) requestEntryRoot.SetActive(enteringRequest);
            if (!enteringRequest) keyboard.Close();
        }
        public void ClearRequest() { if (requestInput != null) requestInput.text = ""; }
        public void SetRequestForTesting(string value) { if (requestInput != null) requestInput.text = value; }
        private static void SetButton(Button button, string label)
        {
            button.gameObject.SetActive(!string.IsNullOrEmpty(label));
            var text = button.GetComponentInChildren<TMP_Text>(true); if (text != null) text.text = label;
        }
        private void Primary() => PrimaryRequested?.Invoke();
        private void Secondary() => SecondaryRequested?.Invoke();
        private void Wellness() => WellnessRequested?.Invoke();
        private void OnDisable()
        {
            if (primaryButton != null) primaryButton.onClick.RemoveListener(Primary);
            if (secondaryButton != null) secondaryButton.onClick.RemoveListener(Secondary);
            if (wellnessButton != null) wellnessButton.onClick.RemoveListener(Wellness);
        }
    }
}
