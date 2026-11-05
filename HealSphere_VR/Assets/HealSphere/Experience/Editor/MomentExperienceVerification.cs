using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HealSphere.Breathing;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace HealSphere.Wellness.Editor
{
    /// <summary>Opt-in integration check using the existing scene and authored buttons. Creates no UI.</summary>
    [InitializeOnLoad]
    public static class MomentExperienceVerification
    {
        private const string Request = "Library/HealSphereMomentPlaytest.request";
        private const string Report = "Library/HealSphereMomentPlaytest.txt";
        private const string Running = "HealSphere.MomentVerification";
        private static ExperienceDirector director;
        private static readonly HashSet<ExperiencePhase> phases = new HashSet<ExperiencePhase>();
        private static double deadline;
        private static int step, frames;
        private static float pausedAt, volume;
        private static bool inhale, exhale, audio, emission, motion;
        private static bool arrivalText, introductionText, inhaleText, exhaleText, endingText;
        private static Vector3 initialRabbit;
        private static string errors = "";
        static MomentExperienceVerification()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false)) BeginCheck();
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Running, false);
            };
        }
        [MenuItem("HealSphere/A Little Moment/Verify Full Experience In Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MomentExperienceAssembler.ScenePath || scene.isDirty) throw new InvalidOperationException("Open and save HealSphereMoment first.");
            SessionState.SetBool(Running, true); EditorApplication.isPlaying = true;
        }
        private static T One<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        private static void BeginCheck()
        {
            volume = AudioListener.volume;
            try
            {
                director = One<ExperienceDirector>();
                Require(director.IsConfigured, "All director and presentation references assigned");
                var treeMaterial = AssetDatabase.LoadAssetAtPath<Material>(MomentExperienceAssembler.Root + "/Materials/Tree Breathing 0.mat");
                Require(treeMaterial != null && treeMaterial.IsKeywordEnabled("_EMISSION") &&
                    (treeMaterial.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0,
                    "Scene-referenced tree emission variant survives material import");
                foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject) == 0, "Missing script: " + go.name);
                var comfort = new SerializedObject(One<VRComfortSettings>()); comfort.FindProperty("forceDesktopSimulation").boolValue = true; comfort.ApplyModifiedPropertiesWithoutUndo();
                phases.Clear(); step = frames = 0; errors = ""; inhale = exhale = audio = emission = motion = false;
                arrivalText = introductionText = inhaleText = exhaleText = endingText = false;
                Require(One<XROrigin>() != null && One<EventSystem>().isActiveAndEnabled, "Single preserved XR Origin and active EventSystem");
                initialRabbit = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "Rabbit Companion").position;
                One<GuidedBreathingProvider>().Signal += signal => { inhale |= signal.Phase == BreathPhase.Inhale; exhale |= signal.Phase == BreathPhase.Exhale; };
                volume = AudioListener.volume; AudioListener.volume = 0; Time.timeScale = 8;
                deadline = EditorApplication.timeSinceStartup + 180;
                File.WriteAllText(Report, "Running full 120-second active-time integration test (accelerated).\n");
                Application.logMessageReceived += OnLog;
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        private static void OnLog(string message, string trace, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors += message + "\n"; }
        private static void Tick()
        {
            if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
            { File.Delete(Request); try { Run(); } catch (Exception e) { File.WriteAllText(Report, e.ToString()); } }
            if (!EditorApplication.isPlaying || director == null || !SessionState.GetBool(Running, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Playtest exceeded 180 seconds.");
                phases.Add(director.Phase);
                var message = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "MomentMessage");
                arrivalText |= director.Phase == ExperiencePhase.Arrival && message.text.Contains("Welcome");
                introductionText |= director.Phase == ExperiencePhase.Introduction && message.text.Contains("Rest a hand");
                inhaleText |= director.Phase == ExperiencePhase.Breathing && message.text.Contains("gentle inhale");
                exhaleText |= director.Phase == ExperiencePhase.Breathing && message.text.Contains("easy exhale");
                endingText |= director.Phase == ExperiencePhase.Ending && message.text.Contains("no hurry");
                if (director.ElapsedSeconds > 5)
                {
                    Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "One active desktop camera");
                    Require(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "One active audio listener");
                    var input = One<XRUIInputModule>();
                    Require(input.enableMouseInput && input.leftClickAction != null && input.leftClickAction.action.enabled, "Desktop pointer actions remain enabled after XR rig deactivation");
                    var camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(c => c.enabled);
                    var corners = new Vector3[4]; message.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners) { var point = camera.WorldToViewportPoint(corner); Require(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1, "Guidance text visible in player viewport"); }
                    Require(!message.isTextOverflowing, "Guidance fits its authored text rectangle");
                }
                if (director.Phase == ExperiencePhase.Breathing && !director.IsPaused)
                {
                    audio |= Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(s => s.isPlaying && s.clip != null && s.volume > 0);
                    emission |= Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials).Any(m => m != null && m.name.Contains("Breathing Instance") && m.GetColor("_EmissionColor").maxColorComponent > .03f);
                    motion |= Vector3.Distance(initialRabbit, Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t => t.name == "Rabbit Companion").position) > .015f;
                }
                Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
                if (step == 0 && director.ElapsedSeconds >= 40)
                { Button("PauseButton").onClick.Invoke(); pausedAt = director.ElapsedSeconds; step = 1; frames = 0; }
                else if (step == 1 && ++frames > 15)
                {
                    Require(director.IsPaused && director.ElapsedSeconds == pausedAt, "Pause freezes active timeline");
                    Require(message.text.Contains("Take your time"), "Pause guidance");
                    Button("PauseButton").onClick.Invoke(); Require(!director.IsPaused, "Resume via authored button"); step = 2;
                }
                else if (step == 2 && director.Phase == ExperiencePhase.Completed)
                {
                    Require(Mathf.Abs(director.ElapsedSeconds - 120) < .01f, "Configured 120-second completion");
                    Require(phases.Count == 5, "All five phases observed");
                    Require(inhale && exhale && audio && emission && motion, "Inhale/exhale, music, tree emission, companion movement");
                    Require(arrivalText && introductionText && inhaleText && exhaleText && endingText, "Correct guidance in every stage");
                    Require(message.text.Contains("Carry this calm") && Button("RestartButton").gameObject.activeInHierarchy, "Ending UI and restart visible");
                    ScreenCapture.CaptureScreenshot("Library/HealSphereMomentCompleted.png");
                    Button("RestartButton").onClick.Invoke();
                    Require(director.Phase == ExperiencePhase.Arrival && director.ElapsedSeconds == 0, "Restart via authored button resets timeline");
                    director.FinishGently(); Require(director.Phase == ExperiencePhase.Ending, "Gentle early ending"); step = 3;
                }
                else if (step == 3 && director.Phase == ExperiencePhase.Completed)
                { Require(errors.Length == 0, "No runtime errors: " + errors); Finish(null); }
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(string failure)
        {
            Application.logMessageReceived -= OnLog; Time.timeScale = 1; AudioListener.volume = volume;
            File.AppendAllText(Report, failure ?? "PASS: serialized foundations, missing-script scan, automatic start, five phases and their guidance, text viewport/overflow, desktop pointer actions, 120 active seconds, inhale/exhale signals, music playback, tree emission, companion movement, pause/resume, ending, authored-button restart, gentle early ending, one XR Origin/EventSystem and active camera/listener, no runtime errors.\n");
            SessionState.SetBool(Running, false); director = null; EditorApplication.isPlaying = false;
        }
    }
}
