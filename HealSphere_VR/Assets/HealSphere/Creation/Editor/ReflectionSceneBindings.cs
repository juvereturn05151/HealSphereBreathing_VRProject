using System;
using System.IO;
using System.Linq;
using HealSphere.Breathing;
using HealSphere.Tripo;
using HealSphere.Wellness;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HealSphere.Creation.Editor
{
    /// <summary>Only binds existing visually authored UI. Adds non-UI controllers and a voice AudioSource.</summary>
    [InitializeOnLoad]
    public static class ReflectionSceneBindings
    {
        static ReflectionSceneBindings() => EditorApplication.update += Consume;
        private static void Consume()
        {
            const string marker = "Library/HealSphereReflectionBind.request";
            if (!File.Exists(marker) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try { File.Delete(marker); } catch (IOException) { return; }
            try { Bind(); } catch (Exception e) { File.WriteAllText("Library/HealSphereReflectionBind.txt", e.ToString()); }
        }
        private static T One<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include).Single();
        private static T Add<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? Undo.AddComponent<T>(root);
        private static void Set(Object item, string field, Object reference) { var so = new SerializedObject(item); so.FindProperty(field).objectReferenceValue = reference; so.ApplyModifiedPropertiesWithoutUndo(); }
        [MenuItem("HealSphere/Reflection/Bind Existing Authored Controls")]
        public static void Bind()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != CreationSceneAssembler.ScenePath) throw new InvalidOperationException("Open the creation scene outside Play Mode.");
            Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include).SingleOrDefault(b => b.name == name) ?? throw new InvalidOperationException("Visually author " + name + " in the existing MomentPanel first. No UI fallback is created.");
            // Resolve all required UI before adding anything or saving the scene.
            var mic = Button("MicrophoneButton"); var mute = Button("VoiceMuteButton"); var volume = Button("VoiceVolumeButton");
            var creation = One<CreationSessionDirector>(); var root = creation.gameObject;
            var controls = Add<ReflectionControlsPresenter>(root); Set(controls,"microphoneButton",mic);Set(controls,"muteButton",mute);Set(controls,"volumeButton",volume);
            var voice = Add<RabbitVoiceNarrator>(root);
            var audio = root.GetComponents<AudioSource>().FirstOrDefault(a => !a.playOnAwake && a.clip == null) ?? Undo.AddComponent<AudioSource>(root);
            audio.playOnAwake = false; audio.loop = false; audio.spatialBlend = 0; audio.volume = .65f;
            Set(voice,"backend",One<TripoModelLoader>());Set(voice,"voiceSource",audio);Set(voice,"ambience",One<AudioManager>());
            var microphone = Add<ReflectionMicrophoneInput>(root);Set(microphone,"backend",One<TripoModelLoader>());Set(microphone,"narrator",voice);
            var director = Add<ReflectionExperienceDirector>(root);
            Set(director,"creation",creation);Set(director,"presentation",One<CreationSessionPresenter>());Set(director,"backend",One<TripoModelLoader>());
            Set(director,"breathing",One<GuidedBreathingProvider>());Set(director,"seed",One<DreamSeedController>());Set(director,"environmentReaction",One<EnvironmentReactionController>());Set(director,"companion",One<FloatingCompanionController>());
            Set(director,"controls",controls);Set(director,"narrator",voice);Set(director,"microphone",microphone);
            if (!director.IsConfigured) throw new InvalidOperationException("Reflection references incomplete.");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            File.WriteAllText("Library/HealSphereReflectionBind.txt","PASS: existing authored mic/mute/volume controls, original presenter/input/keyboard, new reflection/voice/microphone controllers, original creation/reveal/seed/environment/rabbit/audio references assigned. No UI generated.\n");
        }
    }
}
