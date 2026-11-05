using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HealSphere.Breathing.Editor
{
    /// <summary>Renders the actual 3D effect into files; creates no UI and saves no scene changes.</summary>
    public static class FormationPreview
    {
        [MenuItem("HealSphere/Offline Prototype/Render Formation Preview Frames")]
        public static void Run()
        {
            OfflinePrototypeChecks.Run();
            const string root = "Assets/HealSphere/OfflinePrototype/Authored/";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Cat Placeholder.prefab");
            var revealMaterial = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Gentle Reveal.mat");
            var particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Soft Particles.mat");
            if (prefab == null || revealMaterial == null || particleMaterial == null) throw new InvalidOperationException("Assign/create the authored offline assets first.");
            var previous = SceneManager.GetActiveScene();
            // Preview scenes never require saving/closing the user's current scene.
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(512, 512, 24);
            var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var montage = new Texture2D(2048, 512, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var light = PreviewObject("Preview Key").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1; light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.Euler(35, -35, 0);
                var camera = PreviewObject("Preview Camera").AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.fieldOfView = 40; camera.nearClipPlane = 0.02f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.33f, 0.38f, 0.48f);
                camera.transform.position = new Vector3(0, 0.29f, -1.1f); camera.transform.LookAt(new Vector3(0, 0.225f, 0));
                camera.targetTexture = target;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                model.transform.localScale = Vector3.one * 0.37f;
                var system = PreviewObject("Preview Motes").AddComponent<ParticleSystem>();
                var main = system.main; main.playOnAwake = false; main.startSpeed = 0; main.loop = false;
                var particleRenderer = system.GetComponent<ParticleSystemRenderer>(); particleRenderer.sharedMaterial = particleMaterial;
                particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
                var reveal = PreviewObject("Preview Controller").AddComponent<ParticleModelReveal>();
                var serialized = new SerializedObject(reveal);
                serialized.FindProperty("formationParticles").objectReferenceValue = system;
                serialized.FindProperty("dissolveMaterial").objectReferenceValue = revealMaterial;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    foreach (var material in renderer.sharedMaterials) ShaderUtil.CompilePass(material, 0, true);
                ShaderUtil.CompilePass(revealMaterial, 0, true); ShaderUtil.CompilePass(particleMaterial, 0, true);
                if (!reveal.Prepare(model)) throw new InvalidOperationException("Formation preview preparation failed.");
                var advance = typeof(ParticleModelReveal).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
                var stages = new[] { 0.02f, 0.36f, 0.68f, 1f };
                Directory.CreateDirectory("Library/FormationPreview");
                for (int stage = 0; stage < stages.Length; stage++)
                {
                    reveal.SetProgress(stages[stage]);
                    for (int frame = 0; frame < 240; frame++) advance.Invoke(reveal, new object[] { 1f / 60 });
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                    File.WriteAllBytes("Library/FormationPreview/Stage-" + stage + ".png", pixels.EncodeToPNG());
                    montage.SetPixels(stage * 512, 0, 512, 512, pixels.GetPixels());
                }
                montage.Apply(); File.WriteAllBytes("Library/FormationPreview/Formation-Sequence.png", montage.EncodeToPNG());
                foreach (var shader in new[] { revealMaterial.shader, particleMaterial.shader })
                    if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Formation shader compilation failed: " + shader.name);
                Debug.Log("Formation preview rendered: Library/FormationPreview/Formation-Sequence.png. Desktop preview only; Quest stereo/performance still need testing.");
            }
            finally
            {
                RenderTexture.active = previousTarget; ShaderUtil.allowAsyncCompilation = previousAsync;
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(montage);
                EditorSceneManager.ClosePreviewScene(scene);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            GameObject PreviewObject(string name)
            {
                var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
            }
        }
    }
}
