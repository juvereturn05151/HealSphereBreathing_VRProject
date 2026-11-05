using UnityEngine;

namespace HealSphere.Wellness
{
    public sealed class FloatingCompanionController : MonoBehaviour
    {
        [SerializeField] private Transform companionVisual;
        [SerializeField] private Transform viewer;
        [SerializeField] private ExperienceConfiguration configuration;
        [Tooltip("Imported model forward correction; set once in the Inspector.")]
        [SerializeField] private Vector3 facingOffset;
        public bool IsConfigured => companionVisual != null && configuration != null && viewer != null;
        private Vector3 resting;
        private Quaternion restingRotation;
        private float target;
        private float strength;
        private float breath;
        private float time;
        private bool paused;
        private void Awake() { if (companionVisual != null) { resting = companionVisual.localPosition; restingRotation = companionVisual.rotation; } }
        public void SetReaction(float normalizedBreath, float activity) { target = normalizedBreath; strength = activity; }
        public void SetPaused(bool value) => paused = value;
        public void RestartMotion() { time = breath = target = strength = 0; }
        private void Update()
        {
            if (!IsConfigured || paused) return;
            time += Time.deltaTime;
            breath = Mathf.Lerp(breath, target * strength, 1 - Mathf.Exp(-Time.deltaTime * 2f));
            float bob = Mathf.Sin(time * 0.65f) * configuration.IdleFloat;
            companionVisual.localPosition = resting + Vector3.up * (bob + breath * configuration.CompanionRise);
            Vector3 direction = viewer.position - companionVisual.position; direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                var facing = Quaternion.LookRotation(direction) * Quaternion.Euler(facingOffset);
                companionVisual.rotation = Quaternion.Slerp(companionVisual.rotation, facing, 1 - Mathf.Exp(-Time.deltaTime * 0.6f));
            }
        }
        private void OnDisable() { if (companionVisual != null) { companionVisual.localPosition = resting; companionVisual.rotation = restingRotation; } }
    }
}
