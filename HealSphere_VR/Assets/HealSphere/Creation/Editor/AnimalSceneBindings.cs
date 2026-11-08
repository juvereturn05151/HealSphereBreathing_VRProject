using System;
using System.IO;
using System.Linq;
using HealSphere.Breathing;
using HealSphere.Tripo;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HealSphere.Creation.Editor
{
    /// <summary>Bind the scene controls authored visually in Unity.</summary>
    [InitializeOnLoad]
    public static class AnimalSceneBindings
    {
        static AnimalSceneBindings() => EditorApplication.update += Consume;

        private static void Consume()
        {
            const string marker = "Library/HealSphereAnimalBind.request";
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(marker)) return;
            try { File.Delete(marker); } catch (IOException) { return; }
            try { Bind(); File.WriteAllText("Library/HealSphereAnimalBind.txt", "PASS"); }
            catch (Exception e) { File.WriteAllText("Library/HealSphereAnimalBind.txt", e.ToString()); }
        }

        [MenuItem("HealSphere/Animal Creation/Bind Authored Picker")]
        public static void Bind()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != CreationSceneAssembler.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open the saved creation scene in Edit Mode.");
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            Button Named(string name) => buttons.Single(button => button.name == name);
            var choices = new[] { Named("PauseButton"), Named("RestartButton"), Named("WellnessButton"),
                Named("MicrophoneButton"), Named("VoiceMuteButton"), Named("VoiceVolumeButton") };
            var create = Named("PauseButton (1)");
            var more = Named("WellnessButton (1)");
            var panel = create.transform.parent.GetComponent<CanvasGroup>();
            var image = panel.GetComponent<Image>();
            var creation = One<CreationSessionDirector>();
            var presenter = One<CreationSessionPresenter>();
            var backend = One<TripoModelLoader>();
            var breathing = One<GuidedBreathingProvider>();
            var reflection = One<ReflectionExperienceDirector>();
            ApplyArt(image, choices);
            var director = creation.GetComponent<AnimalCreationDirector>() ?? Undo.AddComponent<AnimalCreationDirector>(creation.gameObject);
            var serialized = new SerializedObject(director);
            void Assign(string field, Object value) => serialized.FindProperty(field).objectReferenceValue = value;
            Assign("creation", creation);
            Assign("presenter", presenter);
            Assign("backend", backend);
            Assign("breathing", breathing);
            Assign("panel", panel);
            Assign("panelImage", image);
            Assign("createButton", create);
            Assign("moreButton", more);
            Assign("narrator", creation.GetComponent<RabbitVoiceNarrator>());
            Assign("awakeningPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(AnimalAwakeningPrefab.Path)?.GetComponent<AnimalAwakening>());
            var array = serialized.FindProperty("choices");
            array.arraySize = choices.Length;
            for (int i = 0; i < choices.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = choices[i];
            serialized.ApplyModifiedProperties();
            Undo.RecordObject(reflection, "Use animal creation at launch");
            reflection.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T One<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include).Single();

        private static void ApplyArt(Image panel, Button[] choices)
        {
            const string art = "Assets/HealSphere/Creation/Art/";
            var importer = (TextureImporter)AssetImporter.GetAtPath(art + "LavenderPanel.png");
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Undo.RecordObject(panel, "Assign lavender panel art");
            panel.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art + "LavenderPanel.png");
            panel.type = Image.Type.Simple;
            var icons = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(art + "Animal Faces.asset");
            if (icons == null)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "AnimalFaces.png");
                icons = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                var serialized = new SerializedObject(icons);
                serialized.FindProperty("m_Version").stringValue = "1.1.0";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                icons.spriteSheet = texture;
                icons.material = new Material(Shader.Find("TextMeshPro/Sprite")) { mainTexture = texture };
                for (uint index = 0; index < 8; index++)
                {
                    var glyph = new TMP_SpriteGlyph(index, new GlyphMetrics(128, 128, 0, 105, 135),
                        new GlyphRect((int)index * 128, 0, 128, 128), 1, 0);
                    icons.spriteGlyphTable.Add(glyph);
                    icons.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xe000 + index, icons, glyph) { name = "animal" + index });
                }
                icons.UpdateLookupTables();
                AssetDatabase.CreateAsset(icons, art + "Animal Faces.asset");
                AssetDatabase.AddObjectToAsset(icons.material, icons);
                EditorUtility.SetDirty(icons);
                AssetDatabase.SaveAssets();
            }
            foreach (var choice in choices)
            {
                var label = choice.GetComponentInChildren<TMP_Text>(true);
                Undo.RecordObject(label, "Assign animal icon atlas");
                label.spriteAsset = icons;
            }
        }
    }
}
