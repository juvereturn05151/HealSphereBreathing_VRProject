using UnityEngine;

namespace HealSphere.Breathing
{
    public sealed class DreamSeedController : MonoBehaviour
    {
        [SerializeField] private Transform seedVisual;
        [SerializeField] private float inhaleSize = 0.17f;
        [SerializeField] private float exhaleSize = 0.12f;
        private float target = 0.12f;
        private bool paused;
        public void Show() { paused = false; if (seedVisual != null) seedVisual.gameObject.SetActive(true); }
        public void Hide() { if (seedVisual != null) seedVisual.gameObject.SetActive(false); }
        public void SetPaused(bool value) => paused = value;
        public void SetBreath(BreathSignal signal)
        {
            target = signal.Phase == BreathPhase.Inhale ? Mathf.Lerp(exhaleSize, inhaleSize, signal.Progress) :
                signal.Phase == BreathPhase.Exhale ? Mathf.Lerp(inhaleSize, exhaleSize, signal.Progress) : exhaleSize;
        }
        private void Update()
        {
            if (seedVisual != null && !paused) seedVisual.localScale = Vector3.Lerp(seedVisual.localScale, Vector3.one * target, 1f - Mathf.Exp(-Time.deltaTime * 3f));
        }
    }
}
