using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>
    /// Straßen als ein Mesh: Asphalt je Feld, Mittellinie gestrichelt zu jedem Nachbarn (Kurven, T-Stücke,
    /// Kreuzungen ergeben sich), Vorfahrtsstraßen mit gelben Randlinien. Wird bei Änderungen neu gebaut.
    /// </summary>
    public sealed class RoadsView
    {
        readonly MeshFilter mf;
        readonly Mesh mesh = new Mesh { name = "Straßen" };

        public RoadsView(Transform parent)
        {
            var (_, f, r) = MeshBuilder.Object("Straßen", parent,
                Palette.Lit(Palette.Asphalt, 0.1f), Palette.Lit(Palette.Marking), Palette.Lit(Palette.PriorityMarking));
            mf = f;
            mf.sharedMesh = mesh;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Rebuild(GameState state)
        {
            var net = new RoadNetwork(state);
            var mb = new MeshBuilder(3);
            const float y = 0.012f, my = 0.018f, w = 0.035f;
            foreach (var r in state.Roads)
            {
                float x0 = r.X, z0 = -(r.Z + 1), x1 = r.X + 1, z1 = -r.Z;
                mb.Flat(x0, z0, x1, z1, y, 0);
                int mask = net.Connections(r.X, r.Z);
                int count = RoadNetwork.BitCount(mask);
                float cx = r.X + 0.5f, cz = -(r.Z + 0.5f);
                if (count <= 2)
                {
                    // Mittellinie: vom Mittelpunkt zu jedem angeschlossenen Rand (zwei Striche je Halbfeld).
                    for (int d = 0; d < 4; d++)
                    {
                        if ((mask & (1 << d)) == 0) continue;
                        float dx = RoadNetwork.Dx[d], dz = -RoadNetwork.Dz[d];
                        float a = 0.08f, b = 0.30f;
                        float ax = cx + dx * a, az = cz + dz * a, bx = cx + dx * b, bz = cz + dz * b;
                        mb.Flat(Mathf.Min(ax, bx) - (dx == 0 ? w : 0), Mathf.Min(az, bz) - (dz == 0 ? w : 0),
                            Mathf.Max(ax, bx) + (dx == 0 ? w : 0), Mathf.Max(az, bz) + (dz == 0 ? w : 0), my, 1);
                    }
                }
                if (r.Priority)
                {
                    // Gelbe Randlinien auf den Seiten ohne Anschluss.
                    float e = 0.06f, ins = 0.04f;
                    if ((mask & 1) == 0) mb.Flat(x0, z1 - ins - e, x1, z1 - ins, my, 2);
                    if ((mask & 4) == 0) mb.Flat(x0, z0 + ins, x1, z0 + ins + e, my, 2);
                    if ((mask & 8) == 0) mb.Flat(x0 + ins, z0, x0 + ins + e, z1, my, 2);
                    if ((mask & 2) == 0) mb.Flat(x1 - ins - e, z0, x1 - ins, z1, my, 2);
                }
            }
            mb.Build(mesh);
        }
    }
}
