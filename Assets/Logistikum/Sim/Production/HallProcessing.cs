using System;
using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>
    /// Verarbeitungsstufen in Hallen (M3, T3.3): Verpackung, Etikettierung, Qualitätsprüfung machen die Ware
    /// wertvoller. Feste Reihenfolge, Stufen dürfen fehlen; Tempo wächst mit der Bereichsgröße; die Prüfung
    /// sortiert Ausschuss aus (Zufall aus dem Ereignis-Strom, deterministisch).
    /// </summary>
    public static class HallProcessing
    {
        public static readonly AreaKind[] Order =
        {
            AreaKind.inbound, AreaKind.storage, AreaKind.packing, AreaKind.labeling, AreaKind.inspection, AreaKind.outbound,
        };

        /// <summary>Verarbeitungsstand einer Ware: Endprodukt 0, verpackt 1, etikettiert 2, geprüft 3; sonst −1.</summary>
        public static int Level(ProductId p)
        {
            switch (p)
            {
                case ProductId.final: return 0;
                case ProductId.packed: return 1;
                case ProductId.labeled: return 2;
                case ProductId.@checked: return 3;
                default: return -1;
            }
        }

        /// <summary>Stufe eines Bereichs (1–3) oder 0 für Eingang, Lager, Ausgang.</summary>
        public static int StageLevel(AreaKind k) => k == AreaKind.packing ? 1 : k == AreaKind.labeling ? 2 : k == AreaKind.inspection ? 3 : 0;

        public static ProductId StageOutput(AreaKind k) => k == AreaKind.packing ? ProductId.packed : k == AreaKind.labeling ? ProductId.labeled : ProductId.@checked;

        static int WorkPerUnit(AreaKind k) =>
            k == AreaKind.packing ? HallConfig.PackWorkPerUnit : k == AreaKind.labeling ? HallConfig.LabelWorkPerUnit : HallConfig.InspectWorkPerUnit;

        /// <summary>Hallenwaren vom höchsten zum niedrigsten Stand.</summary>
        public static readonly ProductId[] HallProducts = { ProductId.@checked, ProductId.labeled, ProductId.packed, ProductId.final };

        /// <summary>Eingangsware einer Stufe: die am weitesten verarbeitete Ware unterhalb der Stufe.</summary>
        public static ProductId? InputOf(HallArea a)
        {
            int level = StageLevel(a.Kind);
            if (level == 0) return null;
            foreach (var p in HallProducts)
                if (Level(p) < level && Stock.Of(a.Stock, p) > 0) return p;
            return null;
        }

        public static bool IsWorking(HallArea a)
        {
            var input = InputOf(a);
            return input.HasValue && Stock.Of(a.Stock, StageOutput(a.Kind)) < Stock.AreaCapacity(a);
        }

        public static void Update(GameState state, EventBus bus)
        {
            foreach (var hall in state.Halls)
            {
                foreach (var a in hall.Areas)
                {
                    if (StageLevel(a.Kind) == 0 || !IsWorking(a)) continue;
                    a.Work += a.Rect.Area;
                    int need = WorkPerUnit(a.Kind);
                    if (a.Work < need) continue;
                    a.Work -= need;
                    Stock.Add(a.Stock, InputOf(a).Value, -1);
                    if (a.Kind == AreaKind.inspection && state.EventRng.NextFloat() * 100 < HallConfig.ScrapPercent) continue;
                    var output = StageOutput(a.Kind);
                    Stock.Add(a.Stock, output, 1);
                    bus.Emit(new GoodsProduced { SiteId = hall.Id, Product = output });
                }
            }
        }
    }

    /// <summary>Meldung, wenn ein Lager voll ist (T3.6, ANNAHME: einmal je Ort, bis wieder Platz ist).</summary>
    public static class StorageWarnings
    {
        public const int CheckEveryTicks = 10;

        public static bool IsFull(Dictionary<ProductId, int> stock, IEnumerable<ProductId> products, int capacity)
        {
            if (capacity <= 0) return false;
            foreach (var p in products) if (Stock.Of(stock, p) >= capacity) return true;
            return false;
        }

        public static void Update(GameState state, EventBus bus)
        {
            if (state.Tick % CheckEveryTicks != 0) return;
            foreach (var z in state.Zones)
            {
                if (z.Kind == ZoneKind.W) continue;
                var c = ZoneShape.Center(z.Parts);
                Check(state, bus, z.Id, IsFull(z.Stock, ZoneTypes.Stores(z.Kind), Stock.ZoneCapacity(z)), c);
            }
            foreach (var h in state.Halls)
                foreach (var a in h.Areas)
                    Check(state, bus, a.Id, IsFull(a.Stock, HallProcessing.HallProducts, Stock.AreaCapacity(a)),
                        new Place(a.Rect.X + a.Rect.Width / 2.0, a.Rect.Z + a.Rect.Depth / 2.0));
            state.WarnedFull.RemoveAll(id => state.ZoneById(id) == null && !state.Halls.Exists(h => h.AreaById(id) != null));
        }

        static void Check(GameState state, EventBus bus, int id, bool full, Place at)
        {
            bool warned = state.WarnedFull.Contains(id);
            if (full && !warned)
            {
                state.WarnedFull.Add(id);
                Notices.Add(state, bus, NoticeKind.storageFull, (int)at.X, (int)at.Z, null);
            }
            else if (!full && warned) state.WarnedFull.Remove(id);
        }
    }
}
