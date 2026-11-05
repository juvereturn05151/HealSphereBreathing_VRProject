using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace HealSphere.Wellness.Editor
{
    /// <summary>Explicit, undoable correction of prepared 3D model facing only. Never authors UI.</summary>
    [InitializeOnLoad]
    public static class MomentScenePolish
    {
        static MomentScenePolish() => EditorApplication.delayCall += () => {
            const string request = "Library/HealSphereMomentPolish.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { EnsureTreeEmissionMaterial(); }
            catch (Exception e) { Debug.LogException(e); }
        };
        [MenuItem("HealSphere/A Little Moment/Preserve Tree Emission Shader Variant")]
        public static void EnsureTreeEmissionMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MomentExperienceAssembler.Root + "/Materials/Tree Breathing 0.mat");
            if (material == null) throw new InvalidOperationException("The scene's tree material is missing.");
            Undo.RecordObject(material, "Preserve tree emission shader variant");
            material.SetColor("_EmissionColor", new Color(.02f, .015f, .01f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        }
        [MenuItem("HealSphere/A Little Moment/Face Prepared Models Toward Arrival")]
        public static void FacePreparedModels()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MomentExperienceAssembler.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open HealSphereMoment outside Play Mode.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var viewer = all.Single(t => t.name == "Stationary Desktop Camera");
            foreach (var name in new[] { "Orange Cat", "Gentle Moon", "Rabbit Companion" })
            {
                var model = all.Single(t => t.name == name); var renderers = model.GetComponentsInChildren<Renderer>(true);
                Bounds Bounds() { var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); return b; }
                var before = Bounds().center; var direction = viewer.position - before; direction.y = 0;
                Undo.RecordObject(model, "Face prepared model toward arrival"); model.rotation = Quaternion.LookRotation(direction);
                model.position += before - Bounds().center; PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            }
            var companion = all.Select(t => t.GetComponent<FloatingCompanionController>()).Single(c => c != null);
            Undo.RecordObject(companion, "Correct imported companion forward");
            var fields = new SerializedObject(companion); fields.FindProperty("facingOffset").vector3Value = Vector3.zero; fields.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
