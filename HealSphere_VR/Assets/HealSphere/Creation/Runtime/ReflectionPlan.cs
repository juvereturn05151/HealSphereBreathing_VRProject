using System;
using UnityEngine;

namespace HealSphere.Creation
{
    [Serializable] public sealed class ReflectionDesign
    {
        [SerializeField] private string role, pose, expression, narration, animal, displayName, modelEndpoint, modelSource;
        [SerializeField] private bool isDemo;
        public string Role => role; public string Pose => pose; public string Narration => narration; public string Animal => animal;
        public string Name => displayName; public string Endpoint => modelEndpoint; public bool IsDemo => isDemo;
        public bool IsPrebuilt => isDemo || modelSource == "prebuilt";
        public static ReflectionDesign Demo(string animal, int index) => new ReflectionDesign {
            animal = animal, role = new[] { "reflection", "support", "hope" }[index], pose = new[] { "sleeping", "seated", "standing" }[index],
            displayName = animal + " — " + new[] { "Reflection", "Support", "Hope" }[index] + " (offline illustration)",
            narration = new[] { "This resting friend offers a quiet pause.", "This one offers a little comfort.", "This friend leaves room for a hopeful possibility." }[index], isDemo = true
        };
    }
    [Serializable] public sealed class ReflectionPlan
    {
        [SerializeField] private string id, animal, source, closing;
        [SerializeField] private ReflectionDesign[] designs;
        [SerializeField] private int missingModels;
        public string Id => id; public string Animal => animal; public string Closing => closing; public string Source => source;
        public ReflectionDesign[] Designs => designs; public int Missing => missingModels;
        public bool IsValid => designs != null && designs.Length == 3 && Array.TrueForAll(designs, x => x != null && x.Animal == animal);
        public static ReflectionPlan Offline(string animal) => new ReflectionPlan {
            animal = animal, source = "offline_demo", id = "", closing = "Thank you for this little moment. You can take things at your own pace.",
            designs = new[] { ReflectionDesign.Demo(animal, 0), ReflectionDesign.Demo(animal, 1), ReflectionDesign.Demo(animal, 2) }
        };
    }
}
