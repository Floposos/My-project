using System.Collections.Generic;

namespace Logistikum.Sim
{
    public enum SiteKind { A, B, C, W, export, hall }

    /// <summary>Ein Ort, den LKW anfahren: Zone, Export-Ausfahrt oder Halle (M3).</summary>
    public sealed class Site
    {
        public int Id;
        public SiteKind Kind;
        public List<Footprint> Parts;
        public Side Gate;

        public bool IsZone => Kind == SiteKind.A || Kind == SiteKind.B || Kind == SiteKind.C || Kind == SiteKind.W;
    }

    public static class Sites
    {
        public static SiteKind KindOf(ZoneKind k) => (SiteKind)(int)k;

        /// <summary>Tor der Export-Ausfahrt: die Seite zum Gelände hin.</summary>
        public static Side ExitGate(Footprint f)
        {
            if (f.X == 0) return Side.E;
            if (f.X + f.Width == Grid.Width) return Side.W;
            return f.Z == 0 ? Side.S : Side.N;
        }

        /// <summary>Alle anfahrbaren Orte in fester Reihenfolge (Zonen, Ausfahrten, Hallen).</summary>
        public static List<Site> All(GameState state)
        {
            var list = new List<Site>();
            foreach (var z in state.Zones)
            {
                var parts = new List<Footprint>();
                foreach (var p in z.Parts) parts.Add(p.Copy());
                list.Add(new Site { Id = z.Id, Kind = KindOf(z.Kind), Parts = parts, Gate = z.Gate });
            }
            foreach (var b in state.Buildings)
            {
                if (!BuildingTypes.Get(b.Type).NeedsAccess) continue;
                var f = Buildings.FootprintOf(b);
                list.Add(new Site { Id = b.Id, Kind = SiteKind.export, Parts = new List<Footprint> { f }, Gate = ExitGate(f) });
            }
            foreach (var h in state.Halls)
                list.Add(new Site { Id = h.Id, Kind = SiteKind.hall, Parts = new List<Footprint> { h.Rect }, Gate = h.Gate });
            return list;
        }

        public static Site ById(GameState state, int id) => All(state).Find(s => s.Id == id);

        public static Cell? AccessOf(RoadNetwork network, Site site) => Access.AccessCell(network, site.Parts, site.Gate);

        /// <summary>Zufahrt eines Orts; null = Ort fehlt oder ohne Straße.</summary>
        public static Cell? AccessOf(GameState state, RoadNetwork network, int siteId)
        {
            var site = ById(state, siteId);
            return site == null ? (Cell?)null : AccessOf(network, site);
        }
    }
}
