using System;
using System.IO;
using System.Linq;
using HealSphere.Breathing;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace HealSphere.Wellness.Editor
{
    /// <summary>Assembles existing 3D assets only. UI is authored visually, then references are bound separately.</summary>
    [InitializeOnLoad]
    public static class MomentExperienceAssembler
    {
        public const string Root = "Assets/HealSphere/Experience";
        public const string ScenePath = Root + "/Scenes/HealSphereMoment.unity";
        private const string Request = "Library/HealSphereMomentBuild.request";
        private static Scene authoringScene;
        static MomentExperienceAssembler() => EditorApplication.delayCall += Consume;
        private static void Consume()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Build(); } catch (Exception e) { File.WriteAllText("Library/HealSphereMomentBuild.txt", e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("HealSphere/A Little Moment/Assemble Prepared 3D Experience")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before assembly.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("The experience scene already exists; refusing to overwrite your authored scene.");
            const string source = "Assets/HealSphere/Meadow/BakedMeadow/BreathingMeadow.unity";
            foreach (var loaded in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
                if (loaded.path == source && loaded.isDirty) throw new InvalidOperationException("Save your prepared meadow before making the experience copy.");
            Directory.CreateDirectory(Root + "/Scenes"); AssetDatabase.Refresh();
            Inventory();
            if (!AssetDatabase.CopyAsset(source, ScenePath)) throw new IOException("Unable to preserve/copy the meadow scene.");
            var previous = SceneManager.GetActiveScene();
            authoringScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); SceneManager.SetActiveScene(authoringScene);
            try
            {
                var all = All(authoringScene);
                var origin = all.Select(t => t.GetComponent<XROrigin>()).Single(o => o != null);
                var xrCamera = origin.Camera;
                var floor = origin.transform.position;
                var forward = Quaternion.Euler(0, origin.transform.eulerAngles.y, 0);
                var root = Node("A Little Moment — Experience");
                var configuration = ScriptableObject.CreateInstance<ExperienceConfiguration>();
                AssetDatabase.CreateAsset(configuration, Root + "/MomentConfiguration.asset");
                var tree = Prepared("Magical Tree", "Assets/HealSphere/FallbackModels/TreeModel/tripo_convert_236fc91b-5f3d-4b59-a57a-33b9912f2d0a.fbx", new Vector3(1.9f, 2.4f, 1.9f), P(0, 0, 3.7f));
                // A scene-referenced emissive variant keeps the Android shader keyword reachable.
                Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
                var glowMaterials = new System.Collections.Generic.Dictionary<Material, Material>();
                foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        var original = materials[m]; if (original == null || !original.HasProperty("_EmissionColor")) continue;
                        if (!glowMaterials.TryGetValue(original, out var glow))
                        {
                            glow = new Material(original) { name = "Tree Breathing " + glowMaterials.Count };
                            glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(0.02f, 0.015f, 0.01f));
                            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                            AssetDatabase.CreateAsset(glow, Root + "/Materials/" + glow.name + ".mat"); glowMaterials.Add(original, glow);
                        }
                        materials[m] = glow;
                    }
                    renderer.sharedMaterials = materials; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var cat = Prepared("Orange Cat", "Assets/HealSphere/FallbackModels/CatModel/tripo_convert_43784a09-c8eb-4b61-b5b9-e457343ca009.fbx", new Vector3(0.65f, 0.58f, 0.65f), P(-1.1f, 0.04f, 2.5f));
                var moon = Prepared("Gentle Moon", "Assets/HealSphere/FallbackModels/MoonModel/tripo_convert_6f9e9fd5-1549-4d79-991a-c5a68f3ba2be.fbx", Vector3.one * 0.7f, P(1.15f, 1.95f, 4.2f));
                var rabbitRoot = Node("Rabbit Guide Anchor"); rabbitRoot.transform.position = P(-0.85f, 1.35f, 1.85f);
                var rabbit = Prepared("Rabbit Companion", "Assets/HealSphere/CompanionModel/tripo_convert_22510d44-1eb3-4a3d-8d58-2f0da7d724ea.fbx", new Vector3(0.38f, 0.45f, 0.38f), rabbitRoot.transform.position);
                rabbit.transform.SetParent(rabbitRoot.transform, true);
                tree.transform.SetParent(root.transform, true); cat.transform.SetParent(root.transform, true); moon.transform.SetParent(root.transform, true); rabbitRoot.transform.SetParent(root.transform, true);
                var companion = root.AddComponent<FloatingCompanionController>();
                Set(companion, "companionVisual", rabbit.transform); Set(companion, "viewer", xrCamera.transform); Set(companion, "configuration", configuration);
                SetVector(companion, "facingOffset", new Vector3(0, -90, 0));
                var light = all.First(t => t.name == "DirectionalLight").GetComponent<Light>();
                light.shadows = LightShadows.None; light.intensity = Mathf.Min(light.intensity, 0.85f);
                var ps = Node("Breathing Glow Motes").AddComponent<ParticleSystem>(); ps.transform.position = P(0, 1.1f, 3.7f);
                ps.transform.rotation = forward;
                var main = ps.main; main.playOnAwake = false; main.startSpeed = 0; main.maxParticles = configuration.MoteCount; main.loop = false;
                var emission = ps.emission; emission.enabled = false; var shape = ps.shape; shape.enabled = false;
                var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
                particleRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/HealSphere/OfflinePrototype/Authored/Materials/Soft Particles.mat");
                particleRenderer.shadowCastingMode = ShadowCastingMode.Off; ps.transform.SetParent(root.transform, true);
                var reactions = root.AddComponent<EnvironmentReactionController>();
                Set(reactions, "keyLight", light); Set(reactions, "breathingMotes", ps); Set(reactions, "configuration", configuration);
                SetArray(reactions, "treeRenderers", tree.GetComponentsInChildren<Renderer>(true));
                var audioRoot = all.First(t => t.name == "AudioRoot").gameObject;
                var oldAudio = audioRoot.GetComponent<HealSphere.Meadow.MeadowAmbience>();
                var oldFields = new SerializedObject(oldAudio);
                var music = oldFields.FindProperty("musicSource").objectReferenceValue as AudioSource;
                var chime = oldFields.FindProperty("birdsSource").objectReferenceValue as AudioSource;
                oldAudio.enabled = false;
                var audio = audioRoot.AddComponent<AudioManager>(); Set(audio, "ambienceSource", music); Set(audio, "chimeSource", chime);
                Set(audio, "ambienceClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/HealSphere/RelaxingBGM/Dreamy Pasture.mp3"));
                Set(audio, "creationChime", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/HealSphere/HealSphere_Breathing_SFX/03_Creation_Complete_Chime.wav"));
                foreach (var sourceAudio in audioRoot.GetComponentsInChildren<AudioSource>(true)) sourceAudio.playOnAwake = false;
                var breathing = root.AddComponent<GuidedBreathingProvider>(); breathing.ConfigureTiming(4, 6);
                var presentation = root.AddComponent<ExperiencePresentationController>();
                var director = root.AddComponent<ExperienceDirector>();
                var interaction = root.AddComponent<VRInteractionController>();
                Set(interaction, "head", xrCamera.transform); Set(interaction, "director", director);
                Set(interaction, "leftController", origin.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Left Controller"));
                Set(interaction, "rightController", origin.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Right Controller"));
                Set(director, "configuration", configuration); Set(director, "breathing", breathing); Set(director, "environmentReaction", reactions);
                Set(director, "companion", companion); Set(director, "audioController", audio); Set(director, "presentation", presentation); Set(director, "interaction", interaction);
                var desktop = Node("Stationary Desktop Camera").AddComponent<Camera>();
                desktop.transform.SetPositionAndRotation(P(0, 1.35f, 0), forward); desktop.nearClipPlane = 0.05f; desktop.farClipPlane = 400;
                desktop.fieldOfView = 75; desktop.gameObject.AddComponent<AudioListener>(); desktop.gameObject.SetActive(false);
                var comfort = root.AddComponent<VRComfortSettings>(); Set(comfort, "xrOrigin", origin.gameObject); Set(comfort, "xrCamera", xrCamera); Set(comfort, "desktopCamera", desktop);
                foreach (var provider in origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
                { provider.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(provider); }
                EditorSceneManager.MarkSceneDirty(authoringScene); EditorSceneManager.SaveScene(authoringScene); AssetDatabase.SaveAssets();
                File.WriteAllText("Library/HealSphereMomentBuild.txt", "Created " + ScenePath + "\nPrepared meadow, cat, tree, moon, rabbit, audio and timed systems wired.\nRequired UI: visually create MomentCanvas/MomentPanel/MomentMessage/PauseButton/RestartButton, then run Bind Authored Guidance UI.\n" + string.Join("\n", new[] { Stats(tree), Stats(cat), Stats(moon), Stats(rabbit) }));
                Debug.Log("HealSphere prepared-assets experience assembled. Author the guidance UI visually, then bind it.");
                Vector3 P(float x, float y, float z) => floor + forward * new Vector3(x, y, z);
            }
            finally { EditorSceneManager.CloseScene(authoringScene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        }

        [MenuItem("HealSphere/A Little Moment/Bind Authored Guidance UI And Set Launch Scene")]
        public static void BindUI()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open HealSphereMoment outside Play Mode first.");
            var all = All(scene);
            T Named<T>(string name) where T : Component => all.Where(t => t.name == name).Select(t => t.GetComponent<T>()).Single(c => c != null);
            var canvas = Named<Canvas>("MomentCanvas");
            if (canvas.renderMode != RenderMode.WorldSpace || canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                throw new InvalidOperationException("Set the Canvas to World Space and manually add Tracked Device Graphic Raycaster in the Inspector.");
            var presenter = all.Select(t => t.GetComponent<ExperiencePresentationController>()).Single(c => c != null);
            var panel = Named<CanvasGroup>("MomentPanel");
            var text = Named<TMP_Text>("MomentMessage"); var pause = Named<Button>("PauseButton"); var restart = Named<Button>("RestartButton");
            Undo.RecordObject(presenter, "Bind visually authored guidance UI");
            Set(presenter, "worldCanvas", canvas); Set(presenter, "messageText", text); Set(presenter, "panelGroup", panel); Set(presenter, "pauseButton", pause); Set(presenter, "restartButton", restart);
            var comfort = all.Select(t => t.GetComponent<VRComfortSettings>()).Single(c => c != null); Set(comfort, "worldSpaceCanvas", canvas);
            var origin = all.Select(t => t.GetComponent<XROrigin>()).Single(c => c != null); canvas.worldCamera = origin.Camera;
            var systems = all.Select(t => t.GetComponent<EventSystem>()).Where(c => c != null && c.gameObject.activeInHierarchy).ToArray();
            if (systems.Length != 1) throw new InvalidOperationException("Expected one active, preserved EventSystem; found " + systems.Length);
            Set(comfort, "guidanceInputModule", systems[0].GetComponent<XRUIInputModule>());
            if (!all.Select(t => t.GetComponent<ExperienceDirector>()).Single(c => c != null).IsConfigured) throw new InvalidOperationException("Missing experience references.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(previous).ToArray();
            Debug.Log("Guidance UI bound and saved. HealSphereMoment is now the launch scene; previous scene entries are retained disabled. No UI was created by this command.");
        }

        public static void Inventory()
        {
            var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && File.Exists(p)).ToArray();
            var report = "HealSphere asset inventory\n" + string.Join("\n", paths.GroupBy(Path.GetExtension).OrderByDescending(g => g.Count()).Select(g => g.Key + ": " + g.Count()));
            report += "\n\nPrepared models\n";
            foreach (var path in paths.Where(p => p.StartsWith("Assets/HealSphere/") && p.EndsWith(".fbx")))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (model != null) report += path + "\n" + Stats(model) + "\n";
            }
            report += "\nScenes\n" + string.Join("\n", paths.Where(p => p.EndsWith(".unity")));
            File.WriteAllText("Library/HealSphereAssetInventory.txt", report);
        }
        private static string Stats(GameObject model)
        {
            var meshes = model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)).Where(m => m != null).ToArray();
            long triangles = meshes.Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(s => (long)m.GetIndexCount(s) / 3));
            return model.name + ": " + triangles + " triangles; " + model.GetComponentsInChildren<Renderer>(true).Length + " renderers; " + meshes.Sum(m => (long)m.vertexCount) + " vertices";
        }
        private static Transform[] All(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        private static GameObject Node(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, authoringScene); return go; }
        private static GameObject Prepared(string name, string path, Vector3 maximum, Vector3 basePosition)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("Prepared model missing: " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, authoringScene); go.name = name;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            go.transform.rotation = Quaternion.Euler(0, 90, 0);
            var renderers = go.GetComponentsInChildren<Renderer>(true); if (renderers.Length == 0) throw new InvalidOperationException("Prepared model has no renderers: " + path);
            Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float scale = Mathf.Min(maximum.x / Mathf.Max(0.001f, bounds.size.x), maximum.y / Mathf.Max(0.001f, bounds.size.y), maximum.z / Mathf.Max(0.001f, bounds.size.z));
            go.transform.localScale *= scale;
            bounds = renderers[0].bounds; foreach (var r in renderers) { bounds.Encapsulate(r.bounds); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            go.transform.position += basePosition - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            foreach (var renderer in renderers) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            return go;
        }
        private static void Set(Object target, string field, Object value) { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetVector(Object target, string field, Vector3 value) { var so = new SerializedObject(target); so.FindProperty(field).vector3Value = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetArray(Object target, string field, Object[] values)
        { var so = new SerializedObject(target); var array = so.FindProperty(field); array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
