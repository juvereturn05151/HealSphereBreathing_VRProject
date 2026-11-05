using System;
using UnityEngine;

namespace HealSphere.Breathing
{
    public sealed class ParticleModelReveal : MonoBehaviour
    {
        [Header("Existing Gameplay Components, Not UI")]
        [SerializeField] private ParticleSystem formationParticles;
        [Tooltip("Temporary URP glowing dissolve. All original materials are restored at completion/cancel.")]
        [SerializeField] private Material dissolveMaterial;
        [SerializeField, Range(32, 512)] private int particleCount = 256;
        [SerializeField, Min(0.5f)] private float transitionSeconds = 1.8f;
        [SerializeField, Range(0.005f, 0.03f)] private float particleSize = 0.012f;
        [Header("Gentle Formation")]
        [SerializeField, Range(0.1f, 0.5f)] private float orbitRadius = 0.24f;
        [SerializeField, Range(0.1f, 1.2f)] private float swirlRadiansPerSecond = 0.55f;
        [SerializeField, Range(0f, 0.06f)] private float driftHeight = 0.025f;
        [SerializeField] private Color warmMote = new Color(1f, 0.82f, 0.52f);
        [SerializeField] private Color coolMote = new Color(0.7f, 0.82f, 1f);
        public event Action Revealed;
        public bool IsConfigured => dissolveMaterial != null;
        public float Progress { get; private set; }
        public bool SurfaceSamplingAvailable { get; private set; }
        private GameObject model;
        private Renderer[] renderers;
        private Material[][] originals;
        private MaterialPropertyBlock[] originalBlocks;
        private MaterialPropertyBlock[][] originalSlotBlocks;
        private MaterialPropertyBlock[][] revealBlocks;
        private Vector3[] targets;
        private Vector3[] starts;
        private Vector3 center;
        private float[] orders;
        private float[] phases;
        private Color[] colors;
        private ParticleSystem.Particle[] particles;
        private float targetProgress;
        private float elapsed;
        private float progressVelocity;
        private bool paused;
        private bool finished;
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MinYId = Shader.PropertyToID("_FormationMinY");
        private static readonly int InvHeightId = Shader.PropertyToID("_FormationInvHeight");

        public bool Prepare(GameObject candidate)
        {
            Cancel();
            if (candidate == null || dissolveMaterial == null) return false;
            model = candidate; renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Cancel(); return false; }
            originals = new Material[renderers.Length][]; originalBlocks = new MaterialPropertyBlock[renderers.Length];
            originalSlotBlocks = new MaterialPropertyBlock[renderers.Length][];
            revealBlocks = new MaterialPropertyBlock[renderers.Length][];
            var worldBounds = renderers[0].bounds;
            foreach (var renderer in renderers) worldBounds.Encapsulate(renderer.bounds);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i]; originals[i] = renderer.sharedMaterials;
                originalBlocks[i] = new MaterialPropertyBlock(); renderer.GetPropertyBlock(originalBlocks[i]);
                var temporary = new Material[originals[i].Length];
                originalSlotBlocks[i] = new MaterialPropertyBlock[temporary.Length];
                revealBlocks[i] = new MaterialPropertyBlock[temporary.Length];
                for (int j = 0; j < temporary.Length; j++)
                {
                    temporary[j] = dissolveMaterial;
                    var saved = new MaterialPropertyBlock(); renderer.GetPropertyBlock(saved, j); originalSlotBlocks[i][j] = saved;
                    var original = originals[i][j];
                    var tint = original != null && original.HasProperty("baseColorFactor") ? original.GetColor("baseColorFactor") :
                        original != null && original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") :
                        original != null && original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                    // glTFast graphs also expose URP compatibility slots; their populated glTF slot takes priority.
                    string textureProperty = original != null && original.HasProperty("baseColorTexture") ? "baseColorTexture" :
                        original != null && original.HasProperty("_BaseMap") ? "_BaseMap" : null;
                    var map = textureProperty != null ? original.GetTexture(textureProperty) : null;
                    var materialBlock = new MaterialPropertyBlock();
                    materialBlock.SetColor(TintId, tint); materialBlock.SetTexture(BaseMapId, map != null ? map : Texture2D.whiteTexture);
                    var tiling = map != null ? original.GetTextureScale(textureProperty) : Vector2.one;
                    var offset = map != null ? original.GetTextureOffset(textureProperty) : Vector2.zero;
                    if (textureProperty == "baseColorTexture" && original.HasProperty("baseColorTexture_ST"))
                    { var transform = original.GetVector("baseColorTexture_ST"); tiling = new Vector2(transform.x, transform.y); offset = new Vector2(transform.z, transform.w); }
                    materialBlock.SetVector(BaseMapStId, new Vector4(tiling.x, tiling.y, offset.x, offset.y));
                    materialBlock.SetFloat(MinYId, worldBounds.min.y);
                    materialBlock.SetFloat(InvHeightId, 1f / Mathf.Max(0.001f, worldBounds.size.y));
                    materialBlock.SetFloat(RevealId, 0); revealBlocks[i][j] = materialBlock;
                }
                renderer.sharedMaterials = temporary;
                // Slot blocks deliberately override any original shader properties during formation.
                renderer.SetPropertyBlock(null);
                for (int j = 0; j < temporary.Length; j++) renderer.SetPropertyBlock(revealBlocks[i][j], j);
            }
            try { targets = MeshSurfaceSampler.Collect(model, particleCount); }
            catch (Exception exception) when (exception is UnityException || exception is InvalidOperationException)
            { targets = Array.Empty<Vector3>(); Debug.LogWarning("Surface sampling unsupported; using the glowing dissolve fallback.", this); }
            SurfaceSamplingAvailable = targets.Length > 0 && formationParticles != null;
            if (SurfaceSamplingAvailable)
            {
                formationParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = formationParticles.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = Mathf.Clamp(particleCount, 32, 512); main.simulationSpeed = 0;
                main.scalingMode = ParticleSystemScalingMode.Local;
                var emission = formationParticles.emission; emission.enabled = false;
                var trails = formationParticles.trails; trails.enabled = false;
                var lights = formationParticles.lights; lights.enabled = false;
                particles = new ParticleSystem.Particle[targets.Length]; starts = new Vector3[targets.Length];
                orders = new float[targets.Length]; phases = new float[targets.Length]; colors = new Color[targets.Length];
                var toParticles = formationParticles.transform.worldToLocalMatrix * model.transform.localToWorldMatrix;
                center = formationParticles.transform.InverseTransformPoint(worldBounds.center);
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < targets.Length; i++)
                {
                    targets[i] = toParticles.MultiplyPoint3x4(targets[i]);
                    minY = Mathf.Min(minY, targets[i].y); maxY = Mathf.Max(maxY, targets[i].y);
                }
                var random = new System.Random(921);
                for (int i = 0; i < targets.Length; i++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2;
                    float elevation = (float)random.NextDouble() * 2f - 1f;
                    float radius = orbitRadius * Mathf.Sqrt(1f - elevation * elevation) * Mathf.Lerp(0.7f, 1.15f, (float)random.NextDouble());
                    starts[i] = new Vector3(Mathf.Cos(angle) * radius, elevation * orbitRadius * 0.8f, Mathf.Sin(angle) * radius);
                    phases[i] = angle;
                    orders[i] = Mathf.Clamp01(Mathf.InverseLerp(minY, maxY, targets[i].y) * 0.7f + (float)random.NextDouble() * 0.3f);
                    colors[i] = Color.Lerp(warmMote, coolMote, (float)random.NextDouble());
                    particles[i].startLifetime = particles[i].remainingLifetime = 3600;
                    particles[i].startSize = particleSize * 2.5f; particles[i].startColor = colors[i];
                }
                formationParticles.Play();
            }
            Progress = targetProgress = elapsed = progressVelocity = 0; paused = finished = false;
            UpdateVisuals(); return true;
        }
        public void SetProgress(float value) => targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(value));
        public void SetPaused(bool value) => paused = value;
        private void Update()
        {
            Advance(Time.deltaTime);
        }
        private void Advance(float deltaTime)
        {
            if (model == null || paused || finished) return;
            elapsed += deltaTime;
            Progress = Mathf.SmoothDamp(Progress, targetProgress, ref progressVelocity, transitionSeconds * 0.35f, Mathf.Infinity, deltaTime);
            if (targetProgress == 1 && Progress >= 0.9995f) Progress = 1;
            UpdateVisuals();
            if (Progress < 1) return;
            finished = true; RestoreMaterials();
            if (formationParticles != null) formationParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Revealed?.Invoke();
        }
        private void UpdateVisuals()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (revealBlocks == null) continue;
                for (int j = 0; j < revealBlocks[i].Length; j++)
                {
                    var materialBlock = revealBlocks[i][j]; materialBlock.SetFloat(RevealId, Progress);
                    renderers[i].SetPropertyBlock(materialBlock, j);
                }
            }
            if (!SurfaceSamplingAvailable || particles == null) return;
            float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.84f, 1f, Progress));
            for (int i = 0; i < particles.Length; i++)
            {
                float arrival = SurfaceFormationMotion.Arrival(Progress, orders[i]);
                particles[i].position = SurfaceFormationMotion.Position(center, starts[i], targets[i], arrival, elapsed, swirlRadiansPerSecond, phases[i], driftHeight);
                particles[i].startSize = particleSize * 2.5f * Mathf.Lerp(1, 0.65f, arrival);
                var color = colors[i]; color.a = fade * Mathf.Lerp(0.65f, 0.9f, arrival); particles[i].startColor = color;
            }
            formationParticles.SetParticles(particles, particles.Length);
        }
        private void RestoreMaterials()
        {
            if (renderers == null || originals == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && originals[i] != null)
                {
                    renderers[i].sharedMaterials = originals[i]; renderers[i].SetPropertyBlock(originalBlocks[i]);
                    for (int j = 0; j < originalSlotBlocks[i].Length; j++)
                        renderers[i].SetPropertyBlock(originalSlotBlocks[i][j].isEmpty ? null : originalSlotBlocks[i][j], j);
                }
            originals = null; originalBlocks = null; originalSlotBlocks = null; revealBlocks = null;
        }
        public void Cancel()
        {
            RestoreMaterials();
            if (formationParticles != null) formationParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            model = null; renderers = null; targets = starts = null; particles = null; orders = phases = null; colors = null;
            finished = true; SurfaceSamplingAvailable = false;
        }
        private void OnDisable() => Cancel();
#if UNITY_EDITOR
        [ContextMenu("Formation Preview/Progress To One Third (Play Mode)")]
        private void PreviewFirstCycle() { if (Application.isPlaying) SetProgress(1f / 3f); }
        [ContextMenu("Formation Preview/Progress To Two Thirds (Play Mode)")]
        private void PreviewSecondCycle() { if (Application.isPlaying) SetProgress(2f / 3f); }
        [ContextMenu("Formation Preview/Complete (Play Mode)")]
        private void PreviewComplete() { if (Application.isPlaying) SetProgress(1); }
#endif
        private void OnValidate()
        {
            particleCount = Mathf.Clamp(particleCount, 32, 512); transitionSeconds = Mathf.Max(0.5f, transitionSeconds);
            particleSize = Mathf.Clamp(particleSize, 0.005f, 0.03f); orbitRadius = Mathf.Clamp(orbitRadius, 0.1f, 0.5f);
            swirlRadiansPerSecond = Mathf.Clamp(swirlRadiansPerSecond, 0.1f, 1.2f); driftHeight = Mathf.Clamp(driftHeight, 0, 0.06f);
        }
    }
}
