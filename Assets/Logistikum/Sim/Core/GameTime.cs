using System;

namespace Logistikum.Sim
{
    /// <summary>Kalenderdatum und Uhrzeit zu einem Schritt.</summary>
    public struct CalendarTime
    {
        public int Year, Month, Day, Hour, Minute;
        /// <summary>0 = Sonntag … 6 = Samstag.</summary>
        public int Weekday;
        /// <summary>Laufender Spieltag seit Spielbeginn, ab 0.</summary>
        public int DayIndex;
    }

    /// <summary>Spielzeit-Rechnung: Der Zustand speichert nur den Schrittzähler (Tick).</summary>
    public static class GameTime
    {
        /// <summary>Schritte pro Spieltag (5 min/Tag × 10 Schritte/s = 3000).</summary>
        public const int TicksPerDay = TimeConfig.RealSecondsPerGameDay * TimeConfig.TicksPerRealSecond;
        /// <summary>Spiel-Millisekunden pro Schritt (28.800).</summary>
        public const long GameMsPerTick = 86_400_000L / TicksPerDay;
        /// <summary>Echtzeit-Millisekunden pro Schritt bei 1x.</summary>
        public const double RealMsPerTick = 1000.0 / TimeConfig.TicksPerRealSecond;
        /// <summary>Schritte pro Spielstunde (125).</summary>
        public const int TicksPerHour = TicksPerDay / 24;

        static readonly DateTime Start = new DateTime(TimeConfig.StartYear, TimeConfig.StartMonth, TimeConfig.StartDay, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime DateAt(long tick) => Start.AddTicks(tick * GameMsPerTick * TimeSpan.TicksPerMillisecond);

        public static CalendarTime CalendarAt(long tick)
        {
            var d = DateAt(tick);
            return new CalendarTime
            {
                Year = d.Year, Month = d.Month, Day = d.Day, Hour = d.Hour, Minute = d.Minute,
                Weekday = (int)d.DayOfWeek,
                DayIndex = (int)(tick / TicksPerDay),
            };
        }

        /// <summary>True, wenn mit diesem Schritt ein neuer Spieltag beginnt.</summary>
        public static bool IsDayStart(long tick) => tick > 0 && tick % TicksPerDay == 0;

        /// <summary>Derselbe Zeitpunkt einen Monat später (am Monatsende gekürzt), auf Schritte gerundet.</summary>
        public static int AddOneMonth(long tick)
        {
            var d = DateAt(tick);
            var next = new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
            int day = Math.Min(d.Day, DateTime.DaysInMonth(next.Year, next.Month));
            var target = new DateTime(next.Year, next.Month, day, d.Hour, d.Minute, d.Second, DateTimeKind.Utc).AddMilliseconds(d.Millisecond);
            return (int)Math.Round((target - Start).TotalMilliseconds / GameMsPerTick, MidpointRounding.AwayFromZero);
        }

        /// <summary>Minute des Tages (0 … 1439).</summary>
        public static int MinuteOfDay(long tick) => (int)((tick % TicksPerDay) * 1440 / TicksPerDay);
    }
}
