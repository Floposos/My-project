namespace Logistikum.Sim
{
    /// <summary>Werte je Fahrzeugtyp. Geschwindigkeit in Tausendstel Feld je Schritt.</summary>
    public struct ModelValues
    {
        public int Speed;
        public int Capacity;
        public long PriceCents;
        public long DailyCents;
        public long CostPerKmCents;
    }

    /// <summary>Fahrzeuge (T1.5, T2.5). ANNAHME: Werte sind Platzhalter bis zum Balancing.</summary>
    public static class VehicleConfig
    {
        /// <summary>Transporter klein, schnell, billig, wenig Ladung; LKW groß, langsam, viel Ladung.</summary>
        public static ModelValues Model(VehicleModel model) => model == VehicleModel.van
            ? new ModelValues { Speed = 550, Capacity = 8, PriceCents = 4_500_000, DailyCents = 20_000, CostPerKmCents = 70 }
            : new ModelValues { Speed = 400, Capacity = 20, PriceCents = 9_000_000, DailyCents = 40_000, CostPerKmCents = 120 };

        /// <summary>Elektro nur bei den Kosten: Kauf × 1,3, Kilometerkosten × 0,5 (Prozent).</summary>
        public static int PricePercent(VehicleDrive d) => d == VehicleDrive.electric ? 130 : 100;
        public static int PerKmPercent(VehicleDrive d) => d == VehicleDrive.electric ? 50 : 100;

        public const int SupplierSpeed = 400;
        public const int TruckCapacity = 20;
        public const int MetersPerField = 10;
        /// <summary>Automatik: Mindestmenge je Fahrt (Entscheidung 08.10.2026: 5).</summary>
        public const int TruckMinLoad = 5;
        public const int TruckIdleCheckTicks = 25;
        public const int TourMaxStops = 12;
        public const int TourNameMaxLength = 30;
        /// <summary>Abladen bzw. Aufladen je Halt (Schritte).</summary>
        public const int HandlingTicks = 30;
        /// <summary>Wartezeit vor einem neuen Versuch ohne Weg bzw. ohne Platz.</summary>
        public const int RetryTicks = 125;
        /// <summary>Felder der Eingangsstraße, auf denen Zulieferer außerhalb fahren.</summary>
        public const int OutsideCells = 12;
    }

    /// <summary>
    /// Leasing (Entscheidung 08.10.2026). ANNAHME: 12 Monate, Rate 3 % des Kaufpreises, Strafe 3 Raten;
    /// Verkauf: Restwert 80 % minus 1,5 Punkte je Monat, mindestens 20 %.
    /// </summary>
    public static class LeaseConfig
    {
        public const int TermMonths = 12;
        public const int MonthlyPermille = 30;
        public const int EarlyReturnPenaltyMonths = 3;
        public const int ResidualStartPercent = 80;
        public const int ResidualLossPerMonthPermille = 15;
        public const int ResidualMinPercent = 20;
    }

    /// <summary>
    /// Verschleiß, Werkstatt und Pannen (T2.6). Zustand in Tausendstel Prozent (100.000 = 100 %).
    /// ANNAHME: Wartung ab 40 %, 2 Spielstunden, 800 €; Panne 3 Stunden, 1.500 €, danach +20 Punkte.
    /// </summary>
    public static class MaintenanceConfig
    {
        public const int FullCondition = 100_000;
        public const int WearPerField = 4;
        public const int ServiceBelow = 40_000;
        public const int ServiceTicks = 250;
        public const long ServiceCostCents = 80_000;
        public const int WorkshopFieldsPerBay = 4;
        public const int BreakdownPerKmAtZeroPpm = 50_000;
        public const int BreakdownTicks = 375;
        public const long TowCostCents = 150_000;
        public const int BreakdownRepair = 20_000;
    }

    /// <summary>Verkehr auf dem Campus (M2). ANNAHME: Platzhalter bis zum Balancing.</summary>
    public static class TrafficConfig
    {
        public const int FieldsPerBay = 6;
        public const int MaxBays = 8;
        public const int ExportBays = 3;
        public const int DetourAfterTicks = 60;
        public const int DetourEveryTicks = 50;
        public const int JamWarnTicks = 200;
        public const long PriorityCostCents = 0;
    }

    /// <summary>Einfahrt und externer Verkehr (T2.7). ANNAHME: Rushhour 7–9 und 16–18 Uhr, Faktor 3.</summary>
    public static class EntranceConfig
    {
        public const int BaseMergeTicks = 10;
        public const int RushFactorPercent = 300;
        public static readonly int[][] RushHours = { new[] { 7, 9 }, new[] { 16, 18 } };
        public const int RampMinutes = 60;
        public const int ExternalCars = 4;
        public const int ExternalCarsRush = 12;
    }

    /// <summary>Ereignisse und Meldungen (M2). ANNAHME.</summary>
    public static class EventsConfig
    {
        public const int MaxNotices = 40;
        public const int NoticeMergeTicks = 600;
        public const int NoticeMergeDistance = 6;
    }
}
