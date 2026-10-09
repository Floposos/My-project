namespace Logistikum.Sim
{
    public enum ZoneStatus { working, waitingInput, full, storing, workshop }

    /// <summary>Rezepte der Lieferorte: B kombiniert A + B zur Kombi, C verarbeitet sie zum Endprodukt.</summary>
    public static class Production
    {
        sealed class Recipe
        {
            public ProductId[] Inputs;
            public ProductId Output;
            public int Work;
            public bool ScalesWithArea;
        }

        static readonly Recipe RecipeB = new Recipe
        { Inputs = new[] { ProductId.rawA, ProductId.rawB }, Output = ProductId.combo, Work = ProductionConfig.ComboTicksPerUnit };
        static readonly Recipe RecipeC = new Recipe
        { Inputs = new[] { ProductId.combo }, Output = ProductId.final, Work = ProductionConfig.FinalWorkPerUnit, ScalesWithArea = true };

        static Recipe RecipeOf(ZoneKind k) => k == ZoneKind.B ? RecipeB : k == ZoneKind.C ? RecipeC : null;

        public static ZoneStatus StatusOf(Zone zone)
        {
            if (zone.Kind == ZoneKind.W) return ZoneStatus.workshop;
            var r = RecipeOf(zone.Kind);
            if (r == null) return Stock.IsFull(zone, ProductId.rawA) ? ZoneStatus.full : ZoneStatus.storing;
            if (Stock.IsFull(zone, r.Output)) return ZoneStatus.full;
            foreach (var p in r.Inputs) if (Stock.Of(zone, p) < 1) return ZoneStatus.waitingInput;
            return ZoneStatus.working;
        }

        /// <summary>Je Schritt: arbeitende Zonen kommen voran; volle oder leere Zonen stehen.</summary>
        public static void Update(GameState state, EventBus bus)
        {
            foreach (var zone in state.Zones)
            {
                var r = RecipeOf(zone.Kind);
                if (r == null || StatusOf(zone) != ZoneStatus.working) continue;
                zone.Work += r.ScalesWithArea ? ZoneShape.Area(zone.Parts) : 1;
                if (zone.Work < r.Work) continue;
                foreach (var p in r.Inputs) Stock.Add(zone, p, -1);
                Stock.Add(zone, r.Output, 1);
                zone.Work -= r.Work;
                bus.Emit(new GoodsProduced { SiteId = zone.Id, Product = r.Output });
            }
        }
    }
}
