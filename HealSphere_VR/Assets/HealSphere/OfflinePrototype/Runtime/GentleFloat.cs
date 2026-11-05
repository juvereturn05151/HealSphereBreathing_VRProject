using UnityEngine;

namespace HealSphere.Breathing
{
    public sealed class GentleFloat : MonoBehaviour
    {
        [SerializeField, Range(0, 0.05f)] private float amplitude = 0.025f;
        [SerializeField, Range(0.05f, 0.5f)] private float frequency = 0.15f;
        private Vector3 resting;
        private void Awake() => resting = transform.localPosition;
        private void Update() => transform.localPosition = resting + Vector3.up * (Mathf.Sin(Time.time * frequency * Mathf.PI * 2) * amplitude);
    }
}
