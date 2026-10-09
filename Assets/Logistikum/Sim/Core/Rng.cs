namespace Logistikum.Sim
{
    /// <summary>
    /// Zufallsgenerator mit Seed (Mulberry32, identisch zur Browser-Version). Der Zustand ist eine
    /// einzige 32-Bit-Zahl, liegt im Spielzustand und wird mitgespeichert.
    /// </summary>
    public sealed class RngState
    {
        /// <summary>Aktueller interner Zustand (vorzeichenlose 32-Bit-Zahl).</summary>
        public uint S;

        public RngState() { }
        public RngState(uint seed) { S = seed; }

        /// <summary>Zahl in [0, 1); schreitet den Zustand fort.</summary>
        public double NextFloat()
        {
            unchecked
            {
                S += 0x6d2b79f5u;
                uint t = S;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>Ganze Zahl in [min, max] (beide eingeschlossen).</summary>
        public int NextInt(int min, int max) => min + (int)System.Math.Floor(NextFloat() * (max - min + 1));
    }
}
