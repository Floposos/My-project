using System.Collections.Generic;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>
    /// Farbstil „Hell &amp; freundlich“ (Entscheidung 07.10.2026) und gemeinsame URP-Materialien. Gleiche Farben
    /// teilen sich ein Material, damit der SRP-Batcher viele Objekte günstig zeichnet.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Grass = Hex(0xa9d98e);
        public static readonly Color GrassOutside = Hex(0x93c878);
        public static readonly Color GridLine = Hex(0x96c97c);
        public static readonly Color Asphalt = Hex(0x6f7680);
        public static readonly Color Marking = Hex(0xf4f1e8);
        public static readonly Color PriorityMarking = Hex(0xf5c542);
        public static readonly Color HallWall = Hex(0xeef0f2);
        public static readonly Color HallRoof = Hex(0x9fb3c8);
        public static readonly Color HallFloor = Hex(0xd9dde2);
        public static readonly Color Belt = Hex(0x3c4450);
        public static readonly Color BeltArrow = Hex(0xf5a524);
        public static readonly Color Ok = new Color(0.35f, 0.85f, 0.45f, 0.45f);
        public static readonly Color Bad = new Color(0.95f, 0.3f, 0.3f, 0.45f);
        public static readonly Color Warning = Hex(0xf2b134);
        public static readonly Color Selection = Hex(0xffffff);
        public static readonly Color SupplierPaint = Hex(0xd8dde3);
        public static readonly Color VanPaint = Hex(0xf7f7f2);
        public static readonly Color TruckPaint = Hex(0xffffff);
        public static readonly Color ForkliftPaint = Hex(0xf5b82e);

        public static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);

        public static Color AreaColor(Sim.AreaKind k)
        {
            switch (k)
            {
                case Sim.AreaKind.inbound: return Hex(0xa9cff2);
                case Sim.AreaKind.storage: return Hex(0xe3d5b8);
                case Sim.AreaKind.packing: return Hex(0xf2c6a0);
                case Sim.AreaKind.labeling: return Hex(0xf5b5cf);
                case Sim.AreaKind.inspection: return Hex(0xa8e0d8);
                default: return Hex(0xb9e4a8);
            }
        }

        /// <summary>Feste Farben der Modell-Materialien aus Blender (nach Materialnamen).</summary>
        public static readonly Dictionary<string, Color> ModelColors = new Dictionary<string, Color>
        {
            { "L_Glass", Hex(0x7da7c4) }, { "L_Tire", Hex(0x25272b) }, { "L_Dark", Hex(0x4a4f57) },
            { "L_Light", Hex(0xf4f1ea) }, { "L_Bed", Hex(0xa3927f) }, { "L_Lamp", Hex(0xfff2c2) },
            { "L_Metal", Hex(0xa9aeb5) }, { "L_Accent", Hex(0xf8c24a) }, { "L_Concrete", Hex(0xd3d3cf) },
            { "L_Barrier", Hex(0xe9524a) }, { "L_Container", Hex(0x4f86c6) }, { "L_Leaf", Hex(0x7cc26a) },
            { "L_Trunk", Hex(0x8a6448) }, { "L_Paint", Hex(0xffffff) }, { "L_Stripe", Hex(0x8a94a0) },
            { "L_Crate", Hex(0xc9955a) },
        };

        static Material litBase, transparentBase;
        static readonly Dictionary<int, Material> lit = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> transparent = new Dictionary<int, Material>();

        static int Key(Color c) => Mathf.RoundToInt(c.r * 255) << 24 | Mathf.RoundToInt(c.g * 255) << 16 | Mathf.RoundToInt(c.b * 255) << 8 | Mathf.RoundToInt(c.a * 255);

        /// <summary>Undurchsichtiges URP-Lit-Material in dieser Farbe (geteilt).</summary>
        public static Material Lit(Color c, float smoothness = 0.1f)
        {
            int key = Key(c) ^ Mathf.RoundToInt(smoothness * 100) * 31;
            if (lit.TryGetValue(key, out var m) && m != null) return m;
            if (litBase == null) litBase = Resources.Load<Material>("Materials/LitBase");
            m = new Material(litBase) { name = "Lit " + ColorUtility.ToHtmlStringRGB(c) };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            // Keine Himmelsspiegelung: hält die Pastellfarben satt, auch aus großer Höhe.
            m.SetFloat("_EnvironmentReflections", 0);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            lit[key] = m;
            return m;
        }

        /// <summary>Halbtransparentes Material (Vorschau, Markierungen).</summary>
        public static Material Transparent(Color c)
        {
            int key = Key(c);
            if (transparent.TryGetValue(key, out var m) && m != null) return m;
            if (transparentBase == null) transparentBase = Resources.Load<Material>("Materials/LitTransparent");
            m = new Material(transparentBase) { name = "Trans " + ColorUtility.ToHtmlStringRGBA(c) };
            m.SetColor("_BaseColor", c);
            transparent[key] = m;
            return m;
        }
    }
}
