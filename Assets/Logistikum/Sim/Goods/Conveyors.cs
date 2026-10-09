using System;
using System.Collections.Generic;
using System.Linq;

namespace Logistikum.Sim
{
    public sealed class ConveyorCheck
    {
        public BuildRejection Reason;
        public List<Cell> Cells = new List<Cell>();
        public List<Cell> Blocked = new List<Cell>();
        public int FromSiteId, ToSiteId;
        public long CostCents;
        public bool Ok => Reason == BuildRejection.none;
    }

    public sealed class ConveyorChanged : SimEvent { public int ConveyorId; }

    /// <summary>
    /// Förderbänder (M3, T3.5): auf dem Raster gezogen wie Straßen, eine Ebene, keine Kreuzungen. Der Anfang
    /// grenzt an die Quelle (Zone oder Halle), das Ende an das Ziel (Zone, Halle oder Export-Ausfahrt).
    /// Kisten laufen mit festem Tempo und Abstand; ein volles Ziel staut das Band.
    /// </summary>
    public static class Conveyors
    {
        /// <summary>Ort auf einem Feld: Zone, Halle oder (nur als Ziel) Export-Ausfahrt; 0 = keiner.</summary>
        static int SiteAt(GameState state, Occupancy occ, Cell c, bool asTarget)
        {
            int id = occ.At(c.X, c.Z);
            if (id <= 0) return 0;
            if (state.ZoneById(id) != null || state.HallById(id) != null) return id;
            var b = state.BuildingById(id);
            return asTarget && b != null && b.Type == BuildingTypeId.exportExit ? id : 0;
        }

        static int SiteBeside(GameState state, Occupancy occ, Cell c, Cell? exclude, bool asTarget, int notId)
        {
            for (int d = 0; d < 4; d++)
            {
                var n = new Cell(c.X + RoadNetwork.Dx[d], c.Z + RoadNetwork.Dz[d]);
                if (exclude.HasValue && n == exclude.Value) continue;
                int id = SiteAt(state, occ, n, asTarget);
                if (id != 0 && id != notId) return id;
            }
            return 0;
        }

        public static ConveyorCheck CheckBuild(GameState state, Cell from, Cell to, bool xFirst)
        {
            var occ = new Occupancy(state);
            var check = new ConveyorCheck { Cells = RoadLine.Line(from, to, xFirst) };
            check.CostCents = check.Cells.Count * HallConfig.ConveyorCostPerFieldCents;
            foreach (var c in check.Cells)
                if (!Grid.InBounds(c.X, c.Z) || occ.At(c.X, c.Z) != 0) check.Blocked.Add(c);
            if (check.Blocked.Count > 0)
            {
                check.Reason = check.Blocked.Any(c => !Grid.InBounds(c.X, c.Z)) ? BuildRejection.outOfBounds : BuildRejection.occupied;
                return check;
            }
            var cells = check.Cells;
            check.FromSiteId = SiteBeside(state, occ, cells[0], cells.Count > 1 ? cells[1] : (Cell?)null, false, 0);
            if (check.FromSiteId == 0) { check.Reason = BuildRejection.noSource; return check; }
            check.ToSiteId = SiteBeside(state, occ, cells[cells.Count - 1], cells.Count > 1 ? cells[cells.Count - 2] : (Cell?)null, true, check.FromSiteId);
            if (check.ToSiteId == 0) { check.Reason = BuildRejection.noTarget; return check; }
            if (state.Finance.BalanceCents < check.CostCents) check.Reason = BuildRejection.insufficientFunds;
            return check;
        }

        public static Conveyor Build(GameState state, EventBus bus, Cell from, Cell to, bool xFirst, out ConveyorCheck check)
        {
            check = CheckBuild(state, from, to, xFirst);
            if (!check.Ok) return null;
            var c = new Conveyor
            {
                Id = state.NextId++, Cells = check.Cells, FromSiteId = check.FromSiteId, ToSiteId = check.ToSiteId,
                BuiltTick = state.Tick, PaidCents = check.CostCents,
            };
            state.Conveyors.Add(c);
            var mid = c.Cells[c.Cells.Count / 2];
            Sim.Build.BookBuild(state, bus, -check.CostCents, new Place(mid.X + 0.5, mid.Z + 0.5));
            bus.Emit(new ConveyorChanged { ConveyorId = c.Id });
            return c;
        }

        public static Conveyor At(GameState state, int x, int z) => state.Conveyors.Find(c => c.Cells.Exists(k => k.X == x && k.Z == z));

        /// <summary>Ganzes Band abreißen; Kisten darauf gehen verloren.</summary>
        public static long? Demolish(GameState state, EventBus bus, int x, int z)
        {
            var c = At(state, x, z);
            if (c == null) return null;
            long refund = Sim.Build.DemolishRefund(state, c.BuiltTick, c.PaidCents);
            state.Conveyors.Remove(c);
            Sim.Build.BookBuild(state, bus, refund, new Place(x + 0.5, z + 0.5));
            bus.Emit(new ConveyorChanged { ConveyorId = c.Id });
            return refund;
        }

        /// <summary>Waren, die eine Quelle ans Band abgibt (in Reihenfolge der Vorliebe).</summary>
        public static ProductId[] Outputs(GameState state, int siteId)
        {
            var zone = state.ZoneById(siteId);
            if (zone != null)
            {
                switch (zone.Kind)
                {
                    case ZoneKind.A: return new[] { ProductId.rawA };
                    case ZoneKind.B: return new[] { ProductId.combo };
                    case ZoneKind.C: return new[] { ProductId.final };
                    default: return new ProductId[0];
                }
            }
            return state.HallById(siteId) != null ? HallProcessing.HallProducts : new ProductId[0];
        }

        static bool TargetAccepts(GameState state, int siteId, ProductId p)
        {
            if (state.BuildingById(siteId) != null) return Export.Price(p) != null;
            return Stock.DropStock(state, siteId, p, out _) != null;
        }

        static bool TryDeliver(GameState state, EventBus bus, int siteId, ProductId p)
        {
            if (state.BuildingById(siteId) != null) return Export.SellAtExit(state, bus, siteId, p, 1) != null;
            var stock = Stock.DropStock(state, siteId, p, out _);
            if (stock == null || Stock.FreeSpace(state, siteId, p) < 1) return false;
            Stock.Add(stock, p, 1);
            bus.Emit(new GoodsDelivered { SiteId = siteId, Product = p, Quantity = 1 });
            return true;
        }

        public static bool IsStalled(Conveyor c) =>
            c.Items.Count > 0 && c.Items[0].Position >= c.Cells.Count * Movement.CellUnits;

        public static void Update(GameState state, EventBus bus)
        {
            foreach (var c in state.Conveyors)
            {
                int length = c.Cells.Count * Movement.CellUnits;
                // Vorderste Kiste zuerst (Liste ist nach Position absteigend geordnet).
                for (int i = 0; i < c.Items.Count; i++)
                {
                    var item = c.Items[i];
                    int limit = i == 0 ? length : c.Items[i - 1].Position - HallConfig.BeltSpacing;
                    item.Position = Math.Max(item.Position, Math.Min(item.Position + HallConfig.BeltSpeed, limit));
                }
                if (c.Items.Count > 0 && c.Items[0].Position >= length && TryDeliver(state, bus, c.ToSiteId, c.Items[0].Product))
                    c.Items.RemoveAt(0);
                if (c.Cooldown > 0) { c.Cooldown--; continue; }
                if (c.Items.Count > 0 && c.Items[c.Items.Count - 1].Position < HallConfig.BeltSpacing) continue;
                var source = Stock.PickStock(state, c.FromSiteId);
                if (source == null) continue;
                foreach (var p in Outputs(state, c.FromSiteId))
                {
                    if (Stock.Available(state, c.FromSiteId, p) < 1 || !TargetAccepts(state, c.ToSiteId, p)) continue;
                    Stock.Add(source, p, -1);
                    c.Items.Add(new BeltItem { Product = p, Position = 0 });
                    c.Cooldown = HallConfig.BeltPickTicks;
                    break;
                }
            }
        }
    }

    public sealed class BuildConveyorCommand : Command
    {
        public int FromX, FromZ, ToX, ToZ; public bool XFirst;
        public override string Name => "conveyor/build";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var c = Conveyors.Build(s, bus, new Cell(FromX, FromZ), new Cell(ToX, ToZ), XFirst, out var check);
            return c == null ? CommandResult.Fail(check.Reason.ToString()) : CommandResult.Success(c.Id);
        }
    }

    public sealed class DemolishConveyorCommand : Command
    {
        public int X, Z;
        public override string Name => "conveyor/demolish";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Conveyors.Demolish(s, bus, X, Z) == null ? CommandResult.Fail("notFound") : CommandResult.Success();
    }
}
