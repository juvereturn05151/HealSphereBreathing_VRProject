using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HealSphere.Breathing
{
    /// <summary>Presenter for UI authored visually in the Editor. Never creates missing UI.</summary>
    public sealed class CompanionDialogueController : MonoBehaviour
    {
        [Header("Required Editor-Authored World-Space UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private TMP_Text breathingText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button catButton;
        [SerializeField] private Button treeButton;
        [SerializeField] private Button moonButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button guidedButton;
        [SerializeField] private Button motionProxyButton;
        [SerializeField] private Button calibrateButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button endButton;
        public event Action ContinueRequested;
        public event Action<int> ChoiceRequested;
        public event Action PauseRequested;
        public event Action GuidedRequested;
        public event Action MotionRequested;
        public event Action CalibrationRequested;
        public event Action RestartRequested;
        public event Action EndRequested;
        public bool IsConfigured => panelRoot != null && dialogueText != null && breathingText != null &&
            progressText != null && modeText != null && continueButton != null && catButton != null &&
            treeButton != null && moonButton != null && pauseButton != null && guidedButton != null &&
            motionProxyButton != null && calibrateButton != null && restartButton != null && endButton != null;
        private bool bound;
        private string lastBreath;

        private void OnEnable()
        {
            if (!IsConfigured) return;
            continueButton.onClick.AddListener(Continue); catButton.onClick.AddListener(Cat);
            treeButton.onClick.AddListener(Tree); moonButton.onClick.AddListener(Moon);
            pauseButton.onClick.AddListener(Pause); guidedButton.onClick.AddListener(Guided);
            motionProxyButton.onClick.AddListener(Motion); calibrateButton.onClick.AddListener(Calibrate);
            restartButton.onClick.AddListener(Restart); endButton.onClick.AddListener(End); bound = true;
        }
        private void OnDisable()
        {
            if (!bound) return;
            continueButton.onClick.RemoveListener(Continue); catButton.onClick.RemoveListener(Cat);
            treeButton.onClick.RemoveListener(Tree); moonButton.onClick.RemoveListener(Moon);
            pauseButton.onClick.RemoveListener(Pause); guidedButton.onClick.RemoveListener(Guided);
            motionProxyButton.onClick.RemoveListener(Motion); calibrateButton.onClick.RemoveListener(Calibrate);
            restartButton.onClick.RemoveListener(Restart); endButton.onClick.RemoveListener(End); bound = false;
        }
        public void Show(string message) { if (!IsConfigured) return; panelRoot.SetActive(true); dialogueText.text = message; }
        public void SetMode(string message) { if (modeText != null) modeText.text = message; }
        public void SetProgress(int completedCycles, int totalCycles, int objects)
        { if (progressText != null) progressText.text = $"{completedCycles} / {totalCycles} gentle cycles  •  {objects} / 3 creations"; }
        public void ShowBreath(BreathSignal signal)
        {
            string text = signal.Phase switch
            {
                BreathPhase.Inhale => "A gentle inhale, if comfortable…",
                BreathPhase.Exhale => "Soften into an easy exhale…",
                BreathPhase.Calibrating => "Calibrating motion proxy. Breathe normally.",
                BreathPhase.Unavailable => "Motion unavailable. Guided breathing is always available.",
                _ => "Rest whenever you like. There is no hurry."
            };
            if (text == lastBreath) return;
            lastBreath = text;
            if (breathingText != null) breathingText.text = text;
        }
        public void SetControls(bool greeting, bool choosing, bool breathing, bool paused, bool reflection, bool ended, bool allowModeSwitch,
            bool catCreated, bool treeCreated, bool moonCreated)
        {
            if (!IsConfigured) return;
            continueButton.gameObject.SetActive(greeting || reflection);
            catButton.gameObject.SetActive(choosing); treeButton.gameObject.SetActive(choosing); moonButton.gameObject.SetActive(choosing);
            catButton.interactable = !catCreated; treeButton.interactable = !treeCreated; moonButton.interactable = !moonCreated;
            pauseButton.gameObject.SetActive(breathing);
            guidedButton.gameObject.SetActive(allowModeSwitch); motionProxyButton.gameObject.SetActive(allowModeSwitch);
            calibrateButton.gameObject.SetActive(allowModeSwitch);
            restartButton.gameObject.SetActive(ended);
            endButton.gameObject.SetActive(!ended);
            if (paused && breathingText != null) { breathingText.text = "Paused. Rest and breathe naturally. Press Pause / Resume when ready."; lastBreath = null; }
        }
        private void Continue() => ContinueRequested?.Invoke();
        private void Cat() => ChoiceRequested?.Invoke(0);
        private void Tree() => ChoiceRequested?.Invoke(1);
        private void Moon() => ChoiceRequested?.Invoke(2);
        private void Pause() => PauseRequested?.Invoke();
        private void Guided() => GuidedRequested?.Invoke();
        private void Motion() => MotionRequested?.Invoke();
        private void Calibrate() => CalibrationRequested?.Invoke();
        private void Restart() => RestartRequested?.Invoke();
        private void End() => EndRequested?.Invoke();
    }
}
