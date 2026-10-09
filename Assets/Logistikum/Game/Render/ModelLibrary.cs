using System.Collections.Generic;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>Eine Modell-Instanz mit austauschbaren Farb-Materialien (Lack, Streifen, Kiste).</summary>
    public sealed class ModelInstance
    {
        public GameObject Root;
        readonly List<(Renderer r, Material[] mats, int index, string slot)> slots = new List<(Renderer, Material[], int, string)>();

        public ModelInstance(GameObject root)
        {
            Root = root;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var name = mats[i] != null ? mats[i].name.Replace(" (Instance)", "") : "";
                    if (Palette.ModelColors.TryGetValue(name, out var c)) mats[i] = Palette.Lit(c, name == "L_Glass" ? 0.7f : 0.25f);
                    if (name == "L_Paint" || name == "L_Stripe" || name == "L_Crate") slots.Add((r, mats, i, name));
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        public void SetColor(string slot, Color c)
        {
            var m = Palette.Lit(c);
            foreach (var s in slots)
            {
                if (s.slot != slot || s.mats[s.index] == m) continue;
                s.mats[s.index] = m;
                s.r.sharedMaterials = s.mats;
            }
        }

        public void SetActive(bool on) { if (Root.activeSelf != on) Root.SetActive(on); }
    }

    /// <summary>Lädt die in Blender gebauten Modelle aus Resources/Models und erzeugt Instanzen.</summary>
    public static class ModelLibrary
    {
        /// <summary>
        /// Drehung der Fahrzeugmodelle um die Hochachse, damit die Front (Blender +X) zur Welt +X zeigt.
        /// </summary>
        public static float VehicleYawOffset = 0f;

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject Prefab(string name)
        {
            if (!cache.TryGetValue(name, out var go) || go == null)
            {
                go = Resources.Load<GameObject>("Models/" + name);
                cache[name] = go;
            }
            return go;
        }

        public static ModelInstance Spawn(string name, Transform parent)
        {
            var prefab = Prefab(name);
            GameObject go;
            if (prefab != null) go = Object.Instantiate(prefab, parent);
            else
            {
                // Fallback ohne Modell: Würfel, damit das Spiel trotzdem läuft.
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(0.6f, 0.3f, 0.3f);
            }
            go.name = name;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            return new ModelInstance(go);
        }
    }
}
