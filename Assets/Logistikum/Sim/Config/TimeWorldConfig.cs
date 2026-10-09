namespace Logistikum.Sim
{
    /// <summary>Zeit und Takt. Entscheidung 07.10.2026: 1 Spieltag = 5 Minuten Echtzeit bei 1x.</summary>
    public static class TimeConfig
    {
        /// <summary>Echtzeit-Sekunden pro Spieltag bei Geschwindigkeit 1x.</summary>
        public const int RealSecondsPerGameDay = 300;
        /// <summary>Simulationsschritte pro Echtzeit-Sekunde bei 1x (technischer Wert).</summary>
        public const int TicksPerRealSecond = 10;
        /// <summary>Höchstens so viele Schritte pro Bild, damit das Spiel nach Rucklern nicht einfriert.</summary>
        public const int MaxTicksPerFrame = 40;
        /// <summary>Wählbare Geschwindigkeitsstufen (Entscheidung: 1x/2x/4x).</summary>
        public static readonly int[] Speeds = { 1, 2, 4 };
        /// <summary>Startdatum (Entscheidung 07.10.2026: 1. Januar 2000), 00:00 Uhr.</summary>
        public const int StartYear = 2000, StartMonth = 1, StartDay = 1;
    }

    /// <summary>Gelände. Entscheidung 07.10.2026: 128 × 128 Felder (1 Feld = Straßenbreite).</summary>
    public static class WorldConfig
    {
        public const int CampusWidth = 128;
        public const int CampusDepth = 128;
        /// <summary>Länge der angedeuteten Eingangsstraße außerhalb des Geländes (Felder).</summary>
        public const int EntranceRoadLength = 24;
        /// <summary>Feldreihe der Einfahrt am Westrand: Die Eingangsstraße endet vor Feld (0, EntranceZ).</summary>
        public const int EntranceZ = 61;
    }

    /// <summary>Kamera: Grenzen und Geschwindigkeiten (Aufbauspiel-Stil, Entscheidung 07.10.2026).</summary>
    public static class CameraConfig
    {
        public const float MinPitchDeg = 15, MaxPitchDeg = 85;
        public const float MinDistance = 8, MaxDistance = 170;
        public const float StartDistance = 60, StartPitchDeg = 50, StartYawDeg = 45;
        public const float RotateDegPerPixel = 0.3f, TiltDegPerPixel = 0.2f;
        /// <summary>Zoomfaktor pro Rad-Schritt.</summary>
        public const float ZoomPerWheelStep = 1.15f;
        /// <summary>Tastatur und Bildschirmrand: Verschiebung in Bildschirmhöhen pro Sekunde.</summary>
        public const float PanScreensPerSecond = 0.8f;
        public const float KeyRotateDegPerSecond = 90, KeyTiltDegPerSecond = 45, KeyZoomPerSecond = 2.5f;
        public const float EdgeScrollPixels = 12;
        public const float MenuOrbitDegPerSecond = 4;
    }

    /// <summary>Einstellungen: Standardwerte und erlaubte Werte.</summary>
    public static class SettingsConfig
    {
        /// <summary>Entscheidung 07.10.2026: Autosave alle 5 Minuten Echtzeit, änderbar.</summary>
        public static readonly int[] AutosaveMinutesOptions = { 2, 5, 10, 15 };
        public const int AutosaveMinutesDefault = 5;
        public const float CameraSensitivityMin = 0.25f, CameraSensitivityMax = 2.5f, CameraSensitivityDefault = 1f;
        public const bool EdgeScrollDefault = true;
    }

    /// <summary>Speichern und Autosave.</summary>
    public static class SaveConfig
    {
        /// <summary>Anzahl rotierender Autosave-Backups.</summary>
        public const int BackupCount = 3;
        /// <summary>Entscheidung 07.10.2026: Export-Erinnerung alle 30 Minuten Spielzeit.</summary>
        public const int ExportReminderMinutes = 30;
        public const float SavedToastSeconds = 2f;
        public const float ExportReminderToastSeconds = 12f;
    }
}
