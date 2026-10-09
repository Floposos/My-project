using System;
using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Was die Fahrzeug-Logik je Schritt braucht. Das Straßennetz steckt in Traffic.Network.</summary>
    public sealed class VehicleCtx
    {
        public GameState State;
        public EventBus Bus;
        public Traffic Traffic;
        RouteCache routes;
        List<Site> sites;

        /// <summary>Wege dieses Schritts (das Straßennetz ändert sich innerhalb des Fahrzeug-Systems nicht).</summary>
        public RouteCache Routes => routes ?? (routes = new RouteCache(Traffic.Network));
        /// <summary>Anfahrbare Orte dieses Schritts (ändern sich nur durch Befehle).</summary>
        public List<Site> Sites => sites ?? (sites = Sim.Sites.All(State));

        /// <summary>Zufahrt eines Orts aus der Ortsliste dieses Schritts.</summary>
        public Cell? AccessOf(int siteId)
        {
            var site = Sites.Find(x => x.Id == siteId);
            return site == null ? (Cell?)null : Sim.Sites.AccessOf(Traffic.Network, site);
        }
    }

    /// <summary>
    /// Merkt sich geplante Wege innerhalb eines Schritts. Viele wartende LKW stehen auf demselben Feld und
    /// prüfen dieselben Wege; das Ergebnis ist identisch, solange das Netz gleich bleibt.
    /// </summary>
    public sealed class RouteCache
    {
        readonly RoadNetwork network;
        readonly Dictionary<(int, int, int, int), List<Cell>> map = new Dictionary<(int, int, int, int), List<Cell>>();

        public RouteCache(RoadNetwork network) { this.network = network; }

        /// <summary>Weg als neue Liste (darf verändert werden) oder null.</summary>
        public List<Cell> Plan(Cell from, Cell to)
        {
            var key = (from.X, from.Z, to.X, to.Z);
            if (!map.TryGetValue(key, out var route))
            {
                route = Movement.PlanRoute(network, from, to);
                map[key] = route;
            }
            return route == null ? null : new List<Cell>(route);
        }

        public bool Exists(Cell from, Cell to)
        {
            var key = (from.X, from.Z, to.X, to.Z);
            if (!map.TryGetValue(key, out var route))
            {
                route = Movement.PlanRoute(network, from, to);
                map[key] = route;
            }
            return route != null;
        }
    }

    public struct AdvanceResult
    {
        /// <summary>Am Routenende.</summary>
        public bool Arrived;
        /// <summary>Nächstes Feld ist keine Straße mehr: neu planen.</summary>
        public bool Blocked;
        /// <summary>Steht im Verkehr.</summary>
        public bool Waiting;
        /// <summary>Gefahrene Strecke in Tausendstel Feld.</summary>
        public int Moved;
    }

    public static class Movement
    {
        /// <summary>Tausendstel je Feld.</summary>
        public const int CellUnits = 1000;

        /// <summary>Fahrweg außerhalb des Geländes auf der Eingangsstraße (von außen bis vor die Einfahrt).</summary>
        public static List<Cell> OutsideLane()
        {
            var cells = new List<Cell>();
            var e = RoadNetwork.Entrance;
            for (int i = VehicleConfig.OutsideCells; i >= 2; i--) cells.Add(new Cell(e.X - i + 1, e.Z));
            return cells;
        }

        /// <summary>Felder außerhalb des Geländes (Eingangsstraße) sind immer befahrbar.</summary>
        public static bool Drivable(RoadNetwork network, Cell c) => c.X < RoadNetwork.Entrance.X || network.Has(c);

        /// <summary>Weg vom aktuellen Feld zum Ziel; null = kein Weg.</summary>
        public static List<Cell> PlanRoute(RoadNetwork network, Cell from, Cell to, ISet<int> avoid = null)
        {
            var e = RoadNetwork.Entrance;
            if (from.X < e.X)
            {
                var rest = Pathfinding.FindPath(network, e, to, avoid);
                if (rest == null) return null;
                var lane = new List<Cell>();
                for (int x = from.X; x < e.X; x++) lane.Add(new Cell(x, e.Z));
                lane.AddRange(rest);
                return lane;
            }
            return Pathfinding.FindPath(network, from, to, avoid);
        }

        /// <summary>
        /// Fährt speed Tausendstel Feld weiter. Vor jedem neuen Feld fragt das Fahrzeug den Verkehr: Ist die
        /// Spur belegt oder fehlt die Kreuzungsfreigabe, wartet es an der Feldgrenze.
        /// </summary>
        public static AdvanceResult Advance(Vehicle v, int speed, Traffic traffic)
        {
            int budget = speed;
            AdvanceResult R(bool arrived, bool blocked, bool waiting) =>
                new AdvanceResult { Arrived = arrived, Blocked = blocked, Waiting = waiting, Moved = speed - budget };
            while (v.Route.Count > 1)
            {
                var here = v.Route[0];
                var next = v.Route[1];
                if (!Drivable(traffic.Network, next)) return R(false, true, false);
                int heading = Lanes.HeadingBetween(here, next);
                if (v.Progress == 0)
                {
                    if (!traffic.CanEnter(v)) return R(false, false, budget == speed);
                    if (v.OffRoad)
                    {
                        v.OffRoad = false;
                        v.Heading = heading;
                        traffic.Claim(v, here, heading);
                    }
                    traffic.Claim(v, next, heading);
                    traffic.Gate?.Pass(here, next);
                }
                int step = Math.Min(budget, CellUnits - v.Progress);
                v.Progress += step;
                budget -= step;
                if (v.Progress < CellUnits) return R(false, false, false);
                traffic.Release(v, here, v.Heading);
                v.Route.RemoveAt(0);
                v.Progress = 0;
                v.Heading = heading;
            }
            return R(true, false, false);
        }
    }

    public enum DriveResult { arrived, blocked, driving, waiting }

    /// <summary>Fahren mit Verkehr: Wartezeit, Stau-Meldung, Umweg, Stellplatz (T2.3/T2.4).</summary>
    public static class Driving
    {
        public static DriveResult Drive(VehicleCtx ctx, Vehicle v, int speed, out int moved)
        {
            var r = Movement.Advance(v, speed, ctx.Traffic);
            moved = r.Moved;
            if (r.Moved > 0) v.WaitTicks = 0;
            if (r.Blocked) return DriveResult.blocked;
            if (r.Waiting)
            {
                NoteWait(ctx, v, true);
                return DriveResult.waiting;
            }
            return r.Arrived ? DriveResult.arrived : DriveResult.driving;
        }

        /// <summary>Wartezeit zählen: nach JamWarnTicks Stau melden; im Verkehr regelmäßig Umweg suchen.</summary>
        public static void NoteWait(VehicleCtx ctx, Vehicle v, bool mayDetour)
        {
            v.WaitTicks += 1;
            if (v.WaitTicks == TrafficConfig.JamWarnTicks && v.Route.Count > 0)
            {
                var here = v.Route[0];
                ctx.Bus.Emit(new TrafficJam { VehicleId = v.Id, X = here.X, Z = here.Z });
                Notices.Add(ctx.State, ctx.Bus, NoticeKind.jam, here.X, here.Z, v.Id);
            }
            int after = TrafficConfig.DetourAfterTicks, every = TrafficConfig.DetourEveryTicks;
            if (mayDetour && v.WaitTicks >= after && (v.WaitTicks - after) % every == 0) TryDetour(ctx, v);
        }

        /// <summary>Umweg um das nächste (versperrte) Feld; true = neuer Weg gesetzt.</summary>
        public static bool TryDetour(VehicleCtx ctx, Vehicle v)
        {
            if (v.Route.Count < 2 || v.Progress != 0) return false;
            var here = v.Route[0];
            var blocked = v.Route[1];
            var target = v.Route[v.Route.Count - 1];
            if (blocked == target) return false;
            var avoid = new HashSet<int> { RoadNetwork.CellKey(blocked) };
            var route = Movement.PlanRoute(ctx.Traffic.Network, here, target, avoid);
            if (route == null || route.Count < 2) return false;
            v.Route = route;
            return true;
        }

        /// <summary>Am Ziel: Stellplatz nehmen oder in der Schlange warten. true = Stellplatz belegt.</summary>
        public static bool EnterBay(VehicleCtx ctx, Vehicle v, int siteId)
        {
            if (ctx.Traffic.TakeBay(v, siteId, Bays.Capacity(ctx.State, siteId)))
            {
                v.WaitTicks = 0;
                return true;
            }
            NoteWait(ctx, v, false);
            return false;
        }
    }
}
