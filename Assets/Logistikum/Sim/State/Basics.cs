using System;

namespace Logistikum.Sim
{
    /// <summary>Ein Feld auf dem Raster.</summary>
    public struct Cell : IEquatable<Cell>
    {
        public int X, Z;
        public Cell(int x, int z) { X = x; Z = z; }
        public bool Equals(Cell o) => X == o.X && Z == o.Z;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => X * 73856093 ^ Z * 19349663;
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public override string ToString() => "(" + X + ", " + Z + ")";
    }

    /// <summary>Ort in Feldkoordinaten (für schwebende Beträge und Meldungen).</summary>
    public struct Place
    {
        public double X, Z;
        public Place(double x, double z) { X = x; Z = z; }
    }

    /// <summary>Rechteck auf dem Raster in Feldern: linke obere Ecke (x, z), Breite, Tiefe.</summary>
    public class Footprint
    {
        public int X, Z, Width, Depth;
        public Footprint() { }
        public Footprint(int x, int z, int width, int depth) { X = x; Z = z; Width = width; Depth = depth; }
        [Newtonsoft.Json.JsonIgnore] public int Area => Width * Depth;
        public bool Contains(int x, int z) => x >= X && x < X + Width && z >= Z && z < Z + Depth;
        public Footprint Copy() => new Footprint(X, Z, Width, Depth);
    }

    /// <summary>Seite einer Fläche: Nord (−z), Ost (+x), Süd (+z), West (−x).</summary>
    public enum Side { N, E, S, W }
}
