namespace Logistikum.Sim
{
    /// <summary>Eigene Fahrzeugtypen (T2.5): Transporter und LKW.</summary>
    public enum VehicleModel { van, truck }

    /// <summary>Elektro unterscheidet sich nur bei den Kosten (Entscheidung 08.10.2026).</summary>
    public enum VehicleDrive { diesel, electric }

    /// <summary>Farben der Touren (T2.1); der Spielstand speichert nur den Index.</summary>
    public static class TourColors
    {
        public static readonly uint[] All =
        {
            0xe5484d, 0x2f80ed, 0x30a46c, 0xf5a524, 0x8e4ec6,
            0x12a5b8, 0xe93d82, 0x8a6d3b, 0x9bc53d, 0x1f2d5c,
        };

        /// <summary>Wegfarbe der Automatik-Fahrten und der Zulieferer.</summary>
        public const uint AutoRoute = 0x8a94a0;
    }
}
