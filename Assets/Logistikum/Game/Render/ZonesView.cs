using System.Collections.Generic;
using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>
    /// Zonen (Lieferorte, Werkstatt) und Export-Ausfahrten: Bodenfläche, Torbalken, Buchstabe, Warnsymbol
    /// „nicht angeschlossen“, Kisten je Ware (Bestandssymbol, Entscheidung 07.10.2026), Schild „voll“.
    /// </summary>
    public sealed class ZonesView
    {
        readonly Transform root;
        readonly List<GameObject> built = new List<GameObject>();
        readonly Dictionary<int, StockStacks> stacks = new Dictionary<int, StockStacks>();
        readonly Dictionary<int, TextMesh> warnings = new Dictionary<int, TextMesh>();

        public ZonesView(Transform parent)
        {
            root = new GameObject("Zonen").transform;
            root.SetParent(parent, false);
        }

        public void Rebuild(GameState state)
        {
            foreach (var go in built) Object.Destroy(go);
            built.Clear();
            stacks.Clear();
            warnings.Clear();
            foreach (var z in state.Zones) BuildZone(z);
            foreach (var b in state.Buildings) BuildBuilding(b);
        }

        GameObject Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            built.Add(go);
            return go;
        }

        void BuildZone(Zone z)
        {
            var g = Group("Zone " + z.Id);
            var color = Palette.Hex(ZoneTypes.Color(z.Kind));
            var edge = Color.Lerp(color, Color.black, 0.25f);
            var mb = new MeshBuilder(3);
            var parts = new List<Footprint>(z.Parts);
            foreach (var p in z.Parts)
            {
                mb.Flat(p.X + 0.03f, -(p.Z + p.Depth) + 0.03f, p.X + p.Width - 0.03f, -p.Z - 0.03f, 0.008f, 0);
            }
            // Torbalken: die Felder direkt vor dem Tor, innen an der Zonenkante.
            foreach (var c in Access.CellsBeside(parts, z.Gate))
            {
                int ix = c.X - (z.Gate == Side.E ? 1 : z.Gate == Side.W ? -1 : 0);
                int iz = c.Z - (z.Gate == Side.S ? 1 : z.Gate == Side.N ? -1 : 0);
                float x0 = ix, x1 = ix + 1, z0 = -(iz + 1), z1 = -iz;
                const float t = 0.14f;
                if (z.Gate == Side.N) z0 = z1 - t;
                else if (z.Gate == Side.S) z1 = z0 + t;
                else if (z.Gate == Side.W) x1 = x0 + t;
                else x0 = x1 - t;
                mb.Flat(x0, z0, x1, z1, 0.014f, 1);
            }
            var (go, mf, mr) = MeshBuilder.Object("Fläche", g.transform, Palette.Lit(color), Palette.Lit(edge), Palette.Lit(edge));
            mf.sharedMesh = mb.Build();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var center = ZoneShape.Center(z.Parts);
            var c3 = Coords.World(center.X, center.Z);
            float area = ZoneShape.Area(z.Parts);
            if (z.Kind == ZoneKind.W)
            {
                var w = ModelLibrary.Spawn("Workshop", g.transform);
                w.Root.transform.localPosition = c3;
                w.Root.transform.localRotation = Coords.GateRotation(z.Gate);
            }
            else
            {
                Labels.Flat(g.transform, z.Kind.ToString(), c3 + new Vector3(0, 0.02f, 0), Mathf.Clamp(Mathf.Sqrt(area) * 0.5f, 0.7f, 2.5f), Color.Lerp(color, Color.black, 0.5f));
                stacks[z.Id] = new StockStacks(g.transform, ZoneTypes.Stores(z.Kind), c3);
            }
            var warn = Labels.Standing(g.transform, "!", c3 + new Vector3(0, 1.4f, 0), 1.6f, Palette.Bad);
            warn.fontStyle = FontStyle.Bold;
            warnings[z.Id] = warn;
        }

        void BuildBuilding(Building b)
        {
            if (b.Type != BuildingTypeId.exportExit) return;
            var g = Group("Export " + b.Id);
            var f = Buildings.FootprintOf(b);
            var m = ModelLibrary.Spawn("ExportExit", g.transform);
            m.Root.transform.localPosition = Coords.Center(f);
            m.Root.transform.localRotation = Coords.GateRotation(Sites.ExitGate(f));
            var warn = Labels.Standing(g.transform, "!", Coords.Center(f, 2.0f), 1.6f, Palette.Bad);
            warnings[b.Id] = warn;
        }

        /// <summary>Bestände und Warnsymbole aktualisieren (günstig, mehrmals je Sekunde).</summary>
        public void Refresh(GameState state, Camera cam)
        {
            var net = new RoadNetwork(state);
            foreach (var z in state.Zones)
            {
                if (stacks.TryGetValue(z.Id, out var s)) s.Update(z.Stock, Stock.ZoneCapacity(z));
                if (warnings.TryGetValue(z.Id, out var w))
                {
                    bool show = !Access.AccessCell(net, z.Parts.ConvertAll(p => (Footprint)p), z.Gate).HasValue;
                    Billboard(w, cam, show);
                }
            }
            foreach (var b in state.Buildings)
            {
                if (!warnings.TryGetValue(b.Id, out var w)) continue;
                var site = Sites.ById(state, b.Id);
                Billboard(w, cam, site != null && !Sites.AccessOf(net, site).HasValue);
            }
        }

        public static void Billboard(TextMesh t, Camera cam, bool show)
        {
            if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
            if (show) t.transform.rotation = Quaternion.LookRotation(t.transform.position - cam.transform.position);
        }
    }

    /// <summary>Kistenstapel je Ware: Höhe zeigt den Füllstand, bei vollem Lager ein Schild „voll“.</summary>
    public sealed class StockStacks
    {
        const int MaxCrates = 6;
        readonly ProductId[] products;
        readonly List<ModelInstance>[] crates;
        readonly TextMesh full;

        public StockStacks(Transform parent, ProductId[] products, Vector3 center)
        {
            this.products = products;
            crates = new List<ModelInstance>[products.Length];
            float spread = 0.32f;
            for (int i = 0; i < products.Length; i++)
            {
                crates[i] = new List<ModelInstance>();
                float ox = (i - (products.Length - 1) / 2f) * spread;
                for (int k = 0; k < MaxCrates; k++)
                {
                    var c = ModelLibrary.Spawn("Crate", parent);
                    c.Root.transform.localScale = Vector3.one * 2.2f;
                    c.Root.transform.localPosition = center + new Vector3(ox, k * 0.22f, 0.55f);
                    c.SetColor("L_Crate", Palette.Hex(Products.Color(products[i])));
                    c.SetActive(false);
                    crates[i].Add(c);
                }
            }
            full = Labels.Standing(parent, "voll", center + new Vector3(0, 1.8f, 0.55f), 0.8f, Palette.Bad);
            full.gameObject.SetActive(false);
        }

        public void Update(Dictionary<ProductId, int> stock, int capacity)
        {
            bool anyFull = false;
            for (int i = 0; i < products.Length; i++)
            {
                int n = Sim.Stock.Of(stock, products[i]);
                if (capacity > 0 && n >= capacity) anyFull = true;
                int show = n <= 0 || capacity <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(n * MaxCrates / (float)capacity), 1, MaxCrates);
                for (int k = 0; k < MaxCrates; k++) crates[i][k].SetActive(k < show);
            }
            if (full.gameObject.activeSelf != anyFull) full.gameObject.SetActive(anyFull);
            if (anyFull && Camera.main != null) full.transform.rotation = Quaternion.LookRotation(full.transform.position - Camera.main.transform.position);
        }
    }
}
