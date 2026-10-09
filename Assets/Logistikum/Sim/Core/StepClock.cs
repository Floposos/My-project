using System;

namespace Logistikum.Sim
{
    /// <summary>
    /// Wandelt vergangene Echtzeit in feste Simulationsschritte um (Fixed Timestep). Die Geschwindigkeit
    /// multipliziert die Zeit; 0 = Pause. Überschüssige Zeit wird im nächsten Bild verrechnet.
    /// </summary>
    public sealed class StepClock
    {
        double accumulatorMs;
        readonly int maxTicksPerFrame;

        public StepClock(int maxTicksPerFrame = TimeConfig.MaxTicksPerFrame) { this.maxTicksPerFrame = maxTicksPerFrame; }

        /// <summary>Anzahl fälliger Schritte für realMs Echtzeit bei speed.</summary>
        public int Advance(double realMs, int speed)
        {
            if (speed <= 0 || realMs <= 0) return 0;
            accumulatorMs += realMs * speed;
            int ticks = (int)Math.Floor((accumulatorMs + 1e-6) / GameTime.RealMsPerTick);
            accumulatorMs = Math.Max(0, accumulatorMs - ticks * GameTime.RealMsPerTick);
            if (ticks > maxTicksPerFrame)
            {
                // Nach einem Ruckler nicht alles nachholen, sonst friert das Spiel ein.
                ticks = maxTicksPerFrame;
                accumulatorMs = 0;
            }
            return ticks;
        }

        /// <summary>Anteil (0–1) bis zum nächsten Schritt, für flüssige Darstellung.</summary>
        public float Alpha => (float)(accumulatorMs / GameTime.RealMsPerTick);

        public void Reset() { accumulatorMs = 0; }
    }
}
