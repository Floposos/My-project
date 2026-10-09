using System;

namespace Logistikum.Sim
{
    /// <summary>
    /// Zulieferer-LKW: fahren von der Einfahrt zum Lieferort, warten auf einen Stellplatz, laden ab und
    /// fahren wieder hinaus. Fällt der Lieferort weg, kehren sie um und die Ware wird erstattet.
    /// </summary>
    public static class Suppliers
    {
        /// <summary>Liefert false, wenn der Zulieferer das Gelände verlassen hat.</summary>
        public static bool Step(VehicleCtx ctx, Supplier v)
        {
            var zone = ctx.State.ZoneById(v.TargetId);
            if (v.Cargo != null && zone == null) TurnBack(ctx, v);
            switch (v.Phase)
            {
                case SupplierPhase.toSite:
                case SupplierPhase.toExit:
                {
                    var result = Driving.Drive(ctx, v, VehicleConfig.SupplierSpeed, out _);
                    if (result == DriveResult.blocked) Replan(ctx, v);
                    else if (result == DriveResult.arrived && v.Phase == SupplierPhase.toExit)
                    {
                        ctx.Traffic.Vacate(v);
                        return false;
                    }
                    else if (result == DriveResult.arrived && Driving.EnterBay(ctx, v, v.TargetId))
                    {
                        v.Phase = SupplierPhase.handling;
                        v.Timer = VehicleConfig.HandlingTicks;
                    }
                    return true;
                }
                case SupplierPhase.handling:
                    if (--v.Timer > 0) return true;
                    ctx.Traffic.LeaveBay(v);
                    if (zone != null && v.Cargo != null)
                    {
                        int q = Math.Min(v.Cargo.Quantity, Stock.ZoneCapacity(zone) - Stock.Of(zone, v.Cargo.Product));
                        Stock.Add(zone, v.Cargo.Product, q);
                        ctx.Bus.Emit(new GoodsDelivered { SiteId = zone.Id, Product = v.Cargo.Product, Quantity = q });
                    }
                    v.Cargo = null;
                    Leave(ctx, v);
                    return true;
                default:
                    if (--v.Timer <= 0) Replan(ctx, v);
                    return true;
            }
        }

        static void TurnBack(VehicleCtx ctx, Supplier v)
        {
            Ledger.Book(ctx.State, ctx.Bus, BookingCategory.rawGoods, v.PaidCents);
            v.PaidCents = 0;
            v.Cargo = null;
            ctx.Traffic.LeaveBay(v);
            Leave(ctx, v);
        }

        static void WaitNoRoute(VehicleCtx ctx, Supplier v)
        {
            ctx.Traffic.Park(v);
            v.Phase = SupplierPhase.noRoute;
            v.Timer = VehicleConfig.RetryTicks;
        }

        static void Leave(VehicleCtx ctx, Supplier v)
        {
            var route = Movement.PlanRoute(ctx.Traffic.Network, v.Here, RoadNetwork.Entrance);
            if (route == null)
            {
                WaitNoRoute(ctx, v);
                return;
            }
            var outside = Movement.OutsideLane();
            outside.Reverse();
            route.AddRange(outside);
            v.Route = route;
            v.Progress = 0;
            v.Phase = SupplierPhase.toExit;
        }

        static void Replan(VehicleCtx ctx, Supplier v)
        {
            if (v.Cargo == null)
            {
                Leave(ctx, v);
                return;
            }
            var network = ctx.Traffic.Network;
            var zone = ctx.State.ZoneById(v.TargetId);
            var access = zone != null ? Access.AccessCell(network, zone.Parts, zone.Gate) : null;
            var route = access.HasValue ? Movement.PlanRoute(network, v.Here, access.Value) : null;
            if (route == null)
            {
                WaitNoRoute(ctx, v);
                return;
            }
            v.Route = route;
            v.Progress = 0;
            v.Phase = SupplierPhase.toSite;
        }
    }
}
