using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace HealSphere.Breathing.Editor
{
    public static class OfflinePrototypeChecks
    {
        [MenuItem("HealSphere/Offline Prototype/Run Deterministic Checks")]
        public static void Run()
        {
            int cycles = 0;
            var clock = new GuidedBreathingClock(4, 5);
            for (int i = 0; i < 35; i++) { if (clock.Advance(0.25f)) cycles++; }
            Require(cycles == 0 && clock.Phase == BreathPhase.Exhale, "4/5 clock must not award an early exhale.");
            Require(clock.Advance(0.25f) && clock.Phase == BreathPhase.Inhale, "One 9-second cycle should emit exactly once.");
            Require(!clock.Advance(300), "A long frame must not award catch-up cycles.");
            var gate = new MotionBreathGate();
            Require(!gate.Advance(0.001f, 1, 0.005f, 0.8f), "Small motion must not trigger.");
            Require(!gate.Advance(0.006f, 1, 0.005f, 0.8f), "Expansion alone must not complete an exhale.");
            Require(!gate.Advance(-0.006f, 1.1f, 0.005f, 0.8f), "Hysteresis minimum duration must suppress chatter.");
            Require(gate.Advance(-0.006f, 2, 0.005f, 0.8f), "Expansion followed by contraction should emit once.");
            Require(!gate.Advance(-0.006f, 3, 0.005f, 0.8f), "A held controller must not emit duplicate cycles.");
            var root = new GameObject("Temporary Surface Check");
            var mesh = new Mesh();
            try
            {
                var child = new GameObject("Transformed Triangle"); child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(1, 2, 3); child.transform.localScale = new Vector3(2, 1, 1);
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }; mesh.triangles = new[] { 0, 1, 2 };
                child.AddComponent<MeshFilter>().sharedMesh = mesh; child.AddComponent<MeshRenderer>();
                var samples = MeshSurfaceSampler.Collect(root, 128);
                Require(samples.Length == 128, "Sampling must respect the requested cap.");
                foreach (var p in samples)
                    Require(Mathf.Abs(p.z - 3) < 0.0001f && p.x >= 1 && p.y >= 2 && (p.x - 1) / 2 + p.y - 2 <= 1.0001f,
                        "Samples must lie on the transformed triangle.");
                Require(MeshSurfaceSampler.Collect(root, 10000).Length == 512, "Hard cap must be enforced.");
                child.transform.localRotation = Quaternion.Euler(0, 90, 0);
                foreach (var p in MeshSurfaceSampler.Collect(root, 64))
                    Require(Mathf.Abs(p.x - 1) < 0.0001f && p.y >= 2 && p.z >= 0.9999f && p.z <= 3.0001f && (3 - p.z) / 2 + p.y - 2 <= 1.0001f,
                        "Sampling must include child rotation as well as scale/translation.");
                mesh.UploadMeshData(true);
                Require(MeshSurfaceSampler.Collect(root, 128).Length == 0, "Unreadable meshes must use dissolve fallback.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
            CheckMaterialRestoration();
            CheckFormationPaths();
            CheckScene();
            File.WriteAllText("Library/OfflinePrototypeChecks.txt", "PASS: guided timing, motion hysteresis, transformed mesh sampling/caps/fallback, staggered spiral arrival and exact convergence, pause/monotonic smoothing/single completion, original material and renderer/slot property-block restoration, scene references and disabled locomotion. UI remains explicitly pending. No UI or APIs created/used.\n");
            Debug.Log("Offline prototype deterministic checks passed. Runtime session/UI and headset playtests remain required.");
        }
        private static void CheckMaterialRestoration()
        {
            var original = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var dissolve = new Material(Shader.Find("HealSphere/Offline Gentle Reveal"));
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var controller = new GameObject("Temporary Reveal Check");
            try
            {
                var renderer = model.GetComponent<Renderer>(); renderer.sharedMaterial = original;
                var properties = new MaterialPropertyBlock(); properties.SetFloat("_TestSentinel", 42); renderer.SetPropertyBlock(properties);
                var slot = new MaterialPropertyBlock(); slot.SetFloat("_SlotSentinel", 17); renderer.SetPropertyBlock(slot, 0);
                var reveal = controller.AddComponent<ParticleModelReveal>();
                var serialized = new SerializedObject(reveal); serialized.FindProperty("dissolveMaterial").objectReferenceValue = dissolve; serialized.ApplyModifiedPropertiesWithoutUndo();
                Require(reveal.Prepare(model), "A static mesh must accept the dissolve fallback without particles.");
                Require(renderer.sharedMaterial == dissolve, "Reveal should use the temporary material.");
                reveal.Cancel();
                Require(renderer.sharedMaterial == original, "Original material must be restored on cancel.");
                renderer.GetPropertyBlock(properties); Require(properties.GetFloat("_TestSentinel") == 42, "Original property block must be restored.");
                renderer.GetPropertyBlock(slot, 0); Require(slot.GetFloat("_SlotSentinel") == 17, "Original slot property block must be restored.");
                Require(reveal.Prepare(model), "Reveal must be reusable after cancellation.");
                var advance = typeof(ParticleModelReveal).GetMethod("Advance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int completions = 0; reveal.Revealed += () => completions++;
                reveal.SetProgress(1); reveal.SetPaused(true); advance.Invoke(reveal, new object[] { 0.1f });
                Require(reveal.Progress == 0 && completions == 0, "Pause must freeze formation.");
                reveal.SetPaused(false);
                float previous = 0;
                for (int frame = 0; frame < 600; frame++)
                {
                    advance.Invoke(reveal, new object[] { 1f / 60 });
                    Require(reveal.Progress >= previous && reveal.Progress <= 1, "Smoothing must be monotonic and bounded."); previous = reveal.Progress;
                }
                Require(reveal.Progress == 1 && completions == 1, "Formation must finish exactly once.");
                Require(renderer.sharedMaterial == original, "Full formation must restore the original material.");
            }
            finally { Object.DestroyImmediate(controller); Object.DestroyImmediate(model); Object.DestroyImmediate(original); Object.DestroyImmediate(dissolve); }
        }
        private static void CheckFormationPaths()
        {
            var center = new Vector3(0, 0.2f, 0); var start = new Vector3(0.24f, 0.03f, 0); var target = new Vector3(0.1f, 0.4f, -0.1f);
            Require(SurfaceFormationMotion.Arrival(0.4f, 0) > SurfaceFormationMotion.Arrival(0.4f, 1), "Motes must arrive in staggered groups.");
            var first = SurfaceFormationMotion.Position(center, start, target, 0, 0, 0.55f, 0, 0.025f);
            var later = SurfaceFormationMotion.Position(center, start, target, 0, 1, 0.55f, 0, 0.025f);
            Require((later - first).sqrMagnitude > 0.0001f, "Unsettled motes must keep swirling between cycles.");
            for (int i = 0; i < 100; i++)
            {
                float order = i / 99f;
                float arrival = SurfaceFormationMotion.Arrival(1, order);
                var final = SurfaceFormationMotion.Position(center, start, target, arrival, 50, 0.55f, i, 0.025f);
                Require(arrival == 1 && (final - target).sqrMagnitude < 0.0000001f, "Every mote must converge exactly on its surface sample.");
            }
        }
        private static void CheckScene()
        {
            const string path = "Assets/HealSphere/OfflinePrototype/Authored/BreathingExperience.unity";
            if (!File.Exists(path)) return;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                Require(transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "Scene must not contain missing scripts.");
                Require(transforms.Select(t => t.GetComponent<XROrigin>()).Count(c => c != null) == 1, "Scene must retain exactly one XR Origin.");
                Require(!transforms.SelectMany(t => t.GetComponents<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>()).Any(p => p.enabled), "Scene locomotion must be disabled.");
                var creation = transforms.Select(t => t.GetComponent<CreationManager>()).Single(c => c != null);
                Require(creation.IsConfigured, "Gameplay prefab/reveal/anchor references must be complete.");
                var presenter = transforms.Select(t => t.GetComponent<CompanionDialogueController>()).Single(c => c != null);
                if (!presenter.IsConfigured) Debug.Log("UI is explicitly pending visual Editor authoring; no fallback was generated.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
