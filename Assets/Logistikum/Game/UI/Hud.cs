using System;
using System.Collections.Generic;
using Logistikum.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Logistikum.Game
{
    /// <summary>Kopfleiste: Datum/Uhrzeit, Pause/1x/2x/4x, Kontostand (öffnet die Kasse), Fenster-Knöpfe.</summary>
    public sealed class TopBar
    {
        readonly GameApp app;
        public readonly VisualElement Root;
        readonly Label clock, balance, paused, rush;
        readonly Button pauseBtn, notices;
        readonly Button[] speedBtns = new Button[3];
        string lastClock, lastBalance, lastNotices;

        public TopBar(GameApp app, VisualElement layer)
        {
            this.app = app;
            Root = Ui.Div("lg-topbar");
            var left = Ui.Div("lg-panel", "lg-topgroup");
            clock = Ui.Text("", "lg-clock");
            left.Add(clock);
            pauseBtn = Ui.Btn(T.Pause, () => app.Session.TogglePause());
            pauseBtn.tooltip = "Pause / Weiter (Leertaste)";
            left.Add(pauseBtn);
            for (int i = 0; i < 3; i++)
            {
                int speed = TimeConfig.Speeds[i];
                speedBtns[i] = Ui.Btn(T.Speed(speed), () => app.Session.SetSpeed(speed));
                speedBtns[i].tooltip = "Geschwindigkeit " + speed + "x (Taste " + (i + 1) + ")";
                left.Add(speedBtns[i]);
            }
            paused = Ui.Text(T.Paused, "lg-paused");
            left.Add(paused);
            rush = Ui.Text(T.Rush, "lg-rush");
            rush.tooltip = "Rushhour an der Einfahrt: Fahrzeuge warten etwa dreimal so lange.";
            left.Add(rush);
            Root.Add(left);

            var right = Ui.Div("lg-panel", "lg-topgroup");
            right.Add(Ui.Btn(T.Purchase, app.Windows.OpenPurchase));
            right.Add(Ui.Btn(T.Stock, app.Windows.ToggleStock));
            right.Add(Ui.Btn(T.Tours, app.Windows.ToggleTours));
            right.Add(Ui.Btn(T.Fleet, app.Windows.ToggleFleet));
            notices = Ui.Btn(T.NoticesCount(0), app.Windows.ToggleNotices);
            right.Add(notices);
            balance = Ui.Text("", "lg-balance");
            var balanceBtn = Ui.Btn("", app.Windows.OpenCash);
            balanceBtn.tooltip = "Kontostand · Klick öffnet die Kasse";
            balanceBtn.Add(balance);
            right.Add(balanceBtn);
            right.Add(Ui.Btn(T.Menu, app.OpenPauseMenu));
            Root.Add(right);
            layer.Add(Root);
        }

        public void Update()
        {
            var s = app.State;
            var c = Fmt.GameDateTime(s.Tick);
            if (c != lastClock) { clock.text = c; lastClock = c; }
            var b = Fmt.Euro(s.Finance.BalanceCents);
            if (b != lastBalance) { balance.text = b; lastBalance = b; balance.EnableInClassList("lg-warn", s.Finance.BalanceCents < 0); }
            var n = T.NoticesCount(s.Notices.Count);
            if (n != lastNotices) { notices.text = n; lastNotices = n; }
            bool isPaused = app.Session.Paused;
            paused.style.display = isPaused ? DisplayStyle.Flex : DisplayStyle.None;
            pauseBtn.EnableInClassList("lg-active", isPaused);
            for (int i = 0; i < 3; i++) speedBtns[i].EnableInClassList("lg-active", !isPaused && app.Session.Speed == TimeConfig.Speeds[i]);
            rush.style.display = EntranceRules.IsRushHour(s.Tick) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>Bauleiste unten mittig mit Reitern (Entscheidung 07.10.2026), M3: Reiter „Hallen“.</summary>
    public sealed class BuildBar
    {
        readonly GameApp app;
        public readonly VisualElement Root;
        readonly VisualElement tools;
        readonly List<Button> tabButtons = new List<Button>();
        readonly List<(Button b, Tool t)> toolButtons = new List<(Button, Tool)>();
        int tab = -1;

        static readonly string[] Tabs = { T.TabRoads, T.TabZones, T.TabHalls, T.TabVehicles, T.TabDemolish };

        public BuildBar(GameApp app, VisualElement layer)
        {
            this.app = app;
            Root = Ui.Div("lg-buildbar");
            tools = Ui.Div("lg-panel", "lg-tools");
            tools.style.display = DisplayStyle.None;
            tools.style.marginBottom = 6;
            Root.Add(tools);
            var tabs = Ui.Div("lg-panel", "lg-row");
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                var b = Ui.Btn(Tabs[i], () => SelectTab(index));
                tabButtons.Add(b);
                tabs.Add(b);
            }
            Root.Add(tabs);
            layer.Add(Root);
        }

        void SelectTab(int index)
        {
            if (index == 4)
            {
                // Abriss ist ein Werkzeug ohne Unterauswahl.
                tab = -1;
                tools.style.display = DisplayStyle.None;
                app.SetTool(new Tool { Kind = ToolKind.Demolish });
                RefreshTabs();
                return;
            }
            tab = tab == index ? -1 : index;
            if (tab == -1) app.SetTool(null);
            Fill();
            RefreshTabs();
        }

        void RefreshTabs()
        {
            for (int i = 0; i < tabButtons.Count; i++)
                tabButtons[i].EnableInClassList("lg-active", i == tab || (i == 4 && app.Tool?.Kind == ToolKind.Demolish));
            foreach (var (b, t) in toolButtons) b.EnableInClassList("lg-active", t.Equals(app.Tool));
        }

        public void OnToolChanged()
        {
            if (app.Tool == null && tab == -1) tools.style.display = DisplayStyle.None;
            RefreshTabs();
        }

        void Fill()
        {
            tools.Clear();
            toolButtons.Clear();
            tools.style.display = tab < 0 ? DisplayStyle.None : DisplayStyle.Flex;
            switch (tab)
            {
                case 0:
                    AddTool(T.Road, T.PerField(Fmt.Euro(BuildConfig.RoadCostPerTileCents)), new Tool { Kind = ToolKind.Road }, "Straße ziehen: klicken, ziehen, loslassen (gerade oder mit einem Knick)");
                    AddTool(T.PriorityRoad, T.Free, new Tool { Kind = ToolKind.Priority }, "Vorhandene Straße als Vorfahrtsstraße markieren; sonst gilt rechts vor links.");
                    AddTool(T.PriorityRemove, T.Free, new Tool { Kind = ToolKind.PriorityRemove }, "Markierung „Vorfahrtsstraße“ entfernen");
                    break;
                case 1:
                    foreach (var k in ZoneTypes.All)
                        AddTool(T.Zone(k), T.PerField(Fmt.Euro(ZoneConfig.CostPerFieldCents(k))), new Tool { Kind = ToolKind.Zone, Zone = k },
                            k == ZoneKind.W ? "Werkstatt aufziehen: Fahrzeuge werden hier gewartet." : T.Zone(k) + " aufziehen: Rechteck ziehen (Größe bestimmt den Lagerplatz)", Palette.Hex(ZoneTypes.Color(k)));
                    AddTool(T.ExportExit, Fmt.Euro(BuildConfig.BuildingCostCents[BuildingTypeId.exportExit]), new Tool { Kind = ToolKind.ExportExit }, "Export-Ausfahrt am Geländerand: verkauft Ware zum festen Preis.");
                    break;
                case 2:
                    AddTool(T.Hall, T.PerField(Fmt.Euro(HallConfig.CostPerFieldCents)), new Tool { Kind = ToolKind.Hall }, "Halle aufziehen (mindestens 3 × 3). Danach innen Bereiche einzeichnen.", Palette.HallRoof);
                    foreach (AreaKind a in Enum.GetValues(typeof(AreaKind)))
                        AddTool(T.Area(a), T.PerField(Fmt.Euro(HallConfig.AreaCostPerFieldCents)), new Tool { Kind = ToolKind.Area, Area = a },
                            a == AreaKind.inbound || a == AreaKind.outbound ? T.Area(a) + ": innen am Hallentor einzeichnen" : T.Area(a) + " in einer Halle einzeichnen", Palette.AreaColor(a));
                    AddTool(T.Conveyor, T.PerField(Fmt.Euro(HallConfig.ConveyorCostPerFieldCents)), new Tool { Kind = ToolKind.Conveyor }, "Förderband ziehen: Anfang an der Quelle (Zone oder Halle), Ende am Ziel.", Palette.Belt);
                    var roofs = Ui.Btn(T.RoofToggle, () => app.World.Halls.RoofsForcedOff = !app.World.Halls.RoofsForcedOff);
                    roofs.AddToClassList("lg-tool");
                    tools.Add(roofs);
                    break;
                case 3:
                    foreach (VehicleModel m in Enum.GetValues(typeof(VehicleModel)))
                        foreach (VehicleDrive d in Enum.GetValues(typeof(VehicleDrive)))
                            AddVehicle(m, d);
                    break;
            }
            RefreshTabs();
        }

        void AddTool(string name, string cost, Tool tool, string title, Color? swatch = null)
        {
            var b = new Button(() => app.SetTool(tool.Equals(app.Tool) ? null : tool));
            b.AddToClassList("lg-btn");
            b.AddToClassList("lg-tool");
            var head = Ui.Row();
            head.style.justifyContent = Justify.Center;
            if (swatch.HasValue) head.Add(Ui.Swatch(swatch.Value));
            head.Add(Ui.Text(name, "lg-tool-name"));
            b.Add(head);
            b.Add(Ui.Text(cost, "lg-tool-cost"));
            b.tooltip = title;
            tools.Add(b);
            toolButtons.Add((b, tool));
        }

        void AddVehicle(VehicleModel model, VehicleDrive drive)
        {
            var v = Fleet.Values(model, drive);
            string name = T.Model(model) + " " + T.Drive(drive);
            var box = Ui.Div("lg-col");
            box.style.marginLeft = 6; box.style.marginRight = 6;
            box.Add(Ui.Text(name, "lg-tool-name"));
            box.Add(Ui.Text("lädt " + v.Capacity + " · " + Fmt.Euro(v.DailyCents) + "/Tag", "lg-tool-cost"));
            var buy = Ui.Btn(T.Buy + " " + Fmt.Euro(v.PriceCents), () => app.BuyVehicle(model, drive, false));
            buy.tooltip = T.VehicleItem(name, v.Capacity, Fmt.Euro(v.PriceCents), Fmt.Euro(v.DailyCents), Fmt.EuroExact(v.CostPerKmCents));
            var lease = Ui.Btn(T.Lease + " " + T.PerMonth(Fmt.Euro(Fleet.LeaseMonthlyCents(v.PriceCents))), () => app.BuyVehicle(model, drive, true));
            lease.tooltip = "Leasen: " + LeaseConfig.TermMonths + " Monate feste Laufzeit, Monatsrate (erste sofort). Vorzeitige Rückgabe kostet bis zu " + LeaseConfig.EarlyReturnPenaltyMonths + " Raten.";
            box.Add(buy);
            box.Add(lease);
            tools.Add(box);
        }
    }

    /// <summary>Hinweis am Mauszeiger (Kosten, Grund), schwebende Beträge, Name beim Überfahren, Leistungsanzeige.</summary>
    public sealed class Overlays
    {
        readonly GameApp app;
        readonly Label tip, hover, perf, version;
        readonly VisualElement floats;
        readonly List<(Label l, Vector3 world, float born)> amounts = new List<(Label, Vector3, float)>();
        public bool ShowPerf;
        float fps;

        public Overlays(GameApp app, VisualElement layer)
        {
            this.app = app;
            floats = new VisualElement { pickingMode = PickingMode.Ignore };
            floats.style.position = Position.Absolute;
            floats.style.left = 0; floats.style.top = 0; floats.style.right = 0; floats.style.bottom = 0;
            layer.Add(floats);
            tip = Ui.Text("", "lg-tip");
            tip.pickingMode = PickingMode.Ignore;
            tip.style.display = DisplayStyle.None;
            layer.Add(tip);
            hover = Ui.Text("", "lg-hover");
            hover.pickingMode = PickingMode.Ignore;
            hover.style.display = DisplayStyle.None;
            layer.Add(hover);
            perf = Ui.Text("", "lg-perf");
            perf.pickingMode = PickingMode.Ignore;
            layer.Add(perf);
            version = Ui.Text("v" + Application.version + " · Unity", "lg-version");
            version.pickingMode = PickingMode.Ignore;
            layer.Add(version);
        }

        public void SetTip(string text, bool bad, Vector2 screen)
        {
            if (string.IsNullOrEmpty(text)) { tip.style.display = DisplayStyle.None; return; }
            tip.text = text + " · " + T.EscHint;
            tip.EnableInClassList("lg-bad", bad);
            var p = app.Ui.ScreenToPanel(screen);
            tip.style.left = p.x + 18;
            tip.style.top = p.y + 18;
            tip.style.display = DisplayStyle.Flex;
        }

        public void SetHover(string text, Vector2 screen)
        {
            if (string.IsNullOrEmpty(text)) { hover.style.display = DisplayStyle.None; return; }
            hover.text = text;
            var p = app.Ui.ScreenToPanel(screen);
            hover.style.left = p.x + 14;
            hover.style.top = p.y - 26;
            hover.style.display = DisplayStyle.Flex;
        }

        /// <summary>Schwebender Betrag an der Stelle des Geschehens (T1.6).</summary>
        public void Amount(long cents, Place at)
        {
            var l = Ui.Text(Fmt.SignedEuro(cents), "lg-float", cents >= 0 ? "lg-plus" : "lg-minus");
            l.pickingMode = PickingMode.Ignore;
            floats.Add(l);
            amounts.Add((l, Coords.World(at.X, at.Z, 0.8f), Time.unscaledTime));
            if (amounts.Count > 30) { amounts[0].l.RemoveFromHierarchy(); amounts.RemoveAt(0); }
        }

        public void Update(float dt)
        {
            for (int i = amounts.Count - 1; i >= 0; i--)
            {
                var (l, world, born) = amounts[i];
                float age = Time.unscaledTime - born;
                if (age > 2f) { l.RemoveFromHierarchy(); amounts.RemoveAt(i); continue; }
                var p = app.Ui.WorldToPanel(app.Cam.Camera, world + Vector3.up * age * 1.2f, out bool visible);
                l.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                l.style.left = p.x - 30;
                l.style.top = p.y;
                l.style.opacity = 1 - age / 2f;
            }
            fps = fps == 0 ? 1f / Mathf.Max(dt, 1e-4f) : fps * 0.95f + 0.05f / Mathf.Max(dt, 1e-4f);
            perf.style.display = ShowPerf ? DisplayStyle.Flex : DisplayStyle.None;
            if (ShowPerf) perf.text = T.Perf(fps, app.Session.MsPerTick, app.State.Vehicles.Count);
        }

        public void ClearAmounts()
        {
            foreach (var a in amounts) a.l.RemoveFromHierarchy();
            amounts.Clear();
        }
    }
}
