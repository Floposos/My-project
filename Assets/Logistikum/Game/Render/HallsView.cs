using System.Collections.Generic;
using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>
    /// Hallen (M3): Boden, Wände mit Toröffnung, Dach (blendet sich beim Heranzoomen und per Taste H aus,
    /// Frage 2), farbige Bereiche mit Schild und Kisten, Gabelstapler.
    /// </summary>
    public sealed class HallsView
    {
        const float WallHeight = 1.4f;
        /// <summary>Unter diesem Kameraabstand sind die Dächer ausgeblendet.</summary>
        public const float RoofHideDistance = 45f;

        readonly Transform root;
        readonly List<GameObject> built = new List<GameObject>();
        readonly List<GameObject> roofs = new List<GameObject>();
        // Schilder der Bereiche: Schrift ignoriert die Tiefe, daher nur ohne Dach zeigen.
        readonly List<GameObject> areaLabels = new List<GameObject>();
        readonly Dictionary<int, StockStacks> areaStacks = new Dictionary<int, StockStacks>();
        readonly Dictionary<int, ModelInstance> forklifts = new Dictionary<int, ModelInstance>();
        readonly Dictionary<int, List<ModelInstance>> forkliftCrates = new Dictionary<int, List<ModelInstance>>();
        readonly Transform forkliftRoot;
        public bool RoofsForcedOff;

        public HallsView(Transform parent)
        {
            root = new GameObject("Hallen").transform;
            root.SetParent(parent, false);
            forkliftRoot = new GameObject("Stapler").transform;
            forkliftRoot.SetParent(root, false);
        }

        public void Rebuild(GameState state)
        {
            foreach (var go in built) Object.Destroy(go);
            built.Clear();
            roofs.Clear();
            areaLabels.Clear();
            areaStacks.Clear();
            foreach (var h in state.Halls) BuildHall(h);
        }

        void BuildHall(Hall h)
        {
            var g = new GameObject("Halle " + h.Id);
            g.transform.SetParent(root, false);
            built.Add(g);
            var r = h.Rect;
            float x0 = r.X, x1 = r.X + r.Width, z0 = -(r.Z + r.Depth), z1 = -r.Z;
            var floor = new MeshBuilder(1);
            floor.Flat(x0, z0, x1, z1, 0.006f);
            var (_, ff, fr) = MeshBuilder.Object("Boden", g.transform, Palette.Lit(Palette.HallFloor));
            ff.sharedMesh = floor.Build();
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Wände mit Toröffnung in der Mitte der Tor-Seite.
            var walls = new MeshBuilder(2);
            const float t = 0.1f;
            float gateW = Mathf.Min(3f, (h.Gate == Side.N || h.Gate == Side.S ? r.Width : r.Depth) - 1f);
            void Wall(float ax, float az, float bx, float bz, bool gate)
            {
                bool horizontal = Mathf.Approximately(az, bz);
                float len = horizontal ? bx - ax : bz - az;
                if (!gate)
                {
                    walls.Box(new Vector3(Mathf.Min(ax, bx) - (horizontal ? 0 : t / 2), 0, Mathf.Min(az, bz) - (horizontal ? t / 2 : 0)),
                        new Vector3(Mathf.Max(ax, bx) + (horizontal ? 0 : t / 2), WallHeight, Mathf.Max(az, bz) + (horizontal ? t / 2 : 0)));
                    return;
                }
                float a = (len - gateW) / 2f;
                if (horizontal)
                {
                    walls.Box(new Vector3(ax, 0, az - t / 2), new Vector3(ax + a, WallHeight, az + t / 2));
                    walls.Box(new Vector3(bx - a, 0, az - t / 2), new Vector3(bx, WallHeight, az + t / 2));
                    walls.Box(new Vector3(ax + a, WallHeight * 0.75f, az - t / 2), new Vector3(bx - a, WallHeight, az + t / 2));
                    walls.Flat(ax + a, az - (h.Gate == Side.S ? 0.6f : -0.6f), bx - a, az, 0.016f, 1);
                }
                else
                {
                    walls.Box(new Vector3(ax - t / 2, 0, az), new Vector3(ax + t / 2, WallHeight, az + a));
                    walls.Box(new Vector3(ax - t / 2, 0, bz - a), new Vector3(ax + t / 2, WallHeight, bz));
                    walls.Box(new Vector3(ax - t / 2, WallHeight * 0.75f, az + a), new Vector3(ax + t / 2, WallHeight, bz - a));
                    walls.Flat(ax, az + a, ax + (h.Gate == Side.E ? 0.6f : -0.6f), bz - a, 0.016f, 1);
                }
            }
            Wall(x0, z1, x1, z1, h.Gate == Side.N);
            Wall(x0, z0, x1, z0, h.Gate == Side.S);
            Wall(x0, z0, x0, z1, h.Gate == Side.W);
            Wall(x1, z0, x1, z1, h.Gate == Side.E);
            var (_, wf, _) = MeshBuilder.Object("Wände", g.transform, Palette.Lit(Palette.HallWall), Palette.Lit(Palette.Warning));
            wf.sharedMesh = walls.Build();

            // Dach als flache Platte mit Lichtband.
            var roof = new MeshBuilder(2);
            roof.Box(new Vector3(x0 - 0.15f, WallHeight, z0 - 0.15f), new Vector3(x1 + 0.15f, WallHeight + 0.12f, z1 + 0.15f), 0, true);
            for (float x = x0 + 1.5f; x < x1 - 1f; x += 3f) roof.Flat(x, z0 + 0.4f, x + 0.6f, z1 - 0.4f, WallHeight + 0.125f, 1);
            var (rg, rf, _) = MeshBuilder.Object("Dach", g.transform, Palette.Lit(Palette.HallRoof), Palette.Lit(Palette.Hex(0xd6e6f2), 0.8f));
            rf.sharedMesh = roof.Build();
            roofs.Add(rg);

            foreach (var a in h.Areas)
            {
                var ar = a.Rect;
                var mb = new MeshBuilder(1);
                mb.Flat(ar.X + 0.05f, -(ar.Z + ar.Depth) + 0.05f, ar.X + ar.Width - 0.05f, -ar.Z - 0.05f, 0.012f);
                var (_, af, amr) = MeshBuilder.Object(T.Area(a.Kind), g.transform, Palette.Lit(Palette.AreaColor(a.Kind)));
                af.sharedMesh = mb.Build();
                amr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var c = Coords.Center(ar);
                float size = Mathf.Clamp(Mathf.Min(ar.Width, ar.Depth) * 0.22f, 0.25f, 0.6f);
                areaLabels.Add(Labels.Flat(g.transform, T.Area(a.Kind), c + new Vector3(0, 0.02f, ar.Depth * 0.5f - size * 0.8f), size, Color.Lerp(Palette.AreaColor(a.Kind), Color.black, 0.55f)).gameObject);
                areaStacks[a.Id] = new StockStacks(g.transform, HallProcessing.HallProducts, c + new Vector3(0, 0, -0.5f));
            }
        }

        public void Refresh(GameState state, float cameraDistance)
        {
            bool showRoofs = !RoofsForcedOff && cameraDistance >= RoofHideDistance;
            foreach (var r in roofs) if (r.activeSelf != showRoofs) r.SetActive(showRoofs);
            foreach (var l in areaLabels) if (l.activeSelf == showRoofs) l.SetActive(!showRoofs);
            foreach (var h in state.Halls)
                foreach (var a in h.Areas)
                    if (areaStacks.TryGetValue(a.Id, out var s)) s.Update(a.Stock, Stock.AreaCapacity(a));
        }

        /// <summary>Stapler je Bild: Lage zwischen zwei Feldern, Ladung als Kiste auf der Gabel.</summary>
        public void SyncForklifts(GameState state, float alpha)
        {
            var seen = new HashSet<int>();
            foreach (var h in state.Halls)
            {
                foreach (var f in h.Forklifts)
                {
                    seen.Add(f.Id);
                    if (!forklifts.TryGetValue(f.Id, out var m))
                    {
                        m = ModelLibrary.Spawn("Forklift", forkliftRoot);
                        m.Root.transform.localScale = Vector3.one * 2f;
                        forklifts[f.Id] = m;
                        var crates = new List<ModelInstance>();
                        for (int i = 0; i < 2; i++)
                        {
                            var c = ModelLibrary.Spawn("Crate", m.Root.transform);
                            c.Root.transform.localScale = Vector3.one * 0.7f;
                            c.Root.transform.localPosition = new Vector3(0.15f, 0.03f + i * 0.075f, 0);
                            crates.Add(c);
                        }
                        forkliftCrates[f.Id] = crates;
                    }
                    var here = f.Route[0];
                    Vector3 pos = Coords.CellCenter(here);
                    float yaw = m.Root.transform.localEulerAngles.y;
                    if (f.Route.Count > 1)
                    {
                        var next = f.Route[1];
                        bool moving = f.Phase == ForkliftPhase.toPickup || f.Phase == ForkliftPhase.toDropoff;
                        float t = Mathf.Clamp01((f.Progress + (moving ? HallConfig.ForkliftSpeed * alpha : 0)) / 1000f);
                        pos = Vector3.Lerp(Coords.CellCenter(here), Coords.CellCenter(next), t);
                        yaw = Coords.Yaw(next.X - here.X, next.Z - here.Z) + ModelLibrary.VehicleYawOffset;
                    }
                    m.Root.transform.localPosition = pos;
                    m.Root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                    var cr = forkliftCrates[f.Id];
                    int n = f.Cargo == null ? 0 : Mathf.Min(2, (f.Cargo.Quantity + 1) / 2);
                    for (int i = 0; i < cr.Count; i++)
                    {
                        cr[i].SetActive(i < n);
                        if (f.Cargo != null) cr[i].SetColor("L_Crate", Palette.Hex(Products.Color(f.Cargo.Product)));
                    }
                }
            }
            var gone = new List<int>();
            foreach (var kv in forklifts) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone)
            {
                Object.Destroy(forklifts[id].Root);
                forklifts.Remove(id);
                forkliftCrates.Remove(id);
            }
        }
    }

    /// <summary>Förderbänder (M3): Band mit Laufrichtungspfeilen, Kisten laufen sichtbar mit.</summary>
    public sealed class ConveyorsView
    {
        readonly Transform root;
        readonly MeshFilter mf;
        readonly Mesh mesh = new Mesh { name = "Bänder" };
        readonly List<ModelInstance> pool = new List<ModelInstance>();
        readonly Transform itemRoot;

        public ConveyorsView(Transform parent)
        {
            root = new GameObject("Förderbänder").transform;
            root.SetParent(parent, false);
            var (_, f, _) = MeshBuilder.Object("Band", root, Palette.Lit(Palette.Belt), Palette.Lit(Palette.BeltArrow), Palette.Lit(Palette.Hex(0x9aa3ad)));
            mf = f;
            mf.sharedMesh = mesh;
            itemRoot = new GameObject("Kisten").transform;
            itemRoot.SetParent(root, false);
        }

        public const float BeltHeight = 0.14f;

        public void Rebuild(GameState state)
        {
            var mb = new MeshBuilder(3);
            foreach (var c in state.Conveyors)
            {
                for (int i = 0; i < c.Cells.Count; i++)
                {
                    var cell = c.Cells[i];
                    float x0 = cell.X + 0.25f, x1 = cell.X + 0.75f, z0 = -(cell.Z + 0.75f), z1 = -(cell.Z + 0.25f);
                    var prev = i > 0 ? c.Cells[i - 1] : (Cell?)null;
                    var next = i < c.Cells.Count - 1 ? c.Cells[i + 1] : (Cell?)null;
                    // Band bis zu den Nachbarfeldern verlängern (Kurven schließen).
                    foreach (var n in new[] { prev, next })
                    {
                        if (!n.HasValue) continue;
                        int dx = n.Value.X - cell.X, dz = n.Value.Z - cell.Z;
                        if (dx > 0) x1 = cell.X + 1; if (dx < 0) x0 = cell.X;
                        if (dz > 0) z0 = -(cell.Z + 1); if (dz < 0) z1 = -cell.Z;
                    }
                    if (i == 0 || i == c.Cells.Count - 1)
                    {
                        // Ende reicht bis an den Ort heran.
                        Cell end = i == 0 ? Neighbour(state, cell, c.FromSiteId) : Neighbour(state, cell, c.ToSiteId);
                        int dx = end.X - cell.X, dz = end.Z - cell.Z;
                        if (dx > 0) x1 = cell.X + 1; if (dx < 0) x0 = cell.X;
                        if (dz > 0) z0 = -(cell.Z + 1); if (dz < 0) z1 = -cell.Z;
                    }
                    mb.Box(new Vector3(x0, 0, z0), new Vector3(x1, BeltHeight, z1), 0);
                    mb.Box(new Vector3(cell.X + 0.2f, 0, -(cell.Z + 0.8f)), new Vector3(cell.X + 0.8f, 0.03f, -(cell.Z + 0.2f)), 2);
                    if (next.HasValue)
                    {
                        // Pfeil in Laufrichtung (Dreieck aus zwei schmalen Streifen).
                        float dx = next.Value.X - cell.X, dz = -(next.Value.Z - cell.Z);
                        var cpos = new Vector3(cell.X + 0.5f, BeltHeight + 0.002f, -(cell.Z + 0.5f));
                        var dir = new Vector3(dx, 0, dz);
                        var side = new Vector3(-dz, 0, dx);
                        var tip = cpos + dir * 0.18f;
                        mb.Face(cpos - dir * 0.05f + side * 0.12f, tip + side * 0.02f, tip - side * 0.02f, cpos - dir * 0.05f - side * 0.12f, Vector3.up, 1);
                    }
                }
            }
            mb.Build(mesh);
        }

        static Cell Neighbour(GameState state, Cell cell, int siteId)
        {
            var occ = new Occupancy(state);
            for (int d = 0; d < 4; d++)
            {
                var n = new Cell(cell.X + RoadNetwork.Dx[d], cell.Z + RoadNetwork.Dz[d]);
                if (occ.At(n.X, n.Z) == siteId) return n;
            }
            return cell;
        }

        public void SyncItems(GameState state, float alpha)
        {
            int used = 0;
            foreach (var c in state.Conveyors)
            {
                int length = c.Cells.Count * 1000;
                bool stalled = Conveyors.IsStalled(c);
                float limit = length;
                foreach (var item in c.Items)
                {
                    float pos = Mathf.Min(item.Position + (stalled ? 0 : HallConfig.BeltSpeed * alpha), Mathf.Min(length, limit));
                    limit = pos - HallConfig.BeltSpacing;
                    if (used >= pool.Count)
                    {
                        var m = ModelLibrary.Spawn("Crate", itemRoot);
                        m.Root.transform.localScale = Vector3.one * 2f;
                        pool.Add(m);
                    }
                    var crate = pool[used++];
                    crate.SetActive(true);
                    crate.SetColor("L_Crate", Palette.Hex(Products.Color(item.Product)));
                    crate.Root.transform.localPosition = PathPoint(c.Cells, pos / 1000f) + new Vector3(0, BeltHeight, 0);
                }
            }
            for (int i = used; i < pool.Count; i++) pool[i].SetActive(false);
        }

        /// <summary>Punkt auf dem Band: u = 0 am Anfang des ersten Felds, u = Feldzahl am Ende des letzten.</summary>
        static Vector3 PathPoint(List<Cell> cells, float u)
        {
            float k = u - 0.5f;
            if (k <= 0 || cells.Count == 1) return Coords.CellCenter(cells[0]);
            int i = Mathf.Min((int)k, cells.Count - 1);
            if (i >= cells.Count - 1) return Coords.CellCenter(cells[cells.Count - 1]);
            return Vector3.Lerp(Coords.CellCenter(cells[i]), Coords.CellCenter(cells[i + 1]), k - i);
        }
    }
}
