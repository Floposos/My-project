using System.Collections.Generic;

namespace Logistikum.Sim
{
    // Hinweis: Enum-Namen entsprechen genau den Werten im Spielstand (kompatibel zur Browser-Version).

    /// <summary>
    /// Waren der Produktkette: Rohware A und B, Kombi (B), Endprodukt (C); ab M3 die Hallenstufen
    /// verpackt, etikettiert, geprüft. Namen in Texts.
    /// </summary>
    public enum ProductId { rawA, rawB, combo, final, packed, labeled, @checked }

    public static class Products
    {
        public static readonly ProductId[] All =
        {
            ProductId.rawA, ProductId.rawB, ProductId.combo, ProductId.final,
            ProductId.packed, ProductId.labeled, ProductId.@checked,
        };

        /// <summary>Rohwaren und der Lieferort, an den sie geliefert werden.</summary>
        public static readonly Dictionary<ProductId, ZoneKind> Raw = new Dictionary<ProductId, ZoneKind>
        {
            { ProductId.rawA, ZoneKind.A },
            { ProductId.rawB, ZoneKind.B },
        };

        public static bool IsRaw(ProductId p) => p == ProductId.rawA || p == ProductId.rawB;

        /// <summary>Kistenfarbe (Akzentfarbe im Stil „Hell &amp; freundlich“) als 0xRRGGBB.</summary>
        public static uint Color(ProductId p)
        {
            switch (p)
            {
                case ProductId.rawA: return 0x4f9dde;
                case ProductId.rawB: return 0xf2b134;
                case ProductId.combo: return 0x9b6fd6;
                case ProductId.final: return 0x3fb68b;
                case ProductId.packed: return 0xc9875a;
                case ProductId.labeled: return 0xe5609a;
                default: return 0x2bb5c8;
            }
        }
    }
}
