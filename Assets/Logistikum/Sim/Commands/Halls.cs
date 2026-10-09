using System;
using System.Collections.Generic;
using System.Linq;

namespace Logistikum.Sim
{
    /// <summary>Hallen bauen, Bereiche einteilen, Stapler kaufen (M3, T3.1/T3.2/T3.4).</summary>
    public static class Halls
    {
        public static long Cost(Footprint f) => f.Area * HallConfig.CostPerFieldCents;
        public static long AreaCost(Footprint f) => f.Area * HallConfig.AreaCostPerFieldCents;

        public static BuildCheck CheckPlace(GameState state, Footprint f)
        {
            long cost = Cost(f);
            if (!Grid.IsInsideCampus(f)) return new BuildCheck(BuildRejection.outOfBounds, cost);
            if (f.Width < HallConfig.MinSize || f.Depth < HallConfig.MinSize) return new BuildCheck(BuildRejection.tooSmall, cost);
            if (!new Occupancy(state).IsFree(f)) return new BuildCheck(BuildRejection.occupied, cost);
            if (state.Finance.BalanceCents < cost) return new BuildCheck(BuildRejection.insufficientFunds, cost);
            return new BuildCheck(BuildRejection.none, cost);
        }

        /// <summary>Halle aufziehen. ANNAHME: Jede Halle ist ein eigenes Gebäude (angrenzende verschmelzen nicht).</summary>
        public static Hall Place(GameState state, EventBus bus, Cell from, Cell to, out BuildCheck check)
        {
            var f = Grid.RectBetween(from, to);
            check = CheckPlace(state, f);
            if (!check.Ok) return null;
            var hall = new Hall
            {
                Id = state.NextId++, X = f.X, Z = f.Z, Width = f.Width, Depth = f.Depth,
                Gate = Access.SuggestGate(new RoadNetwork(state), new List<Footprint> { f }),
                BuiltTick = state.Tick, PaidCents = check.CostCents,
            };
            state.Halls.Add(hall);
            Build.BookBuild(state, bus, -check.CostCents, Center(hall));
            bus.Emit(new HallChanged { HallId = hall.Id });
            return hall;
        }

        public static Place Center(Hall h) => new Place(h.X + h.Width / 2.0, h.Z + h.Depth / 2.0);

        /// <summary>Erstattung beim Abriss der Halle samt Bereichen; Stapler werden verkauft.</summary>
        public static long Refund(GameState state, Hall h) =>
            Build.DemolishRefund(state, h.BuiltTick, h.PaidCents)
            + h.Areas.Sum(a => Build.DemolishRefund(state, a.BuiltTick, a.PaidCents))
            + h.Forklifts.Sum(f => ForkliftResale(f));

        public static long ForkliftResale(Forklift f) => f.PriceCents * HallConfig.ForkliftResalePercent / 100;

        public static long? Demolish(GameState state, EventBus bus, int hallId)
        {
            var h = state.HallById(hallId);
            if (h == null) return null;
            long refund = Refund(state, h);
            state.Halls.Remove(h);
            Build.BookBuild(state, bus, refund, Center(h));
            bus.Emit(new HallChanged { HallId = hallId });
            return refund;
        }

        public static bool SetGate(GameState state, int hallId, Side gate)
        {
            var h = state.HallById(hallId);
            if (h == null) return false;
            h.Gate = gate;
            return true;
        }

        /// <summary>Halle, in der das Feld liegt; null = keine.</summary>
        public static Hall HallAt(GameState state, int x, int z) => state.Halls.Find(h => h.Rect.Contains(x, z));

        /// <summary>Berührt das Rechteck die Tor-Seite der Halle von innen?</summary>
        public static bool TouchesGate(Hall h, Footprint f)
        {
            switch (h.Gate)
            {
                case Side.N: return f.Z == h.Z;
                case Side.S: return f.Z + f.Depth == h.Z + h.Depth;
                case Side.W: return f.X == h.X;
                default: return f.X + f.Width == h.X + h.Width;
            }
        }

        /// <summary>
        /// Bereich in einer Halle einzeichnen. Wareneingang und Warenausgang müssen am Tor liegen (Frage 8);
        /// Bereiche überlappen nicht.
        /// </summary>
        public static BuildCheck CheckArea(GameState state, AreaKind kind, Footprint f, out Hall hall)
        {
            long cost = AreaCost(f);
            hall = HallAt(state, f.X, f.Z);
            if (hall == null) return new BuildCheck(BuildRejection.notInHall, cost);
            var r = hall.Rect;
            if (f.X + f.Width > r.X + r.Width || f.Z + f.Depth > r.Z + r.Depth) return new BuildCheck(BuildRejection.notInHall, cost);
            foreach (var a in hall.Areas)
                if (a.Rect.X < f.X + f.Width && f.X < a.Rect.X + a.Rect.Width && a.Rect.Z < f.Z + f.Depth && f.Z < a.Rect.Z + a.Rect.Depth)
                    return new BuildCheck(BuildRejection.occupied, cost);
            if ((kind == AreaKind.inbound || kind == AreaKind.outbound) && !TouchesGate(hall, f))
                return new BuildCheck(BuildRejection.notAtGate, cost);
            if (state.Finance.BalanceCents < cost) return new BuildCheck(BuildRejection.insufficientFunds, cost);
            return new BuildCheck(BuildRejection.none, cost);
        }

        public static HallArea PlaceArea(GameState state, EventBus bus, AreaKind kind, Cell from, Cell to, out BuildCheck check)
        {
            var f = Grid.RectBetween(from, to);
            check = CheckArea(state, kind, f, out var hall);
            if (!check.Ok) return null;
            var area = new HallArea { Id = state.NextId++, Kind = kind, Rect = f, BuiltTick = state.Tick, PaidCents = check.CostCents };
            hall.Areas.Add(area);
            Build.BookBuild(state, bus, -check.CostCents, new Place(f.X + f.Width / 2.0, f.Z + f.Depth / 2.0));
            bus.Emit(new HallChanged { HallId = hall.Id });
            return area;
        }

        /// <summary>Bereich entfernen; sein Bestand geht verloren.</summary>
        public static long? DemolishArea(GameState state, EventBus bus, int areaId)
        {
            foreach (var h in state.Halls)
            {
                var a = h.AreaById(areaId);
                if (a == null) continue;
                long refund = Build.DemolishRefund(state, a.BuiltTick, a.PaidCents);
                h.Areas.Remove(a);
                foreach (var f in h.Forklifts)
                    if (f.Job != null && (f.Job.FromId == areaId || f.Job.ToId == areaId)) Forklifts.Reset(f);
                Build.BookBuild(state, bus, refund, new Place(a.Rect.X + a.Rect.Width / 2.0, a.Rect.Z + a.Rect.Depth / 2.0));
                bus.Emit(new HallChanged { HallId = h.Id });
                return refund;
            }
            return null;
        }

        /// <summary>Feld, auf dem neue Stapler starten: innen am Tor, mittig.</summary>
        public static Cell StartCell(Hall h)
        {
            switch (h.Gate)
            {
                case Side.N: return new Cell(h.X + h.Width / 2, h.Z);
                case Side.W: return new Cell(h.X, h.Z + h.Depth / 2);
                case Side.E: return new Cell(h.X + h.Width - 1, h.Z + h.Depth / 2);
                default: return new Cell(h.X + h.Width / 2, h.Z + h.Depth - 1);
            }
        }

        public static Forklift BuyForklift(GameState state, EventBus bus, int hallId, out string rejection)
        {
            rejection = null;
            var h = state.HallById(hallId);
            if (h == null) { rejection = "notFound"; return null; }
            if (state.Finance.BalanceCents < HallConfig.ForkliftPriceCents) { rejection = "insufficientFunds"; return null; }
            var f = new Forklift
            {
                Id = state.NextId++, Route = new List<Cell> { StartCell(h) },
                PriceCents = HallConfig.ForkliftPriceCents, BoughtTick = state.Tick,
            };
            h.Forklifts.Add(f);
            h.WarnedNoForklift = false;
            Ledger.Book(state, bus, BookingCategory.vehicles, -HallConfig.ForkliftPriceCents, Center(h));
            bus.Emit(new HallChanged { HallId = h.Id });
            return f;
        }

        /// <summary>Stapler verkaufen (zum halben Kaufpreis, ANNAHME); seine Ladung geht verloren.</summary>
        public static bool SellForklift(GameState state, EventBus bus, int forkliftId)
        {
            foreach (var h in state.Halls)
            {
                var f = h.Forklifts.Find(x => x.Id == forkliftId);
                if (f == null) continue;
                h.Forklifts.Remove(f);
                Ledger.Book(state, bus, BookingCategory.vehicles, ForkliftResale(f), Center(h));
                bus.Emit(new HallChanged { HallId = h.Id });
                return true;
            }
            return false;
        }
    }

    public sealed class HallChanged : SimEvent { public int HallId; }

    public sealed class PlaceHallCommand : Command
    {
        public int FromX, FromZ, ToX, ToZ;
        public override string Name => "hall/place";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var h = Halls.Place(s, bus, new Cell(FromX, FromZ), new Cell(ToX, ToZ), out var c);
            return h == null ? CommandResult.Fail(c.Reason.ToString()) : CommandResult.Success(h.Id);
        }
    }

    public sealed class DemolishHallCommand : Command
    {
        public int HallId;
        public override string Name => "hall/demolish";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Halls.Demolish(s, bus, HallId) == null ? CommandResult.Fail("notFound") : CommandResult.Success();
    }

    public sealed class SetHallGateCommand : Command
    {
        public int HallId; public Side Gate;
        public override string Name => "hall/setGate";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Halls.SetGate(s, HallId, Gate) ? CommandResult.Success() : CommandResult.Fail("notFound");
    }

    public sealed class PlaceAreaCommand : Command
    {
        public AreaKind Kind; public int FromX, FromZ, ToX, ToZ;
        public override string Name => "area/place";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var a = Halls.PlaceArea(s, bus, Kind, new Cell(FromX, FromZ), new Cell(ToX, ToZ), out var c);
            return a == null ? CommandResult.Fail(c.Reason.ToString()) : CommandResult.Success(a.Id);
        }
    }

    public sealed class DemolishAreaCommand : Command
    {
        public int AreaId;
        public override string Name => "area/demolish";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Halls.DemolishArea(s, bus, AreaId) == null ? CommandResult.Fail("notFound") : CommandResult.Success();
    }

    public sealed class BuyForkliftCommand : Command
    {
        public int HallId;
        public override string Name => "forklift/buy";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var f = Halls.BuyForklift(s, bus, HallId, out var r);
            return f == null ? CommandResult.Fail(r) : CommandResult.Success(f.Id);
        }
    }

    public sealed class SellForkliftCommand : Command
    {
        public int ForkliftId;
        public override string Name => "forklift/sell";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Halls.SellForklift(s, bus, ForkliftId) ? CommandResult.Success() : CommandResult.Fail("notFound");
    }
}
