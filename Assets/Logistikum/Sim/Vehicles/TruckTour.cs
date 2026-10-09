using System;

namespace Logistikum.Sim
{
    /// <summary>
    /// Feste Tour (T1.5b): Halte der Reihe nach anfahren, je Halt laden oder abladen, nach dem letzten
    /// wieder von vorn. Der LKW nimmt mit, was da ist (Entscheidung 08.10.2026).
    /// </summary>
    public static class TruckTour
    {
        public static void Step(VehicleCtx ctx, Truck t)
        {
            switch (t.Phase)
            {
                case TruckPhase.idle:
                    if (--t.Timer <= 0) StartStop(ctx, t);
                    return;
                case TruckPhase.toPickup:
                case TruckPhase.toDropoff:
                {
                    var result = TruckShared.Drive(ctx, t, CurrentStop(ctx.State, t)?.SiteId);
                    if (result == DriveResult.blocked) HeadToStop(ctx, t);
                    else if (result == DriveResult.arrived)
                    {
                        t.Phase = t.Phase == TruckPhase.toPickup ? TruckPhase.loading : TruckPhase.unloading;
                        t.Timer = VehicleConfig.HandlingTicks;
                    }
                    return;
                }
                case TruckPhase.loading:
                case TruckPhase.unloading:
                    if (--t.Timer > 0) return;
                    HandleStop(ctx, t);
                    ctx.Traffic.LeaveBay(t);
                    t.TourIndex = (t.TourIndex + 1) % Math.Max(1, Tours.TourOf(ctx.State, t)?.Stops.Count ?? 1);
                    t.Phase = TruckPhase.idle;
                    t.Timer = 1;
                    return;
            }
        }

        public static TourStop CurrentStop(GameState state, Truck t)
        {
            var stops = Tours.TourOf(state, t)?.Stops;
            if (stops == null || stops.Count == 0) return null;
            return stops[t.TourIndex % stops.Count];
        }

        static void StartStop(VehicleCtx ctx, Truck t)
        {
            if (CurrentStop(ctx.State, t) == null)
            {
                TruckShared.GoIdle(ctx, t, TruckIdleReason.noTour);
                return;
            }
            HeadToStop(ctx, t);
        }

        static void HeadToStop(VehicleCtx ctx, Truck t)
        {
            var network = ctx.Traffic.Network;
            var stop = CurrentStop(ctx.State, t);
            var target = stop != null ? ctx.AccessOf(stop.SiteId) : null;
            if (stop == null || !target.HasValue || !TruckShared.SetRoute(t, ctx.Routes, target.Value))
            {
                TruckShared.GoIdle(ctx, t, TruckIdleReason.noRoute);
                return;
            }
            t.Phase = stop.Action == TourAction.load ? TruckPhase.toPickup : TruckPhase.toDropoff;
            t.IdleReason = null;
        }

        static void HandleStop(VehicleCtx ctx, Truck t)
        {
            var stop = CurrentStop(ctx.State, t);
            if (stop == null) return;
            if (stop.Action == TourAction.unload)
            {
                if (t.Cargo != null && t.Cargo.Product == stop.Product) TruckShared.UnloadAt(ctx, t, stop.SiteId);
                return;
            }
            var stock = Stock.PickStock(ctx.State, stop.SiteId);
            if (stock == null || (t.Cargo != null && t.Cargo.Product != stop.Product)) return;
            int loaded = t.Cargo?.Quantity ?? 0;
            int quantity = Math.Min(Fleet.ValuesOf(t).Capacity - loaded, Stock.Available(ctx.State, stop.SiteId, stop.Product));
            if (quantity <= 0) return;
            Stock.Add(stock, stop.Product, -quantity);
            t.Cargo = new Cargo(stop.Product, loaded + quantity);
        }
    }

    public static class Trucks
    {
        /// <summary>Ein Schritt eines eigenen LKW: Panne, Werkstatt, sonst feste Tour oder Automatik.</summary>
        public static void Step(VehicleCtx ctx, Truck t)
        {
            if (UpkeepRules.StepBreakdown(t) || Workshop.Step(ctx, t) || Workshop.MaybeStart(ctx, t)) return;
            if (t.TourId != null) TruckTour.Step(ctx, t);
            else StepAuto(ctx, t);
        }

        static void StepAuto(VehicleCtx ctx, Truck t)
        {
            switch (t.Phase)
            {
                case TruckPhase.idle:
                    if (--t.Timer > 0) return;
                    t.Timer = VehicleConfig.TruckIdleCheckTicks;
                    if (t.Cargo != null) DeliverCargoElsewhere(ctx, t);
                    else StartJob(ctx, t);
                    return;
                case TruckPhase.toPickup:
                case TruckPhase.toDropoff:
                {
                    int? siteId = t.Phase == TruckPhase.toPickup ? t.Job?.FromId : t.Job?.ToId;
                    var result = TruckShared.Drive(ctx, t, siteId);
                    if (result == DriveResult.blocked) Reroute(ctx, t);
                    else if (result == DriveResult.arrived)
                    {
                        t.Phase = t.Phase == TruckPhase.toPickup ? TruckPhase.loading : TruckPhase.unloading;
                        t.Timer = VehicleConfig.HandlingTicks;
                    }
                    return;
                }
                case TruckPhase.loading:
                    if (--t.Timer > 0) return;
                    ctx.Traffic.LeaveBay(t);
                    Load(ctx, t);
                    return;
                case TruckPhase.unloading:
                    if (--t.Timer > 0) return;
                    ctx.Traffic.LeaveBay(t);
                    Unload(ctx, t);
                    return;
            }
        }

        static void IdleAuto(VehicleCtx ctx, Truck t, TruckIdleReason? reason)
        {
            TruckShared.GoIdle(ctx, t, reason);
            if (t.Cargo == null) t.Job = null;
        }

        static void StartJob(VehicleCtx ctx, Truck t)
        {
            if (t.Route.Count == 0) return;
            var network = ctx.Traffic.Network;
            var planned = TruckJobs.Find(ctx.State, network, t.Route[0], Fleet.ValuesOf(t).Capacity, out bool noRoute, ctx.Routes, ctx.Sites);
            if (planned == null)
            {
                IdleAuto(ctx, t, noRoute ? TruckIdleReason.noRoute : TruckIdleReason.noJob);
                return;
            }
            t.Job = planned.Job;
            if (!TruckShared.SetRoute(t, ctx.Routes, planned.Target))
            {
                IdleAuto(ctx, t, TruckIdleReason.noRoute);
                return;
            }
            t.Phase = TruckPhase.toPickup;
            t.IdleReason = null;
        }

        static void Reroute(VehicleCtx ctx, Truck t)
        {
            int? id = t.Phase == TruckPhase.toPickup ? t.Job?.FromId : t.Job?.ToId;
            var network = ctx.Traffic.Network;
            var target = id == null ? null : ctx.AccessOf(id.Value);
            if (!target.HasValue || !TruckShared.SetRoute(t, ctx.Routes, target.Value)) IdleAuto(ctx, t, TruckIdleReason.noRoute);
        }

        static void Load(VehicleCtx ctx, Truck t)
        {
            var stock = t.Job != null ? Stock.PickStock(ctx.State, t.Job.FromId) : null;
            int quantity = stock != null ? Math.Min(t.Job.Quantity, Stock.Of(stock, t.Job.Product)) : 0;
            if (stock == null || quantity <= 0)
            {
                IdleAuto(ctx, t, null);
                return;
            }
            Stock.Add(stock, t.Job.Product, -quantity);
            t.Cargo = new Cargo(t.Job.Product, quantity);
            t.Job.Quantity = quantity;
            t.Phase = TruckPhase.toDropoff;
            Reroute(ctx, t);
        }

        static void Unload(VehicleCtx ctx, Truck t)
        {
            if (t.Job != null) TruckShared.UnloadAt(ctx, t, t.Job.ToId);
            t.Job = null;
            IdleAuto(ctx, t, null);
            t.Timer = 1;
            if (t.Cargo != null) DeliverCargoElsewhere(ctx, t);
        }

        /// <summary>Ladung ohne (gültiges) Ziel: neues Ziel für dieselbe Ware suchen.</summary>
        static void DeliverCargoElsewhere(VehicleCtx ctx, Truck t)
        {
            var cargo = t.Cargo;
            if (cargo == null) return;
            var network = ctx.Traffic.Network;
            bool fromHall = t.Job != null && ctx.State.HallById(t.Job.FromId) != null;
            foreach (var drop in TruckJobs.DropsFor(ctx.State, cargo.Product, fromHall, ctx.Sites))
            {
                var access = Sites.AccessOf(network, drop.Site);
                if (!access.HasValue) continue;
                int fromId = t.Job?.FromId ?? drop.Site.Id;
                t.Job = new Job { Product = cargo.Product, FromId = fromId, ToId = drop.Site.Id, Quantity = cargo.Quantity };
                if (!TruckShared.SetRoute(t, ctx.Routes, access.Value)) continue;
                t.Phase = TruckPhase.toDropoff;
                t.IdleReason = null;
                return;
            }
            t.Job = null;
            TruckShared.GoIdle(ctx, t, TruckIdleReason.noDestination);
        }

        /// <summary>Tageswechsel: Tages- und Kilometerkosten je Fahrzeug (Kasse: „Fahrzeuge“).</summary>
        public static void BookCosts(GameState state, EventBus bus)
        {
            foreach (var v in state.Vehicles)
            {
                if (!(v is Truck t)) continue;
                var values = Fleet.ValuesOf(t);
                // Tausendstel Feld × Meter je Feld × Cent je km = Millionstel Cent.
                long rate = VehicleConfig.MetersPerField * values.CostPerKmCents;
                long micro = t.Odometer * rate;
                long kmCents = micro / 1_000_000;
                t.Odometer = rate == 0 ? 0 : (micro - kmCents * 1_000_000) / rate;
                Ledger.Book(state, bus, BookingCategory.vehicles, -(values.DailyCents + kmCents));
            }
        }
    }
}
