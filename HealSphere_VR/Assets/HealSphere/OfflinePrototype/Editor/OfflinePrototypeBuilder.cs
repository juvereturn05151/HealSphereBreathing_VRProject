using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace HealSphere.Breathing.Editor
{
    /// <summary>Authors ONLY 3D gameplay assets. Required UI must be authored visually in the Editor.</summary>
    [InitializeOnLoad]
    public static class OfflinePrototypeBuilder
    {
        private const string Root = "Assets/HealSphere/OfflinePrototype";
        private const string Request = "Library/OfflinePrototypeBuild.request";
        private const string Report = "Library/OfflinePrototypeBuildReport.txt";
        private static Scene scene;
        private static string output;
        static OfflinePrototypeBuilder() => EditorApplication.delayCall += Consume;
        private static void Consume()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Build(); } catch (Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
        }

        public static void BuildAndCheck() { Build(); OfflinePrototypeChecks.Run(); }

        [MenuItem("HealSphere/Offline Prototype/Create New 3D Scene (UI Pending)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            const string source = "Assets/Scenes/BasicScene.unity";
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == source && SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the source BasicScene first; its rig may have unsaved overrides.");
            output = AssetDatabase.GenerateUniqueAssetPath(Root + "/Authored");
            Directory.CreateDirectory(output + "/Materials"); Directory.CreateDirectory(output + "/Prefabs"); AssetDatabase.Refresh();
            string scenePath = output + "/BreathingExperience.unity";
            if (!AssetDatabase.CopyAsset(source, scenePath)) throw new IOException("Could not copy the inspected BasicScene.");
            var previous = SceneManager.GetActiveScene();
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var origins = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).ToArray();
                if (origins.Length != 1 || origins[0].Camera == null) throw new InvalidOperationException("Expected one existing configured XR Origin/camera.");
                var origin = origins[0];
                foreach (var item in scene.GetRootGameObjects())
                    if (item.name == "Plane" || item.name == "Directional Light") Object.DestroyImmediate(item);
                origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrefabUtility.RecordPrefabInstancePropertyModifications(origin.transform);
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                origin.CameraYOffset = 1.15f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(origin);
                foreach (var behaviour in origin.GetComponentsInChildren<Behaviour>(true))
                    if (behaviour is UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider)
                    { behaviour.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour); }

                var environment = Node("FloatingIslandEnvironment");
                var grass = Material("Island Mint", new Color(0.55f, 0.77f, 0.64f));
                var cream = Material("Warm Cream", new Color(1, 0.89f, 0.70f));
                var orange = Material("Cat Apricot", new Color(1, 0.64f, 0.35f));
                var pink = Material("Companion Lavender", new Color(0.79f, 0.67f, 0.92f));
                var dark = Material("Soft Cocoa", new Color(0.18f, 0.12f, 0.22f));
                var bark = Material("Tree Trunk", new Color(0.65f, 0.43f, 0.30f));
                var leaf = Material("Tree Sage", new Color(0.48f, 0.75f, 0.61f));
                Primitive("Floating Island", PrimitiveType.Sphere, environment.transform, new Vector3(0, -0.75f, 1), new Vector3(7, 1.3f, 7), grass);
                for (int i = 0; i < 6; i++) Primitive("Distant Soft Cloud " + i, PrimitiveType.Sphere, environment.transform,
                    new Vector3((i - 2.5f) * 3, 3.3f + (i % 2) * 0.35f, 9), new Vector3(2.4f, 0.4f, 0.7f), cream);
                var light = Node("Gentle Key Light").AddComponent<Light>(); light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45, -25, 0); light.color = new Color(1, 0.92f, 0.84f); light.intensity = 0.8f; light.shadows = LightShadows.None;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.72f, 0.76f, 0.84f);
                RenderSettings.skybox = null; RenderSettings.fog = false;
                CameraSettings(origin.Camera);

                var cat = Node("Cat Placeholder");
                Primitive("Rounded Body", PrimitiveType.Sphere, cat.transform, new Vector3(0, 0.42f, 0), new Vector3(0.88f, 0.73f, 0.65f), orange);
                Primitive("Cream Belly", PrimitiveType.Sphere, cat.transform, new Vector3(0, 0.39f, -0.27f), new Vector3(0.53f, 0.52f, 0.13f), cream);
                Primitive("Fluffy Head", PrimitiveType.Sphere, cat.transform, new Vector3(0, 0.88f, -0.06f), Vector3.one * 0.6f, orange);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var ear = Primitive("Ear", PrimitiveType.Cube, cat.transform, new Vector3(sign * 0.2f, 1.12f, 0), new Vector3(0.2f, 0.2f, 0.15f), orange);
                    ear.transform.localRotation = Quaternion.Euler(0, 0, sign * 30);
                    Primitive("Tiny Paw", PrimitiveType.Sphere, cat.transform, new Vector3(sign * 0.22f, 0.09f, -0.18f), new Vector3(0.2f, 0.16f, 0.23f), cream);
                    var eye = Primitive("Closed Eye", PrimitiveType.Cube, cat.transform, new Vector3(sign * 0.13f, 0.91f, -0.345f), new Vector3(0.09f, 0.018f, 0.02f), dark);
                    eye.transform.localRotation = Quaternion.Euler(0, 0, sign * 12);
                }
                var catPrefab = SavePrefab(cat);
                var tree = Node("Tree Placeholder");
                Primitive("Trunk", PrimitiveType.Cylinder, tree.transform, new Vector3(0, 0.3f, 0), new Vector3(0.18f, 0.3f, 0.18f), bark);
                Primitive("Soft Canopy", PrimitiveType.Sphere, tree.transform, new Vector3(0, 0.8f, 0), new Vector3(0.8f, 0.8f, 0.65f), leaf);
                Primitive("Canopy Side", PrimitiveType.Sphere, tree.transform, new Vector3(0.25f, 0.7f, 0), Vector3.one * 0.5f, leaf);
                var treePrefab = SavePrefab(tree);
                var moon = Node("Moon Placeholder");
                Primitive("Moon", PrimitiveType.Sphere, moon.transform, new Vector3(0, 0.5f, 0), Vector3.one * 0.85f, cream);
                Primitive("Moon Blush", PrimitiveType.Sphere, moon.transform, new Vector3(-0.17f, 0.48f, -0.38f), new Vector3(0.16f, 0.1f, 0.04f), orange);
                var moonPrefab = SavePrefab(moon);

                var companion = Node("Floating Companion", new Vector3(0.72f, 1.45f, 1.8f));
                Primitive("Companion Body", PrimitiveType.Sphere, companion.transform, Vector3.zero, new Vector3(0.29f, 0.28f, 0.26f), pink);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Primitive("Companion Ear", PrimitiveType.Sphere, companion.transform, new Vector3(sign * 0.07f, 0.17f, 0), new Vector3(0.065f, 0.2f, 0.06f), pink);
                    Primitive("Companion Eye", PrimitiveType.Sphere, companion.transform, new Vector3(sign * 0.05f, 0.025f, -0.12f), new Vector3(0.025f, 0.02f, 0.015f), dark);
                }
                companion.AddComponent<GentleFloat>();
                var stage = Node("DreamSeedAnchor", new Vector3(0, 1.05f, 1.65f)).transform;
                var seedVisual = Primitive("Dream Seed Visual", PrimitiveType.Sphere, stage, new Vector3(0, 0.15f, 0), Vector3.one * 0.12f, cream);
                var anchors = new[] { Node("CreationAnchor_01", new Vector3(-0.75f, 0.7f, 1.85f)).transform,
                    Node("CreationAnchor_02", new Vector3(0, 0.65f, 2.25f)).transform,
                    Node("CreationAnchor_03", new Vector3(0.75f, 0.7f, 1.85f)).transform };
                var gameplay = Node("Offline Session Controllers");
                var guided = gameplay.AddComponent<GuidedBreathingProvider>();
                var motion = gameplay.AddComponent<ControllerMotionBreathingProvider>();
                Set(motion, "head", origin.Camera.transform);
                var rightHand = origin.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Right Controller");
                if (rightHand != null) Set(motion, "bellyController", rightHand);
                var input = gameplay.AddComponent<BreathingInputController>(); Set(input, "guided", guided); Set(input, "controllerMotion", motion);
                var seed = gameplay.AddComponent<DreamSeedController>(); Set(seed, "seedVisual", seedVisual.transform);
                var particles = Node("Formation Particles", stage.position).AddComponent<ParticleSystem>();
                var main = particles.main; main.playOnAwake = false; main.loop = false; main.maxParticles = 256; main.startSpeed = 0;
                var emission = particles.emission; emission.enabled = false; var shape = particles.shape; shape.enabled = false;
                var particleMaterial = new Material(RequiredShader("HealSphere/Offline Soft Particle")); AssetDatabase.CreateAsset(particleMaterial, output + "/Materials/Soft Particles.mat");
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
                particles.GetComponent<ParticleSystemRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var revealMaterial = new Material(RequiredShader("HealSphere/Offline Gentle Reveal")); AssetDatabase.CreateAsset(revealMaterial, output + "/Materials/Gentle Reveal.mat");
                var reveal = gameplay.AddComponent<ParticleModelReveal>(); Set(reveal, "formationParticles", particles); Set(reveal, "dissolveMaterial", revealMaterial);
                var creation = gameplay.AddComponent<CreationManager>();
                Set(creation, "catPrefab", catPrefab); Set(creation, "treePrefab", treePrefab); Set(creation, "moonPrefab", moonPrefab);
                Set(creation, "stagingAnchor", stage); Set(creation, "reveal", reveal);
                var serializedCreation = new SerializedObject(creation); var anchorField = serializedCreation.FindProperty("creationAnchors"); anchorField.arraySize = 3;
                for (int i = 0; i < 3; i++) anchorField.GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];
                serializedCreation.ApplyModifiedPropertiesWithoutUndo();
                var presenter = companion.AddComponent<CompanionDialogueController>();
                var audioRoot = Node("Audio Manager"); var audio = audioRoot.AddComponent<AudioManager>();
                var music = audioRoot.AddComponent<AudioSource>(); music.playOnAwake = false; music.spatialBlend = 0; music.volume = 0;
                var chime = audioRoot.AddComponent<AudioSource>(); chime.playOnAwake = false; chime.spatialBlend = 0;
                Set(audio, "ambienceSource", music); Set(audio, "chimeSource", chime);
                Set(audio, "ambienceClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/HealSphere/RelaxingBGM/Dreamy Pasture.mp3"));
                Set(audio, "creationChime", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/HealSphere/HealSphere_Breathing_SFX/03_Creation_Complete_Chime.wav"));
                var session = gameplay.AddComponent<BreathingSessionManager>();
                Set(session, "breathingInput", input); Set(session, "creations", creation); Set(session, "dreamSeed", seed); Set(session, "companion", presenter); Set(session, "audioManager", audio);
                var desktop = Node("Stationary Desktop Camera", new Vector3(0, 1.15f, 0)).AddComponent<Camera>();
                CameraSettings(desktop); desktop.gameObject.AddComponent<AudioListener>(); desktop.gameObject.SetActive(false);
                var comfort = gameplay.AddComponent<VRComfortSettings>(); Set(comfort, "xrOrigin", origin.gameObject); Set(comfort, "xrCamera", origin.Camera); Set(comfort, "desktopCamera", desktop);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                File.WriteAllText(Report, "Created " + scenePath + "\nPlaceholder prefabs, shaders, audio, breathing/reveal/session components and XR/desktop cameras wired.\nUI PENDING: author ExperienceCanvas visually and assign its components. Session intentionally refuses to start without UI.\nMotion right-hand transform: " + (rightHand != null ? rightHand.name : "MISSING — assign manually") + "\n");
                Debug.Log("Offline 3D scene created: " + scenePath + ". Required world-space UI is pending manual Editor authoring.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CameraSettings(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.75f, 0.83f, 0.94f); camera.nearClipPlane = 0.05f; camera.farClipPlane = 30;
            if (PrefabUtility.IsPartOfPrefabInstance(camera)) PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        }
        private static GameObject Node(string name, Vector3 position = default)
        { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = position; return go; }
        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); SceneManager.MoveGameObjectToScene(go, scene); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return go;
        }
        private static Shader RequiredShader(string name) => Shader.Find(name) ?? throw new InvalidOperationException("Missing shader: " + name);
        private static Material Material(string name, Color color)
        {
            var material = new Material(RequiredShader("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", 0.25f); AssetDatabase.CreateAsset(material, output + "/Materials/" + name + ".mat"); return material;
        }
        private static GameObject SavePrefab(GameObject go)
        { var prefab = PrefabUtility.SaveAsPrefabAsset(go, output + "/Prefabs/" + go.name + ".prefab"); Object.DestroyImmediate(go); return prefab; }
        private static void Set(Object target, string field, Object value)
        { var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
