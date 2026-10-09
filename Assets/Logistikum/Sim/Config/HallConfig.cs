namespace Logistikum.Sim
{
    /// <summary>
    /// Hallen, Bereiche, Stapler und Förderbänder (M3). Entscheidungen: empfohlene Antworten aus
    /// fragenm3.md. ANNAHME: alle Zahlen sind Platzhalter bis zum Balancing.
    /// </summary>
    public static class HallConfig
    {
        /// <summary>Mindestbreite und -tiefe einer Halle (Felder).</summary>
        public const int MinSize = 3;
        /// <summary>Hallenbau je Feld (600 €).</summary>
        public const long CostPerFieldCents = 60_000;
        /// <summary>Bereich einrichten je Feld (100 €).</summary>
        public const long AreaCostPerFieldCents = 10_000;
        /// <summary>Arbeit je Einheit in Feld-Schritten (Frage 9: Tempo wächst mit der Bereichsgröße).</summary>
        public const int PackWorkPerUnit = 360;
        public const int LabelWorkPerUnit = 240;
        public const int InspectWorkPerUnit = 300;
        /// <summary>Ausschuss bei der Qualitätsprüfung in Prozent (Frage 10).</summary>
        public const int ScrapPercent = 5;

        /// <summary>Gabelstapler: Kauf 25.000 €, 80 € am Tag (Betrieb), Verkauf zum halben Preis.</summary>
        public const long ForkliftPriceCents = 2_500_000;
        public const long ForkliftDailyCents = 8_000;
        public const int ForkliftResalePercent = 50;
        /// <summary>Tempo in Tausendstel Feld je Schritt (2,5 Felder/s bei 1x).</summary>
        public const int ForkliftSpeed = 250;
        public const int ForkliftCapacity = 4;
        public const int ForkliftHandlingTicks = 10;
        public const int ForkliftIdleCheckTicks = 10;
        /// <summary>So lange wartet ein Stapler auf ein besetztes Feld, bevor er ausweicht.</summary>
        public const int ForkliftDetourTicks = 15;

        /// <summary>Förderband je Feld (200 €).</summary>
        public const long ConveyorCostPerFieldCents = 20_000;
        /// <summary>Bandtempo in Tausendstel Feld je Schritt (2 Felder/s) und Mindestabstand der Kisten.</summary>
        public const int BeltSpeed = 200;
        public const int BeltSpacing = 500;
        /// <summary>Abstand der Aufnahme an der Quelle (Schritte).</summary>
        public const int BeltPickTicks = 3;
    }
}
