using UnityEngine;

namespace HealSphere.Breathing
{
    /// <summary>Allocation-free spiral paths that end exactly on cached mesh-surface samples.</summary>
    public static class SurfaceFormationMotion
    {
        public static float Arrival(float progress, float order)
        {
            order = Mathf.Clamp01(order);
            float t = Mathf.InverseLerp(order * 0.28f, 0.28f + order * 0.62f, progress);
            return t * t * (3f - 2f * t);
        }

        public static Vector3 Position(Vector3 center, Vector3 startOffset, Vector3 target,
            float arrival, float elapsed, float radiansPerSecond, float phase, float lift)
        {
            float remaining = 1f - arrival;
            float angle = elapsed * radiansPerSecond + arrival * Mathf.PI * 2f;
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            var orbit = new Vector3(startOffset.x * cos - startOffset.z * sin,
                startOffset.y + Mathf.Sin(elapsed * 0.7f + phase) * lift,
                startOffset.x * sin + startOffset.z * cos);
            return Vector3.LerpUnclamped(center + orbit, target, arrival) +
                Vector3.up * (Mathf.Sin(arrival * Mathf.PI) * lift * remaining);
        }
    }
}
