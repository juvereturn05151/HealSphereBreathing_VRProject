using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HealSphere.Meadow.Editor
{
    public static partial class MeadowBuilder
    {
        private static void CreateLighting(Transform root, Vector3 player, Quaternion forward)
        {
            var sun = Node("DirectionalLight", root).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1, .96f, .86f); sun.intensity = 1.05f;
            sun.transform.rotation = forward * Quaternion.Euler(48, -32, 0);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .35f;
            sun.shadowBias = .05f; sun.shadowNormalBias = .5f;
            Node("EnvironmentLighting (Scene Render Settings)", root);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.70f, .80f, .88f);
            RenderSettings.ambientEquatorColor = new Color(.65f, .74f, .59f);
            RenderSettings.ambientGroundColor = new Color(.38f, .48f, .29f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.75f, .84f, .80f);
            RenderSettings.fogStartDistance = 180; RenderSettings.fogEndDistance = 365;
            RenderSettings.reflectionIntensity = .3f;
            RenderSettings.skybox = CreateSky();
            // Ambient trilight covers dynamic future objects; no unused unbaked light probe group.
        }
        private static Material CreateSky()
        {
            const int resolution = 512;
            var cubemap = new Cubemap(resolution, TextureFormat.RGB24, true) { name = "Pastel Cloud Sky", wrapMode = TextureWrapMode.Clamp };
            var lobes = new List<Vector4>();
            for (int group = 0; group < 19; group++)
            {
                float a = group * 2.39996f, elevation = R(.16f, .95f);
                Vector3 center = new Vector3(Mathf.Cos(a) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(a) * Mathf.Cos(elevation));
                for (int k = 0; k < 7; k++)
                {
                    Vector3 direction = (center + new Vector3(R(-.18f, .18f), R(-.065f, .065f), R(-.18f, .18f))).normalized;
                    lobes.Add(new Vector4(direction.x, direction.y, direction.z, R(.065f, .14f)));
                }
            }
            for (int face = 0; face < 6; face++)
            {
                var pixels = new Color[resolution * resolution];
                for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
                {
                    float u = 2 * (x + .5f) / resolution - 1, v = 2 * (y + .5f) / resolution - 1;
                    Vector3 d;
                    switch (face)
                    {
                        case 0: d = new Vector3(1, -v, -u); break;
                        case 1: d = new Vector3(-1, -v, u); break;
                        case 2: d = new Vector3(u, 1, v); break;
                        case 3: d = new Vector3(u, -1, -v); break;
                        case 4: d = new Vector3(u, -v, 1); break;
                        default: d = new Vector3(-u, -v, -1); break;
                    }
                    d.Normalize();
                    Color sky = Color.Lerp(new Color(.84f, .9f, .9f), recipe.SkyBlue, Mathf.Pow(Mathf.Clamp01(d.y), .45f));
                    float density = 0, shade = 0;
                    if (d.y > .035f)
                    {
                        foreach (var lobe in lobes)
                        {
                            float dot = d.x * lobe.x + d.y * lobe.y + d.z * lobe.z;
                            float edge = (dot - (1 - lobe.w * lobe.w * 1.7f)) / (lobe.w * lobe.w * 1.7f);
                            if (edge > 0)
                            {
                                float cloud = Mathf.SmoothStep(0, 1, edge);
                                density = Mathf.Max(density, cloud);
                                shade = Mathf.Max(shade, cloud * Mathf.Clamp01((d.y - lobe.y) * 7 + .55f));
                            }
                        }
                    }
                    Color cloudColor = Color.Lerp(new Color(.80f, .85f, .85f), recipe.CloudWhite, .5f + .5f * shade);
                    pixels[y * resolution + x] = Color.Lerp(sky, cloudColor, Mathf.SmoothStep(0, .85f, density)).linear;
                }
                cubemap.SetPixels(pixels, (CubemapFace)face);
            }
            cubemap.Apply();
            AssetDatabase.CreateAsset(cubemap, output + "/Textures/PastelCloudSky.asset");
            var material = new Material(RequiredShader("Skybox/Cubemap")) { name = "Pastel Meadow Sky" };
            material.SetTexture("_Tex", cubemap); material.SetColor("_Tint", new Color(.5f, .5f, .5f)); material.SetFloat("_Exposure", 1);
            AssetDatabase.CreateAsset(material, output + "/Materials/Pastel Meadow Sky.mat");
            return material;
        }
    }
}
