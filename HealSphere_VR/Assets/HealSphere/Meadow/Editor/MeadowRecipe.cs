using UnityEditor;
using UnityEngine;

namespace HealSphere.Meadow.Editor
{
    [CreateAssetMenu(menuName = "HealSphere/Meadow Recipe")]
    public sealed class MeadowRecipe : ScriptableObject
    {
        [Header("Preserved Scene")]
        [SerializeField] private SceneAsset sourceScene;
        [Header("Deterministic Landscape")]
        [SerializeField] private int seed = 23819;
        [SerializeField, Range(512, 1024)] private int terrainWidth = 768;
        [SerializeField, Range(4f, 24f)] private float hillHeight = 16f;
        [SerializeField, Range(30, 110)] private int tuftsPerTile = 75;
        [SerializeField, Range(300, 1800)] private int flowerCount = 1200;
        [SerializeField, Range(8, 36)] private int treeCount = 22;
        [Header("Palette")]
        [SerializeField] private Color meadowGreen = new Color(0.44f, 0.65f, 0.27f);
        [SerializeField] private Color skyBlue = new Color(0.54f, 0.76f, 0.89f);
        [SerializeField] private Color cloudWhite = new Color(0.99f, 0.985f, 0.94f);

        public SceneAsset SourceScene => sourceScene;
        public int Seed => seed;
        public int TerrainWidth => terrainWidth;
        public float HillHeight => hillHeight;
        public int TuftsPerTile => tuftsPerTile;
        public int FlowerCount => flowerCount;
        public int TreeCount => treeCount;
        public Color MeadowGreen => meadowGreen;
        public Color SkyBlue => skyBlue;
        public Color CloudWhite => cloudWhite;
    }
}
