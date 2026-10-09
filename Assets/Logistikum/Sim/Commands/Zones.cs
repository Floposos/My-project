using System;
using System.Collections.Generic;
using System.Linq;

namespace Logistikum.Sim
{
    public static class Zones
    {
        public static long Cost(ZoneKind kind, Footprint f) => f.Area * ZoneConfig.CostPerFieldCents(kind);

        /// <summary>Zonen gleicher Art, an die das Rechteck mit einer Kante grenzt (werden verschmolzen).</summary>
        public static List<Zone> NeighboursOf(GameState state, ZoneKind kind, Footprint f) =>
            state.Zones.FindAll(z => z.Kind == kind && ZoneShape.Touches(z.Parts, f));

        public static BuildCheck CheckPlace(GameState state, ZoneKind kind, Footprint f)
        {
            long cost = Cost(kind, f);
            if (!Grid.IsInsideCampus(f)) return new BuildCheck(BuildRejection.outOfBounds, cost);
            if (f.Width < ZoneConfig.MinSize || f.Depth < ZoneConfig.MinSize) return new BuildCheck(BuildRejection.tooSmall, cost);
            if (!new Occupancy(state).IsFree(f)) return new BuildCheck(BuildRejection.occupied, cost);
            if (state.Finance.BalanceCents < cost) return new BuildCheck(BuildRejection.insufficientFunds, cost);
            return new BuildCheck(BuildRejection.none, cost);
        }

        /// <summary>Zieht eine Zone auf. Grenzt sie an Zonen gleicher Art, wird alles zu einer Zone.</summary>
        public static BuildCheck Place(GameState state, EventBus bus, ZoneKind kind, Cell from, Cell to, Side? gate = null)
        {
            var f = Grid.RectBetween(from, to);
            var check = CheckPlace(state, kind, f);
            if (!check.Ok) return check;
            var part = new ZonePart(f, state.Tick, check.CostCents);
            var neighbours = NeighboursOf(state, kind, f);
            Build.BookBuild(state, bus, -check.CostCents, new Place(f.X + f.Width / 2.0, f.Z + f.Depth / 2.0));
            if (neighbours.Count > 0)
            {
                var merged = Merge(state, neighbours, part);
                bus.Emit(new ZonePlaced { Id = merged.Id, Kind = kind, CostCents = check.CostCents });
                return check;
            }
            int id = state.NextId++;
            var stock = new Dictionary<ProductId, int>();
            foreach (var p in ZoneTypes.Stores(kind)) stock[p] = 0;
            var side = gate ?? Access.SuggestGate(new RoadNetwork(state), new List<Footprint> { f });
            state.Zones.Add(new Zone { Id = id, Kind = kind, Parts = new List<ZonePart> { part }, Gate = side, Stock = stock });
            bus.Emit(new ZonePlaced { Id = id, Kind = kind, CostCents = check.CostCents });
            return check;
        }

        /// <summary>
        /// Verschmilzt angrenzende Zonen gleicher Art mit einem neuen Rechteck. ANNAHME (0.2.1): Nummer und
        /// Tor der größten Zone bleiben; Bestand und Fortschritt werden zusammengezählt; Verweise zeigen danach
        /// auf die verschmolzene Zone.
        /// </summary>
        public static Zone Merge(GameState state, List<Zone> neighbours, ZonePart part)
        {
            var keep = neighbours[0];
            foreach (var b in neighbours.Skip(1))
            {
                int ab = ZoneShape.Area(b.Parts), ak = ZoneShape.Area(keep.Parts);
                if (ab > ak || (ab == ak && b.Id < keep.Id)) keep = b;
            }
            var others = neighbours.Where(z => z != keep).ToList();
            foreach (var o in others)
            {
                keep.Parts.AddRange(o.Parts);
                keep.Work += o.Work;
                foreach (var kv in o.Stock) Stock.Add(keep.Stock, kv.Key, kv.Value);
            }
            keep.Parts.Add(part);
            var removed = new HashSet<int>(others.Select(z => z.Id));
            state.Zones.RemoveAll(z => removed.Contains(z.Id));
            int Remap(int id) => removed.Contains(id) ? keep.Id : id;
            foreach (var v in state.Vehicles)
            {
                if (v is Supplier s) s.TargetId = Remap(s.TargetId);
                else if (v is Truck t && t.Job != null) { t.Job.FromId = Remap(t.Job.FromId); t.Job.ToId = Remap(t.Job.ToId); }
            }
            foreach (var tour in state.Tours) foreach (var stop in tour.Stops) stop.SiteId = Remap(stop.SiteId);
            foreach (var c in state.Conveyors) { c.FromSiteId = Remap(c.FromSiteId); c.ToSiteId = Remap(c.ToSiteId); }
            return keep;
        }

        public static bool SetGate(GameState state, int id, Side gate)
        {
            var zone = state.ZoneById(id);
            if (zone == null) return false;
            zone.Gate = gate;
            return true;
        }

        /// <summary>Erstattung beim Abriss: jeder Teil nach seinem eigenen Bautag.</summary>
        public static long Refund(GameState state, Zone zone) => zone.Parts.Sum(p => Build.DemolishRefund(state, p.BuiltTick, p.PaidCents));

        /// <summary>Reißt eine Zone ab; ihr Bestand geht verloren.</summary>
        public static long? Demolish(GameState state, EventBus bus, int id)
        {
            var zone = state.ZoneById(id);
            if (zone == null) return null;
            long refund = Refund(state, zone);
            state.Zones.Remove(zone);
            Build.BookBuild(state, bus, refund, ZoneShape.Center(zone.Parts));
            bus.Emit(new ZoneDemolished { Id = id, RefundCents = refund });
            return refund;
        }

        /// <summary>Teilt einen Teil um ein Feld herum in bis zu vier Rechtecke; Preis anteilig nach Fläche.</summary>
        static List<ZonePart> CutCell(ZonePart part, int x, int z, out long cellPaid)
        {
            var rects = new[]
            {
                new Footprint(part.X, part.Z, part.Width, z - part.Z),
                new Footprint(part.X, z + 1, part.Width, part.Z + part.Depth - z - 1),
                new Footprint(part.X, z, x - part.X, 1),
                new Footprint(x + 1, z, part.X + part.Width - x - 1, 1),
            }.Where(r => r.Width > 0 && r.Depth > 0);
            long area = part.Area;
            var rest = rects.Select(r => new ZonePart(r, part.BuiltTick, part.PaidCents * r.Area / area)).ToList();
            cellPaid = part.PaidCents - rest.Sum(p => p.PaidCents);
            return rest;
        }

        /// <summary>Zusammenhängende Gruppen von Teilen, größte zuerst.</summary>
        static List<List<ZonePart>> Components(List<ZonePart> parts)
        {
            var groups = new List<List<ZonePart>>();
            var seen = new HashSet<ZonePart>();
            foreach (var start in parts)
            {
                if (seen.Contains(start)) continue;
                var group = new List<ZonePart> { start };
                seen.Add(start);
                for (int i = 0; i < group.Count; i++)
                    foreach (var p in parts)
                        if (!seen.Contains(p) && ZoneShape.SharesEdge(group[i], p)) { seen.Add(p); group.Add(p); }
                groups.Add(group);
            }
            // Stabil sortieren (wie Array.sort in der Browser-Version).
            return groups.Select((g, i) => (g, i)).OrderByDescending(e => ZoneShape.Area(e.g)).ThenBy(e => e.i).Select(e => e.g).ToList();
        }

        /// <summary>
        /// Teilt die Zone nach dem Abriss auf, falls sie zerfallen ist. ANNAHME (08.10.2026): Das größte Stück
        /// behält Nummer, Tor und Fortschritt; der Bestand wird nach Fläche aufgeteilt, Überschuss geht verloren.
        /// </summary>
        static void Split(GameState state, Zone zone)
        {
            var groups = Components(zone.Parts);
            int total = ZoneShape.Area(zone.Parts);
            var stock = new Dictionary<ProductId, int>(zone.Stock);
            var pieces = new List<Zone>();
            for (int i = 0; i < groups.Count; i++)
            {
                var piece = i == 0 ? zone : new Zone { Id = state.NextId++, Kind = zone.Kind, Gate = zone.Gate };
                piece.Parts = groups[i];
                pieces.Add(piece);
            }
            foreach (var piece in pieces) piece.Stock = new Dictionary<ProductId, int>();
            foreach (var p in Products.All)
            {
                if (!stock.TryGetValue(p, out int amount)) continue;
                int left = amount;
                for (int i = 0; i < pieces.Count; i++)
                {
                    int area = ZoneShape.Area(pieces[i].Parts);
                    int share = i == pieces.Count - 1 ? left : amount * area / total;
                    pieces[i].Stock[p] = Math.Min(area * ZoneConfig.CapacityPerField, share);
                    left -= share;
                }
            }
            state.Zones.AddRange(pieces.Skip(1));
        }

        /// <summary>Erstattung, die der Abriss dieses Felds bringen würde; null = kein Feld der Zone.</summary>
        public static long? CellRefund(GameState state, Zone zone, int x, int z)
        {
            var part = zone.Parts.Find(p => p.Contains(x, z));
            if (part == null) return null;
            CutCell(part, x, z, out long paid);
            return Build.DemolishRefund(state, part.BuiltTick, paid);
        }

        /// <summary>Reißt ein einzelnes Feld einer Zone ab. Liefert die Erstattung oder null.</summary>
        public static long? DemolishCell(GameState state, EventBus bus, int zoneId, int x, int z)
        {
            var zone = state.ZoneById(zoneId);
            var part = zone?.Parts.Find(p => p.Contains(x, z));
            if (part == null) return null;
            var rest = CutCell(part, x, z, out long cellPaid);
            int index = zone.Parts.IndexOf(part);
            zone.Parts.RemoveAt(index);
            zone.Parts.InsertRange(index, rest);
            long refund = Build.DemolishRefund(state, part.BuiltTick, cellPaid);
            Build.BookBuild(state, bus, refund, new Place(x + 0.5, z + 0.5));
            if (zone.Parts.Count == 0)
            {
                state.Zones.Remove(zone);
                bus.Emit(new ZoneDemolished { Id = zoneId, RefundCents = refund });
                return refund;
            }
            Split(state, zone);
            bus.Emit(new ZoneCellDemolished { Id = zoneId, X = x, Z = z, RefundCents = refund });
            return refund;
        }
    }

    public sealed class PlaceZoneCommand : Command
    {
        public ZoneKind Kind; public int FromX, FromZ, ToX, ToZ; public Side? Gate;
        public override string Name => "zone/place";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var c = Zones.Place(s, bus, Kind, new Cell(FromX, FromZ), new Cell(ToX, ToZ), Gate);
            return c.Ok ? CommandResult.Success() : CommandResult.Fail(c.Reason.ToString());
        }
    }

    public sealed class SetZoneGateCommand : Command
    {
        public int ZoneId; public Side Gate;
        public override string Name => "zone/setGate";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Zones.SetGate(s, ZoneId, Gate) ? CommandResult.Success() : CommandResult.Fail("notFound");
    }

    public sealed class DemolishZoneCommand : Command
    {
        public int ZoneId;
        public override string Name => "zone/demolish";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Zones.Demolish(s, bus, ZoneId) == null ? CommandResult.Fail("notFound") : CommandResult.Success();
    }

    public sealed class DemolishZoneCellCommand : Command
    {
        public int ZoneId, X, Z;
        public override string Name => "zone/demolishCell";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Zones.DemolishCell(s, bus, ZoneId, X, Z) == null ? CommandResult.Fail("notFound") : CommandResult.Success();
    }

    public sealed class CreateOrderCommand : Command
    {
        public ProductId Product; public int Quantity; public OrderInterval Interval;
        public override string Name => "order/create";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var o = Orders.Create(s, Product, Quantity, Interval);
            return o == null ? CommandResult.Fail("invalidOrder") : CommandResult.Success(o.Id);
        }
    }

    public sealed class CancelOrderCommand : Command
    {
        public int OrderId;
        public override string Name => "order/cancel";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Orders.Cancel(s, OrderId) ? CommandResult.Success() : CommandResult.Fail("notFound");
    }
}
