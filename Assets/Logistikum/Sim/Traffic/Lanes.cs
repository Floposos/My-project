namespace Logistikum.Sim
{
    /// <summary>Fahrtrichtung: 0 Nord (−z), 1 Ost (+x), 2 Süd (+z), 3 West (−x).</summary>
    public static class Lanes
    {
        /// <summary>Richtung von a zum Nachbarfeld b; bei nicht benachbarten Feldern Ost.</summary>
        public static int HeadingBetween(Cell a, Cell b)
        {
            for (int d = 0; d < 4; d++)
                if (RoadNetwork.Dx[d] == b.X - a.X && RoadNetwork.Dz[d] == b.Z - a.Z) return d;
            return 1;
        }

        /// <summary>Fahrspur: Feld plus Fahrtrichtung. Gegenverkehr stört sich nicht.</summary>
        public static int LaneKey(Cell c, int heading) => RoadNetwork.CellKey(c) * 4 + heading;

        /// <summary>Richtung, in der ein Fahrzeug fährt, das von rechts kommt (rechts vor links).</summary>
        public static int FromRight(int heading) => (heading + 3) % 4;
    }
}
