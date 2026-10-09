using System;
using System.Collections.Generic;
using System.Linq;
using Logistikum.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Logistikum.Game
{
    /// <summary>Seitenfenster links (Touren, Flotte, Meldungen, Lager); immer nur eins offen, live aktualisiert.</summary>
    public sealed class SideWindow
    {
        readonly VisualElement panel, body;
        readonly Label title;
        readonly Action<VisualElement> fill;
        readonly Func<string> titleText;
        bool pointerInside;
        float timer;
        readonly float interval;

        public SideWindow(VisualElement layer, Func<string> titleText, Action<VisualElement> fill, Action onClose, float interval = 0.5f, float width = 380)
        {
            this.fill = fill;
            this.titleText = titleText;
            this.interval = interval;
            panel = Ui.Div("lg-panel", "lg-window");
            panel.style.left = 10;
            panel.style.width = width;
            panel.style.display = DisplayStyle.None;
            title = Ui.Text("", "lg-title");
            panel.Add(Ui.Row(title, Ui.Spacer(), Ui.Btn("✕", onClose)));
            var scroll = Ui.Scroll(640);
            body = new VisualElement();
            scroll.Add(body);
            panel.Add(scroll);
            panel.RegisterCallback<PointerEnterEvent>(_ => pointerInside = true);
            panel.RegisterCallback<PointerLeaveEvent>(_ => pointerInside = false);
            layer.Add(panel);
        }

        public bool Visible => panel.style.display == DisplayStyle.Flex;

        public void SetVisible(bool on)
        {
            panel.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) Refresh();
        }

        public void Refresh()
        {
            pointerInside = false;
            timer = interval;
            title.text = titleText();
            body.Clear();
            fill(body);
        }

        public void Update(float dt)
        {
            if (!Visible || interval <= 0) return;
            timer -= dt;
            if (timer > 0 || pointerInside) return;
            Refresh();
        }
    }

    /// <summary>Alle Fenster, Dialoge und Menüs der Oberfläche.</summary>
    public sealed class WindowManager
    {
        readonly GameApp app;
        SideWindow tours, fleet, notices, stock;
        readonly List<SideWindow> all = new List<SideWindow>();
        int? selectedTour;
        string fleetFilter = T.FilterAll;
        readonly HashSet<ProductId> expanded = new HashSet<ProductId>();

        public WindowManager(GameApp app) { this.app = app; }

        public void Build(VisualElement layer)
        {
            tours = Add(new SideWindow(layer, () => T.Tours, FillTours, CloseSide, 0.6f, 420));
            fleet = Add(new SideWindow(layer, () => T.FleetTitle(app.State.Vehicles.Count(v => v is Truck)), FillFleet, CloseSide, 0.5f, 460));
            notices = Add(new SideWindow(layer, () => T.Notices, FillNotices, CloseSide, 0.7f));
            stock = Add(new SideWindow(layer, () => T.StockTitle, FillStock, CloseSide, 0.5f, 400));
        }

        SideWindow Add(SideWindow w) { all.Add(w); return w; }

        public void Update(float dt) { foreach (var w in all) w.Update(dt); }

        public bool AnySideOpen => all.Any(w => w.Visible);

        public void CloseSide()
        {
            foreach (var w in all) w.SetVisible(false);
            app.PickStopsFor = null;
        }

        void Toggle(SideWindow w)
        {
            bool open = !w.Visible;
            CloseSide();
            w.SetVisible(open);
        }

        public void ToggleTours() => Toggle(tours);
        public void ToggleFleet() => Toggle(fleet);
        public void ToggleNotices() => Toggle(notices);
        public void ToggleStock() => Toggle(stock);

        // ---------------- Touren ----------------

        void FillTours(VisualElement b)
        {
            var s = app.State;
            var head = Ui.Row(Ui.Btn(T.NewTour, NewTour, "lg-primary"), Ui.Spacer());
            var all = new Toggle(T.AllRoutes) { value = app.World.Routes.ShowAll };
            all.RegisterValueChangedCallback(e => app.World.Routes.ShowAll = e.newValue);
            head.Add(all);
            b.Add(head);
            if (s.Tours.Count == 0) b.Add(Ui.Text(T.NoTours, "lg-muted"));
            foreach (var tour in s.Tours)
            {
                var row = Ui.Div("lg-list-row");
                row.Add(Ui.Swatch(Palette.Hex(TourColors.All[tour.Color % TourColors.All.Length])));
                var btn = Ui.Btn(tour.Name, () => { selectedTour = tour.Id; this.tours.Refresh(); });
                btn.EnableInClassList("lg-active", selectedTour == tour.Id);
                row.Add(btn);
                row.Add(Ui.Spacer());
                row.Add(Ui.Text(Tours.TrucksOn(s, tour.Id).Count + " LKW", "lg-muted"));
                b.Add(row);
            }
            var sel = Tours.ById(s, selectedTour);
            if (sel != null) TourEditor(b, sel);
        }

        void NewTour()
        {
            int n = app.State.Tours.Count + 1;
            var r = app.Run(new CreateTourCommand { TourName = T.DefaultTourName(n), Stops = new List<TourStop>() });
            if (r.Ok) selectedTour = r.Id;
            tours.Refresh();
        }

        void UpdateStops(Tour tour, List<TourStop> stops)
        {
            var r = app.Session.Execute(new UpdateTourCommand { TourId = tour.Id, Stops = stops });
            if (!r.Ok) app.Toasts.Show(T.TourRejected(r.Reason));
            tours.Refresh();
        }

        void TourEditor(VisualElement b, Tour tour)
        {
            var s = app.State;
            b.Add(Ui.Text(T.Tours + ": " + tour.Name, "lg-subtitle"));
            var name = new TextField(T.Name) { value = tour.Name, maxLength = VehicleConfig.TourNameMaxLength };
            name.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (name.value == tour.Name) return;
                var r = app.Session.Execute(new UpdateTourCommand { TourId = tour.Id, TourName = name.value });
                if (!r.Ok) app.Toasts.Show(T.TourRejected(r.Reason));
            });
            b.Add(name);
            b.Add(Ui.Dropdown(T.Color, T.ColorNames.ToList(), tour.Color, i => { app.Run(new UpdateTourCommand { TourId = tour.Id, Color = i }); tours.Refresh(); }));
            b.Add(Ui.Text(T.TrucksOnTour(Tours.TrucksOn(s, tour.Id).Count), "lg-muted"));
            b.Add(Ui.Text(T.Stops, "lg-subtitle"));
            if (tour.Stops.Count == 0) b.Add(Ui.Text(T.NoStops, "lg-muted"));
            for (int i = 0; i < tour.Stops.Count; i++)
            {
                int index = i;
                var stop = tour.Stops[i];
                var row = Ui.Div("lg-list-row");
                row.Add(Ui.Text((i + 1) + ". " + Names.Site(s, stop.SiteId)));
                row.Add(Ui.Spacer());
                row.Add(Ui.Btn(T.TourLoad(stop.Action) + " " + T.Product(stop.Product), () =>
                {
                    var list = tour.Stops.Select(x => x.Copy()).ToList();
                    list[index].Action = list[index].Action == TourAction.load ? TourAction.unload : TourAction.load;
                    UpdateStops(tour, list);
                }));
                row.Add(Ui.Btn("▲", () =>
                {
                    if (index == 0) return;
                    var list = tour.Stops.Select(x => x.Copy()).ToList();
                    (list[index - 1], list[index]) = (list[index], list[index - 1]);
                    UpdateStops(tour, list);
                }));
                row.Add(Ui.Btn("✕", () =>
                {
                    var list = tour.Stops.Select(x => x.Copy()).ToList();
                    list.RemoveAt(index);
                    UpdateStops(tour, list);
                }));
                b.Add(row);
            }
            // Halt hinzufügen: Ort, Aktion, Ware.
            var sites = Sites.All(s).Where(x => x.Kind != SiteKind.W).ToList();
            if (sites.Count > 0)
            {
                int siteIndex = 0, action = 0, product = 0;
                var products = Products.All.ToList();
                b.Add(Ui.Dropdown("Ort", sites.Select(x => Names.Site(s, x.Id)).ToList(), 0, i => siteIndex = i));
                b.Add(Ui.Dropdown("Aktion", new List<string> { T.TourLoad(TourAction.load), T.TourLoad(TourAction.unload) }, 0, i => action = i));
                b.Add(Ui.Dropdown(T.ProductLabel, products.Select(T.Product).ToList(), 0, i => product = i));
                b.Add(Ui.Btn(T.AddStop, () =>
                {
                    var list = tour.Stops.Select(x => x.Copy()).ToList();
                    list.Add(new TourStop { SiteId = sites[Math.Max(0, siteIndex)].Id, Action = action == 0 ? TourAction.load : TourAction.unload, Product = products[Math.Max(0, product)] });
                    UpdateStops(tour, list);
                }));
            }
            bool picking = app.PickStopsFor == tour.Id;
            var pick = Ui.Btn(picking ? T.PickStopsActive : T.PickStops, () => { app.PickStopsFor = picking ? (int?)null : tour.Id; tours.Refresh(); });
            pick.EnableInClassList("lg-active", picking);
            pick.tooltip = "Orte im Gelände anklicken, um sie als Halt anzuhängen. Esc beendet.";
            b.Add(pick);
            b.Add(Ui.Btn(T.DeleteTour, () => app.Dialogs.Confirm(T.DeleteTour, "„" + tour.Name + "“ löschen? Ihre LKW fahren danach in der Automatik.", T.Delete,
                () => { app.Run(new DeleteTourCommand { TourId = tour.Id }); selectedTour = null; tours.Refresh(); }), "lg-danger"));
        }

        /// <summary>„Orte anklicken“: Halt mit sinnvoller Vorauswahl anhängen (wie tourDefaults der Browser-Version).</summary>
        public void AppendStopAt(int tourId, int siteId)
        {
            var s = app.State;
            var tour = Tours.ById(s, tourId);
            var site = Sites.ById(s, siteId);
            if (tour == null || site == null || site.Kind == SiteKind.W) return;
            var lastLoad = tour.Stops.LastOrDefault(x => x.Action == TourAction.load)?.Product;
            TourStop stop;
            switch (site.Kind)
            {
                case SiteKind.A: stop = new TourStop { Action = TourAction.load, Product = ProductId.rawA }; break;
                case SiteKind.B: stop = lastLoad == ProductId.rawA ? new TourStop { Action = TourAction.unload, Product = ProductId.rawA } : new TourStop { Action = TourAction.load, Product = ProductId.combo }; break;
                case SiteKind.C: stop = lastLoad == ProductId.combo ? new TourStop { Action = TourAction.unload, Product = ProductId.combo } : new TourStop { Action = TourAction.load, Product = ProductId.final }; break;
                case SiteKind.hall: stop = lastLoad == ProductId.final ? new TourStop { Action = TourAction.unload, Product = ProductId.final } : new TourStop { Action = TourAction.load, Product = ProductId.labeled }; break;
                default: stop = new TourStop { Action = TourAction.unload, Product = lastLoad ?? ProductId.final }; break;
            }
            stop.SiteId = siteId;
            var list = tour.Stops.Select(x => x.Copy()).ToList();
            list.Add(stop);
            UpdateStops(tour, list);
        }

        // ---------------- Flotte ----------------

        static readonly string[] Filters = { T.FilterAll, "Transporter", "LKW", T.FilterService, T.FilterBroken, T.FilterIdle };

        IEnumerable<Truck> Filtered()
        {
            foreach (var t in app.State.Vehicles.OfType<Truck>())
            {
                switch (Array.IndexOf(Filters, fleetFilter))
                {
                    case 1: if (t.Model != VehicleModel.van) continue; break;
                    case 2: if (t.Model != VehicleModel.truck) continue; break;
                    case 3: if (!UpkeepRules.NeedsService(t)) continue; break;
                    case 4: if (t.Upkeep.BrokenTicks == 0) continue; break;
                    case 5: if (t.Phase != TruckPhase.idle) continue; break;
                }
                yield return t;
            }
        }

        void FillFleet(VisualElement b)
        {
            var s = app.State;
            b.Add(Ui.Dropdown("Zeigen", Filters.ToList(), Array.IndexOf(Filters, fleetFilter), i => { fleetFilter = Filters[Math.Max(0, i)]; fleet.Refresh(); }));
            var list = Filtered().ToList();
            if (!s.Vehicles.OfType<Truck>().Any()) b.Add(Ui.Text(T.FleetEmpty, "lg-muted"));
            foreach (var t in list)
            {
                var row = Ui.Div("lg-list-row");
                var tour = Tours.TourOf(s, t);
                row.Add(Ui.Swatch(tour != null ? Palette.Hex(TourColors.All[tour.Color % TourColors.All.Length]) : Palette.Hex(TourColors.AutoRoute)));
                row.Add(Ui.Btn(Names.Vehicle(s, t), () => app.ShowVehicle(t.Id)));
                string status = t.Upkeep.BrokenTicks > 0 ? T.FilterBroken : t.Phase == TruckPhase.idle && t.IdleReason.HasValue ? T.IdleReason(t.IdleReason.Value).Replace("Wartet: ", "") : T.TruckPhase(t.Phase);
                row.Add(Ui.Text(status, "lg-muted"));
                row.Add(Ui.Spacer());
                row.Add(Ui.Text((tour?.Name ?? T.Auto) + " · " + t.Upkeep.Condition / 1000 + " %", "lg-muted"));
                b.Add(row);
            }
            if (list.Count == 0) return;
            b.Add(Ui.Text("Für alle gezeigten (" + list.Count + "):", "lg-subtitle"));
            var choices = new List<string> { "Tour zuweisen …", T.Auto };
            choices.AddRange(s.Tours.Select(x => x.Name));
            b.Add(Ui.Dropdown("", choices, 0, i =>
            {
                if (i <= 0) return;
                int? tourId = i == 1 ? (int?)null : app.State.Tours[i - 2].Id;
                foreach (var t in Filtered().ToList()) app.Session.Execute(new AssignTourCommand { TruckId = t.Id, TourId = tourId });
                app.Toasts.Show("Für " + list.Count + " Fahrzeuge erledigt.");
                fleet.Refresh();
            }));
            b.Add(Ui.Btn(T.ToWorkshop, () =>
            {
                if (!app.State.Zones.Exists(z => z.Kind == ZoneKind.W)) { app.Toasts.Show(T.NoWorkshop); return; }
                foreach (var t in Filtered().ToList()) app.Session.Execute(new ServiceVehicleCommand { TruckId = t.Id });
                app.Toasts.Show("Für " + list.Count + " Fahrzeuge erledigt.");
            }));
        }

        // ---------------- Meldungen ----------------

        void FillNotices(VisualElement b)
        {
            var s = app.State;
            if (s.Notices.Count == 0) b.Add(Ui.Text(T.NoNotices, "lg-muted"));
            for (int i = s.Notices.Count - 1; i >= 0; i--)
            {
                var n = s.Notices[i];
                var row = Ui.Div("lg-list-row");
                var col = Ui.Div("lg-col", "lg-grow");
                col.Add(Ui.Text(T.Notice(n.Kind, Names.VehicleById(s, n.VehicleId))));
                col.Add(Ui.Text(Fmt.GameDateTime(n.Tick), "lg-muted"));
                row.Add(col);
                row.Add(Ui.Btn(T.Show, () =>
                {
                    app.Cam.Focus(Coords.CellCenter(n.X, n.Z), 30);
                    if (n.VehicleId.HasValue && app.State.VehicleById(n.VehicleId.Value) != null) app.Select(new Pick { Kind = PickKind.Vehicle, Id = n.VehicleId.Value });
                }));
                b.Add(row);
            }
        }

        // ---------------- Lager (T3.6) ----------------

        struct Place { public string Name; public int Amount, Capacity; public Pick Pick; public Vector3 At; }

        List<Place> PlacesOf(ProductId p)
        {
            var s = app.State;
            var list = new List<Place>();
            foreach (var z in s.Zones)
            {
                if (!Stock.Stores(z, p)) continue;
                var c = ZoneShape.Center(z.Parts);
                list.Add(new Place { Name = Names.Site(s, z.Id), Amount = Stock.Of(z, p), Capacity = Stock.ZoneCapacity(z), Pick = new Pick { Kind = PickKind.Zone, Id = z.Id }, At = Coords.World(c.X, c.Z) });
            }
            if (HallProcessing.Level(p) >= 0)
                foreach (var h in s.Halls)
                    foreach (var a in h.Areas)
                    {
                        int n = Stock.Of(a.Stock, p);
                        if (n == 0) continue;
                        list.Add(new Place { Name = Names.Site(s, h.Id) + " · " + T.Area(a.Kind), Amount = n, Capacity = Stock.AreaCapacity(a), Pick = new Pick { Kind = PickKind.Hall, Id = h.Id }, At = Coords.Center(a.Rect) });
                    }
            return list;
        }

        void FillStock(VisualElement b)
        {
            bool any = false;
            foreach (var p in Products.All)
            {
                var places = PlacesOf(p);
                int total = places.Sum(x => x.Amount);
                int cap = places.Sum(x => x.Capacity);
                if (places.Count == 0) continue;
                any = true;
                bool open = expanded.Contains(p);
                var row = Ui.Div("lg-list-row");
                row.Add(Ui.Swatch(Palette.Hex(Products.Color(p))));
                row.Add(Ui.Btn((open ? "▾ " : "▸ ") + T.Product(p), () => { if (!expanded.Remove(p)) expanded.Add(p); stock.Refresh(); }));
                row.Add(Ui.Spacer());
                row.Add(Ui.Text(total + " / " + cap));
                b.Add(row);
                b.Add(Ui.Bar(cap > 0 ? total / (float)cap : 0));
                if (!open) continue;
                foreach (var pl in places)
                {
                    var r = Ui.Row(Ui.Btn(pl.Name, () => { app.Cam.Focus(pl.At, 35); app.Select(pl.Pick); }), Ui.Spacer(), Ui.Text(pl.Amount + " / " + pl.Capacity, pl.Amount >= pl.Capacity ? "lg-warn" : "lg-muted"));
                    r.style.marginLeft = 18;
                    b.Add(r);
                }
            }
            if (!any) b.Add(Ui.Text(T.StockEmpty, "lg-muted"));
            int onTheRoad = app.State.Vehicles.Sum(v => v.Cargo?.Quantity ?? 0) + app.State.Conveyors.Sum(c => c.Items.Count);
            b.Add(Ui.Text("Unterwegs auf Fahrzeugen und Bändern: " + onTheRoad, "lg-muted"));
        }

        // ---------------- Kasse ----------------

        public void OpenCash()
        {
            app.Dialogs.Show(T.Cash, body =>
            {
                var f = app.State.Finance;
                int tick = app.State.Tick;
                var today = Ledger.Current(f, tick, true);
                var month = Ledger.Current(f, tick, false);
                body.Add(Ui.Row(Ui.Text(T.Balance, "lg-subtitle"), Ui.Spacer(), Ui.Text(Fmt.Euro(f.BalanceCents), "lg-subtitle")));
                var head = Ui.Row(Cell(T.Category, 150, true), Cell(T.Today + " +", 110, true), Cell(T.Today + " −", 110, true), Cell(T.Month + " +", 120, true), Cell(T.Month + " −", 120, true));
                body.Add(head);
                long ti = 0, te = 0, mi = 0, me = 0;
                foreach (var c in Ledger.Categories)
                {
                    body.Add(Ui.Row(Cell(T.BookingCategory(c), 150), Cell(Fmt.Euro(today.Income(c)), 110), Cell(Fmt.Euro(today.Expense(c)), 110), Cell(Fmt.Euro(month.Income(c)), 120), Cell(Fmt.Euro(month.Expense(c)), 120)));
                    ti += today.Income(c); te += today.Expense(c); mi += month.Income(c); me += month.Expense(c);
                }
                body.Add(Ui.Row(Cell(T.Total, 150, true), Cell(Fmt.Euro(ti), 110, true), Cell(Fmt.Euro(te), 110, true), Cell(Fmt.Euro(mi), 120, true), Cell(Fmt.Euro(me), 120, true)));
                body.Add(Ui.Text(T.Recent, "lg-subtitle"));
                var scroll = Ui.Scroll(220);
                if (f.Recent.Count == 0) scroll.Add(Ui.Text(T.NoBookings, "lg-muted"));
                for (int i = f.Recent.Count - 1; i >= 0; i--)
                {
                    var r = f.Recent[i];
                    scroll.Add(Ui.Row(Cell(Fmt.GameDateTime(r.Tick), 190), Cell(T.BookingCategory(r.Category), 140), Ui.Spacer(), Ui.Text(Fmt.SignedEuro(r.AmountCents), "lg-text", r.AmountCents >= 0 ? "lg-good" : "lg-warn")));
                }
                body.Add(scroll);
            }, 700);
        }

        static Label Cell(string text, float width, bool bold = false)
        {
            var l = Ui.Text(text, bold ? "lg-subtitle" : "lg-text");
            l.style.width = width;
            l.style.marginTop = 0; l.style.marginBottom = 0;
            return l;
        }

        // ---------------- Einkauf ----------------

        public void OpenPurchase()
        {
            app.Dialogs.Show(T.Purchase, body => FillPurchase(body), 620);
        }

        void FillPurchase(VisualElement body)
        {
            body.Clear();
            var raws = new List<ProductId> { ProductId.rawA, ProductId.rawB };
            int product = 0, quantity = Array.IndexOf(GoodsConfig.OrderQuantities, GoodsConfig.DefaultOrderQuantity), interval = 1;
            var intervals = new List<OrderInterval> { OrderInterval.once, OrderInterval.daily, OrderInterval.weekly, OrderInterval.monthly };
            body.Add(Ui.Text(T.PurchaseHint, "lg-muted"));
            body.Add(Ui.Text(T.NewOrder, "lg-subtitle"));
            var total = Ui.Text("", "lg-muted");
            void UpdateTotal() => total.text = "Kosten je Lieferung: bis zu " + Fmt.Euro(GoodsConfig.OrderQuantities[quantity] * GoodsConfig.PurchasePriceCents(raws[product]));
            body.Add(Ui.Dropdown(T.ProductLabel, raws.Select(p => T.Product(p) + " (" + Fmt.Euro(GoodsConfig.PurchasePriceCents(p)) + " je Einheit)").ToList(), 0, i => { product = Math.Max(0, i); UpdateTotal(); }));
            body.Add(Ui.Dropdown(T.Quantity, GoodsConfig.OrderQuantities.Select(n => n + " Einheiten").ToList(), quantity, i => { quantity = Math.Max(0, i); UpdateTotal(); }));
            body.Add(Ui.Dropdown(T.Interval, intervals.Select(T.IntervalName).ToList(), interval, i => interval = Math.Max(0, i)));
            UpdateTotal();
            body.Add(total);
            body.Add(Ui.Btn(T.Order, () =>
            {
                app.Run(new CreateOrderCommand { Product = raws[product], Quantity = GoodsConfig.OrderQuantities[quantity], Interval = intervals[interval] });
                FillPurchase(body);
            }, "lg-primary"));
            body.Add(Ui.Text(T.Orders, "lg-subtitle"));
            var s = app.State;
            if (s.Orders.Count == 0) body.Add(Ui.Text(T.NoOrders, "lg-muted"));
            foreach (var o in s.Orders)
            {
                var row = Ui.Div("lg-list-row");
                var col = Ui.Div("lg-col", "lg-grow");
                col.Add(Ui.Text(T.Product(o.Product) + " · " + o.Quantity + " Einheiten · " + T.IntervalName(o.Interval)));
                col.Add(Ui.Text(o.Blocked.HasValue ? T.OrderBlocked(o.Blocked.Value) : "nächste Lieferung " + Fmt.GameDateTime(o.NextTick), o.Blocked.HasValue ? "lg-warn" : "lg-muted"));
                row.Add(col);
                row.Add(Ui.Btn(T.Delete, () => { app.Run(new CancelOrderCommand { OrderId = o.Id }); FillPurchase(body); }));
                body.Add(row);
            }
        }

        // ---------------- Speichern, Laden, Import, Einstellungen ----------------

        public void OpenSave()
        {
            app.Dialogs.Show(T.SaveTitle, body =>
            {
                var name = new TextField(T.NameLabel) { value = T.DefaultSaveName(Fmt.GameDate(app.State.Tick)), maxLength = 40 };
                body.Add(name);
                body.Add(Ui.Row(Ui.Spacer(), Ui.Btn(T.Save, () => TrySave(name.value), "lg-primary")));
                var slots = app.Saves.Repo.List().Where(e => !e.IsAutosave).ToList();
                if (slots.Count > 0) body.Add(Ui.Text(T.OverwriteHint, "lg-muted"));
                var scroll = Ui.Scroll(300);
                foreach (var e in slots)
                    scroll.Add(EntryRow(e, Ui.Btn(T.Overwrite, () => app.Dialogs.Confirm(T.OverwriteTitle, T.OverwriteMessage(e.Meta.Name), T.Overwrite, () => { if (app.Saves.Save(e.Meta.Name)) app.Dialogs.CloseAll(); }))));
                body.Add(scroll);
                body.Add(Ui.Row(Ui.Btn(T.Export, () => app.Saves.Export()), Ui.Btn(T.OpenFolder, () => SaveController.OpenFolder(app.Saves.SaveDir))));
            }, 600);
        }

        void TrySave(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return;
            if (app.Saves.Repo.Exists(name))
                app.Dialogs.Confirm(T.OverwriteTitle, T.OverwriteMessage(name), T.Overwrite, () => { if (app.Saves.Save(name)) app.Dialogs.CloseAll(); });
            else if (app.Saves.Save(name)) app.Dialogs.CloseAll();
        }

        VisualElement EntryRow(SaveEntry e, params VisualElement[] buttons)
        {
            var row = Ui.Div("lg-list-row");
            var col = Ui.Div("lg-col", "lg-grow");
            col.Add(Ui.Text(e.IsAutosave ? T.AutosaveName : e.Meta.Name));
            col.Add(Ui.Text(Fmt.GameDateTime(e.Meta.Tick) + " · " + Fmt.Euro(e.Meta.BalanceCents) + " · gespeichert " + Fmt.RealDateTime(e.CreatedAt), "lg-muted"));
            row.Add(col);
            foreach (var b in buttons) row.Add(b);
            return row;
        }

        public void OpenLoad()
        {
            app.Dialogs.Show(T.LoadTitle, body =>
            {
                var entries = app.Saves.Repo.List();
                void Section(string title, IEnumerable<SaveEntry> list, string empty)
                {
                    body.Add(Ui.Text(title, "lg-subtitle"));
                    var items = list.ToList();
                    if (items.Count == 0) body.Add(Ui.Text(empty, "lg-muted"));
                    var scroll = Ui.Scroll(220);
                    foreach (var e in items)
                    {
                        scroll.Add(EntryRow(e,
                            Ui.Btn(T.Load, () => ConfirmLoad(() => app.Saves.LoadKey(e.Key)), "lg-primary"),
                            Ui.Btn(T.Delete, () => app.Dialogs.Confirm(T.DeleteTitle, T.DeleteMessage(e.IsAutosave ? T.AutosaveName : e.Meta.Name), T.Delete,
                                () => { app.Saves.Repo.Delete(e.Key); app.Dialogs.CloseAll(); OpenLoad(); }), "lg-danger")));
                    }
                    body.Add(scroll);
                }
                Section(T.Slots, entries.Where(e => !e.IsAutosave), T.NoSaves);
                Section(T.Backups, entries.Where(e => e.IsAutosave), T.NoBackups);
                body.Add(Ui.Row(Ui.Btn(T.Import, OpenImport), Ui.Btn(T.OpenFolder, () => SaveController.OpenFolder(app.Saves.SaveDir))));
                body.Add(Ui.Text(T.ImportBrowser, "lg-muted"));
            }, 640);
        }

        void ConfirmLoad(Action load)
        {
            if (app.InGame) app.Dialogs.Confirm(T.LoadInGameTitle, T.LoadInGameMessage, T.Load, () => { app.Dialogs.CloseAll(); load(); });
            else { app.Dialogs.CloseAll(); load(); }
        }

        void OpenImport()
        {
            app.Dialogs.Show(T.ImportTitle, body =>
            {
                body.Add(Ui.Text(T.ImportHint, "lg-muted"));
                var files = app.Saves.ImportCandidates();
                if (files.Count == 0) body.Add(Ui.Text(T.NoImports, "lg-muted"));
                var scroll = Ui.Scroll(320);
                foreach (var (path, meta) in files)
                {
                    var row = Ui.Div("lg-list-row");
                    var col = Ui.Div("lg-col", "lg-grow");
                    col.Add(Ui.Text(meta.Name));
                    col.Add(Ui.Text(System.IO.Path.GetFileName(path) + " · " + Fmt.GameDateTime(meta.Tick) + " · " + Fmt.Euro(meta.BalanceCents), "lg-muted"));
                    row.Add(col);
                    row.Add(Ui.Btn(T.Load, () => ConfirmLoad(() => app.Saves.LoadFile(path)), "lg-primary"));
                    scroll.Add(row);
                }
                body.Add(scroll);
                body.Add(Ui.Btn(T.OpenFolder, () => SaveController.OpenFolder(app.Saves.ExportDir)));
            }, 620);
        }

        public void OpenSettings()
        {
            app.Dialogs.Show(T.Settings, body =>
            {
                var st = app.Settings;
                var options = SettingsConfig.AutosaveMinutesOptions;
                body.Add(Ui.Dropdown(T.Autosave, options.Select(T.AutosaveOption).ToList(), Array.IndexOf(options, st.autosaveMinutes), i => { st.autosaveMinutes = options[Math.Max(0, i)]; Save(); }));
                var slider = new Slider(T.CameraSensitivity, SettingsConfig.CameraSensitivityMin, SettingsConfig.CameraSensitivityMax) { value = st.cameraSensitivity };
                slider.RegisterValueChangedCallback(e => { st.cameraSensitivity = e.newValue; Save(); });
                body.Add(slider);
                var edge = new Toggle(T.EdgeScroll) { value = st.edgeScroll };
                edge.RegisterValueChangedCallback(e => { st.edgeScroll = e.newValue; Save(); });
                body.Add(edge);
                body.Add(Ui.Text("Tasten: Leertaste Pause · 1/2/3 Tempo · WASD verschieben · Q/E zoomen · R/F neigen · Y/X drehen · H Hallendächer · F3 Leistung · Esc Menü", "lg-muted"));
                body.Add(Ui.Text("Spielstände: " + app.Saves.SaveDir, "lg-muted"));
            }, 560);

            void Save()
            {
                app.Settings.Save();
                app.ApplySettings();
            }
        }

        // ---------------- Menüs ----------------

        public void OpenPause()
        {
            app.Dialogs.Show(T.PauseTitle, body =>
            {
                body.Add(Ui.Btn(T.Resume, app.Dialogs.CloseAll, "lg-big", "lg-primary"));
                body.Add(Ui.Btn(T.Save, OpenSave, "lg-big"));
                body.Add(Ui.Btn(T.Load, OpenLoad, "lg-big"));
                body.Add(Ui.Btn(T.Settings, OpenSettings, "lg-big"));
                body.Add(Ui.Btn(T.MainMenu, () => app.Dialogs.Confirm(T.LeaveTitle, T.LeaveMessage, T.MainMenu, () => { app.Dialogs.CloseAll(); app.ShowMainMenu(); }), "lg-big"));
            }, 380);
        }
    }

    /// <summary>Hauptmenü links über dem lebenden Beispiel-Campus (Entscheidung 07.10.2026).</summary>
    public sealed class MainMenu
    {
        public readonly VisualElement Root;

        public MainMenu(GameApp app, VisualElement layer)
        {
            Root = Ui.Div("lg-panel", "lg-menu");
            Root.Add(Ui.Text(T.Title, "lg-logo"));
            Root.Add(Ui.Text(T.Subtitle, "lg-muted"));
            var space = new VisualElement();
            space.style.height = 14;
            Root.Add(space);
            Root.Add(Ui.Btn(T.NewGame, app.NewGame, "lg-big", "lg-primary"));
            Root.Add(Ui.Btn(T.Load, app.Windows.OpenLoad, "lg-big"));
            Root.Add(Ui.Btn(T.Settings, app.Windows.OpenSettings, "lg-big"));
            Root.Add(Ui.Btn(T.Quit, Application.Quit, "lg-big"));
            Root.Add(Ui.Text("Version " + Application.version + " · Modelle aus Blender · Unity " + Application.unityVersion, "lg-muted"));
            layer.Add(Root);
        }

        public void SetVisible(bool on) => Root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
