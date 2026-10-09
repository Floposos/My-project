using System;

namespace Logistikum.Sim
{
    /// <summary>Einfahrt mit externem Verkehr und Rushhour (T2.7).</summary>
    public static class EntranceRules
    {
        /// <summary>Verkehrsfaktor in Prozent (100 = normal), mit sanftem Übergang um die Rushhour.</summary>
        public static int TrafficFactorPercent(int tick)
        {
            int m = GameTime.MinuteOfDay(tick);
            int best = 100;
            foreach (var hours in EntranceConfig.RushHours)
            {
                int start = hours[0] * 60, end = hours[1] * 60, ramp = EntranceConfig.RampMinutes;
                double share = 0;
                if (m >= start && m < end) share = 1;
                else if (m >= start - ramp && m < start) share = (double)(m - start + ramp) / ramp;
                else if (m >= end && m < end + ramp) share = (double)(end + ramp - m) / ramp;
                best = Math.Max(best, (int)Math.Round(100 + (EntranceConfig.RushFactorPercent - 100) * share, MidpointRounding.AwayFromZero));
            }
            return best;
        }

        public static bool IsRushHour(int tick) => TrafficFactorPercent(tick) >= EntranceConfig.RushFactorPercent;

        /// <summary>Wartezeit zum Einfädeln an der Einfahrt (Schritte).</summary>
        public static int MergeTicks(int tick) =>
            (int)Math.Round(EntranceConfig.BaseMergeTicks * TrafficFactorPercent(tick) / 100.0, MidpointRounding.AwayFromZero);

        /// <summary>Überquert der Schritt von here nach next die Einfahrt? +1 hinein, −1 hinaus, 0 nein.</summary>
        public static int Crossing(Cell here, Cell next)
        {
            var e = RoadNetwork.Entrance;
            if (here.Z != e.Z || next.Z != e.Z) return 0;
            if (here.X == e.X - 1 && next.X == e.X) return 1;
            if (here.X == e.X && next.X == e.X - 1) return -1;
            return 0;
        }
    }

    /// <summary>
    /// Wer hinein oder hinaus will, wartet an der Feldgrenze, bis er eingefädelt ist, und je Richtung
    /// fährt immer nur einer im Abstand der Einfädelzeit. Deterministisch nach Schritten.
    /// </summary>
    public sealed class EntranceGate
    {
        readonly GameState state;
        public EntranceGate(GameState state) { this.state = state; }

        public bool Allows(Vehicle v, Cell here, Cell next)
        {
            int dir = EntranceRules.Crossing(here, next);
            if (dir == 0) return true;
            int nextTick = dir > 0 ? state.Entrance.NextInTick : state.Entrance.NextOutTick;
            return v.WaitTicks >= EntranceRules.MergeTicks(state.Tick) && state.Tick >= nextTick;
        }

        public void Pass(Cell here, Cell next)
        {
            int dir = EntranceRules.Crossing(here, next);
            if (dir == 0) return;
            int at = state.Tick + EntranceRules.MergeTicks(state.Tick);
            if (dir > 0) state.Entrance.NextInTick = at;
            else state.Entrance.NextOutTick = at;
        }
    }
}
