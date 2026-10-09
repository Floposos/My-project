using System;
using System.Collections.Generic;

namespace Logistikum.Sim
{
    public static class RoadLine
    {
        /// <summary>
        /// Felder einer gezogenen Linie von from nach to: gerade oder mit einem Knick (L-Form).
        /// xFirst = erst entlang x, dann entlang z. Start und Ziel enthalten, keine Doppelten.
        /// </summary>
        public static List<Cell> Line(Cell from, Cell to, bool xFirst)
        {
            var corner = xFirst ? new Cell(to.X, from.Z) : new Cell(from.X, to.Z);
            var cells = Segment(from, corner);
            var rest = Segment(corner, to);
            for (int i = 1; i < rest.Count; i++) cells.Add(rest[i]);
            return cells;
        }

        static List<Cell> Segment(Cell a, Cell b)
        {
            var cells = new List<Cell>();
            int dx = Math.Sign(b.X - a.X), dz = Math.Sign(b.Z - a.Z);
            int steps = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Z - a.Z));
            for (int i = 0; i <= steps; i++) cells.Add(new Cell(a.X + dx * i, a.Z + dz * i));
            return cells;
        }

        /// <summary>Knick-Richtung beim Ziehen: zuerst entlang der Achse mit dem größeren Abstand.</summary>
        public static bool PrefersXFirst(Cell from, Cell to) => Math.Abs(to.X - from.X) >= Math.Abs(to.Z - from.Z);
    }
}
