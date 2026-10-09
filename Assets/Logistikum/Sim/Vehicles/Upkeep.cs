using System;
using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Verschleiß, Pannen und Werkstatt (T2.6).</summary>
    public static class UpkeepRules
    {
        /// <summary>Pannenrisiko je gefahrenem Feld (0 … 1), quadratisch steigend mit dem Verschleiß.</summary>
        public static double BreakdownChancePerField(int condition)
        {
            double worn = 1 - Math.Max(0, Math.Min(MaintenanceConfig.FullCondition, condition)) / (double)MaintenanceConfig.FullCondition;
            return MaintenanceConfig.BreakdownPerKmAtZeroPpm / 1_000_000.0 * worn * worn * 0.01;
        }

        public static bool NeedsService(Truck t) => t.Upkeep.ServiceRequested || t.Upkeep.Condition < MaintenanceConfig.ServiceBelow;

        /// <summary>Gefahrene Kilometer bis zur nächsten automatischen Wartung (0 = fällig).</summary>
        public static int KmUntilService(Truck t)
        {
            int rest = t.Upkeep.Condition - MaintenanceConfig.ServiceBelow;
            return rest <= 0 ? 0 : (int)Math.Ceiling(rest / (double)(MaintenanceConfig.WearPerField * 100));
        }

        /// <summary>Verschleiß für gefahrene Strecke; je ganzem Feld auf der Straße ein Wurf aus dem Ereignis-Zufall.</summary>
        public static void ApplyWear(VehicleCtx ctx, Truck t, int moved)
        {
            if (moved <= 0) return;
            var u = t.Upkeep;
            u.WearRest += moved;
            while (u.WearRest >= 1000)
            {
                u.WearRest -= 1000;
                u.Condition = Math.Max(0, u.Condition - MaintenanceConfig.WearPerField);
                if (u.BrokenTicks == 0 && !t.OffRoad && ctx.State.EventRng.NextFloat() < BreakdownChancePerField(u.Condition))
                    BreakDown(ctx, t);
            }
        }

        /// <summary>Panne: steht und blockiert die Spur, Abschleppkosten sofort, Meldung.</summary>
        public static void BreakDown(VehicleCtx ctx, Truck t)
        {
            var u = t.Upkeep;
            u.BrokenTicks = MaintenanceConfig.BreakdownTicks;
            u.Breakdowns += 1;
            u.LastBreakdownTick = ctx.State.Tick;
            var here = t.Here;
            Ledger.Book(ctx.State, ctx.Bus, BookingCategory.vehicles, -MaintenanceConfig.TowCostCents, new Place(here.X + 0.5, here.Z + 0.5));
            ctx.Bus.Emit(new VehicleBrokeDown { Id = t.Id, X = here.X, Z = here.Z });
            Notices.Add(ctx.State, ctx.Bus, NoticeKind.breakdown, here.X, here.Z, t.Id);
        }

        /// <summary>Ein Schritt Panne; true = steht noch.</summary>
        public static bool StepBreakdown(Truck t)
        {
            var u = t.Upkeep;
            if (u.BrokenTicks <= 0) return false;
            u.BrokenTicks -= 1;
            if (u.BrokenTicks == 0) u.Condition = Math.Min(MaintenanceConfig.FullCondition, u.Condition + MaintenanceConfig.BreakdownRepair);
            return true;
        }

        public static void FinishService(VehicleCtx ctx, Truck t)
        {
            var u = t.Upkeep;
            var here = t.Here;
            Ledger.Book(ctx.State, ctx.Bus, BookingCategory.vehicles, -MaintenanceConfig.ServiceCostCents, new Place(here.X + 0.5, here.Z + 0.5));
            u.Condition = MaintenanceConfig.FullCondition;
            u.ServiceRequested = false;
            u.WarnedNoWorkshop = false;
            u.LastServiceTick = ctx.State.Tick;
            u.WorkshopId = null;
        }

        public static void WarnNoWorkshop(VehicleCtx ctx, Truck t)
        {
            if (t.Upkeep.WarnedNoWorkshop) return;
            t.Upkeep.WarnedNoWorkshop = true;
            var here = t.Here;
            Notices.Add(ctx.State, ctx.Bus, NoticeKind.noWorkshop, here.X, here.Z, t.Id);
        }
    }

    public static class Workshop
    {
        static int? Nearest(VehicleCtx ctx, Truck t)
        {
            if (t.Route.Count == 0) return null;
            var here = t.Route[0];
            var network = ctx.Traffic.Network;
            int? bestId = null;
            int bestLen = int.MaxValue;
            foreach (var site in ctx.Sites)
            {
                if (site.Kind != SiteKind.W) continue;
                var access = Sites.AccessOf(network, site);
                var route = access.HasValue ? ctx.Routes.Plan(here, access.Value) : null;
                if (route != null && route.Count < bestLen) { bestId = site.Id; bestLen = route.Count; }
            }
            return bestId;
        }

        static bool HeadTo(VehicleCtx ctx, Truck t, int id)
        {
            var access = ctx.AccessOf(id);
            return access.HasValue && TruckShared.SetRoute(t, ctx.Routes, access.Value);
        }

        /// <summary>Entscheidungspunkt: Wartung fällig → zur nächsten Werkstatt. true = fährt hin.</summary>
        public static bool MaybeStart(VehicleCtx ctx, Truck t)
        {
            if (t.Phase != TruckPhase.idle || t.Timer > 1 || !UpkeepRules.NeedsService(t)) return false;
            if (t.TourId == null && t.Cargo != null) return false;
            var id = Nearest(ctx, t);
            if (id == null || !HeadTo(ctx, t, id.Value))
            {
                t.Upkeep.ServiceRequested = false;
                UpkeepRules.WarnNoWorkshop(ctx, t);
                return false;
            }
            t.Upkeep.WorkshopId = id;
            t.Phase = TruckPhase.toWorkshop;
            t.IdleReason = null;
            return true;
        }

        /// <summary>Fahrt zur Werkstatt und Wartung; true = dieser Schritt ist damit erledigt.</summary>
        public static bool Step(VehicleCtx ctx, Truck t)
        {
            if (t.Phase != TruckPhase.toWorkshop && t.Phase != TruckPhase.servicing) return false;
            var id = t.Upkeep.WorkshopId;
            if (id == null || !ctx.State.Zones.Exists(z => z.Id == id && z.Kind == ZoneKind.W))
            {
                t.Upkeep.WorkshopId = null;
                TruckShared.GoIdle(ctx, t, null);
                t.Timer = 1;
                return true;
            }
            if (t.Phase == TruckPhase.toWorkshop)
            {
                var result = TruckShared.Drive(ctx, t, id);
                if (result == DriveResult.blocked && !HeadTo(ctx, t, id.Value))
                {
                    t.Upkeep.WorkshopId = null;
                    TruckShared.GoIdle(ctx, t, TruckIdleReason.noRoute);
                }
                else if (result == DriveResult.arrived)
                {
                    t.Phase = TruckPhase.servicing;
                    t.Timer = MaintenanceConfig.ServiceTicks;
                }
                return true;
            }
            if (--t.Timer > 0) return true;
            UpkeepRules.FinishService(ctx, t);
            TruckShared.GoIdle(ctx, t, null);
            t.Timer = 1;
            return true;
        }
    }

    public static class Destination
    {
        /// <summary>Ort, zu dem das Fahrzeug gerade fährt (null = keiner).</summary>
        public static int? Of(GameState state, Vehicle v)
        {
            if (v is Supplier s) return s.Phase == SupplierPhase.toSite ? s.TargetId : (int?)null;
            var t = (Truck)v;
            if (t.Phase == TruckPhase.toWorkshop) return t.Upkeep.WorkshopId;
            if (t.Phase != TruckPhase.toPickup && t.Phase != TruckPhase.toDropoff) return null;
            if (t.TourId != null) return TruckTour.CurrentStop(state, t)?.SiteId;
            return t.Phase == TruckPhase.toPickup ? t.Job?.FromId : t.Job?.ToId;
        }
    }
}
