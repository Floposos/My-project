using System;
using System.Collections.Generic;
using System.Linq;

namespace Logistikum.Sim
{
    /// <summary>
    /// Gabelstapler (M3, T3.4): holen Ware dort, wo sie wartet, und bringen sie zur nächsten Stufe. Vorrang
    /// hat, was ein volles Lager freimacht. Sie fahren über Hallenfelder, je Feld nur einer; wer wartet,
    /// weicht nach kurzer Zeit aus.
    /// </summary>
    public static class Forklifts
    {
        public static void Reset(Forklift f)
        {
            f.Job = null;
            f.Phase = ForkliftPhase.idle;
            f.Timer = 1;
            if (f.Route.Count > 1 && f.Progress == 0) f.Route.RemoveRange(1, f.Route.Count - 1);
        }

        static int Key(Cell c) => RoadNetwork.CellKey(c);

        public static Cell CenterCell(HallArea a) => new Cell(a.Rect.X + (a.Rect.Width - 1) / 2, a.Rect.Z + (a.Rect.Depth - 1) / 2);

        /// <summary>Nächstes freies Feld des Bereichs (nicht von einem anderen Stapler belegt).</summary>
        static Cell TargetCell(HallArea a, Forklift f, Cell from, Dictionary<int, int> claimed)
        {
            Cell best = CenterCell(a);
            int bestD = int.MaxValue;
            foreach (var c in Grid.Cells(a.Rect))
            {
                if (claimed != null && claimed.TryGetValue(Key(c), out int holder) && holder != f.Id) continue;
                int d = Math.Abs(c.X - from.X) + Math.Abs(c.Z - from.Z);
                if (d < bestD) { best = c; bestD = d; }
            }
            return best;
        }

        static HashSet<int> OthersClaimed(Forklift f, Dictionary<int, int> claimed)
        {
            var set = new HashSet<int>();
            foreach (var kv in claimed) if (kv.Value != f.Id) set.Add(kv.Key);
            return set;
        }

        /// <summary>Ware, die Stapler zu diesem Bereich bringen (geladen oder fest eingeplant).</summary>
        public static int Incoming(Hall h, int areaId, ProductId p) =>
            h.Forklifts.Where(f => f.Job != null && f.Job.ToId == areaId && f.Job.Product == p).Sum(f => f.Cargo?.Quantity ?? f.Job.Quantity);

        public static int ReservedOut(Hall h, int areaId, ProductId p) =>
            h.Forklifts.Where(f => f.Job != null && f.Cargo == null && f.Job.FromId == areaId && f.Job.Product == p).Sum(f => f.Job.Quantity);

        public static int Free(Hall h, HallArea a, ProductId p) =>
            Math.Max(0, Stock.AreaCapacity(a) - Stock.Of(a.Stock, p) - Incoming(h, a.Id, p));

        static double FillRatio(HallArea a)
        {
            int cap = Stock.AreaCapacity(a);
            return cap == 0 ? 0 : HallProcessing.HallProducts.Max(p => Stock.Of(a.Stock, p)) / (double)cap;
        }

        /// <summary>Ist die Ware in diesem Bereich bereit für die nächste Stufe?</summary>
        static bool Ready(HallArea a, ProductId p)
        {
            switch (a.Kind)
            {
                case AreaKind.outbound: return false;
                case AreaKind.inbound:
                case AreaKind.storage: return true;
                default: return HallProcessing.Level(p) >= HallProcessing.StageLevel(a.Kind);
            }
        }

        /// <summary>
        /// Nächster Bereich für eine Ware: das Lager nur direkt nach dem Wareneingang (und nur, wenn dort Platz
        /// ist); vorhandene Stufen, die die Ware noch nicht hat, werden nicht übersprungen (voll = warten);
        /// fehlende Stufen schon; zuletzt der Warenausgang. null = warten bzw. bleibt liegen.
        /// </summary>
        public static HallArea NextArea(Hall h, AreaKind from, ProductId p)
        {
            int start = Array.IndexOf(HallProcessing.Order, from) + 1;
            for (int i = start; i < HallProcessing.Order.Length; i++)
            {
                var kind = HallProcessing.Order[i];
                if (kind == AreaKind.storage && from != AreaKind.inbound) continue;
                if (HallProcessing.StageLevel(kind) > 0 && HallProcessing.Level(p) >= HallProcessing.StageLevel(kind)) continue;
                var candidates = h.Areas.Where(a => a.Kind == kind).ToList();
                if (candidates.Count == 0) continue;
                var best = candidates.Select(a => (a, free: Free(h, a, p))).OrderByDescending(e => e.free).ThenBy(e => e.a.Id).First();
                if (best.free > 0) return best.a;
                if (kind == AreaKind.storage) continue;
                return null;
            }
            return null;
        }

        static Job FindTask(Hall h)
        {
            foreach (var a in h.Areas.OrderByDescending(FillRatio).ThenBy(x => x.Id))
            {
                foreach (var p in HallProcessing.HallProducts)
                {
                    if (!Ready(a, p)) continue;
                    int avail = Stock.Of(a.Stock, p) - ReservedOut(h, a.Id, p);
                    if (avail < 1) continue;
                    var dest = NextArea(h, a.Kind, p);
                    if (dest == null) continue;
                    int q = Math.Min(HallConfig.ForkliftCapacity, Math.Min(avail, Free(h, dest, p)));
                    if (q < 1) continue;
                    return new Job { Product = p, FromId = a.Id, ToId = dest.Id, Quantity = q };
                }
            }
            return null;
        }

        /// <summary>Kürzester Weg über Hallenfelder (Breitensuche, feste Reihenfolge N, O, S, W).</summary>
        public static List<Cell> PathInHall(Hall h, Cell from, Cell to, ISet<int> avoid = null)
        {
            var rect = h.Rect;
            var cameFrom = new Dictionary<int, Cell>();
            var seen = new HashSet<int> { Key(from) };
            var queue = new Queue<Cell>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == to)
                {
                    var path = new List<Cell> { c };
                    while (cameFrom.TryGetValue(Key(c), out var prev)) { path.Add(prev); c = prev; }
                    path.Reverse();
                    return path;
                }
                for (int d = 0; d < 4; d++)
                {
                    var n = new Cell(c.X + RoadNetwork.Dx[d], c.Z + RoadNetwork.Dz[d]);
                    if (!rect.Contains(n.X, n.Z) || seen.Contains(Key(n)) || (avoid != null && avoid.Contains(Key(n)) && n != to)) continue;
                    seen.Add(Key(n));
                    cameFrom[Key(n)] = c;
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        static bool HeadTo(Hall h, Forklift f, HallArea area, Dictionary<int, int> claimed)
        {
            if (area == null) return false;
            var from = f.Progress > 0 && f.Route.Count > 1 ? f.Route[1] : f.Route[0];
            var path = PathInHall(h, from, TargetCell(area, f, from, claimed));
            if (path == null) return false;
            if (f.Progress > 0 && f.Route.Count > 1) path.Insert(0, f.Route[0]);
            f.Route = path;
            return true;
        }

        /// <summary>Fährt weiter; je Feld nur ein Stapler. true = angekommen.</summary>
        static bool Move(Hall h, Forklift f, Dictionary<int, int> claimed)
        {
            int budget = HallConfig.ForkliftSpeed;
            while (f.Route.Count > 1 && budget > 0)
            {
                var next = f.Route[1];
                if (f.Progress == 0)
                {
                    if (claimed.TryGetValue(Key(next), out int holder) && holder != f.Id)
                    {
                        f.WaitTicks++;
                        // Gegenseitige Blockade: nur der Stapler mit der höheren Nummer weicht aus (sofort).
                        var other = h.Forklifts.Find(o => o.Id == holder);
                        bool mutual = other != null && other.Progress == 0 && other.Route.Count > 1 && other.Route[1] == f.Route[0];
                        if ((mutual && f.Id > holder) || (!mutual && f.WaitTicks % HallConfig.ForkliftDetourTicks == 0))
                            Detour(h, f, claimed);
                        return false;
                    }
                    claimed[Key(next)] = f.Id;
                }
                int step = Math.Min(budget, Movement.CellUnits - f.Progress);
                f.Progress += step;
                budget -= step;
                if (f.Progress < Movement.CellUnits) return false;
                if (claimed.TryGetValue(Key(f.Route[0]), out int me) && me == f.Id) claimed.Remove(Key(f.Route[0]));
                f.Route.RemoveAt(0);
                f.Progress = 0;
                f.WaitTicks = 0;
            }
            return f.Route.Count <= 1;
        }

        /// <summary>Ausweichen: Weg um alle belegten Felder; ist das Ziel selbst belegt, ein anderes Feld im Bereich.</summary>
        static void Detour(Hall h, Forklift f, Dictionary<int, int> claimed)
        {
            var avoid = OthersClaimed(f, claimed);
            var target = f.Route[f.Route.Count - 1];
            var area = f.Job == null ? null : h.AreaById(f.Phase == ForkliftPhase.toPickup ? f.Job.FromId : f.Job.ToId);
            if (avoid.Contains(Key(target)) && area != null) target = TargetCell(area, f, f.Route[0], claimed);
            var path = PathInHall(h, f.Route[0], target, avoid);
            if (path != null && path.Count > 1) f.Route = path;
        }

        public static void Update(GameState state, EventBus bus)
        {
            foreach (var h in state.Halls)
            {
                var claimed = new Dictionary<int, int>();
                foreach (var f in h.Forklifts)
                {
                    claimed[Key(f.Route[0])] = f.Id;
                    if (f.Progress > 0 && f.Route.Count > 1) claimed[Key(f.Route[1])] = f.Id;
                }
                foreach (var f in h.Forklifts) Step(state, bus, h, f, claimed);
                WarnNoForklift(state, bus, h);
            }
            if (GameTime.IsDayStart(state.Tick))
                foreach (var h in state.Halls)
                    if (h.Forklifts.Count > 0)
                        Ledger.Book(state, bus, BookingCategory.operations, -HallConfig.ForkliftDailyCents * h.Forklifts.Count);
        }

        static void Step(GameState state, EventBus bus, Hall h, Forklift f, Dictionary<int, int> claimed)
        {
            switch (f.Phase)
            {
                case ForkliftPhase.idle:
                    if (--f.Timer > 0) return;
                    f.Timer = HallConfig.ForkliftIdleCheckTicks;
                    if (f.Cargo != null) { Redirect(h, f, claimed); return; }
                    var job = FindTask(h);
                    if (job == null) return;
                    f.Job = job;
                    if (!HeadTo(h, f, h.AreaById(job.FromId), claimed)) { Reset(f); return; }
                    f.Phase = ForkliftPhase.toPickup;
                    return;
                case ForkliftPhase.toPickup:
                case ForkliftPhase.toDropoff:
                    if (!Move(h, f, claimed)) return;
                    f.Phase = f.Phase == ForkliftPhase.toPickup ? ForkliftPhase.loading : ForkliftPhase.unloading;
                    f.Timer = HallConfig.ForkliftHandlingTicks;
                    return;
                case ForkliftPhase.loading:
                {
                    if (--f.Timer > 0) return;
                    var from = f.Job == null ? null : h.AreaById(f.Job.FromId);
                    int q = from == null ? 0 : Math.Min(f.Job.Quantity, Stock.Of(from.Stock, f.Job.Product));
                    if (q <= 0) { Reset(f); return; }
                    Stock.Add(from.Stock, f.Job.Product, -q);
                    f.Cargo = new Cargo(f.Job.Product, q);
                    f.Job.Quantity = q;
                    if (!HeadTo(h, f, h.AreaById(f.Job.ToId), claimed)) { f.Phase = ForkliftPhase.idle; f.Timer = 1; return; }
                    f.Phase = ForkliftPhase.toDropoff;
                    return;
                }
                case ForkliftPhase.unloading:
                {
                    if (--f.Timer > 0) return;
                    var to = f.Job == null ? null : h.AreaById(f.Job.ToId);
                    if (to != null && f.Cargo != null)
                    {
                        int q = Math.Min(f.Cargo.Quantity, Stock.AreaCapacity(to) - Stock.Of(to.Stock, f.Cargo.Product));
                        if (q > 0)
                        {
                            Stock.Add(to.Stock, f.Cargo.Product, q);
                            bus.Emit(new GoodsDelivered { SiteId = h.Id, Product = f.Cargo.Product, Quantity = q });
                            f.Cargo.Quantity -= q;
                        }
                        if (f.Cargo.Quantity <= 0) f.Cargo = null;
                    }
                    f.Job = f.Cargo == null ? null : new Job { Product = f.Cargo.Product, FromId = to?.Id ?? 0, ToId = 0, Quantity = f.Cargo.Quantity };
                    f.Phase = ForkliftPhase.idle;
                    f.Timer = 1;
                    return;
                }
            }
        }

        /// <summary>Restladung (Ziel war voll oder fehlt): neues Ziel suchen, sonst später erneut.</summary>
        static void Redirect(Hall h, Forklift f, Dictionary<int, int> claimed)
        {
            var from = f.Job != null ? h.AreaById(f.Job.FromId) : null;
            var kind = from?.Kind ?? AreaKind.inbound;
            var dest = NextArea(h, kind, f.Cargo.Product) ?? (from != null && Free(h, from, f.Cargo.Product) > 0 ? from : null);
            if (dest == null) return;
            f.Job = new Job { Product = f.Cargo.Product, FromId = from?.Id ?? dest.Id, ToId = dest.Id, Quantity = f.Cargo.Quantity };
            if (HeadTo(h, f, dest, claimed)) f.Phase = ForkliftPhase.toDropoff;
        }

        /// <summary>Ohne Stapler bleibt die Ware im Wareneingang liegen: einmal Hinweis.</summary>
        static void WarnNoForklift(GameState state, EventBus bus, Hall h)
        {
            if (h.Forklifts.Count > 0 || h.WarnedNoForklift) return;
            var inbound = h.Areas.Where(a => a.Kind == AreaKind.inbound);
            if (!inbound.Any(a => a.Stock.Values.Any(v => v > 0))) return;
            if (!h.Areas.Any(a => a.Kind != AreaKind.inbound)) return;
            h.WarnedNoForklift = true;
            Notices.Add(state, bus, NoticeKind.noForklift, h.X + h.Width / 2, h.Z + h.Depth / 2, null);
        }
    }
}
