using System;
using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Rohware-Bestellungen: einmalig oder als Dauerauftrag (Entscheidung 07.10.2026).</summary>
    public static class Orders
    {
        public static Order Create(GameState state, ProductId product, int quantity, OrderInterval interval)
        {
            if (!Products.IsRaw(product) || quantity <= 0) return null;
            var order = new Order { Id = state.NextId++, Product = product, Quantity = quantity, Interval = interval, NextTick = state.Tick };
            state.Orders.Add(order);
            return order;
        }

        public static bool Cancel(GameState state, int id) => state.Orders.RemoveAll(o => o.Id == id) > 0;

        public static int? NextDelivery(int tick, OrderInterval interval)
        {
            switch (interval)
            {
                case OrderInterval.daily: return tick + GameTime.TicksPerDay;
                case OrderInterval.weekly: return tick + 7 * GameTime.TicksPerDay;
                case OrderInterval.monthly: return GameTime.AddOneMonth(tick);
                default: return null;
            }
        }

        /// <summary>Bester Lieferort: erreichbar, mit dem meisten freien Platz (Gleichstand: kleinste ID).</summary>
        static OrderBlock? FindTarget(GameState state, RoadNetwork network, Order order, out Zone zone, out List<Cell> path, out int free)
        {
            zone = null; path = null; free = 0;
            var kind = Products.Raw[order.Product];
            bool any = false, anyReachable = false;
            foreach (var z in state.Zones)
            {
                if (z.Kind != kind) continue;
                any = true;
                var access = Access.AccessCell(network, z.Parts, z.Gate);
                var p = access.HasValue ? Pathfinding.FindPath(network, RoadNetwork.Entrance, access.Value) : null;
                if (p == null) continue;
                anyReachable = true;
                int f = Stock.FreeSpace(state, z.Id, order.Product);
                if (f > 0 && (zone == null || f > free)) { zone = z; path = p; free = f; }
            }
            if (zone != null) return null;
            if (!any) return OrderBlock.noSite;
            return anyReachable ? OrderBlock.full : OrderBlock.noRoute;
        }

        /// <summary>Versucht eine fällige Lieferung: Ware bezahlen, Zulieferer-LKW losschicken.</summary>
        public static OrderBlock? Dispatch(GameState state, EventBus bus, Order order)
        {
            var network = new RoadNetwork(state);
            var block = FindTarget(state, network, order, out var zone, out var path, out int free);
            if (block.HasValue) return block;
            int quantity = Math.Min(order.Quantity, free);
            long cost = quantity * GoodsConfig.PurchasePriceCents(order.Product);
            if (state.Finance.BalanceCents < cost) return OrderBlock.noMoney;
            Ledger.Book(state, bus, BookingCategory.rawGoods, -cost, new Place(0.5, RoadNetwork.Entrance.Z + 0.5));
            var route = Movement.OutsideLane();
            route.AddRange(path);
            // Kommt von außen und fädelt auf der Eingangsstraße ein, sobald dort Platz ist.
            state.Vehicles.Add(new Supplier
            {
                Id = state.NextId++, Route = route, OffRoad = true,
                Cargo = new Cargo(order.Product, quantity), Phase = SupplierPhase.toSite,
                TargetId = zone.Id, PaidCents = cost,
            });
            return null;
        }

        /// <summary>Prüft je Schritt die fälligen Bestellungen in fester Reihenfolge.</summary>
        public static void Update(GameState state, EventBus bus)
        {
            foreach (var order in state.Orders.ToArray())
            {
                if (order.NextTick > state.Tick) continue;
                var block = Dispatch(state, bus, order);
                order.Blocked = block;
                if (block.HasValue)
                {
                    order.NextTick = state.Tick + VehicleConfig.RetryTicks;
                    continue;
                }
                var next = NextDelivery(state.Tick, order.Interval);
                if (next == null) Cancel(state, order.Id);
                else order.NextTick = next.Value;
            }
        }
    }

    public static class Export
    {
        /// <summary>Fester Exportpreis je Ware; null = nicht exportierbar.</summary>
        public static long? Price(ProductId p) => GoodsConfig.ExportPriceCents.TryGetValue(p, out var v) ? v : (long?)null;

        /// <summary>Verkauft Ware an einer Export-Ausfahrt zum festen Preis. Erlös oder null.</summary>
        public static long? SellAtExit(GameState state, EventBus bus, int exitId, ProductId product, int quantity)
        {
            var exit = state.Buildings.Find(b => b.Id == exitId && b.Type == BuildingTypeId.exportExit);
            var price = Price(product);
            if (exit == null || price == null || quantity <= 0) return null;
            var f = Buildings.FootprintOf(exit);
            long revenue = price.Value * quantity;
            Ledger.Book(state, bus, BookingCategory.exportRevenue, revenue, new Place(f.X + f.Width / 2.0, f.Z + f.Depth / 2.0));
            bus.Emit(new GoodsSold { ExitId = exitId, Product = product, Quantity = quantity, RevenueCents = revenue });
            return revenue;
        }
    }
}
