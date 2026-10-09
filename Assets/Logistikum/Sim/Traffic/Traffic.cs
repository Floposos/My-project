using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>
    /// Verkehrslage eines Schritts (T2.2/T2.3), aus dem Zustand abgeleitet, nie gespeichert: belegte
    /// Fahrspuren, Kreuzungsfreigaben und belegte Stellplätze. Ein Feld mit drei oder vier Anschlüssen
    /// gehört immer nur einem Fahrzeug.
    /// </summary>
    public sealed class Traffic
    {
        readonly Dictionary<int, int> lanes = new Dictionary<int, int>();
        readonly Dictionary<int, int> cellUse = new Dictionary<int, int>();
        readonly Dictionary<int, int> bayUse = new Dictionary<int, int>();
        readonly Dictionary<int, bool> junctionCache = new Dictionary<int, bool>();
        public readonly HashSet<int> Priority = new HashSet<int>();
        readonly HashSet<int> granted;
        public readonly RoadNetwork Network;
        /// <summary>Einfahrt mit externem Verkehr (T2.7); fehlt in reinen Verkehrstests.</summary>
        public EntranceGate Gate;

        public Traffic(RoadNetwork network, GameState state)
        {
            Network = network;
            foreach (var r in state.Roads) if (r.Priority) Priority.Add(RoadNetwork.CellKey(r.X, r.Z));
            foreach (var v in state.Vehicles)
            {
                if (v.BayAt.HasValue) bayUse[v.BayAt.Value] = BaysUsed(v.BayAt.Value) + 1;
                if (!v.OffRoad) Occupy(v);
            }
            granted = Junctions.Grant(this, state.Vehicles);
        }

        /// <summary>T-Stück oder Kreuzung (drei oder vier Anschlüsse).</summary>
        public bool IsJunction(Cell c)
        {
            int key = RoadNetwork.CellKey(c);
            if (!junctionCache.TryGetValue(key, out bool result))
            {
                int mask = Network.Has(c) ? Network.Connections(c.X, c.Z) : 0;
                result = RoadNetwork.BitCount(mask) >= 3;
                junctionCache[key] = result;
            }
            return result;
        }

        public bool LaneFree(Cell c, int heading) => !lanes.ContainsKey(Lanes.LaneKey(c, heading));

        public bool CellFree(Cell c) => !cellUse.TryGetValue(RoadNetwork.CellKey(c), out int n) || n == 0;

        public int? HolderOf(Cell c, int heading) => lanes.TryGetValue(Lanes.LaneKey(c, heading), out int id) ? id : (int?)null;

        public void Claim(Vehicle v, Cell c, int heading)
        {
            int key = Lanes.LaneKey(c, heading);
            if (lanes.TryGetValue(key, out int holder) && holder == v.Id) return;
            lanes[key] = v.Id;
            int cell = RoadNetwork.CellKey(c);
            cellUse[cell] = (cellUse.TryGetValue(cell, out int n) ? n : 0) + 1;
        }

        public void Release(Vehicle v, Cell c, int heading)
        {
            int key = Lanes.LaneKey(c, heading);
            if (!lanes.TryGetValue(key, out int holder) || holder != v.Id) return;
            lanes.Remove(key);
            int cell = RoadNetwork.CellKey(c);
            cellUse[cell] = (cellUse.TryGetValue(cell, out int n) ? n : 1) - 1;
        }

        /// <summary>Belegt Feld und gegebenenfalls das nächste (Fahrzeug fährt gerade hinüber).</summary>
        public void Occupy(Vehicle v)
        {
            if (v.Route.Count == 0) return;
            var here = v.Route[0];
            Claim(v, here, v.Heading);
            if (v.Progress > 0 && v.Route.Count > 1) Claim(v, v.Route[1], Lanes.HeadingBetween(here, v.Route[1]));
        }

        /// <summary>Fahrzeug verlässt die Fahrbahn (Stellplatz, Parken): Spuren werden frei.</summary>
        public void Vacate(Vehicle v)
        {
            if (v.Route.Count == 0) return;
            var here = v.Route[0];
            Release(v, here, v.Heading);
            if (v.Route.Count > 1) Release(v, v.Route[1], Lanes.HeadingBetween(here, v.Route[1]));
        }

        /// <summary>
        /// Darf das Fahrzeug (Fortschritt 0) ins nächste Feld? In eine Kreuzung nur mit Freigabe, sonst wenn
        /// die Spur frei ist. Wer abseits steht, braucht zusätzlich Platz zum Einfädeln.
        /// </summary>
        public bool CanEnter(Vehicle v)
        {
            if (v.Route.Count < 2) return false;
            var here = v.Route[0];
            var next = v.Route[1];
            if (Gate != null && !Gate.Allows(v, here, next)) return false;
            int heading = Lanes.HeadingBetween(here, next);
            if (v.OffRoad && !(IsJunction(here) ? CellFree(here) : LaneFree(here, heading))) return false;
            return IsJunction(next) ? granted.Contains(v.Id) : LaneFree(next, heading);
        }

        public int BaysUsed(int siteId) => bayUse.TryGetValue(siteId, out int n) ? n : 0;

        /// <summary>Stellplatz am Ort belegen, wenn einer frei ist; das Fahrzeug verlässt die Fahrbahn.</summary>
        public bool TakeBay(Vehicle v, int siteId, int capacity)
        {
            if (v.BayAt == siteId) return true;
            if (BaysUsed(siteId) >= capacity) return false;
            Vacate(v);
            v.OffRoad = true;
            v.BayAt = siteId;
            bayUse[siteId] = BaysUsed(siteId) + 1;
            return true;
        }

        /// <summary>Stellplatz freigeben; das Fahrzeug bleibt abseits stehen, bis es einfädelt.</summary>
        public void LeaveBay(Vehicle v)
        {
            if (!v.BayAt.HasValue) return;
            bayUse[v.BayAt.Value] = System.Math.Max(0, BaysUsed(v.BayAt.Value) - 1);
            v.BayAt = null;
        }

        /// <summary>Abseits parken (wartet ohne Auftrag oder ohne Weg).</summary>
        public void Park(Vehicle v)
        {
            if (v.OffRoad) return;
            Vacate(v);
            v.OffRoad = true;
        }
    }

    public static class Junctions
    {
        /// <summary>Ist der Weg hinter der Kreuzung frei („Kreuzung freihalten“)?</summary>
        public static bool ExitClear(Traffic traffic, List<Cell> route)
        {
            for (int i = 1; i < route.Count; i++)
            {
                var prev = route[i - 1];
                var c = route[i];
                if (traffic.IsJunction(c))
                {
                    if (!traffic.CellFree(c)) return false;
                    continue;
                }
                return traffic.LaneFree(c, Lanes.HeadingBetween(prev, c));
            }
            return true;
        }

        static bool WantsJunction(Traffic traffic, Vehicle v)
        {
            if (v.Route.Count < 2 || v.Progress != 0) return false;
            var here = v.Route[0];
            var next = v.Route[1];
            if (!traffic.IsJunction(next)) return false;
            if (!v.OffRoad) return true;
            int heading = Lanes.HeadingBetween(here, next);
            return traffic.IsJunction(here) ? traffic.CellFree(here) : traffic.LaneFree(here, heading);
        }

        static int HeadingOf(Vehicle v) => Lanes.HeadingBetween(v.Route[0], v.Route[1]);

        /// <summary>
        /// Kreuzungsfreigaben (T2.2): je freier Kreuzung höchstens ein Fahrzeug; nur mit Platz dahinter;
        /// Vorfahrtsstraße zuerst; sonst rechts vor links; sonst längste Wartezeit, dann kleinere Id.
        /// </summary>
        public static HashSet<int> Grant(Traffic traffic, List<Vehicle> vehicles)
        {
            var requests = new Dictionary<int, List<Vehicle>>();
            var order = new List<int>();
            foreach (var v in vehicles)
            {
                if (!WantsJunction(traffic, v)) continue;
                int key = RoadNetwork.CellKey(v.Route[1]);
                if (!requests.TryGetValue(key, out var list))
                {
                    list = new List<Vehicle>();
                    requests[key] = list;
                    order.Add(key);
                }
                list.Add(v);
            }
            var granted = new HashSet<int>();
            foreach (int key in order)
            {
                var list = requests[key];
                var junction = list[0].Route[1];
                if (!traffic.CellFree(junction)) continue;
                var eligible = list.FindAll(v => ExitClear(traffic, v.Route));
                var main = eligible.FindAll(v => traffic.Priority.Contains(RoadNetwork.CellKey(v.Route[0])));
                var pool = main.Count > 0 ? main : eligible;
                var free = pool.FindAll(v => !pool.Exists(o => o != v && HeadingOf(o) == Lanes.FromRight(HeadingOf(v))));
                Vehicle winner = null;
                foreach (var v in free.Count > 0 ? free : pool)
                    if (winner == null || v.WaitTicks > winner.WaitTicks || (v.WaitTicks == winner.WaitTicks && v.Id < winner.Id)) winner = v;
                if (winner != null) granted.Add(winner.Id);
            }
            return granted;
        }
    }

    public static class Bays
    {
        /// <summary>Stellplätze am Tor eines Orts (T2.3): wächst mit der Zonengröße; Export fest.</summary>
        public static int Capacity(GameState state, int siteId)
        {
            var zone = state.ZoneById(siteId);
            if (zone != null) return ForArea(zone.Kind, ZoneShape.Area(zone.Parts));
            var hall = state.HallById(siteId);
            if (hall != null) return HallBays(hall);
            return state.Buildings.Exists(b => b.Id == siteId) ? TrafficConfig.ExportBays : 1;
        }

        /// <summary>Stellplätze einer Zone dieser Art und Fläche; Werkstatt: ein Platz je 4 Felder.</summary>
        public static int ForArea(ZoneKind kind, int area)
        {
            int per = kind == ZoneKind.W ? MaintenanceConfig.WorkshopFieldsPerBay : TrafficConfig.FieldsPerBay;
            return System.Math.Min(TrafficConfig.MaxBays, System.Math.Max(1, (area + per - 1) / per));
        }

        /// <summary>Hallen (M3): Stellplätze nach der Fläche von Wareneingang und Warenausgang.</summary>
        public static int HallBays(Hall hall)
        {
            int area = 0;
            foreach (var a in hall.Areas) if (a.Kind == AreaKind.inbound || a.Kind == AreaKind.outbound) area += a.Rect.Area;
            return System.Math.Min(TrafficConfig.MaxBays, System.Math.Max(1, (area + TrafficConfig.FieldsPerBay - 1) / TrafficConfig.FieldsPerBay));
        }

        /// <summary>Belegte Stellplätze und Warteschlange.</summary>
        public static (int used, int queue) Status(GameState state, int siteId)
        {
            int used = 0, queue = 0;
            foreach (var v in state.Vehicles)
            {
                if (v.BayAt == siteId) used++;
                else if (v.WaitTicks > 0 && Destination.Of(state, v) == siteId) queue++;
            }
            return (used, queue);
        }
    }
}
