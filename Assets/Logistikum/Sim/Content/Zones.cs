namespace Logistikum.Sim
{
    /// <summary>Lieferorte A, B, C als frei aufziehbare Zonen, dazu die Werkstatt W (T2.6).</summary>
    public enum ZoneKind { A, B, C, W }

    public static class ZoneTypes
    {
        public static readonly ZoneKind[] All = { ZoneKind.A, ZoneKind.B, ZoneKind.C, ZoneKind.W };

        /// <summary>Waren, die hier gelagert werden (je Ware eigene Kapazität).</summary>
        public static ProductId[] Stores(ZoneKind kind)
        {
            switch (kind)
            {
                case ZoneKind.A: return new[] { ProductId.rawA };
                case ZoneKind.B: return new[] { ProductId.rawA, ProductId.rawB, ProductId.combo };
                case ZoneKind.C: return new[] { ProductId.combo, ProductId.final };
                default: return new ProductId[0];
            }
        }

        public static uint Color(ZoneKind kind)
        {
            switch (kind)
            {
                case ZoneKind.A: return 0xa9cff2;
                case ZoneKind.B: return 0xf7d792;
                case ZoneKind.C: return 0xd4c1f0;
                default: return 0xc3ccd4;
            }
        }
    }
}
