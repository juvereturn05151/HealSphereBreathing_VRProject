using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace HealSphere.Meadow.Editor
{
    public static partial class MeadowBuilder
    {
        private static void ValidateAndPreview(Scene scene, XROrigin origin, Vector3 player, Quaternion forward, List<string> disabled)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            int missing = all.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if (missing > 0) throw new InvalidOperationException("Scene contains " + missing + " missing scripts.");
            string[] anchorNames = { "CreationAnchor_01", "CreationAnchor_02", "CreationAnchor_03", "DreamSeedAnchor", "AICompanionAnchor" };
            foreach (string name in anchorNames)
                if (all.Count(t => t.name == name) != 1) throw new InvalidOperationException("Expected one " + name);
            var meadowTerrain = all.Select(t => t.GetComponent<Terrain>()).First(t => t != null);
            if (Quaternion.Angle(meadowTerrain.transform.rotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException("Unity Terrain and its parents must remain unrotated.");
            var activeRenderers = all.Where(t => t.gameObject.activeInHierarchy).Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null).ToArray();
            long triangles = activeRenderers.Sum(r => (long)(r.GetComponent<MeshFilter>()?.sharedMesh?.triangles.Length ?? 0) / 3);
            foreach (var renderer in activeRenderers)
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null || ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))
                    throw new InvalidOperationException("Missing or invalid material on " + renderer.name);
            foreach (var anchor in all.Where(t => t.name.StartsWith("CreationAnchor_")))
            {
                var p = anchor.position - player; p.y = 0;
                if (p.magnitude < 1.5f || p.magnitude > 2.5f) throw new InvalidOperationException("Creation anchor outside comfort arc.");
            }
            var previewObject = Node("EditorPreviewCamera (Temporary)");
            var camera = previewObject.gameObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            // Ordinary additive scenes can share Editor culling bits. A temporary render-only
            // layer excludes the other loaded scene without changing or saving its objects.
            var environment = all.First(t => t.name == "EnvironmentRoot");
            var previewObjects = environment.GetComponentsInChildren<Transform>(true);
            var originalLayers = previewObjects.Select(t => t.gameObject.layer).ToArray();
            foreach (var item in previewObjects) item.gameObject.layer = 31;
            camera.cullingMask = 1 << 31;
            camera.transform.position = player + Vector3.up * 1.25f;
            camera.transform.rotation = forward * Quaternion.Euler(-1, 0, 0);
            camera.fieldOfView = 76; camera.nearClipPlane = .05f; camera.farClipPlane = 600;
            var target = new RenderTexture(1440, 900, 24);
            var oldTarget = RenderTexture.active;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var material in activeRenderers.Select(r => r.sharedMaterial).Distinct())
                    ShaderUtil.CompilePass(material, 0, true);
                camera.targetTexture = target;
                string[] viewNames = { "SeatedView", "StandingView", "LeftView", "RearView", "RightView" };
                float[] viewHeights = { 1.25f, 1.7f, 1.25f, 1.25f, 1.25f };
                float[] viewYaws = { 0, 0, -90, 180, 90 };
                for (int view = 0; view < viewNames.Length; view++)
                {
                    camera.transform.position = player + Vector3.up * viewHeights[view];
                    camera.transform.rotation = forward * Quaternion.Euler(-1, viewYaws[view], 0);
                    camera.Render(); RenderTexture.active = target;
                    var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); image.Apply();
                    File.WriteAllBytes(output + "/Previews/" + viewNames[view] + ".png", image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
                for (int i = 0; i < previewObjects.Length; i++) previewObjects[i].gameObject.layer = originalLayers[i];
                RenderTexture.active = oldTarget; camera.targetTexture = null; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(previewObject.gameObject);
            }
            File.WriteAllText(Report, "SUCCESS\nScene: " + scene.path + "\nSeed: " + recipe.Seed +
                "\nXR Origin count: 1\nOriginal rig: " + origin.name + "\nMissing scripts: " + missing +
                "\nGameplay anchors: 5 unique; creation distances within 1.5-2.5 m\nTerrain rotation: identity\n" +
                "\nActive mesh renderers (includes both tree LODs): " + activeRenderers.Length +
                "\nMesh triangles (both LODs, excludes Terrain/XR skinned meshes): " + triangles +
                "\nDisabled existing locomotion providers in copy: " + string.Join(", ", disabled) +
                "\nHeadset stereo, APK build and 72 FPS require Quest 2 validation.\n");
            AssetDatabase.Refresh();
        }
    }
}
