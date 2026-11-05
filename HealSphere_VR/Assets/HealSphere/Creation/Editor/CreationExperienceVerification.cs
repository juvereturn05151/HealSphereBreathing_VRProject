using System;
using System.IO;
using System.Linq;
using HealSphere.Breathing;
using HealSphere.Wellness;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HealSphere.Creation.Editor
{
    /// <summary>Opt-in, zero-cost integration test against the authored scene and real demo GLB. Creates no UI.</summary>
    [InitializeOnLoad]
    public static class CreationExperienceVerification
    {
        private const string Request = "Library/HealSphereCreationPlaytest.request";
        private const string Report = "Library/HealSphereCreationPlaytest.txt";
        private const string Running = "HealSphere.CreationVerification";
        private static CreationSessionDirector director;
        private static CreationSessionPresenter presenter;
        private static ParticleModelReveal reveal;
        private static int step, frames, previousCycles;
        private static double deadline;
        private static string errors;
        private static bool surface, inhale, exhale, audio;
        private static bool mock;
        private static bool capturedFormation;
        private static float volume, pausedProgress;
        static CreationExperienceVerification()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false)) Begin();
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Running, false);
            };
        }
        [MenuItem("HealSphere/Creation/Verify Offline Creation And Wellness In Play Mode")]
        public static void Run() => RunCheck(false);
        private static void RunCheck(bool mocked)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().path != CreationSceneAssembler.ScenePath)
                throw new InvalidOperationException("Open and save HealSphereCreation outside Play Mode first.");
            SessionState.SetBool(Running + ".Mock", mocked); SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
        }
        private static T One<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
        private static TMP_Text Message => Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "MomentMessage");
        private static void Begin()
        {
            volume = AudioListener.volume;
            try
            {
                director = One<CreationSessionDirector>(); presenter = One<CreationSessionPresenter>(); reveal = One<ParticleModelReveal>();
                Require(director.IsConfigured, "Serialized creation references assigned");
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script: " + transform.name);
                mock = SessionState.GetBool(Running + ".Mock", false);
                var settings = new SerializedObject(director); settings.FindProperty("useBackendWhenConfigured").boolValue = mock; settings.ApplyModifiedPropertiesWithoutUndo();
                if (mock)
                {
                    Require(!File.Exists(Path.Combine(Application.persistentDataPath, "healsphere-backend.json")), "Mock check requires no device-local backend configuration; existing configuration is never overwritten");
                    var loader = One<HealSphere.Tripo.TripoModelLoader>(); var fields = new SerializedObject(loader);
                    fields.FindProperty("backendUrl").stringValue = "http://127.0.0.1:3317";
                    fields.FindProperty("backendAccessToken").stringValue = "healsphere-local-mock-only-0123456789"; fields.ApplyModifiedPropertiesWithoutUndo();
                    typeof(HealSphere.Tripo.TripoModelLoader).GetProperty("LiveConnectionRequested").SetValue(loader, true);
                    // EnteredPlayMode may follow Start; force only this test instance after pinning its fake localhost connection.
                    typeof(CreationSessionDirector).GetField("live", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(director, true);
                }
                var comfort = new SerializedObject(One<VRComfortSettings>()); comfort.FindProperty("forceDesktopSimulation").boolValue = true; comfort.ApplyModifiedPropertiesWithoutUndo();
                step = frames = 0; errors = ""; surface = inhale = exhale = audio = capturedFormation = false;
                One<GuidedBreathingProvider>().Signal += s => { inhale |= s.Phase == BreathPhase.Inhale; exhale |= s.Phase == BreathPhase.Exhale; };
                AudioListener.volume = 0; Time.timeScale = 8; deadline = EditorApplication.timeSinceStartup + 180;
                File.WriteAllText(Report, "Running authored-scene " + (mock ? "MOCK provider (incomplete fixture mesh, not a generated fox)" : "offline cat") + " integration check, accelerated guidance; no real provider calls.\n");
                Application.logMessageReceived += Log;
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        private static void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors += message + "\n"; }
        private static void Tick()
        {
            if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                string mode; try { mode = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
                try { RunCheck(mode == "mock"); } catch (Exception e) { File.WriteAllText(Report, e.ToString()); }
            }
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Running, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Creation verification exceeded 180 seconds at step " + step);
                if (step < 8 && director == null) return;
                if (director != null && director.State == CreationSessionState.ErrorOrOfflineFallback) throw new InvalidOperationException("Creation failed: " + Message.text);
                if (director != null && director.State == CreationSessionState.RevealingCreation)
                {
                    surface |= reveal.SurfaceSamplingAvailable;
                    Require(director.ChargedCycles >= 3, "Model held until three charge cycles");
                    Require(reveal.SurfaceSamplingAvailable, "Actual imported cat mesh is sampled");
                    var block = new MaterialPropertyBlock(); director.CurrentModel.GetComponentInChildren<Renderer>().GetPropertyBlock(block, 0);
                    if (reveal.Progress < 1 && (block.GetTexture("_BaseMap") == null || block.GetTexture("_BaseMap") == Texture2D.whiteTexture))
                    {
                        var originals = (Material[][])typeof(ParticleModelReveal).GetField("originals", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(reveal);
                        var material = originals[0][0];
                        throw new InvalidOperationException("Missing formation texture; original shader=" + material.shader.name + "; slots=" + string.Join(",", material.GetTexturePropertyNames().Select(p => p + ":" + (material.GetTexture(p)?.name ?? "none"))));
                    }
                    if (!capturedFormation && reveal.Progress > .4f) { capturedFormation = true; ScreenCapture.CaptureScreenshot("Library/HealSphereCreationFormation.png"); }
                    Require(!Message.isTextOverflowing, "Creation guidance fits authored rectangle");
                    audio |= Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(s => s.isPlaying && s.clip != null);
                }
                if (step == 0 && ++frames > 15)
                {
                    Require(director.State == CreationSessionState.Welcome && director.UsesLiveBackend == mock, "Welcome without generation in selected verification mode");
                    Button("PauseButton").onClick.Invoke(); Require(director.State == CreationSessionState.WaitingForRequest, "Authored primary button");
                    presenter.SetRequestForTesting(""); Button("PauseButton").onClick.Invoke(); Require(director.State == CreationSessionState.WaitingForRequest, "Empty idea rejected");
                    presenter.SetRequestForTesting("something"); Button("PauseButton").onClick.Invoke(); step = 1;
                }
                else if (step == 1 && director.State == CreationSessionState.WaitingForRequest)
                {
                    Require(Message.text.Contains("imagining"), "Vague request prompts clarification");
                    presenter.SetRequestForTesting("I want a cute sleeping fox."); Button("PauseButton").onClick.Invoke(); step = 2;
                }
                else if (step == 2 && director.State == CreationSessionState.AwaitingConfirmation)
                {
                    Require(Message.text.Contains(mock ? "one paid Tripo task" : "Prebuilt demo cat"), "Explicit confirmation and mode labeling");
                    Require(!Message.isTextOverflowing, "Confirmation fits guidance panel");
                    Button("PauseButton").onClick.Invoke(); step = 3;
                }
                else if (step == 3 && director.State == CreationSessionState.RevealingCreation && reveal.Progress > .05f)
                {
                    Button("PauseButton").onClick.Invoke(); previousCycles = director.ChargedCycles; pausedProgress = reveal.Progress; frames = 0; step = 4;
                }
                else if (step == 4 && ++frames > 15)
                {
                    Require(director.IsPaused && previousCycles == director.ChargedCycles && pausedProgress == reveal.Progress, "Pause freezes guide and reveal");
                    Button("PauseButton").onClick.Invoke(); step = 5; frames = 0;
                }
                else if (step == 5 && director.State == CreationSessionState.CreationComplete)
                {
                    if (frames++ == 0) { ScreenCapture.CaptureScreenshot("Library/HealSphereCreationCompleted.png"); return; }
                    if (frames < 4) return;
                    Require(director.CompletedCount == 1 && inhale && exhale && surface && audio, "Breathing, mesh formation, completion and audio observed");
                    var model = Object.FindObjectsByType<HealSphere.Tripo.RuntimeModelOwnership>(FindObjectsSortMode.None).Single();
                    Require(model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).All(m => m != null && !m.name.Contains("Gentle Reveal") && m.shader.isSupported), "Original supported materials restored");
                    Require(model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Any(m => m.mainTexture != null), "Imported textures preserved");
                    Button("PauseButton").onClick.Invoke(); presenter.SetRequestForTesting("a friendly moon"); Button("PauseButton").onClick.Invoke(); step = 6;
                }
                else if (step == 6 && director.State == CreationSessionState.AwaitingConfirmation)
                { Button("PauseButton").onClick.Invoke(); Button("RestartButton").onClick.Invoke(); Require(director.CurrentModel == null, "Cancel removes pending model"); director.NewRequest(); presenter.SetRequestForTesting("a soft little tree"); director.SubmitIdea(); step = 7; }
                else if (step == 7 && director.State == CreationSessionState.AwaitingConfirmation)
                { Button("PauseButton").onClick.Invoke(); step = 8; }
                else if (step == 8 && director != null && director.State == CreationSessionState.CreationComplete)
                {
                    Require(director.CompletedCount == 2 && Object.FindObjectsByType<HealSphere.Tripo.RuntimeModelOwnership>(FindObjectsSortMode.None).Length == 2, "Second creation retains exactly two owned models");
                    Require(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Single(p => p.name == "Creation Surface Motes").particleCount == 0, "No stale formation particles");
                    Button("WellnessButton").onClick.Invoke(); frames = 0; step = 9;
                }
                else if (step == 9 && SceneManager.GetActiveScene().name == "HealSphereMoment" && ++frames > 20)
                {
                    var wellness = One<ExperienceDirector>(); Require(wellness.enabled && wellness.IsConfigured && wellness.ElapsedSeconds > 0, "Just breathe loads preserved automatic wellness scene");
                    if (wellness.Phase != ExperiencePhase.Completed) return;
                    Require(Mathf.Abs(wellness.ElapsedSeconds - 120) < .01f, "Preserved wellness completes at 120 active seconds");
                    Button("RestartButton").onClick.Invoke(); Require(wellness.Phase == ExperiencePhase.Arrival && wellness.ElapsedSeconds == 0, "Wellness restart resets timeline");
                    Require(errors.Length == 0, "No runtime errors: " + errors); Finish(null);
                }
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(string failure)
        {
            Application.logMessageReceived -= Log; Time.timeScale = 1; AudioListener.volume = volume;
            File.AppendAllText(Report, failure ?? "PASS: assigned references, no missing scripts, authored buttons, empty/vague requests and clarification, explicit confirmation, zero-cost real GLB import, three-cycle hold, actual surface particles, inhale/exhale, pause/resume, original materials and textures, two completed creations, cancellation cleanup, particle cleanup, Just breathe scene transition, full 120-second wellness and restart, no runtime errors.\n");
            SessionState.SetBool(Running, false); director = null; EditorApplication.isPlaying = false;
        }
    }
}
