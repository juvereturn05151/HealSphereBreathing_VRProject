using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace HealSphere.Meadow.Editor
{
    /// <summary>Explicit editor bake only. Never builds interface objects or runs in a player.</summary>
    [InitializeOnLoad]
    public static partial class MeadowBuilder
    {
        private const string Root = "Assets/HealSphere/Meadow";
        private const string Request = "Library/MeadowBuild.request";
        private const string Report = "Library/MeadowBuildReport.txt";
        private static System.Random random;
        private static string output;
        private static MeadowRecipe recipe;
        private static TerrainData terrainData;
        private static Material vegetationMaterial;
        private static Scene authoringScene;

        static MeadowBuilder() { EditorApplication.delayCall += ConsumeRequest; }

        private static void ConsumeRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { BuildDefault(); }
            catch (Exception exception)
            {
                File.WriteAllText(Report, exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("HealSphere/Meadow/Bake New Scene From Selected Recipe")]
        public static void BuildDefault()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before baking a meadow.");
            recipe = Selection.activeObject as MeadowRecipe;
            if (recipe == null) recipe = AssetDatabase.LoadAssetAtPath<MeadowRecipe>(Root + "/MeadowRecipe.asset");
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<MeadowRecipe>();
                var serialized = new SerializedObject(recipe);
                serialized.FindProperty("sourceScene").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/SampleScene.unity");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(recipe, Root + "/MeadowRecipe.asset");
            }
            if (recipe.SourceScene == null) throw new InvalidOperationException("Assign Source Scene in the recipe Inspector.");
            string sourcePath = AssetDatabase.GetAssetPath(recipe.SourceScene);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == sourcePath && loaded.isDirty)
                    throw new InvalidOperationException("Source scene has unsaved edits. Save it before baking so the preserved rig is current.");
            }
            output = AssetDatabase.GenerateUniqueAssetPath(Root + "/BakedMeadow");
            Directory.CreateDirectory(output);
            foreach (string folder in new[] { "Materials", "Meshes", "Prefabs", "Textures", "Terrain", "Previews" })
                Directory.CreateDirectory(output + "/" + folder);
            AssetDatabase.Refresh();
            random = new System.Random(recipe.Seed);
            var oldActive = SceneManager.GetActiveScene();
            string scenePath = output + "/BreathingMeadow.unity";
            if (!AssetDatabase.CopyAsset(sourcePath, scenePath)) throw new IOException("Unable to copy the source scene.");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            authoringScene = scene;
            SceneManager.SetActiveScene(scene);
            try
            {
                var roots = scene.GetRootGameObjects();
                var origins = roots.SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).ToArray();
                if (origins.Length != 1) throw new InvalidOperationException("Expected exactly one existing XR Origin; found " + origins.Length);
                var origin = origins[0];
                Vector3 player = origin.transform.position;
                Quaternion forward = Quaternion.Euler(0, origin.transform.eulerAngles.y, 0);
                var experience = Node("BreathingExperience");
                var legacy = Node("PreservedTemplateContent (Inactive)", experience);
                legacy.gameObject.SetActive(false);
                var legacyNames = new HashSet<string> { "Interactables", "UI", "Environment", "Teleport Area Setup", "Lighting" };
                foreach (var go in roots)
                    go.transform.SetParent(legacyNames.Contains(go.name) ? legacy : experience, true);
                var disabled = new List<string>();
                foreach (var behaviour in origin.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour is UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider)
                    {
                        behaviour.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                        disabled.Add(behaviour.GetType().Name);
                    }
                }
                var environment = Node("EnvironmentRoot", experience);
                // Unity Terrain requires an unrotated transform, including every parent.
                environment.position = player;
                var hills = Node("DistantHills", environment);
                Node("EnvironmentDecorations", environment);
                var grass = Node("Grass", environment);
                var flowers = Node("Flowers", environment);
                var trees = Node("Trees", environment);
                Node("Clouds (Baked Into Skybox)", environment);
                vegetationMaterial = new Material(RequiredShader("HealSphere/Meadow Soft")) { name = "Meadow Vegetation", enableInstancing = true };
                AssetDatabase.CreateAsset(vegetationMaterial, output + "/Materials/Meadow Vegetation.mat");
                CreateTerrain(environment); CreateHorizon(hills); CreateReusablePlants();
                CreateGrass(grass); CreateFlowers(flowers); CreateTrees(trees);
                CreateLighting(Node("LightingRoot", experience), player, forward);
                CreateAnchors(Node("GameplayAnchors", experience), player, forward);
                CreateAudio(Node("AudioRoot", experience));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAndPreview(scene, origin, player, forward, disabled);
                Selection.activeGameObject = environment.gameObject;
                Debug.Log("Meadow baked to " + scenePath + ". Original scenes, XR assets and project settings were not changed.");
            }
            finally
            {
                if (oldActive.IsValid() && oldActive.isLoaded) SceneManager.SetActiveScene(oldActive);
                // Keep the new scene on disk; don't leave two live XR rigs in the Editor.
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Shader RequiredShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Missing shader " + name);
            return shader;
        }
        private static Transform Node(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (authoringScene.IsValid() && authoringScene.isLoaded)
                SceneManager.MoveGameObjectToScene(go, authoringScene);
            if (parent != null) go.transform.SetParent(parent, false);
            return go.transform;
        }
        private static float R(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
        private static Color Green(float t) => Color.Lerp(new Color(.25f, .46f, .12f), new Color(.62f, .76f, .34f), t);
        private static float Height(float x, float z)
        {
            float d = new Vector2(x, z).magnitude;
            float flat = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12, 95, d));
            float broad = Mathf.PerlinNoise(x * .009f + 18.2f, z * .009f + 63.7f);
            float small = Mathf.PerlinNoise(x * .032f + 7.3f, z * .032f + 22.9f);
            float rim = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(80, 210, d));
            return flat * (.3f * small + recipe.HillHeight * rim * broad * broad);
        }
        private static float Surface(float x, float z) => terrainData.GetInterpolatedHeight(
            x / recipe.TerrainWidth + .5f, z / recipe.TerrainWidth + .5f) - 2f;
        private static void CreateAnchors(Transform root, Vector3 player, Quaternion forward)
        {
            root.position = player; root.rotation = forward;
            Node("CreationAnchor_01", root).localPosition = new Vector3(-1.15f, .35f, 1.85f);
            Node("CreationAnchor_02", root).localPosition = new Vector3(0, .35f, 2.3f);
            Node("CreationAnchor_03", root).localPosition = new Vector3(1.15f, .35f, 1.85f);
            Node("DreamSeedAnchor", root).localPosition = new Vector3(0, 1.15f, 1.75f);
            Node("AICompanionAnchor", root).localPosition = new Vector3(-1.25f, 1.35f, 1.55f);
        }
        private static void CreateAudio(Transform root)
        {
            var ambience = root.gameObject.AddComponent<MeadowAmbience>();
            var serialized = new SerializedObject(ambience);
            string[] names = { "AmbientAudioSource", "BirdsongAudioSource", "MusicAudioSource" };
            string[] fields = { "windSource", "birdsSource", "musicSource" };
            for (int i = 0; i < names.Length; i++)
            {
                var source = Node(names[i], root).gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = true; source.spatialBlend = 0;
                serialized.FindProperty(fields[i]).objectReferenceValue = source;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
