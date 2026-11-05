using System.Collections.Generic;
using UnityEngine;

namespace HealSphere.Wellness
{
    public sealed class EnvironmentReactionController : MonoBehaviour
    {
        [SerializeField] private Renderer[] treeRenderers;
        [SerializeField] private Light keyLight;
        [SerializeField] private ParticleSystem breathingMotes;
        [SerializeField] private ExperienceConfiguration configuration;
        public bool IsConfigured => treeRenderers != null && treeRenderers.Length > 0 && keyLight != null && breathingMotes != null && configuration != null;
        private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        private Material[][] originals;
        private ParticleSystem.Particle[] particles;
        private Vector3[] offsets;
        private Color originalLightColor;
        private float originalIntensity;
        private float targetBreath;
        private float targetActivity;
        private float breath;
        private float activity;
        private float elapsed;
        private bool paused;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private void Awake()
        {
            if (!IsConfigured) return;
            originalLightColor = keyLight.color; originalIntensity = keyLight.intensity;
            originals = new Material[treeRenderers.Length][];
            for (int i = 0; i < treeRenderers.Length; i++)
            {
                originals[i] = treeRenderers[i].sharedMaterials;
                var materials = (Material[])originals[i].Clone();
                for (int j = 0; j < materials.Length; j++)
                {
                    var original = materials[j];
                    if (original == null || !original.HasProperty(EmissionId)) continue;
                    if (!copies.TryGetValue(original, out var copy))
                    {
                        copy = new Material(original) { name = original.name + " (Breathing Instance)" };
                        copy.EnableKeyword("_EMISSION"); copies.Add(original, copy);
                    }
                    materials[j] = copy;
                }
                treeRenderers[i].sharedMaterials = materials;
            }
            int count = Mathf.Clamp(configuration.MoteCount, 32, 256);
            particles = new ParticleSystem.Particle[count]; offsets = new Vector3[count];
            var random = new System.Random(9727);
            for (int i = 0; i < count; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = Mathf.Lerp(0.5f, 1.0f, (float)random.NextDouble());
                offsets[i] = new Vector3(Mathf.Cos(angle) * radius, (float)random.NextDouble() * 1.1f - 0.4f, Mathf.Sin(angle) * radius * 0.5f);
                particles[i].startLifetime = particles[i].remainingLifetime = 3600;
                particles[i].startSize = configuration.MoteSize * 2.5f;
            }
            breathingMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = breathingMotes.main; main.maxParticles = count; main.simulationSpeed = 0; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = breathingMotes.emission; emission.enabled = false;
            breathingMotes.Play();
        }
        public void SetReaction(float normalizedBreath, float strength) { targetBreath = Mathf.Clamp01(normalizedBreath); targetActivity = Mathf.Clamp01(strength); }
        public void SetPaused(bool value) => paused = value;
        public void ResetReaction() { elapsed = breath = activity = targetBreath = targetActivity = 0; }
        private void Update()
        {
            if (!IsConfigured || particles == null || paused) return;
            elapsed += Time.deltaTime;
            float easing = 1 - Mathf.Exp(-Time.deltaTime * 1.5f);
            breath = Mathf.Lerp(breath, targetBreath, easing); activity = Mathf.Lerp(activity, targetActivity, easing);
            foreach (var copy in copies.Values) copy.SetColor(EmissionId, new Color(1, 0.76f, 0.44f) * (configuration.TreeEmission * breath * activity));
            keyLight.color = Color.Lerp(originalLightColor, new Color(1, 0.87f, 0.72f), configuration.LightWarmth * breath * activity);
            keyLight.intensity = originalIntensity * (1 + 0.025f * breath * activity);
            for (int i = 0; i < particles.Length; i++)
            {
                var point = offsets[i] * (1 + breath * 0.12f * activity);
                point.y += breath * 0.08f * activity + Mathf.Sin(elapsed * 0.3f + i) * 0.018f;
                particles[i].position = point;
                particles[i].startColor = new Color(1, 0.86f, 0.64f, activity * 0.38f);
            }
            breathingMotes.SetParticles(particles, particles.Length);
        }
        private void OnDestroy()
        {
            if (originals != null)
                for (int i = 0; i < treeRenderers.Length; i++) if (treeRenderers[i] != null) treeRenderers[i].sharedMaterials = originals[i];
            foreach (var material in copies.Values) Destroy(material);
            if (keyLight != null) { keyLight.color = originalLightColor; keyLight.intensity = originalIntensity; }
        }
    }
}
