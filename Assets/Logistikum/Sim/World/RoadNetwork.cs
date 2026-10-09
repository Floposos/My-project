using System.Collections.Generic;

namespace Logistikum.Sim
{
    public enum RoadShape { single, end, straight, curve, tee, cross }

    /// <summary>Straßennetz, aus dem Zustand abgeleitet (wird nie gespeichert).</summary>
    public sealed class RoadNetwork
    {
        /// <summary>Die Einfahrt: virtuelles Straßenfeld direkt westlich vor dem Campus.</summary>
        public static readonly Cell Entrance = new Cell(-1, WorldConfig.EntranceZ);

        /// <summary>Richtungen: 0 Nord (−z), 1 Ost (+x), 2 Süd (+z), 3 West (−x); Bits 1, 2, 4, 8.</summary>
        public static readonly int[] Dx = { 0, 1, 0, -1 };
        public static readonly int[] Dz = { -1, 0, 1, 0 };

        /// <summary>Schlüssel eines Felds; erlaubt den Rand −1 … Breite für die Einfahrt.</summary>
        public static int CellKey(int x, int z) => (z + 1) * (Grid.Width + 2) + (x + 1);
        public static int CellKey(Cell c) => CellKey(c.X, c.Z);

        readonly HashSet<int> cells = new HashSet<int>();

        public RoadNetwork(GameState state) : this(state.Roads) { }

        public RoadNetwork(IEnumerable<RoadTile> roads)
        {
            foreach (var r in roads) cells.Add(CellKey(r.X, r.Z));
            cells.Add(CellKey(Entrance));
        }

        public bool Has(int x, int z)
        {
            if (x < -1 || z < -1 || x > Grid.Width || z > Grid.Depth) return false;
            return cells.Contains(CellKey(x, z));
        }

        public bool Has(Cell c) => Has(c.X, c.Z);

        /// <summary>Bitmaske der Nachbarn mit Straße (Form: gerade, Kurve, T-Stück, Kreuzung).</summary>
        public int Connections(int x, int z)
        {
            int mask = 0;
            for (int d = 0; d < 4; d++) if (Has(x + Dx[d], z + Dz[d])) mask |= 1 << d;
            return mask;
        }

        public static int BitCount(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1) + ((mask >> 3) & 1);

        public static RoadShape ShapeOf(int mask)
        {
            int count = BitCount(mask);
            if (count == 0) return RoadShape.single;
            if (count == 1) return RoadShape.end;
            if (count == 3) return RoadShape.tee;
            if (count == 4) return RoadShape.cross;
            return mask == 5 || mask == 10 ? RoadShape.straight : RoadShape.curve;
        }
    }
}
