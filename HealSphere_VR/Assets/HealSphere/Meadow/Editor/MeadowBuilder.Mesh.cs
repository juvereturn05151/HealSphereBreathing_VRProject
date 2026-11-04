using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HealSphere.Meadow.Editor
{
    public static partial class MeadowBuilder
    {
        // Opaque geometry avoids alpha overdraw and needs no runtime spawning or wind controller.
        private sealed class MeshDraft
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> indices = new List<int>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
            {
                int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                colors.Add(ca); colors.Add(cb); colors.Add(cc);
                indices.Add(index); indices.Add(index + 1); indices.Add(index + 2);
            }
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color) => Triangle(a, b, c, color, color, color);
            public void Ellipsoid(Vector3 center, Vector3 scale, Color color, int rings = 7, int segments = 10)
            {
                int start = vertices.Count;
                for (int y = 0; y <= rings; y++) for (int x = 0; x <= segments; x++)
                {
                    float latitude = Mathf.PI * y / rings;
                    float longitude = 2 * Mathf.PI * x / segments;
                    Vector3 unit = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    vertices.Add(center + Vector3.Scale(unit, scale));
                    colors.Add(color * Mathf.Lerp(.87f, 1.07f, (unit.y + 1) * .5f));
                }
                for (int y = 0; y < rings; y++) for (int x = 0; x < segments; x++)
                {
                    int a = start + y * (segments + 1) + x, b = a + segments + 1;
                    indices.Add(a); indices.Add(a + 1); indices.Add(b);
                    indices.Add(a + 1); indices.Add(b + 1); indices.Add(b);
                }
            }
            public void Stem(Vector3 bottom, Vector3 top, float width, Color color)
            {
                Vector3 right = Vector3.right * width, front = Vector3.forward * width;
                Tri(bottom - right, top, bottom + right, color);
                Tri(bottom - front, top, bottom + front, color);
            }
            public void Grass(Vector3 p, float height, float angle, Color color)
            {
                for (int blade = 0; blade < 3; blade++)
                {
                    float a = angle + blade * 2.09f;
                    Vector3 right = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Vector3 foot = p + right * .035f;
                    Vector3 tip = foot + Vector3.up * height + right * height * .4f;
                    Triangle(foot - right * .025f, tip, foot + right * .025f, color * .72f, color * 1.13f, color * .72f);
                }
            }
            public void Flower(Vector3 p, Color petal, float size, float rotation)
            {
                float height = size * R(1.9f, 3.1f);
                Stem(p, p + Vector3.up * height, .007f, Green(.2f));
                Vector3 center = p + Vector3.up * height;
                Vector3 normal = new Vector3(.2f * Mathf.Sin(rotation), 1, .2f * Mathf.Cos(rotation)).normalized;
                Vector3 u = Vector3.Cross(normal, Vector3.forward).normalized, v = Vector3.Cross(normal, u);
                for (int k = 0; k < 5; k++)
                {
                    float a = rotation + k * Mathf.PI * .4f;
                    Vector3 direction = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    Vector3 across = Vector3.Cross(normal, direction), end = center + direction * size;
                    Tri(center, end + across * size * .36f, end + direction * size * .2f + normal * .006f, petal);
                    Tri(center, end + direction * size * .2f + normal * .006f, end - across * size * .36f, petal);
                }
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3, b = (k + 1) * Mathf.PI / 3;
                    Tri(center + normal * .009f, center + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * size * .28f + normal * .009f,
                        center + (u * Mathf.Cos(b) + v * Mathf.Sin(b)) * size * .28f + normal * .009f, new Color(.98f, .72f, .17f));
                }
            }
            public Mesh Save(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, output + "/Meshes/" + name + ".asset");
                return mesh;
            }
        }
        private static GameObject MeshObject(string name, Transform parent, Mesh mesh)
        {
            var transform = Node(name, parent);
            transform.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = transform.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = vegetationMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, StaticEditorFlags.BatchingStatic);
            return transform.gameObject;
        }
        private static void SavePlant(string name, MeshDraft draft)
        {
            var temporary = MeshObject(name, null, draft.Save(name));
            PrefabUtility.SaveAsPrefabAsset(temporary, output + "/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(temporary);
        }
    }
}
