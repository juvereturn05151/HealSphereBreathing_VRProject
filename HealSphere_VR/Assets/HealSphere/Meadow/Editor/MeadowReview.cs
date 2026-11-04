using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace HealSphere.Meadow.Editor
{
    public static partial class MeadowBuilder
    {
        [InitializeOnLoadMethod]
        private static void QueueReview() => EditorApplication.delayCall += ReviewRequest;

        private static void ReviewRequest()
        {
            const string request = "Library/MeadowReview.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            ReviewMeadow();
        }

        [MenuItem("HealSphere/Meadow/Validate and Render Selected Meadow Scene")]
        public static void ReviewMeadow()
        {
            var selected = Selection.activeObject as SceneAsset;
            string path = selected != null ? AssetDatabase.GetAssetPath(selected) : Root + "/BakedMeadow/BreathingMeadow.unity";
            if (!path.StartsWith(Root + "/BakedMeadow") || !File.Exists(path))
                throw new System.InvalidOperationException("Select a generated meadow scene asset in the Project window.");
            if (SceneManager.GetSceneByPath(path).isLoaded)
                throw new System.InvalidOperationException("Close the selected meadow before reviewing its saved state. Save any intended edits first.");
            output = Path.GetDirectoryName(path).Replace('\\', '/');
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            authoringScene = scene;
            SceneManager.SetActiveScene(scene);
            try
            {
                recipe = AssetDatabase.LoadAssetAtPath<MeadowRecipe>(Root + "/MeadowRecipe.asset");
                var origins = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).ToArray();
                if (origins.Length != 1) throw new System.InvalidOperationException("Expected exactly one preserved XR Origin.");
                var origin = origins[0];
                var disabled = origin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true)
                    .Where(p => !p.enabled).Select(p => p.GetType().Name).ToList();
                ValidateAndPreview(scene, origin, origin.transform.position, Quaternion.Euler(0, origin.transform.eulerAngles.y, 0), disabled);
            }
            catch (System.Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
