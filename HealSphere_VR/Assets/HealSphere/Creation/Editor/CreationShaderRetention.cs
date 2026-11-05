using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HealSphere.Creation.Editor
{
    /// <summary>Retains supported runtime-import material variants using new Resources assets, without global graphics changes.</summary>
    [InitializeOnLoad]
    public static class CreationShaderRetention
    {
        static CreationShaderRetention() => EditorApplication.delayCall += () => {
            const string request = "Library/HealSphereCreationShaders.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request); Ensure();
        };
        [MenuItem("HealSphere/Creation/Prepare Runtime Import Shader References")]
        public static void Ensure()
        {
            const string root = "Assets/HealSphere/Creation/Resources/CreationImportShaders";
            Directory.CreateDirectory(root); AssetDatabase.Refresh();
            foreach (bool unlit in new[] { false, true })
            {
                string graph = unlit ? "glTF-unlit" : "glTF-pbrMetallicRoughness";
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.cloud.gltfast/Runtime/Shader/" + graph + ".shadergraph");
                if (shader == null) throw new InvalidOperationException("Missing installed glTFast URP shader: " + graph);
                for (int mask = 0; mask < (unlit ? 2 : 8); mask++) for (int transparent = 0; transparent < 3; transparent++)
                {
                    string path = root + "/" + graph + "-" + mask + "-" + transparent + ".mat";
                    if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;
                    var material = new Material(shader); var keywords = new List<string>();
                    if ((mask & 1) != 0) keywords.Add("_TEXTURE_TRANSFORM");
                    if (!unlit && (mask & 2) != 0) keywords.Add("_OCCLUSION");
                    if (!unlit && (mask & 4) != 0) keywords.Add("_EMISSIVE");
                    if (transparent == 1) { keywords.Add("_SURFACE_TYPE_TRANSPARENT"); material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0); material.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); material.renderQueue = 3000; }
                    if (transparent == 2) { keywords.Add("_ALPHATEST_ON"); material.SetFloat("_AlphaClip", 1); material.renderQueue = 2450; }
                    material.shaderKeywords = keywords.ToArray(); AssetDatabase.CreateAsset(material, path);
                }
            }
            AssetDatabase.SaveAssets(); File.WriteAllText("Library/HealSphereCreationShaders.txt", "PASS: 30 Resources material references retain supported glTFast PBR/unlit, emission, occlusion, texture transform and opaque/masked/transparent variants; no existing graphics settings changed.\n");
        }
    }
}
