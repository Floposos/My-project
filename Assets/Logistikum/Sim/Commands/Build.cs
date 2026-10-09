namespace Logistikum.Sim
{
    /// <summary>Gründe, warum etwas nicht gebaut werden kann (Texte in der Oberfläche).</summary>
    public enum BuildRejection { none, outOfBounds, occupied, insufficientFunds, unknownType, tooSmall, notAtEdge, notInHall, notAtGate, noSource, noTarget, notFound }

    public struct BuildCheck
    {
        public BuildRejection Reason;
        public long CostCents;
        public bool Ok => Reason == BuildRejection.none;
        public BuildCheck(BuildRejection reason, long cost) { Reason = reason; CostCents = cost; }
    }

    public static class Build
    {
        public static long BuildingCost(BuildingTypeId type) => BuildConfig.BuildingCostCents[type];

        /// <summary>Prüft, ob ein Gebäude an (x, z) gebaut werden kann. Für Vorschau und Befehl identisch.</summary>
        public static BuildCheck CheckPlaceBuilding(GameState state, BuildingTypeId type, int x, int z)
        {
            long cost = BuildingCost(type);
            var t = BuildingTypes.Get(type);
            var f = new Footprint(x, z, t.Width, t.Depth);
            if (!Grid.IsInsideCampus(f)) return new BuildCheck(BuildRejection.outOfBounds, cost);
            if (t.AtEdge && !Grid.TouchesEdge(f)) return new BuildCheck(BuildRejection.notAtEdge, cost);
            if (!new Occupancy(state).IsFree(f)) return new BuildCheck(BuildRejection.occupied, cost);
            if (state.Finance.BalanceCents < cost) return new BuildCheck(BuildRejection.insufficientFunds, cost);
            return new BuildCheck(BuildRejection.none, cost);
        }

        /// <summary>Erstattung beim Abriss: 100 % am selben Spieltag, danach 50 % (auf Cent gerundet).</summary>
        public static long DemolishRefund(GameState state, int builtTick, long paidCents)
        {
            bool sameDay = builtTick / GameTime.TicksPerDay == state.Tick / GameTime.TicksPerDay;
            double share = sameDay ? BuildConfig.RefundSameDay : BuildConfig.RefundLater;
            return (long)System.Math.Round(paidCents * share, System.MidpointRounding.AwayFromZero);
        }

        public static BuildCheck PlaceBuilding(GameState state, EventBus bus, BuildingTypeId type, int x, int z)
        {
            var check = CheckPlaceBuilding(state, type, x, z);
            if (!check.Ok) return check;
            int id = state.NextId++;
            state.Buildings.Add(new Building { Id = id, Type = type, X = x, Z = z, BuiltTick = state.Tick, PaidCents = check.CostCents });
            var t = BuildingTypes.Get(type);
            BookBuild(state, bus, -check.CostCents, new Place(x + t.Width / 2.0, z + t.Depth / 2.0));
            bus.Emit(new BuildingPlaced { Id = id, Type = type, X = x, Z = z, CostCents = check.CostCents });
            return check;
        }

        public static long? DemolishBuilding(GameState state, EventBus bus, int id)
        {
            var b = state.BuildingById(id);
            if (b == null) return null;
            long refund = DemolishRefund(state, b.BuiltTick, b.PaidCents);
            state.Buildings.Remove(b);
            var f = Buildings.FootprintOf(b);
            BookBuild(state, bus, refund, new Place(f.X + f.Width / 2.0, f.Z + f.Depth / 2.0));
            bus.Emit(new BuildingDemolished { Id = id, RefundCents = refund });
            return refund;
        }

        /// <summary>Bucht Baukosten bzw. Erstattungen (Kategorie „Bau“) mit Ort.</summary>
        public static void BookBuild(GameState state, EventBus bus, long amountCents, Place at) =>
            Ledger.Book(state, bus, BookingCategory.build, amountCents, at);
    }

    public sealed class PlaceBuildingCommand : Command
    {
        public BuildingTypeId Type; public int X, Z;
        public override string Name => "build/place";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var c = Build.PlaceBuilding(s, bus, Type, X, Z);
            return c.Ok ? CommandResult.Success(s.NextId - 1) : CommandResult.Fail(c.Reason.ToString());
        }
    }

    public sealed class DemolishBuildingCommand : Command
    {
        public int BuildingId;
        public override string Name => "build/demolish";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Build.DemolishBuilding(s, bus, BuildingId) == null ? CommandResult.Fail("notFound") : CommandResult.Success();
    }

    /// <summary>Entwicklerbefehl (nicht in der Oberfläche): Kontostand ändern.</summary>
    public sealed class AdjustBalanceCommand : Command
    {
        public long DeltaCents;
        public override string Name => "finance/adjustBalance";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            Ledger.Book(s, bus, BookingCategory.operations, DeltaCents);
            return CommandResult.Success();
        }
    }
}
