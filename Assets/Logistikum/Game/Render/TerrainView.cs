using Logistikum.Sim;
using UnityEngine;
using Grid = Logistikum.Sim.Grid;

namespace Logistikum.Game
{
    /// <summary>
    /// Gelände: Campus 128 × 128 mit Raster, Rand, Umland, Eingangsstraße zur Bundesstraße im Westen, Bäume.
    /// </summary>
    public sealed class TerrainView
    {
        public readonly GameObject Root;

        public TerrainView(Transform parent)
        {
            Root = new GameObject("Gelände");
            Root.transform.SetParent(parent, false);
            BuildGround();
            BuildOutside();
            BuildBorder();
            BuildRoads();
            BuildTrees();
        }

        void BuildGround()
        {
            // Eine Textur über das ganze Gelände: 8 Pixel je Feld, Rasterlinie am Feldrand.
            const int px = 8;
            var tex = new Texture2D(Grid.Width * px, Grid.Depth * px, TextureFormat.RGBA32, true, false)
            { name = "Raster", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var pixels = new Color32[tex.width * tex.height];
            Color32 grass = Palette.Grass, line = Palette.GridLine;
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    pixels[y * tex.width + x] = (x % px == 0 || y % px == 0) ? line : grass;
            tex.SetPixels32(pixels);
            tex.Apply(true);
            var mat = new Material(Palette.Lit(Color.white)) { name = "Boden" };
            mat.SetTexture("_BaseMap", tex);
            var mb = new MeshBuilder();
            mb.Flat(0, -Grid.Depth, Grid.Width, 0, 0);
            var mesh = mb.Build();
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            var (_, mf, mr) = MeshBuilder.Object("Campus", Root.transform, mat);
            mf.sharedMesh = mesh;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildOutside()
        {
            var mb = new MeshBuilder();
            float m = 160;
            mb.Flat(-m, -Grid.Depth - m, 0, m, -0.02f);
            mb.Flat(Grid.Width, -Grid.Depth - m, Grid.Width + m, m, -0.02f);
            mb.Flat(0, 0, Grid.Width, m, -0.02f);
            mb.Flat(0, -Grid.Depth - m, Grid.Width, -Grid.Depth, -0.02f);
            var (_, mf, _) = MeshBuilder.Object("Umland", Root.transform, Palette.Lit(Palette.GrassOutside));
            mf.sharedMesh = mb.Build();
        }

        /// <summary>Niedriger Zaun um den Campus mit Lücke an der Einfahrt (Westrand, Reihe EntranceZ).</summary>
        void BuildBorder()
        {
            var mb = new MeshBuilder();
            float t = 0.12f, h = 0.12f, gapN = -WorldConfig.EntranceZ, gapS = -WorldConfig.EntranceZ - 1;
            mb.Box(new Vector3(Grid.Width, 0, -Grid.Depth - t), new Vector3(Grid.Width + t, h, t));
            mb.Box(new Vector3(-t, 0, 0), new Vector3(Grid.Width + t, h, t));
            mb.Box(new Vector3(-t, 0, -Grid.Depth - t), new Vector3(Grid.Width + t, h, -Grid.Depth));
            mb.Box(new Vector3(-t, 0, gapN), new Vector3(0, h, 0));
            mb.Box(new Vector3(-t, 0, -Grid.Depth), new Vector3(0, h, gapS));
            var (_, mf, _) = MeshBuilder.Object("Zaun", Root.transform, Palette.Lit(Palette.Hex(0x8c96a0)));
            mf.sharedMesh = mb.Build();
        }

        /// <summary>X der Bundesstraße (Nord–Süd) westlich des Campus.</summary>
        public const float HighwayX = -WorldConfig.EntranceRoadLength - 1.5f;

        void BuildRoads()
        {
            var mb = new MeshBuilder(2);
            float z0 = -(WorldConfig.EntranceZ + 1), z1 = -WorldConfig.EntranceZ;
            mb.Flat(HighwayX + 1.5f, z0, 0, z1, 0.005f, 0);
            for (float x = HighwayX + 2f; x < -0.5f; x += 1f) mb.Flat(x, z0 + 0.47f, x + 0.5f, z0 + 0.53f, 0.012f, 1);
            mb.Flat(HighwayX - 1.5f, -Grid.Depth - 150, HighwayX + 1.5f, 150, 0.004f, 0);
            for (float z = -Grid.Depth - 150; z < 150; z += 2f) mb.Flat(HighwayX - 0.04f, z, HighwayX + 0.04f, z + 1f, 0.011f, 1);
            var (go, mf, mr) = MeshBuilder.Object("Eingangsstraße", Root.transform, Palette.Lit(Palette.Asphalt), Palette.Lit(Palette.Marking));
            mf.sharedMesh = mb.Build();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildTrees()
        {
            var rng = new System.Random(7);
            var trees = new GameObject("Bäume");
            trees.transform.SetParent(Root.transform, false);
            for (int i = 0; i < 260; i++)
            {
                float x, z;
                int side = rng.Next(4);
                if (side == 0) { x = (float)(HighwayX - 3 - rng.NextDouble() * 60); z = (float)(-rng.NextDouble() * 220 + 40); }
                else if (side == 1) { x = (float)(Grid.Width + 2 + rng.NextDouble() * 70); z = (float)(-rng.NextDouble() * 220 + 40); }
                else if (side == 2) { x = (float)(HighwayX + 2 + rng.NextDouble() * (Grid.Width - HighwayX + 60)); z = (float)(2 + rng.NextDouble() * 50); }
                else { x = (float)(HighwayX + 2 + rng.NextDouble() * (Grid.Width - HighwayX + 60)); z = (float)(-Grid.Depth - 2 - rng.NextDouble() * 50); }
                if (x > HighwayX + 1 && x < 0 && Mathf.Abs(z + WorldConfig.EntranceZ + 0.5f) < 3) continue;
                var t = ModelLibrary.Spawn(rng.Next(3) == 0 ? "Pine" : "Tree", trees.transform);
                float s = (float)(2.2 + rng.NextDouble() * 2.5);
                t.Root.transform.localPosition = new Vector3(x, 0, z);
                t.Root.transform.localScale = Vector3.one * s;
                t.Root.transform.localRotation = Quaternion.Euler(0, (float)(rng.NextDouble() * 360), 0);
            }
        }
    }
}
