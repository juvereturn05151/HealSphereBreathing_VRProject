using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HealSphere.Meadow.Editor
{
    public static partial class MeadowBuilder
    {
        private static void CreateReusablePlants()
        {
            var tuft = new MeshDraft();
            for (int i = 0; i < 7; i++) tuft.Grass(new Vector3(R(-.13f, .13f), 0, R(-.13f, .13f)), R(.09f, .2f), R(0, 6.28f), Green(R(.3f, .85f)));
            SavePlant("GrassTuft", tuft);
            Color[] palette = { new Color(.99f, .98f, .89f), new Color(1, .84f, .23f), new Color(.91f, .30f, .24f), new Color(.97f, .68f, .76f) };
            string[] names = { "FlowerWhite", "FlowerYellow", "FlowerRed", "FlowerPink" };
            for (int i = 0; i < names.Length; i++)
            {
                var flower = new MeshDraft(); flower.Flower(Vector3.zero, palette[i], .07f, 0);
                SavePlant(names[i], flower);
            }
            for (int type = 0; type < 3; type++)
            {
                var tree = Node("Tree" + (type + 1));
                var renderers = new Renderer[2];
                for (int lod = 0; lod < 2; lod++)
                {
                    var mesh = new MeshDraft();
                    mesh.Ellipsoid(new Vector3(0, 1.6f, 0), new Vector3(.18f, 1.6f, .16f), new Color(.43f, .32f, .19f), 4, 7);
                    for (int crown = 0; crown < (lod == 0 ? 5 : 2); crown++)
                    {
                        float a = crown * 2.4f + type;
                        float h = 3.6f + type * .4f + (crown == 0 ? .7f : 0);
                        mesh.Ellipsoid(new Vector3(Mathf.Cos(a) * .65f, h, Mathf.Sin(a) * .6f),
                            new Vector3(1.25f, 1.6f + type * .2f, 1.15f), Green(.36f + crown * .07f), lod == 0 ? 7 : 5, lod == 0 ? 10 : 7);
                    }
                    var child = MeshObject("CrownAndTrunk_LOD" + lod, tree, mesh.Save(tree.name + "_LOD" + lod));
                    renderers[lod] = child.GetComponent<Renderer>();
                }
                var group = tree.gameObject.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(.075f, new[] { renderers[0] }), new LOD(.008f, new[] { renderers[1] }) });
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(tree.gameObject, output + "/Prefabs/" + tree.name + ".prefab");
                Object.DestroyImmediate(tree.gameObject);
            }
        }
        private static void CreateGrass(Transform parent)
        {
            for (int z = -6; z < 6; z++) for (int x = -6; x < 6; x++)
            {
                Vector3 center = new Vector3(x * 10 + 5, 0, z * 10 + 5);
                if (center.magnitude > 63) continue;
                var draft = new MeshDraft();
                int count = Mathf.RoundToInt(recipe.TuftsPerTile * Mathf.Lerp(1.6f, .45f, center.magnitude / 65));
                for (int k = 0; k < count; k++)
                {
                    float px = center.x + R(-5, 5), pz = center.z + R(-5, 5);
                    float distance = new Vector2(px, pz).magnitude;
                    if (distance < 1.3f) continue;
                    draft.Grass(new Vector3(px - center.x, Surface(px, pz) - .008f, pz - center.z),
                        R(.08f, .23f) * (distance < 3 ? .5f : 1), R(0, 6.28f), Green(R(.28f, .87f)));
                }
                var go = MeshObject("GrassTile_" + x + "_" + z, parent, draft.Save("GrassTile_" + x + "_" + z));
                go.transform.localPosition = center;
                var renderer = go.GetComponent<Renderer>();
                var lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.025f, new[] { renderer }) }); lod.RecalculateBounds();
            }
        }
        private static void CreateFlowers(Transform parent)
        {
            var buckets = new Dictionary<Vector2Int, MeshDraft>();
            Color[] colors = { new Color(.99f, .98f, .89f), new Color(1, .84f, .23f), new Color(.91f, .30f, .24f), new Color(.97f, .68f, .76f) };
            int created = 0;
            while (created < recipe.FlowerCount)
            {
                float angle = R(0, Mathf.PI * 2), radius = R(5, 55);
                Vector2 cluster = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                int size = random.Next(9, 23);
                for (int j = 0; j < size && created < recipe.FlowerCount; j++)
                {
                    float px = cluster.x + R(-2.2f, 2.2f), pz = cluster.y + R(-2.2f, 2.2f);
                    if (new Vector2(px, pz).magnitude < 3.5f) continue;
                    var key = new Vector2Int(Mathf.FloorToInt(px / 12), Mathf.FloorToInt(pz / 12));
                    if (!buckets.TryGetValue(key, out var draft)) buckets[key] = draft = new MeshDraft();
                    float choice = R(0, 1);
                    int color = choice < .48f ? 0 : choice < .8f ? 1 : choice < .96f ? 2 : 3;
                    draft.Flower(new Vector3(px - key.x * 12, Surface(px, pz), pz - key.y * 12), colors[color], R(.045f, .085f), R(0, 6.28f));
                    created++;
                }
            }
            foreach (var pair in buckets)
            {
                string name = "FlowerCluster_" + pair.Key.x + "_" + pair.Key.y;
                var go = MeshObject(name, parent, pair.Value.Save(name));
                go.transform.localPosition = new Vector3(pair.Key.x * 12, 0, pair.Key.y * 12);
                var lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.018f, new[] { go.GetComponent<Renderer>() }) }); lod.RecalculateBounds();
            }
        }
        private static void CreateTrees(Transform parent)
        {
            for (int i = 0; i < recipe.TreeCount; i++)
            {
                float a = i * 2 * Mathf.PI / recipe.TreeCount + R(-.08f, .08f);
                float radius = R(75, 165);
                if (Mathf.Abs(Mathf.Sin(a)) < .5f && Mathf.Cos(a) > 0) radius += 70;
                float x = Mathf.Sin(a) * radius, z = Mathf.Cos(a) * radius;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(output + "/Prefabs/Tree" + (i % 3 + 1) + ".prefab");
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                tree.name = "MeadowTree_" + (i + 1).ToString("00");
                tree.transform.localPosition = new Vector3(x, Surface(x, z) - .05f, z);
                tree.transform.localRotation = Quaternion.Euler(0, R(0, 360), 0);
                tree.transform.localScale = Vector3.one * R(.9f, 1.6f);
            }
        }
    }
}
