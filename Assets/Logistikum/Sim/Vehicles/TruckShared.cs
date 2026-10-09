using System;

namespace Logistikum.Sim
{
    /// <summary>Gemeinsame Bausteine der LKW-Logik (Automatik, Tour, Werkstatt).</summary>
    public static class TruckShared
    {
        /// <summary>Wartet (abseits geparkt, gibt den Stellplatz frei) und versucht es später erneut.</summary>
        public static void GoIdle(VehicleCtx ctx, Truck t, TruckIdleReason? reason)
        {
            ctx.Traffic.LeaveBay(t);
            ctx.Traffic.Park(t);
            t.Phase = TruckPhase.idle;
            t.IdleReason = reason;
            t.Timer = VehicleConfig.TruckIdleCheckTicks;
        }

        /// <summary>Neuer Weg zum Ziel; zwischen zwei Feldern erst aufs nächste Feld (kein Rucken).</summary>
        public static bool SetRoute(Truck t, RouteCache routes, Cell target)
        {
            if (t.Route.Count == 0) return false;
            var here = t.Route[0];
            if (t.Progress > 0 && t.Route.Count > 1)
            {
                var rest = routes.Plan(t.Route[1], target);
                if (rest != null)
                {
                    rest.Insert(0, here);
                    t.Route = rest;
                    return true;
                }
            }
            var route = routes.Plan(here, target);
            if (route == null) return false;
            t.Route = route;
            t.Progress = 0;
            return true;
        }

        /// <summary>
        /// Fährt weiter und zählt die Strecke. blocked = Straße unterbrochen; arrived erst, wenn am Ziel
        /// ein Stellplatz frei ist (sonst Warteschlange).
        /// </summary>
        public static DriveResult Drive(VehicleCtx ctx, Truck t, int? siteId)
        {
            var result = Driving.Drive(ctx, t, Fleet.ValuesOf(t).Speed, out int moved);
            t.Odometer += moved;
            UpkeepRules.ApplyWear(ctx, t, moved);
            if (result == DriveResult.blocked)
            {
                t.Progress = 0;
                return DriveResult.blocked;
            }
            if (result != DriveResult.arrived) return DriveResult.driving;
            return siteId == null || Driving.EnterBay(ctx, t, siteId.Value) ? DriveResult.arrived : DriveResult.driving;
        }

        /// <summary>Lädt am Ort ab, so viel passt (Zone/Halle) bzw. verkauft alles (Export). Abgeladene Menge.</summary>
        public static int UnloadAt(VehicleCtx ctx, Truck t, int siteId)
        {
            var cargo = t.Cargo;
            if (cargo == null) return 0;
            var state = ctx.State;
            int quantity = 0;
            var stock = Stock.DropStock(state, siteId, cargo.Product, out _);
            if (stock != null)
            {
                // Eigene Ladung ist im Platz bereits als „unterwegs“ eingerechnet (Automatik).
                int own = t.Job != null && t.Job.ToId == siteId ? cargo.Quantity : 0;
                quantity = Math.Min(cargo.Quantity, Stock.FreeSpace(state, siteId, cargo.Product) + own);
                if (quantity > 0)
                {
                    Stock.Add(stock, cargo.Product, quantity);
                    ctx.Bus.Emit(new GoodsDelivered { SiteId = siteId, Product = cargo.Product, Quantity = quantity });
                }
            }
            else if (Export.SellAtExit(state, ctx.Bus, siteId, cargo.Product, cargo.Quantity) != null)
            {
                quantity = cargo.Quantity;
            }
            cargo.Quantity -= quantity;
            if (cargo.Quantity <= 0) t.Cargo = null;
            return quantity;
        }
    }
}
