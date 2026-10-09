using System;
using System.Collections.Generic;
using System.Linq;
using Logistikum.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Logistikum.Game
{
    /// <summary>
    /// Infofenster der Auswahl (T1.7): Zonen, Hallen, Bänder, Export-Ausfahrten, Fahrzeuge. Aktualisiert sich
    /// live, aber nicht, solange die Maus darüber ist (damit Klicks nicht verloren gehen).
    /// </summary>
    public sealed class InfoPanel
    {
        readonly GameApp app;
        readonly VisualElement panel, body;
        readonly Label title;
        bool pointerInside;
        float timer;
        public Pick Current = Pick.None;

        public InfoPanel(GameApp app, VisualElement layer)
        {
            this.app = app;
            panel = Ui.Div("lg-panel", "lg-info");
            panel.style.display = DisplayStyle.None;
            title = Ui.Text("", "lg-title");
            panel.Add(Ui.Row(title, Ui.Spacer(), Ui.Btn("✕", () => app.Select(Pick.None))));
            var scroll = Ui.Scroll(620);
            body = new VisualElement();
            scroll.Add(body);
            panel.Add(scroll);
            panel.RegisterCallback<PointerEnterEvent>(_ => pointerInside = true);
            panel.RegisterCallback<PointerLeaveEvent>(_ => pointerInside = false);
            layer.Add(panel);
        }

        public void Show(Pick p)
        {
            Current = p;
            pointerInside = false;
            panel.style.display = p.Kind == PickKind.None ? DisplayStyle.None : DisplayStyle.Flex;
            Rebuild();
        }

        public void Update(float dt)
        {
            if (Current.Kind == PickKind.None) return;
            timer -= dt;
            if (timer > 0 || pointerInside) return;
            timer = 0.4f;
            Rebuild();
        }

        public void Refresh() { timer = 0; pointerInside = false; }

        void Rebuild()
        {
            body.Clear();
            var s = app.State;
            switch (Current.Kind)
            {
                case PickKind.Zone: { var z = s.ZoneById(Current.Id); if (z == null) Gone(); else ZoneInfo(z); break; }
                case PickKind.Hall: { var h = s.HallById(Current.Id); if (h == null) Gone(); else HallInfo(h); break; }
                case PickKind.Building: { var b = s.BuildingById(Current.Id); if (b == null) Gone(); else ExitInfo(b); break; }
                case PickKind.Conveyor: { var c = s.Conveyors.Find(x => x.Id == Current.Id); if (c == null) Gone(); else BeltInfo(c); break; }
                case PickKind.Vehicle: { var v = s.VehicleById(Current.Id); if (v == null) Gone(); else VehicleInfo(v); break; }
            }
        }

        void Gone() => app.Select(Pick.None);

        void Line(string label, string value, string cls = null)
        {
            var row = Ui.Row(Ui.Text(label, "lg-muted"), Ui.Spacer(), cls == null ? Ui.Text(value) : Ui.Text(value, "lg-text", cls));
            body.Add(row);
        }

        void Head(string text) => body.Add(Ui.Text(text, "lg-subtitle"));

        void StockLines(Dictionary<ProductId, int> stock, IEnumerable<ProductId> products, int capacity, bool skipEmpty = false)
        {
            foreach (var p in products)
            {
                int n = Stock.Of(stock, p);
                if (skipEmpty && n == 0) continue;
                var row = Ui.Row(Ui.Swatch(Palette.Hex(Products.Color(p))), Ui.Text(T.Product(p)), Ui.Spacer(), Ui.Text(n + " / " + capacity));
                body.Add(row);
                body.Add(Ui.Bar(capacity > 0 ? n / (float)capacity : 0));
            }
        }

        void Bays(int siteId)
        {
            var (used, queue) = Sim.Bays.Status(app.State, siteId);
            Line(T.Bays, T.BaysLine(used, Sim.Bays.Capacity(app.State, siteId), queue));
        }

        void Connection(Site site)
        {
            bool ok = site != null && Sites.AccessOf(new RoadNetwork(app.State), site).HasValue;
            body.Add(Ui.Text(ok ? T.Connected : T.NotConnected, "lg-text", ok ? "lg-good" : "lg-warn"));
        }

        void GateButtons(Side current, Action<Side> set)
        {
            var row = Ui.Row(Ui.Text(T.GateLabel, "lg-muted"), Ui.Spacer());
            foreach (var side in Access.Sides)
            {
                var b = Ui.Btn(side == Side.N ? "N" : side == Side.E ? "O" : side == Side.S ? "S" : "W", () => { set(side); Refresh(); });
                b.tooltip = T.Gate(side) + ": Tor auf diese Seite legen";
                b.EnableInClassList("lg-active", side == current);
                row.Add(b);
            }
            body.Add(row);
        }

        void ZoneInfo(Zone z)
        {
            var s = app.State;
            title.text = Names.Site(s, z.Id);
            int area = ZoneShape.Area(z.Parts);
            Line("Fläche", area + (area == 1 ? " Feld" : " Felder"));
            Line(T.Status, T.ZoneStatus(Production.StatusOf(z)));
            Connection(Sites.ById(s, z.Id));
            GateButtons(z.Gate, side => app.Run(new SetZoneGateCommand { ZoneId = z.Id, Gate = side }));
            Bays(z.Id);
            if (z.Kind != ZoneKind.W)
            {
                Head(T.StockLabel);
                StockLines(z.Stock, ZoneTypes.Stores(z.Kind), Stock.ZoneCapacity(z));
            }
            var del = Ui.Btn(T.DemolishZone, () => app.Dialogs.Confirm(T.DemolishZone, T.DemolishConfirm(Fmt.Euro(Zones.Refund(app.State, z))), T.Demolish,
                () => app.Run(new DemolishZoneCommand { ZoneId = z.Id })), "lg-danger");
            del.style.marginTop = 10;
            body.Add(del);
        }

        void ExitInfo(Building b)
        {
            var s = app.State;
            title.text = Names.Site(s, b.Id);
            Connection(Sites.ById(s, b.Id));
            Bays(b.Id);
            Head("Kauft");
            foreach (var kv in GoodsConfig.ExportPriceCents)
                body.Add(Ui.Row(Ui.Swatch(Palette.Hex(Products.Color(kv.Key))), Ui.Text(T.Product(kv.Key)), Ui.Spacer(), Ui.Text(Fmt.Euro(kv.Value) + " je Einheit")));
        }

        void HallInfo(Hall h)
        {
            var s = app.State;
            title.text = Names.Site(s, h.Id);
            Line("Größe", h.Width + " × " + h.Depth + " Felder");
            Connection(Sites.ById(s, h.Id));
            GateButtons(h.Gate, side => app.Run(new SetHallGateCommand { HallId = h.Id, Gate = side }));
            Bays(h.Id);
            body.Add(Ui.Text(T.HallHint, "lg-muted"));
            Head(T.Forklifts);
            Line(T.Forklifts, T.ForkliftLine(h.Forklifts.Count, Fmt.Euro(HallConfig.ForkliftDailyCents)));
            var row = Ui.Row(Ui.Btn(T.BuyForklift + " (" + Fmt.Euro(HallConfig.ForkliftPriceCents) + ")", () => { app.Run(new BuyForkliftCommand { HallId = h.Id }); Refresh(); }, "lg-primary"));
            if (h.Forklifts.Count > 0) row.Add(Ui.Btn(T.SellForklift, () => { app.Run(new SellForkliftCommand { ForkliftId = h.Forklifts[h.Forklifts.Count - 1].Id }); Refresh(); }));
            body.Add(row);
            if (h.Forklifts.Count == 0) body.Add(Ui.Text(T.NoForklifts, "lg-text", "lg-warn"));
            Head(T.Areas);
            if (h.Areas.Count == 0) body.Add(Ui.Text(T.NoAreas, "lg-muted"));
            foreach (var kind in HallProcessing.Order)
            {
                foreach (var a in h.Areas.Where(x => x.Kind == kind))
                {
                    var head = Ui.Row(Ui.Swatch(Palette.AreaColor(a.Kind)), Ui.Text(T.Area(a.Kind) + " (" + a.Rect.Width + " × " + a.Rect.Depth + ")"), Ui.Spacer());
                    if (HallProcessing.StageLevel(a.Kind) > 0) head.Add(Ui.Text(T.AreaWork(HallProcessing.IsWorking(a)), "lg-muted"));
                    body.Add(head);
                    StockLines(a.Stock, HallProcessing.HallProducts, Stock.AreaCapacity(a), true);
                }
            }
            var del = Ui.Btn(T.DemolishHall, () => app.Dialogs.Confirm(T.DemolishHall, T.DemolishConfirm(Fmt.Euro(Halls.Refund(app.State, h))), T.Demolish,
                () => app.Run(new DemolishHallCommand { HallId = h.Id })), "lg-danger");
            del.style.marginTop = 10;
            body.Add(del);
        }

        void BeltInfo(Conveyor c)
        {
            var s = app.State;
            title.text = T.Conveyor;
            Line("Von", Names.Site(s, c.FromSiteId));
            Line("Nach", Names.Site(s, c.ToSiteId));
            Line("Länge", c.Cells.Count + " Felder");
            Line(T.Status, Conveyors.IsStalled(c) ? T.Stalled : T.Running, Conveyors.IsStalled(c) ? "lg-warn" : "lg-good");
            body.Add(Ui.Text(T.BeltLine(c.Items.Count), "lg-muted"));
            var first = c.Cells[0];
            body.Add(Ui.Btn(T.DemolishConveyor, () => app.Run(new DemolishConveyorCommand { X = first.X, Z = first.Z }), "lg-danger"));
        }

        void VehicleInfo(Vehicle v)
        {
            var s = app.State;
            title.text = Names.Vehicle(s, v);
            if (v is Supplier sup)
            {
                Line(T.Status, T.SupplierPhase(sup.Phase));
                Line(T.Cargo, sup.Cargo == null ? T.Empty : sup.Cargo.Quantity + " × " + T.Product(sup.Cargo.Product));
                Line(T.Target, Names.Site(s, sup.TargetId));
                return;
            }
            var t = (Truck)v;
            var values = Fleet.ValuesOf(t);
            Line("Fahrzeug", T.Model(t.Model) + ", " + T.Drive(t.Drive) + ", lädt " + values.Capacity);
            string status = t.Upkeep.BrokenTicks > 0
                ? T.Broken(Mathf.CeilToInt(t.Upkeep.BrokenTicks / (float)GameTime.TicksPerHour))
                : t.Phase == TruckPhase.idle && t.IdleReason.HasValue ? T.IdleReason(t.IdleReason.Value) : T.TruckPhase(t.Phase);
            Line(T.Status, status, t.Upkeep.BrokenTicks > 0 ? "lg-warn" : null);
            Line(T.Cargo, t.Cargo == null ? T.Empty : t.Cargo.Quantity + " × " + T.Product(t.Cargo.Product));
            var dest = Destination.Of(s, t);
            Line(T.Target, dest.HasValue ? Names.Site(s, dest.Value) : T.None);
            var choices = new List<string> { T.Auto };
            choices.AddRange(s.Tours.Select(x => x.Name));
            int index = t.TourId == null ? 0 : s.Tours.FindIndex(x => x.Id == t.TourId) + 1;
            body.Add(Ui.Dropdown(T.Mode, choices, index, i => { app.Run(new AssignTourCommand { TruckId = t.Id, TourId = i <= 0 ? (int?)null : app.State.Tours[i - 1].Id }); Refresh(); }));
            body.Add(Ui.Btn("Touren …", app.Windows.ToggleTours));
            Head(T.Condition);
            Line(T.Condition, T.ConditionLine(t.Upkeep.Condition / 1000, UpkeepRules.KmUntilService(t)));
            Line(T.Breakdowns, t.Upkeep.Breakdowns == 0 ? "keine" : t.Upkeep.Breakdowns + (t.Upkeep.LastBreakdownTick.HasValue ? ", zuletzt am " + Fmt.GameDate(t.Upkeep.LastBreakdownTick.Value) : ""));
            Line("Letzte Wartung", t.Upkeep.LastServiceTick.HasValue ? Fmt.GameDate(t.Upkeep.LastServiceTick.Value) : "noch nie");
            var service = Ui.Btn(T.ToWorkshop, () => { app.Run(new ServiceVehicleCommand { TruckId = t.Id }); Refresh(); });
            service.tooltip = "Nach dem laufenden Auftrag bzw. Halt zur nächsten Werkstatt fahren.";
            body.Add(service);
            if (t.Upkeep.ServiceRequested) body.Add(Ui.Text("Fährt nach dem laufenden Auftrag zur Werkstatt.", "lg-muted"));
            Head("Besitz");
            if (t.Lease != null)
            {
                Line("Leasing", T.Leased(Fmt.Euro(t.Lease.MonthlyCents), Fmt.GameDate(t.Lease.EndTick)));
                long penalty = Fleet.ReturnPenaltyCents(t);
                body.Add(Ui.Btn(T.GiveBack, () => app.Dialogs.Confirm(T.GiveBack, T.GiveBackConfirm(Names.Vehicle(app.State, t), Fmt.Euro(penalty)), T.GiveBack,
                    () => app.Run(new DisposeVehicleCommand { TruckId = t.Id })), "lg-danger"));
            }
            else
            {
                long residual = Fleet.ResidualCents(s, t);
                Line("Kauf", T.Owned(Fmt.Euro(residual)));
                body.Add(Ui.Btn(T.Sell, () => app.Dialogs.Confirm(T.Sell, T.SellConfirm(Names.Vehicle(app.State, t), Fmt.Euro(residual)), T.Sell,
                    () => app.Run(new DisposeVehicleCommand { TruckId = t.Id })), "lg-danger"));
            }
        }
    }
}
