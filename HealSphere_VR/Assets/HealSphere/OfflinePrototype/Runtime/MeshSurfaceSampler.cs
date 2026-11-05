using System;
using System.Collections.Generic;
using UnityEngine;

namespace HealSphere.Breathing
{
    public static class MeshSurfaceSampler
    {
        private readonly struct Triangle
        {
            public readonly Vector3 A, B, C;
            public readonly float AreaEnd;
            public Triangle(Vector3 a, Vector3 b, Vector3 c, float areaEnd) { A = a; B = b; C = c; AreaEnd = areaEnd; }
        }

        /// <summary>Samples area-weighted triangles after transforming vertices into the model's root space.</summary>
        public static Vector3[] Collect(GameObject model, int count, int triangleBudget = 20000)
        {
            var triangles = new List<Triangle>();
            float area = 0;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !filter.gameObject.activeInHierarchy || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                AddMesh(filter.sharedMesh, model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix);
            }
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy || renderer.sharedMesh == null || renderer.sharedMesh.vertexCount > 60000) continue;
                var baked = new Mesh();
                try { renderer.BakeMesh(baked); AddMesh(baked, model.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix); }
                catch (UnityException) { /* The shader reveal remains available for unsupported skinning. */ }
                finally { UnityEngine.Object.Destroy(baked); }
            }
            if (triangles.Count == 0 || area <= 0) return Array.Empty<Vector3>();
            count = Mathf.Clamp(count, 1, 512);
            var result = new Vector3[count];
            var random = new System.Random(7531);
            for (int i = 0; i < count; i++)
            {
                float sample = (float)random.NextDouble() * area;
                int lo = 0, hi = triangles.Count - 1;
                while (lo < hi) { int mid = (lo + hi) / 2; if (triangles[mid].AreaEnd < sample) lo = mid + 1; else hi = mid; }
                var t = triangles[lo];
                float u = Mathf.Sqrt((float)random.NextDouble()), v = (float)random.NextDouble();
                result[i] = (1 - u) * t.A + u * (1 - v) * t.B + u * v * t.C;
            }
            return result;

            void AddMesh(Mesh mesh, Matrix4x4 matrix)
            {
                if (triangles.Count >= triangleBudget || mesh.vertexCount > 60000) return;
                // Do not allocate mesh arrays until the input index/vertex budget has passed.
                long indexCount = 0;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetTopology(s) != MeshTopology.Triangles) return;
                    indexCount += mesh.GetIndexCount(s);
                }
                // Keep a distributed subset of actual triangles on larger reviewed offline assets.
                int stride = Mathf.Max(1, Mathf.CeilToInt((float)(indexCount / 3) / (triangleBudget - triangles.Count)));
                var vertices = mesh.vertices;
                var indices = mesh.triangles;
                for (int i = 0; i + 2 < indices.Length && triangles.Count < triangleBudget; i += 3 * stride)
                {
                    Vector3 a = matrix.MultiplyPoint3x4(vertices[indices[i]]), b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]), c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                    float triangleArea = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                    if (!float.IsFinite(triangleArea) || triangleArea < 0.00000001f) continue;
                    area += triangleArea; triangles.Add(new Triangle(a, b, c, area));
                }
            }
        }
    }
}
