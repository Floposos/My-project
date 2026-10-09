using Logistikum.Sim;

namespace Logistikum.Game
{
    /// <summary>Alle deutschen Oberflächentexte an einer Stelle (übernommen aus der Browser-Version, M3 ergänzt).</summary>
    public static class T
    {
        public const string Title = "Logistikum";
        public const string Subtitle = "Logistik-Campus-Manager";
        public const string Ok = "OK", Cancel = "Abbrechen", Close = "Schließen";

        // Hauptmenü und Pause
        public const string NewGame = "Neues Spiel", Load = "Laden", Settings = "Einstellungen", Quit = "Beenden";
        public const string PauseTitle = "Spiel pausiert", Resume = "Weiterspielen", Save = "Speichern", MainMenu = "Hauptmenü";
        public const string LeaveTitle = "Zum Hauptmenü?", LeaveMessage = "Nicht gespeicherter Fortschritt geht verloren.";
        public const string ImportBrowser = "Spielstände aus der Browser-Version (Version 0.3.0) lassen sich über „Datei importieren“ übernehmen.";

        // Kopfleiste
        public const string Pause = "Pause", Paused = "PAUSE", Menu = "Menü", Purchase = "Einkauf";
        public static string Speed(int s) => s + "x";
        public static readonly string[] Weekdays = { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };
        public const string Rush = "Rushhour";
        public const string Stock = "Lager", Tours = "Touren", Fleet = "Flotte";
        public static string NoticesCount(int n) => n > 0 ? "Meldungen (" + n + ")" : "Meldungen";

        // Einstellungen
        public const string Autosave = "Autosave-Intervall", CameraSensitivity = "Kamera-Empfindlichkeit";
        public const string EdgeScroll = "Karte am Bildschirmrand verschieben", SettingsSaved = "Einstellungen gespeichert";
        public static string AutosaveOption(int m) => "alle " + m + " Minuten";

        // Speichern und Laden
        public const string SaveTitle = "Spielstand speichern", LoadTitle = "Spielstand laden", NameLabel = "Name des Spielstands";
        public const string OverwriteHint = "Oder einen vorhandenen Spielstand überschreiben:", OverwriteTitle = "Spielstand überschreiben?";
        public static string OverwriteMessage(string n) => "„" + n + "“ wird durch den aktuellen Stand ersetzt.";
        public const string Overwrite = "Überschreiben", Slots = "Speicherplätze", Backups = "Automatische Sicherungen";
        public const string NoSaves = "Noch keine Spielstände.", NoBackups = "Noch keine automatischen Sicherungen.";
        public const string Delete = "Löschen", DeleteTitle = "Spielstand löschen?";
        public static string DeleteMessage(string n) => "„" + n + "“ wird endgültig gelöscht.";
        public const string LoadInGameTitle = "Spielstand laden?", LoadInGameMessage = "Nicht gespeicherter Fortschritt des laufenden Spiels geht verloren.";
        public const string AutosaveName = "Automatische Sicherung", Export = "Als Datei exportieren", Import = "Datei importieren …";
        public const string Saved = "Gespeichert", Loaded = "Spielstand geladen", Autosaved = "Automatisch gespeichert";
        public const string SaveFailed = "Speichern fehlgeschlagen. Der vorherige Spielstand ist unverändert.";
        public const string AutosaveFailed = "Automatisches Speichern fehlgeschlagen";
        public static string Exported(string path) => "Datei exportiert: " + path;
        public const string ImportTitle = "Datei importieren", ImportHint = "Logistikum-Spielstände (.json) aus dem Ordner „Exporte“ und aus „Downloads“:";
        public const string NoImports = "Keine Spielstand-Dateien gefunden.", OpenFolder = "Ordner öffnen";
        public const string ErrorTitle = "Spielstand kann nicht geladen werden";
        public static string DefaultSaveName(string date) => "Campus " + date;
        public static string SaveError(SaveError e)
        {
            switch (e)
            {
                case Sim.SaveError.wrongFormat: return "Die Datei ist kein Logistikum-Spielstand.";
                case Sim.SaveError.tooNew: return "Der Spielstand stammt aus einer neueren Spielversion.";
                case Sim.SaveError.tooOld: return "Der Spielstand ist älter als Version 0.3.0 und kann nicht übernommen werden. Bitte in der Browser-Version einmal laden und neu speichern.";
                case Sim.SaveError.invalidVersion: return "Die Versionsangabe im Spielstand ist beschädigt.";
                case Sim.SaveError.migrationFailed: return "Der Spielstand konnte nicht auf die aktuelle Version umgestellt werden.";
                case Sim.SaveError.invalidState: return "Der Spielstand ist beschädigt oder unvollständig.";
                default: return "Die Datei ist kein Spielstand (kein gültiges Format) oder wurde nicht gefunden.";
            }
        }

        // Bauleiste
        public const string TabRoads = "Straßen", TabZones = "Zonen/Gebäude", TabHalls = "Hallen", TabVehicles = "Fahrzeuge", TabDemolish = "Abriss";
        public const string Road = "Straße", PriorityRoad = "Vorfahrtsstraße", PriorityRemove = "Vorfahrt entfernen", Free = "kostenlos";
        public const string ExportExit = "Export-Ausfahrt", Hall = "Halle", Conveyor = "Förderband", Demolish = "Abreißen";
        public static string Zone(ZoneKind k) => k == ZoneKind.W ? "Werkstatt" : "Lieferort " + k;
        public static string PerField(string cost) => cost + " je Feld";
        public static string RoadCost(int n, string cost) => n + (n == 1 ? " Feld" : " Felder") + " · Kosten: " + cost;
        public static string ZoneSize(int w, int d, int cap, string cost) => w + " × " + d + " Felder · Lager " + cap + " je Ware · Kosten: " + cost;
        public static string WorkshopSize(int w, int d, int bays, string cost) => w + " × " + d + " Felder · " + bays + (bays == 1 ? " Werkstattplatz" : " Werkstattplätze") + " · Kosten: " + cost;
        public static string HallSize(int w, int d, string cost) => "Halle " + w + " × " + d + " Felder · Kosten: " + cost;
        public static string AreaSize(string name, int w, int d, int cap, string cost) => name + " " + w + " × " + d + " · Lager " + cap + " je Ware · Kosten: " + cost;
        public static string PriorityCells(int n, bool on) => n + (n == 1 ? " Feld " : " Felder ") + (on ? "als Vorfahrtsstraße markieren" : "ohne Vorfahrt");
        public const string NoRoadHere = "Hier ist keine Straße", ZoneMerges = "wird Teil der angrenzenden Zone";
        public static string Cost(string c) => "Kosten: " + c;
        public static string Refund(string r) => "Abreißen, Erstattung: " + r;
        public const string NothingToDemolish = "Hier steht nichts zum Abreißen.", EscHint = "Esc bricht ab";
        public static string ConveyorInfo(int n, string from, string to, string cost) => n + " Felder · von " + from + " nach " + to + " · Kosten: " + cost;
        public static string Reason(BuildRejection r)
        {
            switch (r)
            {
                case BuildRejection.outOfBounds: return "Außerhalb des Geländes";
                case BuildRejection.occupied: return "Fläche ist belegt";
                case BuildRejection.insufficientFunds: return "Nicht genug Geld";
                case BuildRejection.tooSmall: return "Zu klein (Halle mindestens " + HallConfig.MinSize + " × " + HallConfig.MinSize + ")";
                case BuildRejection.notAtEdge: return "Muss am Geländerand stehen";
                case BuildRejection.notInHall: return "Bereiche liegen innerhalb einer Halle";
                case BuildRejection.notAtGate: return "Wareneingang und Warenausgang liegen am Hallentor";
                case BuildRejection.noSource: return "Anfang muss an einen Lieferort oder eine Halle grenzen";
                case BuildRejection.noTarget: return "Ende muss an einen anderen Ort grenzen (Zone, Halle, Export)";
                default: return "Geht hier nicht";
            }
        }
        public static string Gate(Side s) => s == Side.N ? "Tor Nord" : s == Side.E ? "Tor Ost" : s == Side.S ? "Tor Süd" : "Tor West";
        public const string NotConnected = "Nicht angeschlossen: Straße an das Tor bauen";

        // Waren und Bereiche
        public static string Product(ProductId p)
        {
            switch (p)
            {
                case ProductId.rawA: return "Rohware A";
                case ProductId.rawB: return "Rohware B";
                case ProductId.combo: return "Kombi";
                case ProductId.final: return "Endprodukt";
                case ProductId.packed: return "Verpackt";
                case ProductId.labeled: return "Etikettiert";
                default: return "Geprüft";
            }
        }
        public static string Area(AreaKind k)
        {
            switch (k)
            {
                case AreaKind.inbound: return "Wareneingang";
                case AreaKind.storage: return "Lager";
                case AreaKind.packing: return "Verpackung";
                case AreaKind.labeling: return "Etikettierung";
                case AreaKind.inspection: return "Qualitätsprüfung";
                default: return "Warenausgang";
            }
        }

        // Kasse
        public const string Cash = "Kasse", Balance = "Kontostand", Category = "Bereich", Today = "Heute", Month = "Diesen Monat";
        public const string Income = "Einnahmen", Expense = "Ausgaben", Total = "Summe", Recent = "Letzte Buchungen", NoBookings = "Noch keine Buchungen.";
        public static string BookingCategory(BookingCategory c)
        {
            switch (c)
            {
                case Sim.BookingCategory.build: return "Bau";
                case Sim.BookingCategory.vehicles: return "Fahrzeuge";
                case Sim.BookingCategory.operations: return "Betrieb";
                case Sim.BookingCategory.rawGoods: return "Rohware";
                default: return "Export-Erlös";
            }
        }

        // Einkauf
        public const string NewOrder = "Neue Bestellung", ProductLabel = "Ware", Quantity = "Menge je Lieferung", Interval = "Lieferung";
        public const string Order = "Bestellen", Orders = "Bestellungen", NoOrders = "Keine offenen Bestellungen.";
        public const string PurchaseHint = "Zulieferer bringen Rohware A nach Lieferort A und Rohware B nach Lieferort B. Bezahlt wird beim Losschicken.";
        public static string IntervalName(OrderInterval i) => i == OrderInterval.once ? "Einmalig" : i == OrderInterval.daily ? "Täglich" : i == OrderInterval.weekly ? "Wöchentlich" : "Monatlich";
        public static string OrderBlocked(OrderBlock b) => b == OrderBlock.noSite ? "Wartet: kein passender Lieferort gebaut" : b == OrderBlock.noRoute ? "Wartet: Lieferort nicht über die Straße erreichbar" : b == OrderBlock.full ? "Wartet: Lager voll" : "Wartet: nicht genug Geld";

        // Infofenster
        public const string StockLabel = "Lager", Status = "Status", Bays = "Stellplätze", GateLabel = "Tor", Cargo = "Ladung", Empty = "leer", Target = "Ziel", None = "–";
        public const string Connected = "An die Straße angeschlossen", DemolishZone = "Ganze Zone abreißen";
        public static string DemolishConfirm(string refund) => "Samt Bestand abreißen? Erstattung: " + refund + ".";
        public static string BaysLine(int used, int total, int queue) => used + " / " + total + " belegt" + (queue > 0 ? " · " + queue + " warten" : "");
        public static string ZoneStatus(ZoneStatus s) => s == Sim.ZoneStatus.working ? "Verarbeitet" : s == Sim.ZoneStatus.waitingInput ? "Wartet auf Ware" : s == Sim.ZoneStatus.full ? "Ausgangslager voll" : s == Sim.ZoneStatus.storing ? "Lagert" : "Werkstatt";
        public static string TruckPhase(TruckPhase p)
        {
            switch (p)
            {
                case Sim.TruckPhase.idle: return "Wartet";
                case Sim.TruckPhase.toPickup: return "Fährt zum Laden";
                case Sim.TruckPhase.loading: return "Lädt";
                case Sim.TruckPhase.toDropoff: return "Fährt zum Abladen";
                case Sim.TruckPhase.unloading: return "Lädt ab";
                case Sim.TruckPhase.toWorkshop: return "Fährt zur Werkstatt";
                default: return "Wird gewartet";
            }
        }
        public static string IdleReason(TruckIdleReason r) => r == TruckIdleReason.noJob ? "Wartet: keine Aufgabe" : r == TruckIdleReason.noRoute ? "Wartet: kein Weg (Straße fehlt)" : r == TruckIdleReason.noDestination ? "Wartet: kein Ziel mit Platz für die Ladung" : "Wartet: Tour hat keine Halte";
        public static string SupplierPhase(SupplierPhase p) => p == Sim.SupplierPhase.toSite ? "Bringt Rohware" : p == Sim.SupplierPhase.handling ? "Lädt ab" : p == Sim.SupplierPhase.toExit ? "Fährt hinaus" : "Wartet: kein Weg";
        public const string Supplier = "Zulieferer", Mode = "Betrieb", Auto = "Automatik";
        public static string Model(VehicleModel m) => m == VehicleModel.van ? "Transporter" : "LKW";
        public static string Drive(VehicleDrive d) => d == VehicleDrive.electric ? "Elektro" : "Diesel";
        public const string Buy = "Kaufen", Lease = "Leasen", Sell = "Verkaufen", GiveBack = "Leasing zurückgeben";
        public static string Owned(string residual) => "Gekauft · Restwert " + residual;
        public static string Leased(string rate, string end) => "Geleast · " + rate + " / Monat bis " + end;
        public static string SellConfirm(string name, string amount) => name + " für " + amount + " verkaufen? Ladung geht verloren.";
        public static string GiveBackConfirm(string name, string penalty) => name + " zurückgeben? Vorzeitige Rückgabe kostet " + penalty + ".";
        public const string Condition = "Zustand", Breakdowns = "Pannen", ToWorkshop = "Zur Werkstatt", NoWorkshop = "Es gibt keine Werkstatt.";
        public static string ConditionLine(int percent, int km) => km > 0 ? percent + " % · Wartung in ca. " + km + " km" : percent + " % · Wartung fällig";
        public static string Broken(int hours) => "Panne: steht noch ca. " + hours + " Std.";
        public static string VehicleItem(string name, int capacity, string cost, string daily, string perKm) =>
            name + ": lädt " + capacity + " Einheiten. " + cost + ", dazu " + daily + " am Tag und " + perKm + " je km.";
        public static string PerMonth(string rate) => rate + " / Monat";
        public const string NoMoney = "Nicht genug Geld.";

        // Hallen (M3)
        public const string Forklifts = "Gabelstapler", BuyForklift = "Stapler kaufen", SellForklift = "Stapler verkaufen";
        public static string ForkliftLine(int n, string daily) => n + " Stapler · " + daily + " je Stapler und Tag";
        public const string NoForklifts = "Ohne Stapler bleibt die Ware im Wareneingang liegen.";
        public const string Areas = "Bereiche", NoAreas = "Noch keine Bereiche. Unter „Hallen“ einzeichnen.", RoofToggle = "Dächer (H)";
        public const string HallHint = "LKW liefern in den Wareneingang und holen am Warenausgang ab. Stapler bringen die Ware durch die Stufen.";
        public static string AreaWork(bool working) => working ? "arbeitet" : "wartet";
        public const string DemolishHall = "Halle abreißen", DemolishArea = "Bereich entfernen", DemolishConveyor = "Band abreißen";
        public const string Stalled = "Steht: Ziel voll", Running = "Läuft";
        public static string BeltLine(int items) => items + (items == 1 ? " Kiste" : " Kisten") + " auf dem Band";

        // Lager-Fenster (T3.6)
        public const string StockTitle = "Lager", StockTotal = "Gesamt", StockEmpty = "Noch keine Ware auf dem Campus.";

        // Touren
        public const string NewTour = "Neue Tour", NoTours = "Noch keine Touren. „Neue Tour“ legt eine an.", Name = "Name", Color = "Farbe", Stops = "Halte";
        public const string DeleteTour = "Tour löschen", AddStop = "Halt hinzufügen", PickStops = "Orte anklicken", PickStopsActive = "Klicken beenden";
        public const string AllRoutes = "Alle Wege", NoStops = "Noch keine Halte.";
        public static string TourLoad(TourAction a) => a == TourAction.load ? "Laden" : "Abladen";
        public static string DefaultTourName(int n) => "Tour " + n;
        public static string TrucksOnTour(int n) => n == 0 ? "Kein LKW fährt diese Tour." : n == 1 ? "1 LKW fährt diese Tour." : n + " LKW fahren diese Tour.";
        public static string TourRejected(string r) => r == "invalidName" ? "Der Name braucht 1 bis 30 Zeichen." : r == "invalidStop" ? "Dieser Halt passt nicht zum Ort." : r == "tooManyStops" ? "Mehr Halte gehen nicht." : "Geht nicht: " + r;
        public static readonly string[] ColorNames = { "Rot", "Blau", "Grün", "Orange", "Violett", "Türkis", "Pink", "Braun", "Hellgrün", "Dunkelblau" };

        // Meldungen
        public const string Notices = "Meldungen", NoNotices = "Keine Meldungen.", Show = "Hinzeigen";
        public static string Notice(NoticeKind k, string who)
        {
            switch (k)
            {
                case NoticeKind.jam: return "Stau: " + who + " kommt nicht weiter.";
                case NoticeKind.breakdown: return "Panne: " + who + " steht und blockiert die Spur.";
                case NoticeKind.noWorkshop: return who + " braucht eine Wartung, aber es gibt keine erreichbare Werkstatt.";
                case NoticeKind.leaseRenewed: return "Leasing von " + who + " läuft weiter (neue Laufzeit).";
                case NoticeKind.storageFull: return "Lager voll: Hier passt keine Ware mehr hinein.";
                default: return "Halle ohne Stapler: Die Ware bleibt im Wareneingang liegen.";
            }
        }

        // Flotte
        public static string FleetTitle(int n) => "Flotte (" + n + ")";
        public const string FleetEmpty = "Keine Fahrzeuge. Unter „Fahrzeuge“ in der Bauleiste anschaffen.";
        public const string FilterAll = "Alle", FilterService = "Wartung fällig", FilterBroken = "Panne", FilterIdle = "Wartend";

        public static string Perf(float fps, float ms, int vehicles) => fps.ToString("0") + " Bilder/s · Simulation " + ms.ToString("0.00") + " ms/Schritt · " + vehicles + " Fahrzeuge";
    }
}
