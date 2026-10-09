using System;
using System.Globalization;
using Logistikum.Sim;

namespace Logistikum.Game
{
    /// <summary>Deutsche Formatierung: Euro, Datum, Uhrzeit.</summary>
    public static class Fmt
    {
        static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Cent als ganze Euro, z. B. „1.000.000 €“.</summary>
        public static string Euro(long cents) => Math.Round(cents / 100.0, MidpointRounding.AwayFromZero).ToString("#,0", De) + " €";

        /// <summary>Cent mit zwei Nachkommastellen, z. B. „1,20 €“ (Kilometerkosten).</summary>
        public static string EuroExact(long cents) => (cents / 100.0).ToString("#,0.00", De) + " €";

        public static string SignedEuro(long cents) => cents > 0 ? "+" + Euro(cents) : cents < 0 ? "−" + Euro(-cents) : Euro(0);

        public static string Pad2(int n) => n.ToString("00");

        /// <summary>Spielzeit als „Sa 01.01.2000 08:15“.</summary>
        public static string GameDateTime(int tick)
        {
            var c = GameTime.CalendarAt(tick);
            return T.Weekdays[c.Weekday] + " " + Pad2(c.Day) + "." + Pad2(c.Month) + "." + c.Year + " " + Pad2(c.Hour) + ":" + Pad2(c.Minute);
        }

        public static string GameDate(int tick)
        {
            var c = GameTime.CalendarAt(tick);
            return Pad2(c.Day) + "." + Pad2(c.Month) + "." + c.Year;
        }

        /// <summary>ISO-Zeitstempel als „09.10.2026, 18:05“ (Ortszeit).</summary>
        public static string RealDateTime(string iso)
        {
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d))
                return d.ToLocalTime().ToString("dd.MM.yyyy, HH:mm", De);
            return "–";
        }
    }
}
