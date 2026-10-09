using System;

namespace Logistikum.Sim
{
    /// <summary>Flotte (T2.5): Werte je Typ und Antrieb, Kauf, Leasing, Verkauf.</summary>
    public static class Fleet
    {
        public static ModelValues Values(VehicleModel model, VehicleDrive drive)
        {
            var v = VehicleConfig.Model(model);
            v.PriceCents = (long)Math.Round(v.PriceCents * VehicleConfig.PricePercent(drive) / 100.0, MidpointRounding.AwayFromZero);
            v.CostPerKmCents = (long)Math.Round(v.CostPerKmCents * VehicleConfig.PerKmPercent(drive) / 100.0, MidpointRounding.AwayFromZero);
            return v;
        }

        public static ModelValues ValuesOf(Truck t) => Values(t.Model, t.Drive);

        public static long LeaseMonthlyCents(long priceCents) =>
            (long)Math.Round(priceCents * LeaseConfig.MonthlyPermille / 1000.0, MidpointRounding.AwayFromZero);

        static int AddMonths(int tick, int months)
        {
            for (int i = 0; i < months; i++) tick = GameTime.AddOneMonth(tick);
            return tick;
        }

        static int MonthsSince(int from, int to)
        {
            int months = 0;
            for (int t = GameTime.AddOneMonth(from); t <= to; t = GameTime.AddOneMonth(t)) months++;
            return months;
        }

        /// <summary>Restwert beim Verkauf eines gekauften Fahrzeugs.</summary>
        public static long ResidualCents(GameState state, Truck t)
        {
            int permille = Math.Max(LeaseConfig.ResidualMinPercent * 10,
                LeaseConfig.ResidualStartPercent * 10 - MonthsSince(t.BoughtTick, state.Tick) * LeaseConfig.ResidualLossPerMonthPermille);
            return (long)Math.Round(t.PriceCents * permille / 1000.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Strafe bei vorzeitiger Rückgabe: bis zu EarlyReturnPenaltyMonths offene Raten.</summary>
        public static long ReturnPenaltyCents(Truck t)
        {
            if (t.Lease == null) return 0;
            int open = 0;
            for (int p = t.Lease.NextPaymentTick; p < t.Lease.EndTick; p = GameTime.AddOneMonth(p)) open++;
            return Math.Min(open, LeaseConfig.EarlyReturnPenaltyMonths) * t.Lease.MonthlyCents;
        }

        /// <summary>Was das Abgeben bringt (+ Verkauf) bzw. kostet (− Leasing-Rückgabe).</summary>
        public static long DisposalCents(GameState state, Truck t) => t.Lease != null ? -ReturnPenaltyCents(t) : ResidualCents(state, t);

        public static long AcquisitionCents(VehicleModel model, VehicleDrive drive, bool lease)
        {
            long price = Values(model, drive).PriceCents;
            return lease ? LeaseMonthlyCents(price) : price;
        }

        static Place EntrancePlace => new Place(RoadNetwork.Entrance.X + 1, RoadNetwork.Entrance.Z + 0.5);

        /// <summary>Kauft oder least ein Fahrzeug; es erscheint an der Einfahrt und startet in der Automatik.</summary>
        public static Truck Buy(GameState state, EventBus bus, VehicleModel model, VehicleDrive drive, bool lease, out string rejection)
        {
            rejection = null;
            long cost = AcquisitionCents(model, drive, lease);
            if (state.Finance.BalanceCents < cost)
            {
                rejection = "insufficientFunds";
                return null;
            }
            long price = Values(model, drive).PriceCents;
            var truck = new Truck
            {
                Id = state.NextId++, Route = new System.Collections.Generic.List<Cell> { RoadNetwork.Entrance }, OffRoad = true,
                Phase = TruckPhase.idle, Model = model, Drive = drive, PriceCents = price, BoughtTick = state.Tick,
                Lease = lease ? NewLease(state, price) : null,
            };
            state.Vehicles.Add(truck);
            Ledger.Book(state, bus, BookingCategory.vehicles, -cost, EntrancePlace);
            bus.Emit(new VehicleBought { Id = truck.Id });
            return truck;
        }

        /// <summary>Fahrzeug verkaufen bzw. Leasing zurückgeben; es verschwindet sofort.</summary>
        public static bool Dispose(GameState state, EventBus bus, int truckId)
        {
            if (!(state.VehicleById(truckId) is Truck t)) return false;
            long amount = DisposalCents(state, t);
            if (amount != 0) Ledger.Book(state, bus, BookingCategory.vehicles, amount, EntrancePlace);
            state.Vehicles.Remove(t);
            bus.Emit(new VehicleDisposed { Id = truckId, AmountCents = amount });
            return true;
        }

        /// <summary>Leasingraten am Fälligkeitstag; am Laufzeitende verlängert sich der Vertrag (Meldung).</summary>
        public static void UpdateLeases(GameState state, EventBus bus)
        {
            foreach (var v in state.Vehicles)
            {
                if (!(v is Truck t) || t.Lease == null) continue;
                var lease = t.Lease;
                if (state.Tick >= lease.EndTick)
                {
                    lease.EndTick = AddMonths(lease.EndTick, LeaseConfig.TermMonths);
                    var at = t.Here;
                    Notices.Add(state, bus, NoticeKind.leaseRenewed, at.X, at.Z, t.Id);
                }
                if (state.Tick >= lease.NextPaymentTick)
                {
                    Ledger.Book(state, bus, BookingCategory.vehicles, -lease.MonthlyCents);
                    lease.NextPaymentTick = GameTime.AddOneMonth(lease.NextPaymentTick);
                }
            }
        }

        public static Lease NewLease(GameState state, long price) => new Lease
        {
            MonthlyCents = LeaseMonthlyCents(price),
            NextPaymentTick = GameTime.AddOneMonth(state.Tick),
            EndTick = AddMonths(state.Tick, LeaseConfig.TermMonths),
        };
    }
}
