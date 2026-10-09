using System;
using System.Collections.Generic;

namespace Logistikum.Sim
{
    public static class Grid
    {
        public const int Width = WorldConfig.CampusWidth;
        public const int Depth = WorldConfig.CampusDepth;

        public static bool IsInsideCampus(Footprint f) =>
            f.X >= 0 && f.Z >= 0 && f.X + f.Width <= Width && f.Z + f.Depth <= Depth;

        public static bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;

        public static int CellIndex(int x, int z) => z * Width + x;

        /// <summary>Berührt die Fläche den Geländerand?</summary>
        public static bool TouchesEdge(Footprint f) =>
            f.X == 0 || f.Z == 0 || f.X + f.Width == Width || f.Z + f.Depth == Depth;

        /// <summary>Rechteck zwischen zwei Eckfeldern (beide enthalten), egal in welche Richtung gezogen.</summary>
        public static Footprint RectBetween(Cell a, Cell b) =>
            new Footprint(Math.Min(a.X, b.X), Math.Min(a.Z, b.Z), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Z - b.Z) + 1);

        public static IEnumerable<Cell> Cells(Footprint f)
        {
            for (int z = f.Z; z < f.Z + f.Depth; z++)
                for (int x = f.X; x < f.X + f.Width; x++)
                    yield return new Cell(x, z);
        }
    }

    /// <summary>
    /// Belegung des Rasters: je Feld die ID des Objekts darauf, RoadCell, ConveyorCell oder 0.
    /// Wird aus dem Zustand abgeleitet (nicht gespeichert), damit sie nie abweicht.
    /// </summary>
    public sealed class Occupancy
    {
        public const int RoadCell = -1;
        public const int ConveyorCell = -2;
        readonly int[] cells = new int[Grid.Width * Grid.Depth];

        public Occupancy(GameState state)
        {
            foreach (var b in state.Buildings) Fill(Buildings.FootprintOf(b), b.Id);
            foreach (var zone in state.Zones) foreach (var p in zone.Parts) Fill(p, zone.Id);
            foreach (var h in state.Halls) Fill(h.Rect, h.Id);
            foreach (var c in state.Conveyors) foreach (var cell in c.Cells) Set(cell.X, cell.Z, ConveyorCell);
            foreach (var r in state.Roads) Set(r.X, r.Z, RoadCell);
        }

        void Fill(Footprint f, int id)
        {
            for (int z = f.Z; z < f.Z + f.Depth; z++)
                for (int x = f.X; x < f.X + f.Width; x++) Set(x, z, id);
        }

        void Set(int x, int z, int v) { if (Grid.InBounds(x, z)) cells[Grid.CellIndex(x, z)] = v; }

        /// <summary>ID des Objekts auf einem Feld, RoadCell, ConveyorCell oder 0 (auch außerhalb).</summary>
        public int At(int x, int z) => Grid.InBounds(x, z) ? cells[Grid.CellIndex(x, z)] : 0;

        public bool IsFree(Footprint f)
        {
            for (int z = f.Z; z < f.Z + f.Depth; z++)
                for (int x = f.X; x < f.X + f.Width; x++)
                    if (At(x, z) != 0) return false;
            return true;
        }
    }

    public static class Buildings
    {
        public static Footprint FootprintOf(Building b)
        {
            var t = BuildingTypes.Get(b.Type);
            return new Footprint(b.X, b.Z, t.Width, t.Depth);
        }
    }
}
