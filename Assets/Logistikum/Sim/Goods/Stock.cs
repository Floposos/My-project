using System;
using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>
    /// Lager: Bestand je Ware in Zonen und Hallenbereichen. Bei Hallen liefern LKW in den ersten
    /// Wareneingang und holen aus dem ersten Warenausgang ab (ANNAHME M3).
    /// </summary>
    public static class Stock
    {
        public static int Of(Dictionary<ProductId, int> stock, ProductId p) => stock.TryGetValue(p, out var v) ? v : 0;
        public static void Add(Dictionary<ProductId, int> stock, ProductId p, int q) { stock[p] = Of(stock, p) + q; }

        public static int Of(Zone zone, ProductId p) => Of(zone.Stock, p);
        public static void Add(Zone zone, ProductId p, int q) => Add(zone.Stock, p, q);

        public static int ZoneCapacity(Zone zone) => ZoneShape.Area(zone.Parts) * ZoneConfig.CapacityPerField;
        public static int AreaCapacity(HallArea a) => a.Rect.Area * ZoneConfig.CapacityPerField;

        public static bool Stores(Zone zone, ProductId p) => Array.IndexOf(ZoneTypes.Stores(zone.Kind), p) >= 0;

        /// <summary>Waren, die eine Halle im Wareneingang annimmt (Endprodukt und Hallenstufen).</summary>
        public static bool HallAccepts(ProductId p) => p == ProductId.final || p == ProductId.packed || p == ProductId.labeled || p == ProductId.@checked;

        public static bool IsFull(Zone zone, ProductId p) => Of(zone, p) >= ZoneCapacity(zone);

        /// <summary>Lager, in das Ware für diesen Ort abgeladen wird; null = nimmt die Ware nicht.</summary>
        public static Dictionary<ProductId, int> DropStock(GameState state, int siteId, ProductId p, out int capacity)
        {
            capacity = 0;
            var zone = state.ZoneById(siteId);
            if (zone != null)
            {
                if (!Stores(zone, p)) return null;
                capacity = ZoneCapacity(zone);
                return zone.Stock;
            }
            var hall = state.HallById(siteId);
            var inbound = hall?.FirstArea(AreaKind.inbound);
            if (inbound == null || !HallAccepts(p)) return null;
            capacity = AreaCapacity(inbound);
            return inbound.Stock;
        }

        /// <summary>Lager, aus dem an diesem Ort aufgeladen wird; null = keins.</summary>
        public static Dictionary<ProductId, int> PickStock(GameState state, int siteId)
        {
            var zone = state.ZoneById(siteId);
            if (zone != null) return zone.Stock;
            return state.HallById(siteId)?.FirstArea(AreaKind.outbound)?.Stock;
        }

        /// <summary>
        /// Momentaufnahme aller Reservierungen (unterwegs, eingeplante Abholungen) in einem Durchlauf über die
        /// Fahrzeuge. Gleiches Ergebnis wie Incoming/ReservedOut, solange sich der Zustand nicht ändert.
        /// </summary>
        public sealed class Reservations
        {
            readonly Dictionary<(int, ProductId), int> incoming = new Dictionary<(int, ProductId), int>();
            readonly Dictionary<(int, ProductId), int> reserved = new Dictionary<(int, ProductId), int>();
            readonly GameState state;

            public Reservations(GameState state)
            {
                this.state = state;
                foreach (var v in state.Vehicles)
                {
                    if (v is Supplier s)
                    {
                        if (s.Cargo != null && s.Phase != SupplierPhase.toExit) Add(incoming, (s.TargetId, s.Cargo.Product), s.Cargo.Quantity);
                    }
                    else if (v is Truck t && t.Job != null)
                    {
                        Add(incoming, (t.Job.ToId, t.Job.Product), t.Cargo?.Quantity ?? t.Job.Quantity);
                        if (t.Cargo == null) Add(reserved, (t.Job.FromId, t.Job.Product), t.Job.Quantity);
                    }
                }
            }

            static void Add(Dictionary<(int, ProductId), int> d, (int, ProductId) k, int q) => d[k] = (d.TryGetValue(k, out var n) ? n : 0) + q;

            public int Available(int siteId, ProductId p)
            {
                var stock = PickStock(state, siteId);
                return stock == null ? 0 : Math.Max(0, Of(stock, p) - (reserved.TryGetValue((siteId, p), out var r) ? r : 0));
            }

            public int FreeSpace(int siteId, ProductId p)
            {
                var stock = DropStock(state, siteId, p, out int cap);
                return stock == null ? 0 : Math.Max(0, cap - Of(stock, p) - (incoming.TryGetValue((siteId, p), out var n) ? n : 0));
            }
        }

        /// <summary>Ware, die gerade zu diesem Ort unterwegs oder fest eingeplant ist.</summary>
        public static int Incoming(GameState state, int siteId, ProductId p)
        {
            int sum = 0;
            foreach (var v in state.Vehicles)
            {
                if (v is Supplier s)
                {
                    if (s.TargetId == siteId && s.Cargo != null && s.Cargo.Product == p && s.Phase != SupplierPhase.toExit) sum += s.Cargo.Quantity;
                }
                else if (v is Truck t && t.Job != null && t.Job.ToId == siteId && t.Job.Product == p)
                {
                    sum += t.Cargo?.Quantity ?? t.Job.Quantity;
                }
            }
            return sum;
        }

        /// <summary>Ware, die LKW hier schon abholen wollen (noch nicht aufgeladen).</summary>
        public static int ReservedOut(GameState state, int siteId, ProductId p)
        {
            int sum = 0;
            foreach (var v in state.Vehicles)
                if (v is Truck t && t.Job != null && t.Cargo == null && t.Job.FromId == siteId && t.Job.Product == p) sum += t.Job.Quantity;
            return sum;
        }

        /// <summary>Abholbereite Menge (Bestand minus reservierte Abholungen).</summary>
        public static int Available(GameState state, int siteId, ProductId p)
        {
            var stock = PickStock(state, siteId);
            return stock == null ? 0 : Math.Max(0, Of(stock, p) - ReservedOut(state, siteId, p));
        }

        /// <summary>Freier Platz für eine Ware, abzüglich bereits unterwegs befindlicher Lieferungen.</summary>
        public static int FreeSpace(GameState state, int siteId, ProductId p)
        {
            var stock = DropStock(state, siteId, p, out int cap);
            return stock == null ? 0 : Math.Max(0, cap - Of(stock, p) - Incoming(state, siteId, p));
        }
    }
}
