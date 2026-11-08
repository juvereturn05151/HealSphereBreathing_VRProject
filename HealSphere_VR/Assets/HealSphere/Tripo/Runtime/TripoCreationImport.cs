using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

namespace HealSphere.Tripo
{
    public sealed partial class TripoModelLoader
    {
        [Header("Animal Placement")]
        [Tooltip("Optional placement socket directly under Creation Staging Anchor. Leave empty to use the anchor directly.")]
        [SerializeField] private Transform animalPlacementRoot;
        [Tooltip("Local offset in meters applied after centering and grounding the imported geometry.")]
        [SerializeField] private Vector3 modelPositionOffset;
        [Tooltip("Fitted animal height in meters before the awakening animation.")]
        [SerializeField, Range(0.1f, 1f)] private float targetAnimalHeight = 0.45f;
        [Tooltip("Use the camera's initial position to set upright yaw. Turn off to use the authored anchor orientation.")]
        [SerializeField] private bool automaticallyFaceCamera;
        [Tooltip("Stage 1.75 meters along the headset's horizontal forward direction once per creation, 0.65 meters below eye height.")]
        [SerializeField] private bool useCameraRelativeStaging;

        public bool LiveConnectionRequested { get; private set; }
        public bool HasBackendConnection => !string.IsNullOrWhiteSpace(backendUrl) && (backendAccessToken?.Length ?? 0) >= 32;
        public Task<byte[]> PrepareAnimalAsync(string json, CancellationToken token) =>
            Request("/api/animals/prepare", json, token);
        [Serializable] private sealed class LocalConnection
        {
            [SerializeField] private string url;
            [SerializeField] private string token;
            [SerializeField] private bool live;
            public string Url => url; public string Token => token; public bool Live => live;
        }
        /// <summary>Reads a device-local development capability, never a provider API key or committed asset.</summary>
        public void ReloadLocalConnection()
        {
            string path = Path.Combine(Application.persistentDataPath, "healsphere-backend.json");
            if (!File.Exists(path)) return;
            try
            {
                if (new FileInfo(path).Length > 4096) throw new InvalidOperationException();
                var settings = JsonUtility.FromJson<LocalConnection>(File.ReadAllText(path));
                if (settings == null || !Uri.TryCreate(settings.Url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https") || (settings.Token?.Length ?? 0) < 32) throw new InvalidOperationException();
                backendUrl = settings.Url; backendAccessToken = settings.Token; LiveConnectionRequested = settings.Live;
            }
            catch { Debug.LogWarning("HealSphere: device-local backend settings are invalid; using offline mode.", this); LiveConnectionRequested = false; }
        }
        public Task<byte[]> SendCreationRequestAsync(string route, string json, CancellationToken cancellationToken)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(route, @"^/api/(conversation|capabilities|sessions|library(?:/search|/[a-f0-9-]{36}(?:/model)?)?|proposals/[a-f0-9-]{36}/cancel|creations(?:/[a-f0-9-]{36}(?:/(?:model|cancel))?)?)$"))
                throw new InvalidOperationException("Invalid creation route.");
            return Request(route, json, cancellationToken);
        }
        public async Task<GameObject> LoadCreationAsync(byte[] bytes, string name, bool reviewedDemo,
            CancellationToken cancellationToken, bool stageAnimal = false, Pose? viewerAtStart = null)
        {
            RequireAnchor();
            if (bytes == null || bytes.Length < 28 || bytes.Length > MaxModelBytes || BitConverter.ToUInt32(bytes, 0) != 0x46546c67 ||
                BitConverter.ToUInt32(bytes, 4) != 2 || BitConverter.ToUInt32(bytes, 8) != bytes.Length)
                throw new InvalidOperationException("Invalid GLB header or download size.");
            var importer = new GltfImport(); GameObject candidate = null;
            try
            {
                if (!await importer.Load(bytes, cancellationToken: cancellationToken)) throw new InvalidOperationException("GLB import failed.");
                cancellationToken.ThrowIfCancellationRequested();
                candidate = new GameObject(name); candidate.SetActive(false);
                var geometry = candidate.transform;
                if (stageAnimal)
                {
                    PositionAnimal(candidate.transform, viewerAtStart);
                    geometry = new GameObject("Generated Animal Model").transform;
                    geometry.SetParent(candidate.transform, false);
                }
                else candidate.transform.SetParent(creationAnchor, false);
                if (!await importer.InstantiateMainSceneAsync(geometry, cancellationToken)) throw new InvalidOperationException("GLB instantiation failed.");
                cancellationToken.ThrowIfCancellationRequested();
                long vertices = 0, triangles = 0; int materials = 0;
                foreach (var filter in candidate.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue; vertices += filter.sharedMesh.vertexCount;
                    for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) triangles += filter.sharedMesh.GetIndexCount(i) / 3;
                }
                foreach (var renderer in candidate.GetComponentsInChildren<Renderer>(true))
                { materials += renderer.sharedMaterials.Length; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; }
                if (vertices > 60000 || triangles < 1 || triangles > (reviewedDemo ? 60000 : 20000) || materials > 8)
                    throw new InvalidOperationException("Model exceeds the Quest geometry/material budget.");
                if (stageAnimal) Fit(geometry.gameObject, candidate.transform, targetAnimalHeight, modelPositionOffset);
                else Fit(candidate);
                candidate.AddComponent<RuntimeModelOwnership>().Own(importer); importer = null;
                var result = candidate; candidate = null; return result; // Remains hidden until the reveal system is prepared.
            }
            finally { if (candidate != null) Destroy(candidate); importer?.Dispose(); }
        }

        private void PositionAnimal(Transform placement, Pose? viewerAtStart)
        {
            if (animalPlacementRoot != null && animalPlacementRoot.parent != creationAnchor)
                throw new InvalidOperationException("Animal Placement Root must be a direct child of Creation Staging Anchor.");
            placement.SetParent(animalPlacementRoot != null ? animalPlacementRoot : creationAnchor, false);
            if (!viewerAtStart.HasValue) return;

            var viewer = viewerAtStart.Value;
            if (useCameraRelativeStaging)
            {
                var forward = Vector3.ProjectOnPlane(viewer.rotation * Vector3.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.001f)
                    forward = Vector3.ProjectOnPlane(placement.position - viewer.position, Vector3.up);
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                placement.position = viewer.position + forward.normalized * 1.75f - Vector3.up * 0.65f;
            }
            if (automaticallyFaceCamera)
            {
                var towardViewer = Vector3.ProjectOnPlane(viewer.position - placement.position, Vector3.up);
                if (towardViewer.sqrMagnitude > 0.001f) placement.rotation = Quaternion.LookRotation(towardViewer);
            }
        }

        public async Task<byte[]> ReadLocalDemoAsync(CancellationToken cancellationToken)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "healsphere-cat-demo.glb");
            string url = path.Contains("://") ? path : new Uri(path).AbsoluteUri;
            using var request = UnityWebRequest.Get(url); request.timeout = 30;
            var pending = request.SendWebRequest();
            try
            {
                while (!pending.isDone) { cancellationToken.ThrowIfCancellationRequested(); if (request.downloadedBytes > MaxModelBytes) throw new InvalidOperationException("Demo exceeds download budget."); await Task.Yield(); }
                cancellationToken.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success) throw new InvalidOperationException("Reviewed local demo GLB is unavailable.");
                return request.downloadHandler.data;
            }
            finally { if (!pending.isDone) request.Abort(); }
        }
    }
}

