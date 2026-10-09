using System;
using System.Collections.Generic;
using System.Linq;

namespace Logistikum.Sim
{
    /// <summary>Fläche aus einem oder mehreren Rechtecken (verschmolzene Zone). Teile überlappen nie.</summary>
    public static class ZoneShape
    {
        public static int Area(IEnumerable<Footprint> parts) => parts.Sum(p => p.Width * p.Depth);

        public static bool Contains(IEnumerable<Footprint> parts, int x, int z) => parts.Any(p => p.Contains(x, z));

        /// <summary>Umgebendes Rechteck.</summary>
        public static Footprint Bounds(IEnumerable<Footprint> shape)
        {
            var parts = shape.ToList();
            int x = parts.Min(p => p.X), z = parts.Min(p => p.Z);
            return new Footprint(x, z, parts.Max(p => p.X + p.Width) - x, parts.Max(p => p.Z + p.Depth) - z);
        }

        /// <summary>Mitte der Fläche (Mittelpunkt des größten Teils, liegt sicher auf der Fläche).</summary>
        public static Place Center(IEnumerable<Footprint> shape)
        {
            Footprint largest = null;
            foreach (var p in shape) if (largest == null || p.Area > largest.Area) largest = p;
            return new Place(largest.X + largest.Width / 2.0, largest.Z + largest.Depth / 2.0);
        }

        /// <summary>Teilen zwei Rechtecke eine Kante (nicht nur eine Ecke)?</summary>
        public static bool SharesEdge(Footprint a, Footprint b)
        {
            bool overlapX = a.X < b.X + b.Width && b.X < a.X + a.Width;
            bool overlapZ = a.Z < b.Z + b.Depth && b.Z < a.Z + a.Depth;
            bool touchX = a.X + a.Width == b.X || b.X + b.Width == a.X;
            bool touchZ = a.Z + a.Depth == b.Z || b.Z + b.Depth == a.Z;
            return (touchX && overlapZ) || (touchZ && overlapX);
        }

        public static bool Touches(IEnumerable<Footprint> shape, Footprint rect) => shape.Any(p => SharesEdge(p, rect));
    }

    public static class Access
    {
        public static readonly Side[] Sides = { Side.N, Side.E, Side.S, Side.W };

        static IEnumerable<Cell> BesideRect(Footprint f, Side side)
        {
            if (side == Side.N || side == Side.S)
            {
                int z = side == Side.N ? f.Z - 1 : f.Z + f.Depth;
                for (int x = f.X; x < f.X + f.Width; x++) yield return new Cell(x, z);
            }
            else
            {
                int x = side == Side.W ? f.X - 1 : f.X + f.Width;
                for (int z = f.Z; z < f.Z + f.Depth; z++) yield return new Cell(x, z);
            }
        }

        /// <summary>Felder direkt vor einer Seite (außerhalb der Fläche), von der Mitte nach außen sortiert.</summary>
        public static List<Cell> CellsBeside(IReadOnlyList<Footprint> shape, Side side)
        {
            var cells = shape.SelectMany(p => BesideRect(p, side)).Where(c => !ZoneShape.Contains(shape, c.X, c.Z)).ToList();
            var b = ZoneShape.Bounds(shape);
            bool horizontal = side == Side.N || side == Side.S;
            double mid = horizontal ? b.X + (b.Width - 1) / 2.0 : b.Z + (b.Depth - 1) / 2.0;
            return cells.Select((c, i) => (c, d: Math.Abs((horizontal ? c.X : c.Z) - mid), i))
                .OrderBy(e => e.d).ThenBy(e => e.i).Select(e => e.c).ToList();
        }

        /// <summary>Zufahrt: das Straßenfeld vor der Tor-Seite; null = nicht angeschlossen.</summary>
        public static Cell? AccessCell(RoadNetwork network, IReadOnlyList<Footprint> shape, Side gate)
        {
            foreach (var c in CellsBeside(shape, gate)) if (network.Has(c)) return c;
            return null;
        }

        /// <summary>Vorschlag für die Tor-Seite: die erste Seite mit angrenzender Straße, sonst Süden.</summary>
        public static Side SuggestGate(RoadNetwork network, IReadOnlyList<Footprint> shape)
        {
            foreach (var s in Sides) if (AccessCell(network, shape, s).HasValue) return s;
            return Side.S;
        }
    }
}
