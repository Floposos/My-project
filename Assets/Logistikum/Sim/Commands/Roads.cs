using System.Collections.Generic;

namespace Logistikum.Sim
{
    public sealed class RoadCheck
    {
        public BuildRejection Reason;
        public List<Cell> NewCells = new List<Cell>();
        public List<Cell> Blocked = new List<Cell>();
        public long CostCents;
        public bool Ok => Reason == BuildRejection.none;
    }

    public static class Roads
    {
        /// <summary>
        /// Prüft eine gezogene Straße. Vorhandene Straßenfelder werden übernommen und kosten nichts;
        /// liegt ein Feld außerhalb oder auf einem Gebäude, wird die ganze Straße abgelehnt.
        /// </summary>
        public static RoadCheck CheckBuild(GameState state, Cell from, Cell to, bool xFirst)
        {
            var occ = new Occupancy(state);
            var check = new RoadCheck();
            var outside = new List<Cell>();
            var occupied = new List<Cell>();
            foreach (var c in RoadLine.Line(from, to, xFirst))
            {
                if (!Grid.InBounds(c.X, c.Z)) outside.Add(c);
                else
                {
                    int o = occ.At(c.X, c.Z);
                    if (o == 0) check.NewCells.Add(c);
                    else if (o != Occupancy.RoadCell) occupied.Add(c);
                }
            }
            check.CostCents = check.NewCells.Count * BuildConfig.RoadCostPerTileCents;
            if (outside.Count > 0) { check.Reason = BuildRejection.outOfBounds; check.Blocked = outside; }
            else if (occupied.Count > 0) { check.Reason = BuildRejection.occupied; check.Blocked = occupied; }
            else if (state.Finance.BalanceCents < check.CostCents) check.Reason = BuildRejection.insufficientFunds;
            return check;
        }

        public static RoadCheck BuildRoad(GameState state, EventBus bus, Cell from, Cell to, bool xFirst)
        {
            var check = CheckBuild(state, from, to, xFirst);
            if (!check.Ok || check.NewCells.Count == 0) return check;
            foreach (var c in check.NewCells)
                state.Roads.Add(new RoadTile { X = c.X, Z = c.Z, BuiltTick = state.Tick, PaidCents = BuildConfig.RoadCostPerTileCents });
            var mid = check.NewCells[check.NewCells.Count / 2];
            Build.BookBuild(state, bus, -check.CostCents, new Place(mid.X + 0.5, mid.Z + 0.5));
            bus.Emit(new RoadBuilt { Cells = check.NewCells, CostCents = check.CostCents });
            return check;
        }

        /// <summary>Erstattung für das Straßenfeld (x, z) oder null.</summary>
        public static long? RefundAt(GameState state, int x, int z)
        {
            var t = state.Roads.Find(r => r.X == x && r.Z == z);
            return t == null ? (long?)null : Build.DemolishRefund(state, t.BuiltTick, t.PaidCents);
        }

        public static long? Demolish(GameState state, EventBus bus, int x, int z)
        {
            var t = state.Roads.Find(r => r.X == x && r.Z == z);
            if (t == null) return null;
            long refund = Build.DemolishRefund(state, t.BuiltTick, t.PaidCents);
            state.Roads.Remove(t);
            Build.BookBuild(state, bus, refund, new Place(x + 0.5, z + 0.5));
            bus.Emit(new RoadDemolished { X = x, Z = z, RefundCents = refund });
            return refund;
        }

        /// <summary>Straßenfelder auf der gezogenen Strecke (andere Felder werden übersprungen).</summary>
        public static List<RoadTile> TilesOnLine(GameState state, Cell from, Cell to, bool xFirst)
        {
            var byKey = new Dictionary<int, RoadTile>();
            foreach (var r in state.Roads) byKey[RoadNetwork.CellKey(r.X, r.Z)] = r;
            var list = new List<RoadTile>();
            foreach (var c in RoadLine.Line(from, to, xFirst))
                if (byKey.TryGetValue(RoadNetwork.CellKey(c), out var t)) list.Add(t);
            return list;
        }

        /// <summary>Vorfahrtsstraße markieren bzw. entfernen (T2.2, kostenlos). null = keine Straße.</summary>
        public static int? SetPriority(GameState state, EventBus bus, Cell from, Cell to, bool xFirst, bool priority)
        {
            var tiles = TilesOnLine(state, from, to, xFirst);
            if (tiles.Count == 0) return null;
            var changed = tiles.FindAll(t => t.Priority != priority);
            foreach (var t in changed) t.Priority = priority;
            if (changed.Count > 0)
                bus.Emit(new RoadPriorityChanged { Cells = changed.ConvertAll(t => new Cell(t.X, t.Z)), Priority = priority });
            return changed.Count;
        }
    }

    public sealed class BuildRoadCommand : Command
    {
        public int FromX, FromZ, ToX, ToZ; public bool XFirst;
        public override string Name => "road/build";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var c = Roads.BuildRoad(s, bus, new Cell(FromX, FromZ), new Cell(ToX, ToZ), XFirst);
            return c.Ok ? CommandResult.Success() : CommandResult.Fail(c.Reason.ToString());
        }
    }

    public sealed class DemolishRoadCommand : Command
    {
        public int X, Z;
        public override string Name => "road/demolish";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Roads.Demolish(s, bus, X, Z) == null ? CommandResult.Fail("notFound") : CommandResult.Success();
    }

    public sealed class SetPriorityCommand : Command
    {
        public int FromX, FromZ, ToX, ToZ; public bool XFirst, Priority;
        public override string Name => "road/setPriority";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Roads.SetPriority(s, bus, new Cell(FromX, FromZ), new Cell(ToX, ToZ), XFirst, Priority) == null
                ? CommandResult.Fail("noRoad") : CommandResult.Success();
    }
}
