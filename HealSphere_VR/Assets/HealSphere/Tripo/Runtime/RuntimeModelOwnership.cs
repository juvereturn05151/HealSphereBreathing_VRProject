using GLTFast;
using UnityEngine;
namespace HealSphere.Tripo
{
    /// <summary>Releases this completed creation's glTFast meshes, materials and textures when it leaves the world.</summary>
    public sealed class RuntimeModelOwnership : MonoBehaviour
    {
        private GltfImport importer;
        internal void Own(GltfImport value) => importer = value;
        private void OnDestroy() { importer?.Dispose(); importer = null; }
    }
}
