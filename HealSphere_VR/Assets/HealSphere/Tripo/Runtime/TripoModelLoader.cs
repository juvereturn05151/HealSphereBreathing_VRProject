using System;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

namespace HealSphere.Tripo
{
    /// <summary>Explicit, single-cat test client. No network requests are made on Play Mode entry.</summary>
    public sealed partial class TripoModelLoader : MonoBehaviour
    {
        [Header("Backend (Never Enter The Tripo API Key)")]
        [SerializeField] private string backendUrl = "http://127.0.0.1:3000";
        [Tooltip("Local development backend access token. Keep it out of committed scenes and builds for distribution.")]
        [SerializeField] private string backendAccessToken;
        [SerializeField, Min(1)] private int requestTimeoutSeconds = 30;
        [SerializeField, Min(30)] private int operationTimeoutSeconds = 660;
        [SerializeField, Min(2)] private float pollIntervalSeconds = 3f;

        [Header("Scene References")]
        [InspectorName("Creation Staging Anchor")]
        [Tooltip("Sets the authored reveal position and orientation. Keep its scale at (1,1,1).")]
        [SerializeField] private Transform creationAnchor;
        [SerializeField] private GameObject offlineFallbackPrefab;
        [SerializeField] private bool useFallbackOnError = true;
        [Tooltip("Optional Quest test: only fetch an already approved model on Play Mode entry. Never generates.")]
        [SerializeField] private bool loadApprovedModelOnStart;

        [Header("Display Bounds (Meters, In Anchor Space)")]
        [SerializeField] private Vector3 maximumSize = new Vector3(0.5f, 0.5f, 0.5f);
        [InspectorName("Model Rotation Offset")]
        [Tooltip("Local Euler correction for the imported asset's forward axis. Try Y = 90, -90, or 180 for sideways or backward models.")]
        [SerializeField] private Vector3 modelRotation;

        public string Status { get; private set; } = "Idle";
        public string LastError { get; private set; }
        public float Progress { get; private set; }
        public bool IsBusy { get; private set; }
        public event Action StateChanged;

        private CancellationTokenSource operation;
        private GameObject currentModel;
        private GltfImport currentImport;
        private string currentTaskId;
        private const int MaxModelBytes = 32 * 1024 * 1024;
        [Serializable] private sealed class BackendError
        {
            [SerializeField] private string error;
            public string Code => error;
        }

        // Private DTO fields are populated through JsonUtility; no mutable public data API.
        [Serializable]
        private sealed class Job
        {
            [SerializeField] private string task_id;
            [SerializeField] private string status;
            [SerializeField] private string error;
            [SerializeField] private float progress;
            public string TaskId => task_id;
            public string Status => status;
            public string Error => error;
            public float Progress => progress;
        }

        private void Start()
        {
            if (loadApprovedModelOnStart) LoadApprovedModel();
        }

        [ContextMenu("Tripo/1 Check Backend Connection (Free)")]
        public void CheckBackendConnection() => Run(async ct =>
        {
            await Request("/health", null, ct);
            SetStatus("Backend connected");
        });

        [ContextMenu("Tripo/2 Check Authentication And Balance (Free)")]
        public void CheckBalance() => Run(async ct =>
        {
            var bytes = await Request("/api/balance", null, ct);
            // Backend returns only sanitized numeric balance/frozen values.
            SetStatus("Tripo authenticated: " + System.Text.Encoding.UTF8.GetString(bytes));
        });

        [ContextMenu("Tripo/3 Generate ONE Test Cat (PAID, Explicit)")]
        public void GenerateTestCat() => Run(async ct =>
        {
            RequireAnchor();
            await Request("/api/test-cat", "{\"confirm\":\"GENERATE_ONE_CAT\"}", ct);
            await Poll(ct);
        });

        [ContextMenu("Tripo/4 Resume Polling Existing Task (No Submission)")]
        public void ResumePolling() => Run(Poll);

        [ContextMenu("Tripo/5 Load Reviewed And Approved Model (No Submission)")]
        public void LoadApprovedModel() => Run(async ct =>
        {
            RequireAnchor();
            var job = await ReadJob(ct);
            if (job.Status != "ready") throw new InvalidOperationException("Model is not approved. Review Backend/data/test-cat.glb and approve using the README steps.");
            if (currentModel != null && currentTaskId == job.TaskId) { SetStatus("Model already displayed"); return; }
            await Import(job.TaskId, ct);
        });

        [ContextMenu("Tripo/Show Offline Fallback (Free)")]
        public void ShowOfflineFallback()
        {
            if (!Application.isPlaying || IsBusy) return;
            try { ShowFallback(); } catch { SetStatus("Assign a valid fallback prefab and Creation Anchor in the Inspector."); }
        }

        [ContextMenu("Tripo/Cancel Local Operation (Does Not Cancel Paid Task)")]
        public void CancelOperation() => operation?.Cancel();

        private async void Run(Func<CancellationToken, Task> action)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsBusy) return;
            IsBusy = true;
            LastError = null;
            var source = new CancellationTokenSource(TimeSpan.FromSeconds(Mathf.Max(30, operationTimeoutSeconds)));
            operation = source;
            try
            {
                SetStatus("Loading");
                await action(source.Token);
                source.Token.ThrowIfCancellationRequested();
            }
            catch (Exception e)
            {
                if (this != null && isActiveAndEnabled)
                {
                    LastError = e is OperationCanceledException ? "Operation cancelled or timed out; backend task may still be running. Resume polling; do not resubmit." : e.Message;
                    SetStatus("Error: " + LastError);
                    Debug.LogWarning("TripoModelLoader: " + LastError, this);
                    if (useFallbackOnError && currentModel == null && offlineFallbackPrefab != null)
                    {
                        try { ShowFallback(); } catch { Debug.LogWarning("TripoModelLoader: fallback could not be fitted.", this); }
                    }
                }
            }
            finally
            {
                source.Dispose();
                if (ReferenceEquals(operation, source)) operation = null;
                IsBusy = false;
            }
        }

        private async Task Poll(CancellationToken ct)
        {
            while (true)
            {
                var job = await ReadJob(ct);
                Progress = Mathf.Clamp01(job.Progress / 100f);
                SetStatus(job.Status);
                if (job.Status == "ready") { RequireAnchor(); await Import(job.TaskId, ct); return; }
                if (job.Status == "awaiting_review")
                {
                    SetStatus("Downloaded and validated. Review Backend/data/test-cat.glb, then approve and Load Reviewed And Approved Model.");
                    return;
                }
                if (job.Status != "polling" && job.Status != "downloading")
                    throw new InvalidOperationException("Backend task: " + job.Status + ". " + job.Error + " See README recovery steps.");
                await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(2, pollIntervalSeconds)), ct);
            }
        }

        private async Task<Job> ReadJob(CancellationToken ct)
        {
            var bytes = await Request("/api/test-cat", null, ct);
            var job = JsonUtility.FromJson<Job>(System.Text.Encoding.UTF8.GetString(bytes));
            if (job == null || string.IsNullOrEmpty(job.Status)) throw new InvalidOperationException("Invalid backend task response.");
            return job;
        }

        private async Task<byte[]> Request(string route, string body, CancellationToken ct)
            => await RequestBytes(route, body == null ? null : System.Text.Encoding.UTF8.GetBytes(body), "application/json", ct);

        private async Task<byte[]> RequestBytes(string route, byte[] body, string contentType, CancellationToken ct)
        {
            if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var root) ||
                (root.Scheme != "http" && root.Scheme != "https") || !string.IsNullOrEmpty(root.UserInfo) ||
                !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment))
                throw new InvalidOperationException("Configure a valid HTTP(S) backend base URL without credentials or query strings.");
            using var request = new UnityWebRequest(backendUrl.TrimEnd('/') + route, body == null ? "GET" : "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);
            request.redirectLimit = 0;
            if (route != "/health") request.SetRequestHeader("X-Backend-Token", backendAccessToken ?? "");
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.SetRequestHeader("Content-Type", contentType);
            }
            var pending = request.SendWebRequest();
            var limit = route.EndsWith("/model", StringComparison.Ordinal) ? MaxModelBytes : route.StartsWith("/api/speech/audio/", StringComparison.Ordinal) ? 4 * 1024 * 1024 : 65536;
            try
            {
                while (!pending.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    if (request.downloadedBytes > (ulong)limit) throw new InvalidOperationException("Backend response exceeds download budget.");
                    await Task.Yield();
                }
                ct.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (request.result == UnityWebRequest.Result.ConnectionError && request.responseCode <= 0)
                        throw new InvalidOperationException("Backend: CONNECTION_UNAVAILABLE");
                    if (route.StartsWith("/api/", StringComparison.Ordinal) && request.downloadHandler.data?.Length < 65536)
                    {
                        try
                        {
                            var error = JsonUtility.FromJson<BackendError>(request.downloadHandler.text);
                            if (error != null && System.Text.RegularExpressions.Regex.IsMatch(error.Code ?? "", "^[A-Z][A-Z0-9_]{1,79}$"))
                                throw new InvalidOperationException("Backend: " + error.Code);
                        }
                        catch (ArgumentException) { }
                    }
                    throw new InvalidOperationException("Backend request failed (HTTP " + request.responseCode + "). Check connection, local access token, backend state, and README. No automatic paid retry.");
                }
                var bytes = request.downloadHandler.data;
                if (bytes == null || bytes.Length > limit) throw new InvalidOperationException("Invalid backend response size.");
                return bytes;
            }
            finally { if (!pending.isDone) request.Abort(); }
        }

        private async Task Import(string taskId, CancellationToken ct)
        {
            if (currentModel != null && currentTaskId == taskId) return;
            var bytes = await Request("/api/test-cat/model", null, ct);
            if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546c67 ||
                BitConverter.ToUInt32(bytes, 4) != 2 || BitConverter.ToUInt32(bytes, 8) != bytes.Length)
                throw new InvalidOperationException("Backend did not return a GLB 2.0 file.");
            var importer = new GltfImport();
            GameObject candidate = null;
            try
            {
                SetStatus("Importing GLB");
                if (!await importer.Load(bytes, cancellationToken: ct))
                    throw new InvalidOperationException("glTFast GLB import failed. Check shader variants and supported extensions.");
                ct.ThrowIfCancellationRequested();
                RequireAnchor();
                candidate = new GameObject("Tripo Reviewed Cat");
                candidate.SetActive(false);
                candidate.transform.SetParent(creationAnchor, false);
                if (!await importer.InstantiateMainSceneAsync(candidate.transform, ct))
                    throw new InvalidOperationException("glTFast scene instantiation failed.");
                ct.ThrowIfCancellationRequested();
                Fit(candidate);
                ClearModel();
                currentModel = candidate; currentImport = importer; currentTaskId = taskId;
                candidate = null; importer = null;
                currentModel.SetActive(true);
                SetStatus("Reviewed model displayed");
            }
            finally
            {
                if (candidate != null) Destroy(candidate);
                importer?.Dispose();
            }
        }

        private void Fit(GameObject model, Transform fitSpace = null, float targetHeight = 0, Vector3 positionOffset = default)
        {
            var root = model.transform;
            fitSpace ??= creationAnchor;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.Euler(modelRotation);
            root.localScale = Vector3.one;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers.");
            var bounds = new Bounds();
            var initialized = false;
            foreach (var renderer in renderers)
            {
                var b = renderer.localBounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = fitSpace.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) throw new InvalidOperationException("Invalid model bounds.");
                    if (!initialized) { bounds = new Bounds(p, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(p);
                }
            }
            var size = bounds.size;
            if (size.sqrMagnitude < 0.00000001f) throw new InvalidOperationException("Model bounds are empty.");
            var scale = targetHeight > 0 && size.y > 0.0001f
                ? targetHeight / size.y
                : Mathf.Min(maximumSize.x / Mathf.Max(size.x, 0.0001f), maximumSize.y / Mathf.Max(size.y, 0.0001f), maximumSize.z / Mathf.Max(size.z, 0.0001f));
            root.localScale = Vector3.one * scale;
            root.localPosition = positionOffset - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
        }

        private void ShowFallback()
        {
            RequireAnchor();
            if (offlineFallbackPrefab == null) throw new InvalidOperationException("Assign an offline fallback prefab.");
            if (currentModel != null) return;
            var candidate = Instantiate(offlineFallbackPrefab, creationAnchor, false);
            try { Fit(candidate); currentModel = candidate; currentTaskId = null; }
            catch { Destroy(candidate); throw; }
        }

        private void RequireAnchor()
        {
            if (creationAnchor == null) throw new InvalidOperationException("Assign CreationAnchor_01 to Creation Anchor in the Inspector.");
        }

        private void SetStatus(string value)
        {
            Status = value;
            Debug.Log("TripoModelLoader: " + value, this);
            StateChanged?.Invoke();
        }

        private void ClearModel()
        {
            if (currentModel != null) { currentModel.SetActive(false); Destroy(currentModel); }
            currentModel = null;
            currentImport?.Dispose(); currentImport = null; currentTaskId = null;
        }

        private void OnDisable() { operation?.Cancel(); ClearModel(); }
        private void OnValidate()
        {
            maximumSize = Vector3.Max(Vector3.one * 0.01f, maximumSize);
            requestTimeoutSeconds = Mathf.Max(1, requestTimeoutSeconds);
            operationTimeoutSeconds = Mathf.Max(30, operationTimeoutSeconds);
            pollIntervalSeconds = Mathf.Max(2, pollIntervalSeconds);
        }
    }
}
