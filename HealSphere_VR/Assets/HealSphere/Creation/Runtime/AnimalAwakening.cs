using UnityEngine;

namespace HealSphere.Creation
{
    /// <summary>Poses the imported root and plays bounded effects without changing its mesh or materials.</summary>
    public sealed class AnimalAwakening : MonoBehaviour
    {
        [SerializeField] private ParticleSystem warmth;
        [SerializeField] private ParticleSystem pulse;
        private Transform animal;
        private Transform viewer;
        private Vector3 restPosition, originalScale, approach;
        private Quaternion originalRotation;
        private Quaternion wakeRotation;
        private Vector3 wakeScale;
        private float wakeTime = -1;
        private float restTime;
        private float breath;

        public void Rest(Transform model, Transform camera)
        {
            animal = model;
            viewer = camera;
            restPosition = model.localPosition;
            originalScale = model.localScale;
            originalRotation = model.localRotation;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                transform.localPosition = model.InverseTransformPoint(bounds.center);
            }
            warmth.Play();
        }

        public void SetBreath(float value)
        {
            breath = Mathf.Clamp01(value);
            var emission = warmth.emission;
            emission.rateOverTime = Mathf.Lerp(7, 20, breath);
        }

        public void Wake()
        {
            if (animal == null || wakeTime >= 0) return;
            wakeTime = 0;
            wakeScale = animal.localScale;
            wakeRotation = animal.localRotation;
            Vector3 toward = viewer != null ? viewer.position - animal.position : Vector3.zero;
            toward.y = 0;
            if (toward.sqrMagnitude > .01f)
            {
                var worldApproach = toward.normalized * Mathf.Min(.16f, Mathf.Max(0, toward.magnitude - 1.3f));
                approach = animal.parent != null ? animal.parent.InverseTransformVector(worldApproach) : worldApproach;
            }
            pulse.Play();
            var emission = warmth.emission;
            emission.rateOverTime = 6;
        }

        private void ApplyRest()
        {
            if (animal == null || wakeTime >= 0) return;
            float settled = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(restTime / .8f));
            animal.localScale = Vector3.Scale(originalScale, new Vector3(1 + .035f * settled,
                Mathf.Lerp(1, .86f + .045f * breath, settled), 1 + .035f * settled));
            animal.localRotation = originalRotation * Quaternion.Euler((7f - 3f * breath) * settled, 0, 0);
        }

        private void LateUpdate()
        {
            if (animal == null) return;
            if (wakeTime < 0)
            {
                restTime += Time.deltaTime;
                ApplyRest();
                return;
            }
            wakeTime += Time.deltaTime;
            float progress = Mathf.Clamp01(wakeTime / 3.5f);
            float eased = Mathf.SmoothStep(0, 1, progress);
            float stretch = .065f * Mathf.Sin(Mathf.PI * progress);
            float idle = .018f * Mathf.Sin(wakeTime * 2f) * eased;
            animal.localScale = Vector3.Lerp(wakeScale, originalScale, eased) +
                Vector3.Scale(originalScale, new Vector3(-stretch * .4f, stretch + idle, 0));
            animal.localRotation = Quaternion.Slerp(wakeRotation, originalRotation, eased);
            animal.localPosition = restPosition + approach * eased + Vector3.up * (.045f * Mathf.Sin(Mathf.PI * progress));
        }
    }
}
