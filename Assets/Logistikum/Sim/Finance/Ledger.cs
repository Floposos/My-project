using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Buchungskategorien (T1.6).</summary>
    public enum BookingCategory { build, vehicles, operations, rawGoods, exportRevenue }

    public sealed class Booking
    {
        public int Tick;
        public BookingCategory Category;
        /// <summary>Positiv = Einnahme, negativ = Ausgabe.</summary>
        public long AmountCents;
    }

    /// <summary>Summen eines Zeitraums (Tag oder Monat), je Kategorie nach Einnahmen und Ausgaben.</summary>
    public sealed class PeriodTotals
    {
        /// <summary>Tag: Tagesnummer seit Spielbeginn; Monat: Jahr · 12 + Monat − 1.</summary>
        public int Key;
        public Dictionary<BookingCategory, long> IncomeCents = Zero();
        public Dictionary<BookingCategory, long> ExpenseCents = Zero();

        public static Dictionary<BookingCategory, long> Zero()
        {
            var d = new Dictionary<BookingCategory, long>();
            foreach (var c in Ledger.Categories) d[c] = 0;
            return d;
        }

        public long Income(BookingCategory c) => IncomeCents.TryGetValue(c, out var v) ? v : 0;
        public long Expense(BookingCategory c) => ExpenseCents.TryGetValue(c, out var v) ? v : 0;
    }

    public sealed class Finance
    {
        public long BalanceCents;
        /// <summary>Letzte Buchungen, neueste zuletzt (begrenzt).</summary>
        public List<Booking> Recent = new List<Booking>();
        public PeriodTotals Today;
        public PeriodTotals Month;

        public static Finance Create(long balanceCents, int tick) => new Finance
        {
            BalanceCents = balanceCents,
            Today = new PeriodTotals { Key = Ledger.DayKey(tick) },
            Month = new PeriodTotals { Key = Ledger.MonthKey(tick) },
        };
    }

    /// <summary>Kasse: jede Geldbewegung läuft über Book mit Kategorie und optionalem Ort.</summary>
    public static class Ledger
    {
        public static readonly BookingCategory[] Categories =
        {
            BookingCategory.build, BookingCategory.vehicles, BookingCategory.operations,
            BookingCategory.rawGoods, BookingCategory.exportRevenue,
        };

        public static int DayKey(int tick) => GameTime.CalendarAt(tick).DayIndex;

        public static int MonthKey(int tick)
        {
            var c = GameTime.CalendarAt(tick);
            return c.Year * 12 + (c.Month - 1);
        }

        /// <summary>Summen für „heute“ bzw. „diesen Monat“; ein veralteter Zeitraum zählt als leer.</summary>
        public static PeriodTotals Current(Finance f, int tick, bool today)
        {
            int key = today ? DayKey(tick) : MonthKey(tick);
            var totals = today ? f.Today : f.Month;
            return totals != null && totals.Key == key ? totals : new PeriodTotals { Key = key };
        }

        /// <summary>Bucht einen Betrag; meldet Booked (mit Ort) und BalanceChanged.</summary>
        public static void Book(GameState state, EventBus bus, BookingCategory category, long amountCents, Place? at = null)
        {
            if (amountCents == 0) return;
            var f = state.Finance;
            int tick = state.Tick;
            f.BalanceCents += amountCents;
            f.Today = Current(f, tick, true);
            f.Month = Current(f, tick, false);
            foreach (var t in new[] { f.Today, f.Month })
            {
                if (amountCents > 0) t.IncomeCents[category] = t.Income(category) + amountCents;
                else t.ExpenseCents[category] = t.Expense(category) - amountCents;
            }
            f.Recent.Add(new Booking { Tick = tick, Category = category, AmountCents = amountCents });
            if (f.Recent.Count > EconomyConfig.RecentBookings) f.Recent.RemoveAt(0);
            bus.Emit(new Booked { Category = category, AmountCents = amountCents, At = at });
            bus.Emit(new BalanceChanged { BalanceCents = f.BalanceCents, DeltaCents = amountCents });
        }
    }
}
