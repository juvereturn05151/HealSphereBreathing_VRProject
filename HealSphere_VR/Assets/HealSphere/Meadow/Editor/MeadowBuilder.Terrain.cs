using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HealSphere.Meadow.Editor
{
    public static partial class MeadowBuilder
    {
        private static void CreateTerrain(Transform parent)
        {
            terrainData = new TerrainData { name = "Editable Meadow Terrain", heightmapResolution = 257,
                alphamapResolution = 256, baseMapResolution = 512, size = new Vector3(recipe.TerrainWidth, 64, recipe.TerrainWidth) };
            var heights = new float[257, 257];
            for (int z = 0; z < 257; z++) for (int x = 0; x < 257; x++)
            {
                float px = (x / 256f - .5f) * recipe.TerrainWidth;
                float pz = (z / 256f - .5f) * recipe.TerrainWidth;
                heights[z, x] = (Height(px, pz) + 2) / 64;
            }
            terrainData.SetHeights(0, 0, heights);
            var layers = new TerrainLayer[3];
            for (int layer = 0; layer < layers.Length; layer++)
            {
                var texture = new Texture2D(256, 256, TextureFormat.RGB24, true) { name = "Meadow Ground " + layer, wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color[256 * 256];
                Color tint = recipe.MeadowGreen * new[] { .93f, 1.06f, 1f }[layer];
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    float noise = R(.89f, 1.1f);
                    float fleck = Mathf.PerlinNoise(x * .17f, y * .17f);
                    Color c = tint * (noise + .1f * fleck);
                    if (random.NextDouble() < .035) c = Color.Lerp(c, new Color(.68f, .74f, .34f), .35f);
                    c.a = 1; pixels[y * 256 + x] = c;
                }
                texture.SetPixels(pixels); texture.Apply();
                string path = output + "/Textures/Ground" + layer + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Repeat; importer.maxTextureSize = 256;
                importer.SaveAndReimport();
                layers[layer] = new TerrainLayer { name = "Soft Grass " + layer,
                    diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path), tileSize = new Vector2(5, 5), metallic = 0, smoothness = 0 };
                AssetDatabase.CreateAsset(layers[layer], output + "/Terrain/Grass" + layer + ".terrainlayer");
            }
            terrainData.terrainLayers = layers;
            var weights = new float[256, 256, 3];
            for (int z = 0; z < 256; z++) for (int x = 0; x < 256; x++)
            {
                float t = Mathf.PerlinNoise(x * .043f + 27, z * .043f + 15);
                weights[z, x, 0] = Mathf.Clamp01((.65f - t) * 1.8f);
                weights[z, x, 1] = Mathf.Clamp01((t - .35f) * 1.8f);
                weights[z, x, 2] = 1 - weights[z, x, 0] - weights[z, x, 1];
            }
            terrainData.SetAlphamaps(0, 0, weights);
            AssetDatabase.CreateAsset(terrainData, output + "/Terrain/MeadowTerrain.asset");
            var terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "MeadowTerrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.localPosition = new Vector3(-recipe.TerrainWidth * .5f, -2, -recipe.TerrainWidth * .5f);
            var terrain = terrainObject.GetComponent<Terrain>();
            var material = new Material(RequiredShader("Universal Render Pipeline/Terrain/Lit")) { name = "Meadow Terrain" };
            AssetDatabase.CreateAsset(material, output + "/Materials/Meadow Terrain.mat");
            terrain.materialTemplate = material; terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8; terrain.basemapDistance = 100;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.detailObjectDistance = 0; terrain.treeDistance = 0;
            GameObjectUtility.SetStaticEditorFlags(terrainObject, StaticEditorFlags.BatchingStatic);
        }

        private static void CreateHorizon(Transform parent)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                var draft = new MeshDraft();
                draft.Ellipsoid(Vector3.zero, new Vector3(R(85, 125), R(12, 22), R(70, 105)),
                    Color.Lerp(recipe.MeadowGreen, new Color(.64f, .75f, .58f), .35f), 10, 24);
                var go = MeshObject("HorizonHill_" + i, parent, draft.Save("HorizonHill_" + i));
                go.transform.localPosition = new Vector3(Mathf.Sin(a) * 280, -2, Mathf.Cos(a) * 280);
                go.transform.localRotation = Quaternion.Euler(0, i * 30, 0);
            }
        }
    }
}
