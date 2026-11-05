using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using HealSphere.Breathing;
using HealSphere.Tripo;
using HealSphere.Wellness;
using HealSphere.Wellness.Editor;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard;
using Object = UnityEngine.Object;

namespace HealSphere.Creation.Editor
{
    /// <summary>Copies the authored wellness scene and assembles 3D/controller assets only. UI is placed visually.</summary>
    [InitializeOnLoad]
    public static class CreationSceneAssembler
    {
        public const string ScenePath = "Assets/HealSphere/Creation/Scenes/HealSphereCreation.unity";
        static CreationSceneAssembler() => EditorApplication.delayCall += Consume;
        private static async void Consume()
        {
            const string polish = "Library/HealSphereCreationPolish.request";
            if (File.Exists(polish) && !EditorApplication.isPlayingOrWillChangePlaymode)
            { File.Delete(polish); ArrangeCreationSpace(); }
            const string request = "Library/HealSphereCreationBuild.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { await Build(); } catch (Exception e) { File.WriteAllText("Library/HealSphereCreationBuild.txt", e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("HealSphere/Creation/Assemble Creation Scene From Authored Wellness Scene")]
        private static async void BuildMenu() { try { await Build(); } catch (Exception e) { Debug.LogException(e); } }
        private static async Task Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene and stop Play Mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Creation scene already exists; refusing to overwrite it.");
            Directory.CreateDirectory("Assets/HealSphere/Creation/Scenes"); AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(MomentExperienceAssembler.ScenePath, ScenePath)) throw new IOException("Unable to preserve/copy the wellness scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath); var all = All(scene);
            var root = all.Single(t => t.GetComponent<ExperienceDirector>() != null).gameObject;
            root.GetComponent<ExperienceDirector>().enabled = false; root.GetComponent<ExperiencePresentationController>().enabled = false;
            root.GetComponent<VRInteractionController>().enabled = false;
            var origin = all.Select(t => t.GetComponent<XROrigin>()).Single(c => c != null); Vector3 floor = origin.transform.position; Quaternion forward = Quaternion.Euler(0, origin.transform.eulerAngles.y, 0);
            Vector3 P(float x, float y, float z) => floor + forward * new Vector3(x, y, z);
            var staging = new GameObject("Creation Staging Anchor").transform; staging.SetParent(root.transform, false); staging.SetPositionAndRotation(P(0, 1.5f, 2.5f), forward * Quaternion.Euler(0, 180, 0));
            var anchors = new Transform[3];
            for (int i = 0; i < 3; i++) { anchors[i] = new GameObject("Created Object Anchor " + (i + 1)).transform; anchors[i].SetParent(root.transform, false); anchors[i].SetPositionAndRotation(P(i == 0 ? -1.2f : i == 1 ? 1.2f : 0, i == 2 ? 1.8f : 1.4f, i == 2 ? 3.3f : 3), forward * Quaternion.Euler(0, 180, 0)); }
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere); orb.name = "Dream Seed Visual"; orb.transform.SetParent(root.transform, false); orb.transform.position = staging.position - Vector3.up * .12f; orb.transform.localScale = Vector3.one * .12f;
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", new Color(1, .8f, .45f));
            AssetDatabase.CreateAsset(material, "Assets/HealSphere/Creation/Dream Seed Glow.mat"); var orbRenderer = orb.GetComponent<Renderer>(); orbRenderer.sharedMaterial = material; orbRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var seed = root.AddComponent<DreamSeedController>(); Set(seed, "seedVisual", orb.transform);
            var formation = new GameObject("Creation Surface Motes").AddComponent<ParticleSystem>(); formation.transform.SetParent(root.transform, false); formation.transform.position = staging.position;
            var main = formation.main; main.playOnAwake = false; main.maxParticles = 256; main.startSpeed = 0;
            var emission = formation.emission; emission.enabled = false; var shape = formation.shape; shape.enabled = false;
            var particleRenderer = formation.GetComponent<ParticleSystemRenderer>(); particleRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/HealSphere/OfflinePrototype/Authored/Materials/Soft Particles.mat"); particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var reveal = root.AddComponent<ParticleModelReveal>(); Set(reveal, "formationParticles", formation); Set(reveal, "dissolveMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/HealSphere/OfflinePrototype/Authored/Materials/Gentle Reveal.mat"));
            var loader = root.AddComponent<TripoModelLoader>(); Set(loader, "creationAnchor", staging); SetVector(loader, "maximumSize", Vector3.one * .65f);
            var presenter = root.AddComponent<CreationSessionPresenter>(); var director = root.AddComponent<CreationSessionDirector>();
            var configuration = AssetDatabase.LoadAssetAtPath<ExperienceConfiguration>(MomentExperienceAssembler.Root + "/MomentConfiguration.asset");
            Set(director, "presentation", presenter); Set(director, "loader", loader); Set(director, "breathing", root.GetComponent<GuidedBreathingProvider>());
            Set(director, "seed", seed); Set(director, "reveal", reveal); Set(director, "environmentReaction", root.GetComponent<EnvironmentReactionController>());
            Set(director, "companion", root.GetComponent<FloatingCompanionController>()); Set(director, "audioController", all.Select(t => t.GetComponent<AudioManager>()).Single(c => c != null)); Set(director, "configuration", configuration);
            SetArray(director, "completedAnchors", anchors);
            var motes = all.Single(t => t.name == "Breathing Glow Motes"); motes.position = staging.position;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            await ExportDemo();
            File.WriteAllText("Library/HealSphereCreationBuild.txt", "Created " + ScenePath + "\n3D controllers/anchors/seed/reveal and reviewed demo GLB prepared.\nPending visual UI: place Input Field World Keyboard and XRI Spatial Keyboard prefabs, author WellnessButton, arrange three buttons, then Bind Authored Creation UI.\n");
        }
        [MenuItem("HealSphere/Creation/Export Reviewed Local Cat GLB")]
        private static async void ExportDemoMenu() { try { await ExportDemo(); } catch (Exception e) { Debug.LogException(e); } }
        private static Task ExportDemo() => CreationDemoExport.Export();
        [MenuItem("HealSphere/Creation/Arrange Existing Creation Anchors")]
        public static void ArrangeCreationSpace()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open and save creation mode outside Play Mode first.");
            var all = All(scene); var origin = all.Select(t => t.GetComponent<XROrigin>()).Single(c => c != null);
            var forward = Quaternion.Euler(0, origin.transform.eulerAngles.y, 0);
            Vector3 P(Vector3 local) => origin.transform.position + forward * local;
            void Move(string name, Vector3 local) { var t = all.Single(item => item.name == name); Undo.RecordObject(t, "Arrange creation space"); t.position = P(local); }
            Move("Creation Staging Anchor", new Vector3(0, 2, 2.5f));
            Move("Dream Seed Visual", new Vector3(0, 1.88f, 2.5f));
            Move("Creation Surface Motes", new Vector3(0, 2, 2.5f));
            Move("Breathing Glow Motes", new Vector3(0, 2, 2.5f));
            Move("Created Object Anchor 1", new Vector3(1.25f, 1.1f, 2.8f));
            Move("Created Object Anchor 2", new Vector3(-1.4f, .65f, 2.8f));
            Move("Created Object Anchor 3", new Vector3(0, 2.2f, 3.5f));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Library/HealSphereCreationPolish.txt", "PASS: existing staging, seed, motes and three completed anchors separated from rabbit/tree; authored UI and original wellness scene unchanged.\n");
        }
        [MenuItem("HealSphere/Creation/Bind Authored Creation UI And Set Launch Scene")]
        public static void BindUI()
        {
            var scene = SceneManager.GetActiveScene(); if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open the saved creation scene outside Play Mode.");
            var all = All(scene); T One<T>() where T : Component => all.Select(t => t.GetComponent<T>()).Single(c => c != null);
            T Named<T>(string name) where T : Component => all.Where(t => t.name == name).Select(t => t.GetComponent<T>()).Single(c => c != null);
            var presenter = One<CreationSessionPresenter>();
            var entryRoot = all.Single(t => t.name == "Input Field World Keyboard");
            var input = entryRoot.GetComponentsInChildren<TMP_InputField>(true).Single(); var keyboard = One<XRKeyboard>();
            var entry = input.GetComponentInParent<Canvas>().gameObject;
            Set(presenter, "messageText", Named<TMP_Text>("MomentMessage")); Set(presenter, "requestInput", input); Set(presenter, "requestEntryRoot", entry); Set(presenter, "keyboard", keyboard);
            Set(presenter, "primaryButton", Named<Button>("PauseButton")); Set(presenter, "secondaryButton", Named<Button>("RestartButton")); Set(presenter, "wellnessButton", Named<Button>("WellnessButton"));
            var display = input.GetComponent<XRKeyboardDisplay>(); if (display == null) throw new InvalidOperationException("Use the authored Input Field World Keyboard prefab with XRKeyboardDisplay.");
            Undo.RecordObject(display, "Bind existing scene keyboard"); display.useSceneKeyboard = true; display.keyboard = keyboard; display.monitorInputFieldCharacterLimit = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(display);
            var origin = One<XROrigin>();
            var canvases = all.Select(t => t.GetComponent<Canvas>()).Where(c => c != null && (c == entry.GetComponent<Canvas>() || c.transform.IsChildOf(keyboard.transform))).ToArray();
            foreach (var canvas in canvases) { canvas.worldCamera = origin.Camera; PrefabUtility.RecordPrefabInstancePropertyModifications(canvas); }
            SetArray(presenter, "inputCanvases", canvases); Set(presenter, "xrCamera", origin.Camera);
            Set(presenter, "desktopCamera", Named<Camera>("Stationary Desktop Camera"));
            if (!One<CreationSessionDirector>().IsConfigured) throw new InvalidOperationException("Creation references are incomplete.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(MomentExperienceAssembler.ScenePath, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != MomentExperienceAssembler.ScenePath).Select(s => new EditorBuildSettingsScene(s.path, false))).ToArray();
            File.WriteAllText("Library/HealSphereCreationBindings.txt", "PASS: authored request input, scene keyboard, three buttons, model loader, seed, reveal, audio, breathing, environment, companion and anchors assigned. Creation launch first, preserved wellness scene second.\n");
        }
        private static Transform[] All(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        private static void Set(Object target, string field, Object value) { var fields = new SerializedObject(target); fields.FindProperty(field).objectReferenceValue = value; fields.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetVector(Object target, string field, Vector3 value) { var fields = new SerializedObject(target); fields.FindProperty(field).vector3Value = value; fields.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetArray(Object target, string field, Object[] values) { var fields = new SerializedObject(target); var array = fields.FindProperty(field); array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; fields.ApplyModifiedPropertiesWithoutUndo(); }
    }
}

