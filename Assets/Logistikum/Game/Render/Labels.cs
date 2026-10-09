using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>Schriftzüge in der Welt (Zonenbuchstaben, Hinweise) mit der eingebauten Schrift.</summary>
    public static class Labels
    {
        static Font font;

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Flach auf dem Boden liegende Schrift (von oben lesbar, Norden oben).</summary>
        public static TextMesh Flat(Transform parent, string text, Vector3 pos, float size, Color color)
        {
            var tm = Make(parent, text, size, color);
            tm.transform.localPosition = pos;
            tm.transform.localRotation = Quaternion.Euler(90, 0, 0);
            return tm;
        }

        /// <summary>Stehende Schrift, die sich zur Kamera dreht (Billboard; Drehung setzt der Aufrufer).</summary>
        public static TextMesh Standing(Transform parent, string text, Vector3 pos, float size, Color color)
        {
            var tm = Make(parent, text, size, color);
            tm.transform.localPosition = pos;
            return tm;
        }

        static TextMesh Make(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Schrift");
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size / 10f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }
    }
}
