using System.Collections.Generic;
using Logistikum.Sim;
using UnityEngine;
using Grid = Logistikum.Sim.Grid;

namespace Logistikum.Game
{
    /// <summary>Lage eines Fahrzeugs (wie vehiclePose der Browser-Version): Rechtsverkehr, gleitet zwischen Schritten.</summary>
    public static class VehiclePose
    {
        public const float LaneOffset = 0.22f;
        public const float ParkedOffset = 0.42f;

        /// <summary>Position (Sim-Koordinaten) und Richtung (dx, dz) auf der Route.</summary>
        public static (float x, float z, float dx, float dz) At(List<Cell> route, float progress, int heading, float offset)
        {
            var first = route[0];
            int index = 0;
            float t = progress;
            while (t >= 1000 && index < route.Count - 2) { t -= 1000; index++; }
            var a = route[index];
            if (index + 1 >= route.Count)
            {
                float hx, hz;
                if (index > 0) { hx = a.X - route[index - 1].X; hz = a.Z - route[index - 1].Z; }
                else { hx = RoadNetwork.Dx[heading]; hz = RoadNetwork.Dz[heading]; }
                return Place(a.X, a.Z, hx, hz, 0, offset);
            }
            var b = route[index + 1];
            return Place(a.X, a.Z, b.X - a.X, b.Z - a.Z, Mathf.Min(1, t / 1000f), offset);
        }

        static (float, float, float, float) Place(float cx, float cz, float dx, float dz, float t, float offset) =>
            (cx + 0.5f + dx * t - dz * offset, cz + 0.5f + dz * t + dx * offset, dx, dz);

        public static int SpeedOf(Vehicle v) => v is Truck t ? Fleet.ValuesOf(t).Speed : VehicleConfig.SupplierSpeed;

        /// <summary>Fährt das Fahrzeug gerade (für die Vorausschau zwischen zwei Schritten)?</summary>
        public static bool Moving(Vehicle v)
        {
            if (v.OffRoad || v.Route.Count < 2 || v.WaitTicks > 0) return false;
            if (v is Truck t) return t.Upkeep.BrokenTicks == 0 && (t.Phase == TruckPhase.toPickup || t.Phase == TruckPhase.toDropoff || t.Phase == TruckPhase.toWorkshop);
            var s = (Supplier)v;
            return s.Phase == SupplierPhase.toSite || s.Phase == SupplierPhase.toExit;
        }

        public static Vector3 WorldPosition(Vehicle v, float alpha, out float yaw)
        {
            float extra = Moving(v) ? SpeedOf(v) * alpha : 0;
            var (x, z, dx, dz) = At(v.Route, v.Progress + extra, v.Heading, v.OffRoad ? ParkedOffset : LaneOffset);
            yaw = Coords.Yaw(dx, dz);
            return Coords.World(x, z);
        }
    }

    /// <summary>Alle Fahrzeuge: Modell je Typ, Lack, Tourfarbe, Ladung als Kisten, Panne, Auswahl.</summary>
    public sealed class VehiclesView
    {
        sealed class Entry
        {
            public Transform Frame;
            public ModelInstance Model;
            public string ModelName;
            public List<ModelInstance> Crates = new List<ModelInstance>();
            public TextMesh Warning;
        }

        readonly Transform root;
        readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        readonly GameObject ring;
        public int? Selected;

        public VehiclesView(Transform parent)
        {
            root = new GameObject("Fahrzeuge").transform;
            root.SetParent(parent, false);
            var mb = new MeshBuilder();
            mb.Flat(-0.5f, -0.5f, 0.5f, 0.5f, 0.02f);
            var (go, mf, _) = MeshBuilder.Object("Auswahl", root, Palette.Transparent(new Color(1, 1, 1, 0.55f)));
            mf.sharedMesh = mb.Build();
            ring = go;
            ring.SetActive(false);
        }

        static string ModelFor(Vehicle v) => v is Truck t && t.Model == VehicleModel.van ? "Van" : "Truck";

        Entry Create(Vehicle v)
        {
            var e = new Entry { ModelName = ModelFor(v) };
            e.Frame = new GameObject("Fahrzeug " + v.Id).transform;
            e.Frame.SetParent(root, false);
            e.Model = ModelLibrary.Spawn(e.ModelName, e.Frame);
            e.Model.Root.transform.localRotation = Quaternion.Euler(0, ModelLibrary.VehicleYawOffset, 0);
            bool van = e.ModelName == "Van";
            int slots = van ? 2 : 6;
            for (int i = 0; i < slots; i++)
            {
                var c = ModelLibrary.Spawn("Crate", e.Frame);
                // Rahmen: +X = vorne, +Z = links. LKW: 3 × 2 auf der Ladefläche; Transporter: 2 auf dem Dach.
                c.Root.transform.localPosition = van ? new Vector3(-0.12f + i * 0.13f, 0.3f, 0) : new Vector3(-0.33f + (i / 2) * 0.13f, 0.12f, (i % 2 == 0 ? -0.06f : 0.06f));
                c.SetActive(false);
                e.Crates.Add(c);
            }
            e.Warning = Labels.Standing(e.Frame, "!", new Vector3(0, 0.9f, 0), 1.2f, Palette.Bad);
            e.Warning.fontStyle = FontStyle.Bold;
            e.Warning.gameObject.SetActive(false);
            return e;
        }

        public void Sync(GameState state, float alpha, Camera cam)
        {
            var seen = new HashSet<int>();
            foreach (var v in state.Vehicles)
            {
                seen.Add(v.Id);
                if (!entries.TryGetValue(v.Id, out var e) || e.ModelName != ModelFor(v))
                {
                    if (e != null) Object.Destroy(e.Frame.gameObject);
                    e = Create(v);
                    entries[v.Id] = e;
                }
                var pos = VehiclePose.WorldPosition(v, alpha, out float yaw);
                e.Frame.localPosition = pos;
                e.Frame.localRotation = Quaternion.Euler(0, yaw, 0);
                Color paint, stripe;
                bool broken = false;
                if (v is Truck t)
                {
                    paint = t.Model == VehicleModel.van ? Palette.VanPaint : Palette.TruckPaint;
                    if (t.Drive == VehicleDrive.electric) paint = Color.Lerp(paint, Palette.Hex(0x7fd1a8), 0.35f);
                    var tour = Tours.TourOf(state, t);
                    stripe = tour != null ? Palette.Hex(TourColors.All[tour.Color % TourColors.All.Length]) : Palette.Hex(TourColors.AutoRoute);
                    broken = t.Upkeep.BrokenTicks > 0;
                }
                else
                {
                    paint = Palette.SupplierPaint;
                    stripe = Palette.Hex(0x5b6470);
                }
                e.Model.SetColor("L_Paint", paint);
                e.Model.SetColor("L_Stripe", stripe);
                int crates = 0;
                if (v.Cargo != null)
                {
                    int cap = v is Truck tt ? Fleet.ValuesOf(tt).Capacity : 20;
                    crates = Mathf.Clamp(Mathf.CeilToInt(v.Cargo.Quantity * e.Crates.Count / (float)cap), 1, e.Crates.Count);
                    var col = Palette.Hex(Products.Color(v.Cargo.Product));
                    foreach (var c in e.Crates) c.SetColor("L_Crate", col);
                }
                for (int i = 0; i < e.Crates.Count; i++) e.Crates[i].SetActive(i < crates);
                ZonesView.Billboard(e.Warning, cam, broken || v.WaitTicks >= TrafficConfig.JamWarnTicks);
                if (Selected == v.Id)
                {
                    ring.SetActive(true);
                    ring.transform.localPosition = pos;
                }
            }
            if (Selected == null || !seen.Contains(Selected.Value)) ring.SetActive(false);
            var gone = new List<int>();
            foreach (var kv in entries) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone)
            {
                Object.Destroy(entries[id].Frame.gameObject);
                entries.Remove(id);
            }
        }

        public void Clear()
        {
            foreach (var e in entries.Values) Object.Destroy(e.Frame.gameObject);
            entries.Clear();
        }
    }

    /// <summary>Weg eines Fahrzeugs als Band in der Tourfarbe (T2.1); „Alle Wege“ zeigt alle.</summary>
    public sealed class RouteLinesView
    {
        readonly Transform root;
        readonly List<LineRenderer> pool = new List<LineRenderer>();
        public bool ShowAll;
        public int? Selected;
        float lastAll;

        public RouteLinesView(Transform parent)
        {
            root = new GameObject("Wege").transform;
            root.SetParent(parent, false);
        }

        LineRenderer Line(int i)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject("Weg");
                go.transform.SetParent(root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.widthMultiplier = 0.12f;
                lr.numCornerVertices = 2;
                lr.alignment = LineAlignment.TransformZ;
                go.transform.rotation = Quaternion.Euler(90, 0, 0);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                pool.Add(lr);
            }
            return pool[i];
        }

        static Color ColorOf(GameState s, Vehicle v)
        {
            var tour = v is Truck t ? Tours.TourOf(s, t) : null;
            return tour != null ? Palette.Hex(TourColors.All[tour.Color % TourColors.All.Length]) : Palette.Hex(TourColors.AutoRoute);
        }

        public void Sync(GameState state, float alpha)
        {
            int used = 0;
            // „Alle Wege“ höchstens viermal je Sekunde neu (gedrosselt wie in der Browser-Version).
            bool refreshAll = ShowAll && Time.unscaledTime - lastAll > 0.25f;
            if (refreshAll) lastAll = Time.unscaledTime;
            foreach (var v in state.Vehicles)
            {
                bool selected = Selected == v.Id;
                if (!selected && !ShowAll) continue;
                if (v.Route.Count < 2) continue;
                if (!selected && !refreshAll) { used++; continue; }
                var lr = Line(used++);
                lr.enabled = true;
                var c = ColorOf(state, v);
                c.a = selected ? 0.95f : 0.6f;
                lr.sharedMaterial = Palette.Transparent(c);
                lr.widthMultiplier = selected ? 0.14f : 0.08f;
                var pts = new List<Vector3> { VehiclePose.WorldPosition(v, alpha, out _) + Vector3.up * 0.06f };
                for (int i = 1; i < v.Route.Count; i++)
                {
                    var (x, z, _, _) = VehiclePose.At(v.Route, i * 1000, v.Heading, VehiclePose.LaneOffset);
                    pts.Add(Coords.World(x, z, 0.06f));
                }
                lr.positionCount = pts.Count;
                lr.SetPositions(pts.ToArray());
            }
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
        }
    }

    /// <summary>Autos auf der Bundesstraße (nur Darstellung, aus der Zeit berechnet; Rushhour: mehr Autos).</summary>
    public sealed class ExternalTrafficView
    {
        readonly List<ModelInstance> cars = new List<ModelInstance>();
        readonly Transform root;
        static readonly Color[] Colors = { Palette.Hex(0xe5484d), Palette.Hex(0x2f80ed), Palette.Hex(0xf5f5f5), Palette.Hex(0x30a46c), Palette.Hex(0x3b3f46), Palette.Hex(0xf5a524) };

        public ExternalTrafficView(Transform parent)
        {
            root = new GameObject("Bundesstraße").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < EntranceConfig.ExternalCarsRush * 2; i++)
            {
                var c = ModelLibrary.Spawn("Car", root);
                c.Root.transform.localScale = Vector3.one * 1.6f;
                c.SetColor("L_Paint", Colors[i % Colors.Length]);
                cars.Add(c);
            }
        }

        public void Sync(int tick, float alpha)
        {
            int perDir = EntranceRules.IsRushHour(tick) ? EntranceConfig.ExternalCarsRush : EntranceConfig.ExternalCars;
            float t = tick + alpha;
            const float span = Grid.Depth + 300f, speed = 0.35f;
            for (int i = 0; i < cars.Count; i++)
            {
                int dir = i % 2;
                int k = i / 2;
                bool on = k < perDir;
                cars[i].SetActive(on);
                if (!on) continue;
                float phase = (k * 977 % 1000) / 1000f * span;
                float s = Mathf.Repeat(phase + t * speed * (1 + (k % 3) * 0.12f), span);
                float z = dir == 0 ? 150 - s : -Grid.Depth - 150 + s;
                float x = TerrainView.HighwayX + (dir == 0 ? -0.75f : 0.75f);
                cars[i].Root.transform.localPosition = new Vector3(x, 0, z);
                cars[i].Root.transform.localRotation = Quaternion.Euler(0, (dir == 0 ? 90 : -90) + ModelLibrary.VehicleYawOffset, 0);
            }
        }
    }

    /// <summary>Vorschau beim Bauen: Felder grün (baubar) oder rot (nicht baubar).</summary>
    public sealed class GhostView
    {
        readonly MeshFilter mf;
        readonly MeshRenderer mr;
        readonly Mesh mesh = new Mesh { name = "Vorschau" };

        public GhostView(Transform parent)
        {
            var (_, f, r) = MeshBuilder.Object("Vorschau", parent, Palette.Transparent(Palette.Ok), Palette.Transparent(Palette.Bad));
            mf = f; mr = r;
            mf.sharedMesh = mesh;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.enabled = false;
        }

        public void Hide() { mr.enabled = false; }

        /// <summary>Felder zeigen; blocked werden rot, die übrigen grün (ok) bzw. rot (nicht ok).</summary>
        public void Cells(IEnumerable<Cell> cells, bool ok, ICollection<Cell> blocked = null, float height = 0.06f)
        {
            var mb = new MeshBuilder(2);
            foreach (var c in cells)
            {
                bool bad = !ok || (blocked != null && blocked.Contains(c));
                mb.Box(new Vector3(c.X + 0.04f, 0.02f, -(c.Z + 1) + 0.04f), new Vector3(c.X + 0.96f, height, -c.Z - 0.04f), bad ? 1 : 0);
            }
            mb.Build(mesh);
            mr.enabled = true;
        }

        public void Rect(Footprint f, bool ok, float height = 0.3f)
        {
            var mb = new MeshBuilder(2);
            mb.Box(new Vector3(f.X + 0.03f, 0.02f, -(f.Z + f.Depth) + 0.03f), new Vector3(f.X + f.Width - 0.03f, height, -f.Z - 0.03f), ok ? 0 : 1);
            mb.Build(mesh);
            mr.enabled = true;
        }
    }
}
