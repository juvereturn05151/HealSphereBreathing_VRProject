using System;
using System.IO;
using System.Linq;
using System.Text;
using HealSphere.Breathing;
using HealSphere.Tripo;
using HealSphere.Wellness;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HealSphere.Creation.Editor
{
    /// <summary>Opt-in test of existing authored UI and the real saved fox. The server must have paid generation disabled.</summary>
    [InitializeOnLoad]
    public static class LibraryExperienceVerification
    {
        private const string Marker = "Library/HealSphereLibraryPlaytest.request", Report = "Library/HealSphereLibraryPlaytest.txt", Running = "HealSphere.LibraryVerification";
        [Serializable] private sealed class Connection { [SerializeField] private string url, token, creationId; public string Url => url; public string Token => token; public string Creation => creationId; }
        [Serializable] private sealed class Budget { [SerializeField] private int used; public int Used => used; }
        [Serializable] private sealed class Capability { [SerializeField] private bool liveGenerationEnabled; [SerializeField] private Budget generationBudget; public bool Enabled => liveGenerationEnabled; public int Paid => generationBudget.Used; }
        private static CreationSessionDirector director;
        private static CreationSessionPresenter presenter;
        private static TripoModelLoader loader;
        private static ParticleModelReveal reveal;
        private static int step, frames;
        private static float volume;
        private static double deadline;
        private static string errors;
        private static bool surface, checking;
        static LibraryExperienceVerification()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false)) Begin();
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Running, false);
            };
        }
        [MenuItem("HealSphere/Creation/Verify Real Saved Fox Reuse (No Paid Generation)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().path != CreationSceneAssembler.ScenePath) throw new InvalidOperationException("Open and save creation mode outside Play Mode first.");
            if (!File.Exists("Library/HealSphereLibraryVerification.connection.json")) throw new InvalidOperationException("Start the protected library verification server first.");
            SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
        }
        private static T One<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(x => x.name == name);
        private static void Set(Object target, string name, string value) { var settings = new SerializedObject(target); settings.FindProperty(name).stringValue = value; settings.ApplyModifiedPropertiesWithoutUndo(); }
        private static async void Begin()
        {
            volume = AudioListener.volume;
            try {
                var config = JsonUtility.FromJson<Connection>(File.ReadAllText("Library/HealSphereLibraryVerification.connection.json"));
                Require(config.Url == "http://127.0.0.1:3337" && config.Token.Length >= 32 && Guid.TryParse(config.Creation, out _), "Pinned local verification service and approved fox");
                Require(!File.Exists(Path.Combine(Application.persistentDataPath, "healsphere-backend.json")), "Existing device connection is not overwritten; temporarily move it before this isolated check");
                director = One<CreationSessionDirector>(); presenter = One<CreationSessionPresenter>(); loader = One<TripoModelLoader>(); reveal = One<ParticleModelReveal>();
                Require(director.IsConfigured, "All authored scene references assigned");
                Set(loader, "backendUrl", config.Url); Set(loader, "backendAccessToken", config.Token);
                typeof(TripoModelLoader).GetProperty("LiveConnectionRequested").SetValue(loader, true);
                typeof(CreationSessionDirector).GetField("live", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(director, true);
                var comfort = new SerializedObject(One<VRComfortSettings>()); comfort.FindProperty("forceDesktopSimulation").boolValue = true; comfort.ApplyModifiedPropertiesWithoutUndo();
                step = frames = 0; errors = ""; surface = false; checking = true; Time.timeScale = 8; AudioListener.volume = 0; deadline = EditorApplication.timeSinceStartup + 180;
                Application.logMessageReceived += Log; File.WriteAllText(Report, "Running actual saved Tripo fox, real backend conversation, paid generation disabled.\n");
                await CheckBudget(); checking = false; director.NewRequest(); presenter.SetRequestForTesting("I want a cute sleeping fox."); Button("PauseButton").onClick.Invoke(); step = 1;
            } catch (Exception e) { Finish(e.ToString()); }
        }
        private static async System.Threading.Tasks.Task CheckBudget()
        {
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
            var bytes = await loader.SendCreationRequestAsync("/api/capabilities", null, timeout.Token);
            var status = JsonUtility.FromJson<Capability>(Encoding.UTF8.GetString(bytes));
            Require(!status.Enabled && status.Paid == 1, "Paid requests disabled and durable submission count remains exactly one");
        }
        private static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors += message + "\n"; }
        private static async void Tick()
        {
            const string restore = "Library/HealSphereLibraryRestoreTestScene.request";
            if (File.Exists(restore) && !EditorApplication.isPlayingOrWillChangePlaymode) {
                File.Delete(restore);
                // Explicit opt-in restoration of test-only in-memory changes; never saves or overwrites an asset.
                EditorSceneManager.OpenScene(CreationSceneAssembler.ScenePath);
            }
            if (File.Exists(Marker) && !EditorApplication.isPlayingOrWillChangePlaymode) { File.Delete(Marker); try { Run(); } catch (Exception e) { File.WriteAllText(Report, e.ToString()); } }
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Running, false) || checking || director == null && step < 7) return;
            try {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Library verification timed out at step " + step);
                if (director != null) {
                    if (step != 5) Require(director.State != CreationSessionState.ErrorOrOfflineFallback, "Saved fox must load successfully");
                    if (step < 4) Require(director.State != CreationSessionState.AwaitingConfirmation, "Suitable saved fox starts breathing automatically, without paid confirmation");
                    if (director.State == CreationSessionState.RevealingCreation) {
                        Require(director.ChargedCycles >= 3 && reveal.SurfaceSamplingAvailable, "Real fox imported and sampled after charge cycles"); surface = true;
                        var message = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(x => x.name == "MomentMessage");
                        Require(!message.isTextOverflowing, "Saved-model guidance fits authored panel");
                    }
                }
                if (step == 1 && director.State == CreationSessionState.CreationComplete) {
                    Require(surface && director.CompletedCount == 1, "First saved fox completes actual mesh formation");
                    var imported = One<RuntimeModelOwnership>();
                    var meshes = imported.GetComponentsInChildren<MeshFilter>(true);
                    Require(meshes.Sum(x => x.sharedMesh.triangles.Length / 3) == 4942, "Real saved fox geometry");
                    var renderer = imported.GetComponentsInChildren<Renderer>(true).Single();
                    var material = renderer.sharedMaterial;
                    Require(material != null && material.shader.isSupported && material.HasProperty("baseColorTexture") && material.GetTexture("baseColorTexture") != null, "Original supported glTF material and texture restored");
                    Require(renderer.bounds.size.magnitude > .1f && renderer.bounds.size.magnitude < 2, "Comfortable bounded model scale");
                    ScreenCapture.CaptureScreenshot("Library/HealSphereRealFoxCompleted.png"); frames = 0; step = 2;
                } else if (step == 2 && ++frames > 4) {
                    Button("PauseButton").onClick.Invoke(); presenter.SetRequestForTesting("Can you make a cute fox taking a nap?"); Button("PauseButton").onClick.Invoke(); step = 3;
                } else if (step == 3 && director.State == CreationSessionState.CreationComplete) {
                    Require(director.CompletedCount == 2 && Object.FindObjectsByType<RuntimeModelOwnership>(FindObjectsSortMode.None).Length == 2, "Second saved fox imports without duplicate instances or stale state");
                    Require(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Single(x => x.name == "Creation Surface Motes").particleCount == 0, "Formation motes cleaned up");
                    checking = true; await CheckBudget(); checking = false;
                    Button("PauseButton").onClick.Invoke(); presenter.SetRequestForTesting("A standing fox."); Button("PauseButton").onClick.Invoke(); step = 4;
                } else if (step == 4 && director.State == CreationSessionState.AwaitingConfirmation) {
                    // Intentionally do not invoke its paid-generation button.
                    checking = true; await CheckBudget(); checking = false;
                    Set(loader, "backendUrl", "http://127.0.0.1:1");
                    director.NewRequest(); presenter.SetRequestForTesting("A sleeping fox."); Button("PauseButton").onClick.Invoke(); step = 5;
                } else if (step == 5 && director.State == CreationSessionState.ErrorOrOfflineFallback) {
                    Button("RestartButton").onClick.Invoke(); step = 6; // Existing secondary action: explicitly choose offline cat.
                } else if (step == 6 && director.State == CreationSessionState.CreationComplete) {
                    Require(director.CompletedCount == 3, "Disconnected backend recovers through reviewed offline cat");
                    Button("WellnessButton").onClick.Invoke(); step = 7;
                } else if (step == 7 && SceneManager.GetActiveScene().name == "HealSphereMoment") {
                    var wellness = One<ExperienceDirector>();
                    if (wellness.Phase != ExperiencePhase.Completed) return;
                    Require(wellness.ElapsedSeconds == 120, "Preserved wellness runs for 120 active seconds"); Button("RestartButton").onClick.Invoke();
                    Require(wellness.ElapsedSeconds == 0 && wellness.Phase == ExperiencePhase.Arrival, "Wellness restart");
                    Require(errors.Length == 0, "No runtime errors: " + errors); Finish(null);
                }
            } catch (Exception e) { Finish(e.ToString()); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Finish(string error)
        {
            Application.logMessageReceived -= Log; Time.timeScale = 1; AudioListener.volume = volume; checking = false;
            File.AppendAllText(Report, error ?? "PASS: actual approved Tripo GLB (4942 triangles), exact and taking-a-nap saved-fox requests, automatic reuse without paid confirmation, real mesh sampling, three charge cycles, particle formation, supported original material and texture restored, bounded scale, two retained foxes, cleanup, standing fox excluded, paid submissions stayed at one, disconnected backend recovers through explicitly chosen offline cat, full wellness mode and restart, no runtime errors.\n");
            SessionState.SetBool(Running, false); director = null; EditorApplication.isPlaying = false;
            File.WriteAllText("Library/HealSphereLibraryRestoreTestScene.request", "Restore the saved scene after temporary runtime test settings.");
        }
    }
}
