using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>
    /// Feldkoordinaten → Welt: 1 Feld = 1 Einheit. Sim-x = Welt-X, Sim-z = Welt −Z (Norden zeigt in der
    /// Draufsicht nach oben, Rechtsverkehr bleibt rechts).
    /// </summary>
    public static class Coords
    {
        public static Vector3 World(double x, double z, float y = 0f) => new Vector3((float)x, y, (float)-z);
        public static Vector3 CellCenter(int x, int z, float y = 0f) => new Vector3(x + 0.5f, y, -(z + 0.5f));
        public static Vector3 CellCenter(Cell c, float y = 0f) => CellCenter(c.X, c.Z, y);

        /// <summary>Mitte eines Rechtecks in Welt-Koordinaten.</summary>
        public static Vector3 Center(Footprint f, float y = 0f) => new Vector3(f.X + f.Width * 0.5f, y, -(f.Z + f.Depth * 0.5f));

        /// <summary>Feld unter einem Weltpunkt.</summary>
        public static Cell CellAt(Vector3 p) => new Cell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(-p.z));

        /// <summary>Sim-Richtung (dx, dz) als Welt-Drehung um die Hochachse; 0° = Fahrt nach +X.</summary>
        public static float Yaw(float dx, float dz) => Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;

        /// <summary>Drehung, damit ein Modell mit Tor nach Süden (Welt −Z vorne) zur Seite zeigt.</summary>
        public static Quaternion GateRotation(Side gate)
        {
            switch (gate)
            {
                case Side.N: return Quaternion.Euler(0, 180, 0);
                case Side.E: return Quaternion.Euler(0, -90, 0);
                case Side.W: return Quaternion.Euler(0, 90, 0);
                default: return Quaternion.identity;
            }
        }
    }
}
