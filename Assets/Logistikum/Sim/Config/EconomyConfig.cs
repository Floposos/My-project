using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Geldwerte in Cent (ganzzahlig, damit Rechnungen exakt bleiben).</summary>
    public static class EconomyConfig
    {
        /// <summary>Entscheidung 07.10.2026: Startkapital 2.000.000 €.</summary>
        public const long StartingBalanceCents = 200_000_000;
        /// <summary>So viele letzte Buchungen merkt sich die Kasse.</summary>
        public const int RecentBookings = 50;
    }

    /// <summary>
    /// Bauen und Abreißen. Entscheidungen 07.10.2026: Abriss am selben Spieltag 100 %, danach 50 %;
    /// Baupreise „Mittel“.
    /// </summary>
    public static class BuildConfig
    {
        public const double RefundSameDay = 1;
        public const double RefundLater = 0.5;
        public static readonly Dictionary<BuildingTypeId, long> BuildingCostCents = new Dictionary<BuildingTypeId, long>
        {
            { BuildingTypeId.testHall, 5_000_000 },
            { BuildingTypeId.exportExit, 5_000_000 },
        };
        /// <summary>Straße je Feld (500 €).</summary>
        public const long RoadCostPerTileCents = 50_000;
    }

    /// <summary>
    /// Zonen (Lieferorte A, B, C, Werkstatt W). Keine Mindestgröße; Kapazität skaliert mit der Fläche,
    /// je Ware getrennt; Baukosten je Feld (A 250 €, B/C 400 €, W 500 € ANNAHME).
    /// </summary>
    public static class ZoneConfig
    {
        public const int MinSize = 1;
        public static long CostPerFieldCents(ZoneKind kind)
        {
            switch (kind)
            {
                case ZoneKind.A: return 25_000;
                case ZoneKind.B: return 40_000;
                case ZoneKind.C: return 40_000;
                default: return 50_000;
            }
        }
        /// <summary>Lagerplatz je Feld und Ware (Einheiten).</summary>
        public const int CapacityPerField = 10;
    }

    /// <summary>
    /// Waren, Einkauf und Verkauf. Rohware per Dauerauftrag oder Einzelbestellung; Export zum festen
    /// Preis je Ware; das Endprodukt ist etwas mehr wert als die Kombi.
    /// </summary>
    public static class GoodsConfig
    {
        /// <summary>Einkaufspreis je Einheit Rohware (A 15 €, B 20 €).</summary>
        public static long PurchasePriceCents(ProductId p) => p == ProductId.rawA ? 1_500 : 2_000;
        /// <summary>Verkaufspreis je Einheit an der Export-Ausfahrt; null = nicht exportierbar.</summary>
        public static readonly Dictionary<ProductId, long> ExportPriceCents = new Dictionary<ProductId, long>
        {
            { ProductId.combo, 11_000 },
            { ProductId.final, 13_000 },
            // M3: jede Hallenstufe macht die Ware wertvoller (ANNAHME bis zum Balancing).
            { ProductId.packed, 14_500 },
            { ProductId.labeled, 15_500 },
            { ProductId.@checked, 17_000 },
        };
        public static readonly int[] OrderQuantities = { 10, 20, 50, 100 };
        public const int DefaultOrderQuantity = 20;
    }

    /// <summary>
    /// Verarbeitung in den Lieferorten. 1 Schritt = 28,8 Spielsekunden; 125 Schritte = 1 Spielstunde.
    /// Entscheidung 07.10.2026: In C steigt der Durchsatz mit der Zonengröße.
    /// </summary>
    public static class ProductionConfig
    {
        /// <summary>B: 1 Rohware A + 1 Rohware B → 1 Kombi, feste Dauer je Einheit.</summary>
        public const int ComboTicksPerUnit = 60;
        /// <summary>C: 1 Kombi → 1 Endprodukt; jedes Feld leistet 1 je Schritt.</summary>
        public const int FinalWorkPerUnit = 540;
    }
}
