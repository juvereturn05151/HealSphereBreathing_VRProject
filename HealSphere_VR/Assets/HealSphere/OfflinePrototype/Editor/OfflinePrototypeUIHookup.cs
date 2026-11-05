using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace HealSphere.Breathing.Editor
{
    public static class OfflinePrototypeUIHookup
    {
        [MenuItem("HealSphere/Offline Prototype/Bind Existing Visually Authored UI")]
        public static void Bind()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.name.StartsWith("BreathingExperience"))
                throw new InvalidOperationException("Open the new BreathingExperience scene outside Play Mode first.");
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var canvas = Required<Canvas>("ExperienceCanvas");
            if (canvas.renderMode != RenderMode.WorldSpace || canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                throw new InvalidOperationException("Manually configure ExperienceCanvas as World Space and add Tracked Device Graphic Raycaster through the Inspector.");
            var presenters = objects.Select(t => t.GetComponent<CompanionDialogueController>()).Where(c => c != null).ToArray();
            if (presenters.Length != 1) throw new InvalidOperationException("Expected one existing CompanionDialogueController.");
            var presenter = presenters[0];
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("panelRoot").objectReferenceValue = Required<RectTransform>("ExperiencePanel").gameObject;
            foreach (var entry in new[] { ("dialogueText", "DialogueText"), ("breathingText", "BreathingText"), ("progressText", "ProgressText"), ("modeText", "ModeText") })
                serialized.FindProperty(entry.Item1).objectReferenceValue = Required<TMP_Text>(entry.Item2);
            foreach (var entry in new[] { ("continueButton", "ContinueButton"), ("catButton", "CatButton"), ("treeButton", "TreeButton"),
                ("moonButton", "MoonButton"), ("pauseButton", "PauseButton"), ("guidedButton", "GuidedButton"),
                ("motionProxyButton", "MotionProxyButton"), ("calibrateButton", "CalibrateButton"), ("restartButton", "RestartButton"), ("endButton", "EndButton") })
                serialized.FindProperty(entry.Item1).objectReferenceValue = Required<Button>(entry.Item2);
            Undo.RecordObject(presenter, "Bind existing offline UI"); serialized.ApplyModifiedProperties();
            var comfort = objects.Select(t => t.GetComponent<VRComfortSettings>()).Single(c => c != null);
            var settings = new SerializedObject(comfort); settings.FindProperty("worldSpaceCanvas").objectReferenceValue = canvas;
            Undo.RecordObject(comfort, "Bind existing offline canvas"); settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Existing UI references bound. Review the Inspector and save this scene. No UI was created or arranged by this command.");

            T Required<T>(string name) where T : Component
            {
                var matches = objects.Where(t => t.name == name).Select(t => t.GetComponent<T>()).Where(c => c != null).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Manually author exactly one " + name + " with " + typeof(T).Name + ". See README hierarchy.");
                return matches[0];
            }
        }
    }
}
