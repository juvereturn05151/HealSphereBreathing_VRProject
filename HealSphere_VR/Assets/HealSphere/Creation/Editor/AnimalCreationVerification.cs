using System;
using System.IO;
using System.Linq;
using HealSphere.Breathing;
using HealSphere.Tripo;
using HealSphere.Wellness;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HealSphere.Creation.Editor
{
    [InitializeOnLoad]
    public static class AnimalCreationVerification
    {
        private const string Running = "HealSphere.AnimalVerification";
        private const string Report = "Library/HealSphereAnimalPlaytest.txt";
        private const string Marker = "Library/HealSphereAnimalPlaytest.request";
        private static int step, frames;
        private static double deadline;
        private static bool sampled, sawInhale, sawExhale;
        private static float quietStarted;
        private static bool sawInvitation, sawWake;
        private static bool EndingChoices => Button("PauseButton").gameObject.activeInHierarchy &&
            Button("PauseButton").GetComponentInChildren<TMP_Text>().text == "Create Another Friend";
        private static string error;
        private static CreationSessionDirector creation;
        private static CreationSessionPresenter presenter;
        private static TripoModelLoader backend;
        private static ParticleModelReveal reveal;

        static AnimalCreationVerification()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false)) Begin();
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Running, false);
            };
        }

        [MenuItem("HealSphere/Animal Creation/Test Picker With Mock Library")]
        public static void Run()
        {
            if (SceneManager.GetActiveScene().path != CreationSceneAssembler.ScenePath || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Open the saved creation scene first.");
            SessionState.SetBool(Running, true);
            EditorApplication.isPlaying = true;
        }

        private static T One<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include).Single();
        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include).Single(b => b.name == name);
        private static TMP_Text Message => Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).Single(t => t.name == "MomentMessage");
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Begin()
        {
            try
            {
                creation = One<CreationSessionDirector>();
                presenter = One<CreationSessionPresenter>();
                backend = One<TripoModelLoader>();
                reveal = One<ParticleModelReveal>();
                Require(One<AnimalCreationDirector>().isActiveAndEnabled, "Animal picker is enabled");
                Require(!One<ReflectionExperienceDirector>().enabled, "Previous reflection controller is inactive");
                var comfort = new SerializedObject(One<VRComfortSettings>());
                comfort.FindProperty("forceDesktopSimulation").boolValue = true;
                comfort.ApplyModifiedPropertiesWithoutUndo();
                step = frames = 0;
                sampled = sawInhale = sawExhale = false;
                quietStarted = -1;
                sawInvitation = sawWake = false;
                error = "";
                deadline = EditorApplication.timeSinceStartup + 180;
                Time.timeScale = 5;
                Application.logMessageReceived += Log;
                File.WriteAllText(Report, "Testing saved mock GLBs and local fallback. No provider access.\n");
            }
            catch (Exception e) { Finish(e.ToString()); }
        }

        private static void Log(string message, string trace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
                !trace.Contains("Unity.AI.") && !message.Contains("generators.ai.unity.com")) error += message + "\n";
        }

        private static void SetBackend(string url)
        {
            var serialized = new SerializedObject(backend);
            serialized.FindProperty("backendUrl").stringValue = url;
            serialized.FindProperty("backendAccessToken").stringValue = "healsphere-reflection-mock-only-0123456789";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            typeof(TripoModelLoader).GetProperty("LiveConnectionRequested").SetValue(backend, true);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(Marker))
            {
                File.Delete(Marker);
                try { Run(); } catch (Exception e) { File.WriteAllText(Report, e.ToString()); }
                return;
            }
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Running, false) || creation == null) return;
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out at step " + step + ", " + creation.State);
                Require(error.Length == 0, error);
                var message = Message;
                message.ForceMeshUpdate();
                Require(!message.isTextOverflowing, "Guidance text fits");
                sawInhale |= message.text.Contains("Breathe in");
                sawExhale |= message.text.Contains("Breathe out");
                sampled |= creation.State == CreationSessionState.RevealingCreation && reveal.SurfaceSamplingAvailable;
                if (step == 2)
                {
                    sawInvitation |= message.text.Contains("one last gentle breath");
                    sawWake |= message.text.Contains("Your breath gave life");
                    if (sawWake && quietStarted < 0 && !EndingChoices && !message.text.Contains("wonderful") &&
                        !message.text.Contains("gave life")) quietStarted = Time.time;
                    if (message.text.Contains("one last gentle breath") || message.text.Contains("gave life"))
                        Require(!EndingChoices, "Ending actions stay hidden during the awakening");
                }
                if (step == 0 && ++frames > 24)
                {
                    Require(message.text.Contains("Create an Animal"), "Opens directly to animal picker");
                    SetBackend("http://127.0.0.1:3347");
                    foreach (string name in new[] { "PauseButton", "RestartButton", "WellnessButton", "MicrophoneButton", "VoiceMuteButton", "VoiceVolumeButton" })
                        Button(name).onClick.Invoke();
                    Require(message.text.Contains("Selected · Owl"), "All six authored buttons select a preset");
                    Button("WellnessButton (1)").onClick.Invoke();
                    Require(Button("PauseButton").GetComponentInChildren<TMP_Text>().text.Contains("Elephant"), "More page opens");
                    Button("WellnessButton (1)").onClick.Invoke();
                    Button("PauseButton").onClick.Invoke();
                    Require(message.text.Contains("Selected · Cat") && creation.CurrentModel == null, "Selecting Cat starts no request");
                    ScreenCapture.CaptureScreenshot("Library/HealSphereAnimalPicker.png");
                    frames = 0;
                    step = 1;
                }
                else if (step == 1 && ++frames > 10)
                {
                    Button("PauseButton (1)").onClick.Invoke();
                    step = 2;
                }
                else if (step == 2 && EndingChoices)
                {
                    Require(creation.CompletedCount == 1 && sampled && sawInhale && sawExhale, "Cat uses timed mesh reveal");
                    Require(sawInvitation && sawWake && quietStarted >= 0 && Time.time - quietStarted >= 7.5f,
                        "Final breath and awakening precede the quiet eight-second hold");
                    Require(creation.LatestAnimal.GetComponentInChildren<AnimalAwakening>() != null, "Awakening prefab is attached");
                    CheckModel();
                    Button("PauseButton").onClick.Invoke();
                    Require(creation.CompletedCount == 1 && !creation.LatestAnimal.activeSelf, "Create Another retains the friend offstage");
                    Button("PauseButton").onClick.Invoke();
                    presenter.SetRequestText("A little sleeping fox");
                    Require(!message.text.Contains("Selected · Cat"), "Custom typing overrides the preset");
                    Button("PauseButton (1)").onClick.Invoke();
                    sampled = false;
                    step = 3;
                }
                else if (step == 3 && EndingChoices)
                {
                    Require(creation.CompletedCount == 2 && sampled, "Saved custom fox completes with mesh sampling");
                    CheckModel();
                    ScreenCapture.CaptureScreenshot("Library/HealSphereAnimalComplete.png");
                    frames = 0;
                    step = 4;
                }
                else if (step == 4 && ++frames > 10)
                {
                    Button("PauseButton").onClick.Invoke();
                    SetBackend("http://127.0.0.1:1");
                    Button("VoiceVolumeButton").onClick.Invoke();
                    Button("PauseButton (1)").onClick.Invoke();
                    step = 5;
                }
                else if (step == 5 && EndingChoices)
                {
                    CheckModel();
                    Button("RestartButton").onClick.Invoke();
                    Require(message.text.Contains("3 / 3") && creation.LatestAnimal.activeSelf, "Visit My Little Friends shows the latest model");
                    Button("RestartButton").onClick.Invoke();
                    Require(message.text.Contains("1 / 3") && !creation.LatestAnimal.activeSelf, "Next Friend cycles the retained models");
                    Finish(null);
                }
            }
            catch (Exception e) { Finish(e.ToString()); }
        }

        private static void CheckModel()
        {
            var model = creation.LatestAnimal.GetComponent<RuntimeModelOwnership>();
            var anchor = (Transform)new SerializedObject(backend).FindProperty("creationAnchor").objectReferenceValue;
            var placement = model.transform;
            Require(placement.IsChildOf(anchor) && placement.Find("Generated Animal Model") != null,
                "Imported geometry is nested below the authored anchor");
            var original = new Pose(anchor.position, anchor.rotation);
            var localPosition = anchor.InverseTransformPoint(placement.position);
            var localRotation = Quaternion.Inverse(anchor.rotation) * placement.rotation;
            try
            {
                anchor.SetPositionAndRotation(original.position + new Vector3(.2f, -.3f, .1f),
                    Quaternion.AngleAxis(35f, Vector3.up) * original.rotation);
                Require(Vector3.Distance(placement.position, anchor.TransformPoint(localPosition)) < .001f &&
                    Quaternion.Angle(placement.rotation, anchor.rotation * localRotation) < .1f,
                    "Staged animal follows anchor translation and rotation");
            }
            finally { anchor.SetPositionAndRotation(original.position, original.rotation); }
            var materials = model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials);
            Require(materials.Any() && materials.All(m => m != null && m.shader.isSupported && !m.shader.name.Contains("Gentle")),
                "Original imported materials restored");
        }

        private static void Finish(string failure)
        {
            Application.logMessageReceived -= Log;
            Time.timeScale = 1;
            File.AppendAllText(Report, failure ?? "PASS: picker, saved models, mesh reveal, final breath, awakening prefab, quiet hold, offline Owl and session gallery.\n");
            SessionState.SetBool(Running, false);
            EditorApplication.isPlaying = false;
        }
    }
}
