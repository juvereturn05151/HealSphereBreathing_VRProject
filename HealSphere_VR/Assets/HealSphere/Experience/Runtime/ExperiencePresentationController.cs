using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HealSphere.Wellness
{
    /// <summary>Updates existing visually authored scene UI; missing UI is never constructed.</summary>
    public sealed class ExperiencePresentationController : MonoBehaviour
    {
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private CanvasGroup panelGroup;
        public event Action PauseRequested;
        public event Action RestartRequested;
        public bool IsConfigured => worldCanvas != null && messageText != null && pauseButton != null && restartButton != null && panelGroup != null;
        private ExperiencePhase phase;
        private bool paused;
        private bool nearBelly;
        private string breath = "";
        private TMP_Text pauseLabel;
        private bool bound;
        private float alpha;
        private void OnEnable() => Bind();
        private void Bind()
        {
            if (!IsConfigured || bound) return;
            pauseLabel = pauseButton.GetComponentInChildren<TMP_Text>(true);
            var restartLabel = restartButton.GetComponentInChildren<TMP_Text>(true);
            if (restartLabel != null) restartLabel.text = "Begin again";
            pauseButton.onClick.AddListener(Pause); restartButton.onClick.AddListener(Restart); bound = true;
        }
        public void ShowPhase(ExperiencePhase value)
        {
            Bind(); phase = value; breath = ""; Refresh();
            if (restartButton != null) restartButton.gameObject.SetActive(value == ExperiencePhase.Completed);
            if (pauseButton != null) pauseButton.gameObject.SetActive(value != ExperiencePhase.Completed && value != ExperiencePhase.Ready);
        }
        public void SetPaused(bool value) { paused = value; Refresh(); if (pauseLabel != null) pauseLabel.text = value ? "Resume" : "Pause / Rest"; }
        public void SetNearBelly(bool value) { if (nearBelly == value) return; nearBelly = value; Refresh(); }
        public void ShowBreath(bool inhaling)
        {
            string value = inhaling ? "A gentle inhale, if comfortable…" : "Soften into an easy exhale…";
            if (value == breath) return; breath = value; Refresh();
        }
        private void Refresh()
        {
            if (messageText == null) return;
            string body = phase switch
            {
                ExperiencePhase.Arrival => "Welcome to your little moment of peace.\nLet’s breathe together.",
                ExperiencePhase.Introduction => "Rest a hand gently on your belly, if that feels comfortable.\nOr simply relax your hands. Either is welcome.",
                ExperiencePhase.Breathing => breath,
                ExperiencePhase.Ending => "You’ve taken a little moment for yourself.\nBreathe naturally. There is no hurry.",
                ExperiencePhase.Completed => "Carry this calm with you.\nStay a little longer, or begin again whenever you wish.",
                _ => "A little moment to breathe"
            };
            if (paused) body = "Take your time.\nRest and breathe in your own way. Resume whenever you’re ready.";
            string footer = phase == ExperiencePhase.Breathing ? "The rhythm is a suggestion. There are no holds, scores or missed breaths." : "You are welcome to pause at any time.";
            if (nearBelly && (phase == ExperiencePhase.Introduction || phase == ExperiencePhase.Breathing))
                footer = "Controller placement noticed — optional, not a breathing measurement. Follow your own comfortable pace.";
            if (phase == ExperiencePhase.Completed) footer = "Thank you for sharing this quiet moment.";
            messageText.text = "<size=24><color=#805B79>HEALSPHERE · A LITTLE MOMENT TO BREATHE</color></size>\n\n" + body + "\n\n<size=26>" + footer + "</size>";
        }
        private void Update()
        {
            if (panelGroup == null) return;
            alpha = Mathf.MoveTowards(alpha, 1, Time.unscaledDeltaTime / 2f); panelGroup.alpha = alpha;
        }
        private void Pause() => PauseRequested?.Invoke();
        private void Restart() => RestartRequested?.Invoke();
        private void OnDisable()
        {
            if (!bound) return;
            pauseButton.onClick.RemoveListener(Pause); restartButton.onClick.RemoveListener(Restart); bound = false;
        }
    }
}
