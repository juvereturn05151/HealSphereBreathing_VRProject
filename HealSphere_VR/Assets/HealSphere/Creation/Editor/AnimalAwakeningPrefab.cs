using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HealSphere.Creation.Editor
{
    internal static class AnimalAwakeningPrefab
    {
        internal const string Path = "Assets/HealSphere/Creation/Resources/HealSphereAnimalAwakening.prefab";
        [MenuItem("HealSphere/Animal Creation/Create Awakening VFX Prefab")]
        private static void Ensure()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/HealSphere/OfflinePrototype/Authored/Materials/Soft Particles.mat");
            if (material == null) return;
            if (!AssetDatabase.IsValidFolder("Assets/HealSphere/Creation/Resources"))
                AssetDatabase.CreateFolder("Assets/HealSphere/Creation", "Resources");
            var root = new GameObject("HealSphereAnimalAwakening");
            try
            {
                var controller = root.AddComponent<AnimalAwakening>();
                var references = new SerializedObject(controller);
                references.FindProperty("warmth").objectReferenceValue = Make(root.transform, material, false);
                references.FindProperty("pulse").objectReferenceValue = Make(root.transform, material, true);
                references.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static ParticleSystem Make(Transform parent, Material material, bool pulse)
        {
            var child = new GameObject(pulse ? "Exhale Pulse" : "Warm Motes");
            child.transform.SetParent(parent, false);
            var particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = !pulse;
            main.duration = 2f;
            main.maxParticles = pulse ? 24 : 36;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = pulse ? 1.5f : 2.5f;
            main.startSpeed = pulse ? .39f : .075f;
            main.startSize = new ParticleSystem.MinMaxCurve(.017f, .039f);
            main.startColor = new Color(1f, .72f, .47f, .45f);
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = pulse ? .15f : .34f;
            shape.radiusThickness = .35f;
            var emission = particles.emission;
            emission.rateOverTime = pulse ? 0f : 7f;
            if (pulse) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)22) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .28f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }
    }
}
