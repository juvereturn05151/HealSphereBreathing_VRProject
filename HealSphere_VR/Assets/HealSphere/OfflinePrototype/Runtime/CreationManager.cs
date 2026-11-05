using System;
using System.Collections;
using UnityEngine;

namespace HealSphere.Breathing
{
    public sealed class CreationManager : MonoBehaviour
    {
        [Header("Reviewed Offline Gameplay Prefabs")]
        [SerializeField] private GameObject catPrefab;
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private GameObject moonPrefab;
        [Header("World Anchors")]
        [SerializeField] private Transform stagingAnchor;
        [SerializeField] private Transform[] creationAnchors = new Transform[3];
        [SerializeField] private ParticleModelReveal reveal;
        [SerializeField] private Vector3 maximumModelSize = new Vector3(0.45f, 0.45f, 0.45f);
        [SerializeField, Min(0.5f)] private float settleSeconds = 1.5f;
        public event Action CreationCompleted;
        public int CompletedCount { get; private set; }
        public bool IsConfigured => catPrefab != null && treePrefab != null && moonPrefab != null && stagingAnchor != null &&
            creationAnchors != null && creationAnchors.Length == 3 && creationAnchors[0] != null && creationAnchors[1] != null && creationAnchors[2] != null && reveal != null && reveal.IsConfigured;
        private readonly GameObject[] completed = new GameObject[3];
        private GameObject active;
        private int activeChoice = -1;
        private bool paused;
        private Coroutine settle;
        private Vector3 placementOffset;
        private void OnEnable() { if (reveal != null) reveal.Revealed += OnRevealed; }
        private void OnDisable() { if (reveal != null) reveal.Revealed -= OnRevealed; CancelActive(); }
        public bool HasCreated(int choice) => choice >= 0 && choice < 3 && completed[choice] != null;
        public bool BeginCreation(int choice)
        {
            if (!IsConfigured || choice < 0 || choice > 2 || active != null || HasCreated(choice) || CompletedCount >= 3) return false;
            var prefab = choice == 0 ? catPrefab : choice == 1 ? treePrefab : moonPrefab;
            active = Instantiate(prefab, stagingAnchor, false); active.name = "Dream " + prefab.name;
            activeChoice = choice; paused = false;
            try
            {
                Fit(active);
                placementOffset = active.transform.localPosition;
                if (reveal.Prepare(active)) return true;
            }
            catch (Exception exception) when (exception is UnityException || exception is InvalidOperationException)
            { Debug.LogError("Offline creation could not be prepared: " + exception.Message, this); }
            CancelActive(); return false;
        }
        public void SetProgress(float value) { if (active != null) reveal.SetProgress(value); }
        public void SetPaused(bool value) { paused = value; if (reveal != null) reveal.SetPaused(value); }
        private void OnRevealed() { if (active != null && settle == null) settle = StartCoroutine(Settle()); }
        private IEnumerator Settle()
        {
            var anchor = creationAnchors[CompletedCount];
            Vector3 start = active.transform.position;
            Quaternion rotation = active.transform.rotation;
            float elapsed = 0;
            while (elapsed < settleSeconds && active != null)
            {
                if (!paused)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0, 1, elapsed / settleSeconds);
                    active.transform.position = Vector3.Lerp(start, anchor.TransformPoint(placementOffset), t);
                    active.transform.rotation = Quaternion.Slerp(rotation, anchor.rotation, t);
                }
                yield return null;
            }
            if (active == null) yield break;
            active.transform.SetParent(anchor, true); active.transform.SetPositionAndRotation(anchor.TransformPoint(placementOffset), anchor.rotation);
            completed[activeChoice] = active; active = null; activeChoice = -1; CompletedCount++; settle = null;
            reveal.Cancel(); CreationCompleted?.Invoke();
        }
        public void CancelActive()
        {
            if (settle != null) StopCoroutine(settle);
            settle = null;
            if (reveal != null) reveal.Cancel();
            if (active != null) Destroy(active);
            active = null; activeChoice = -1; paused = false;
        }
        public void ResetCreations()
        {
            CancelActive();
            for (int i = 0; i < completed.Length; i++) { if (completed[i] != null) Destroy(completed[i]); completed[i] = null; }
            CompletedCount = 0;
        }
#if UNITY_EDITOR
        [ContextMenu("Formation Preview/Begin Cat Without Session (Play Mode)")]
        private void PreviewCat()
        {
            var session = GetComponent<BreathingSessionManager>();
            if (!Application.isPlaying || (session != null && session.Phase != SessionPhase.NotReady)) return;
            BeginCreation(0);
        }
        [ContextMenu("Formation Preview/Clear Preview Objects (Play Mode)")]
        private void ClearPreview()
        {
            var session = GetComponent<BreathingSessionManager>();
            if (Application.isPlaying && (session == null || session.Phase == SessionPhase.NotReady)) ResetCreations();
        }
#endif
        private void Fit(GameObject model)
        {
            var root = model.transform; root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Prefab has no renderers.");
            bool first = true;
            var bounds = new Bounds();
            foreach (var renderer in renderers)
            {
                var b = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = stagingAnchor.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) throw new InvalidOperationException("Invalid prefab bounds.");
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            if (bounds.size.sqrMagnitude < 0.000001f) throw new InvalidOperationException("Prefab bounds are empty.");
            var size = bounds.size;
            float scale = Mathf.Min(maximumModelSize.x / Mathf.Max(size.x, 0.0001f), maximumModelSize.y / Mathf.Max(size.y, 0.0001f), maximumModelSize.z / Mathf.Max(size.z, 0.0001f));
            root.localScale = Vector3.one * scale;
            root.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
        }
        private void OnValidate() { maximumModelSize = Vector3.Max(Vector3.one * 0.05f, maximumModelSize); settleSeconds = Mathf.Max(0.5f, settleSeconds); }
    }
}
