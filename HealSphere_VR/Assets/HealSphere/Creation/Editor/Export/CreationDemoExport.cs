using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast.Export;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace HealSphere.Creation.Editor
{
    public static class CreationDemoExport
    {
        public static async Task Export()
        {
            const string path = "Assets/StreamingAssets/healsphere-cat-demo.glb"; if (File.Exists(path)) return;
            Directory.CreateDirectory("Assets/StreamingAssets");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HealSphere/FallbackModels/CatModel/tripo_convert_43784a09-c8eb-4b61-b5b9-e457343ca009.fbx");
            var preview = EditorSceneManager.NewPreviewScene(); GameObject cat = null;
            try
            {
                cat = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview); cat.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); cat.transform.localScale = Vector3.one;
                var exporter = new GameObjectExport(new ExportSettings { Format = GltfFormat.Binary, ImageDestination = ImageDestination.MainBuffer, FileConflictResolution = FileConflictResolution.Abort });
                if (!exporter.AddScene(new[] { cat }, "Reviewed prebuilt orange cat") || !await exporter.SaveToFileAndDispose(path)) throw new InvalidOperationException("Prepared cat GLB export failed.");
            }
            finally { if (cat != null) Object.DestroyImmediate(cat); EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.Refresh();
        }
    }
}
