using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Ladung eines Fahrzeugs.</summary>
    public sealed class Cargo
    {
        public ProductId Product;
        public int Quantity;
        public Cargo() { }
        public Cargo(ProductId p, int q) { Product = p; Quantity = q; }
    }

    /// <summary>Gemeinsame Felder aller Fahrzeuge. Position: Route[0] ist das aktuelle Feld.</summary>
    public abstract class Vehicle
    {
        public int Id;
        public List<Cell> Route = new List<Cell>();
        /// <summary>Fortschritt zum nächsten Feld in Tausendsteln.</summary>
        public int Progress;
        public Cargo Cargo;
        /// <summary>Restschritte beim Ab-/Aufladen bzw. bis zum nächsten Versuch.</summary>
        public int Timer = 1;
        /// <summary>Fahrtrichtung im aktuellen Feld: 0 Nord, 1 Ost, 2 Süd, 3 West (T2.2).</summary>
        public int Heading = 1;
        /// <summary>Steht abseits der Fahrbahn (Stellplatz, geparkt) und belegt keine Spur.</summary>
        public bool OffRoad;
        /// <summary>Belegter Stellplatz: Id des Orts (T2.3).</summary>
        public int? BayAt;
        /// <summary>Schritte ohne Unterbrechung im Verkehr oder vor dem Tor (T2.4).</summary>
        public int WaitTicks;

        /// <summary>Art im Spielstand: "supplier" oder "truck".</summary>
        public abstract string Kind { get; }

        [Newtonsoft.Json.JsonIgnore] public Cell Here => Route.Count > 0 ? Route[0] : RoadNetwork.Entrance;
    }

    public enum SupplierPhase { toSite, handling, toExit, noRoute }

    /// <summary>Zulieferer: bringt eingekaufte Rohware von außen zu einem Lieferort.</summary>
    public sealed class Supplier : Vehicle
    {
        public override string Kind => "supplier";
        public SupplierPhase Phase;
        public int TargetId;
        /// <summary>Bezahlter Betrag (Erstattung, wenn das Ziel wegfällt).</summary>
        public long PaidCents;
    }

    /// <summary>Transportauftrag eines LKW: Ware von Ort zu Ort.</summary>
    public sealed class Job
    {
        public ProductId Product;
        public int FromId, ToId, Quantity;
    }

    public enum TourAction { load, unload }

    /// <summary>Halt einer festen Tour (T1.5b).</summary>
    public sealed class TourStop
    {
        public int SiteId;
        public TourAction Action;
        public ProductId Product;
        public TourStop Copy() => new TourStop { SiteId = SiteId, Action = Action, Product = Product };
    }

    /// <summary>Tour als eigener Eintrag (T2.1): mehreren LKW zuweisbar, mit Farbe.</summary>
    public sealed class Tour
    {
        public int Id;
        public string Name;
        /// <summary>Index in TourColors.</summary>
        public int Color;
        public List<TourStop> Stops = new List<TourStop>();
    }

    /// <summary>Leasingvertrag (T2.5): feste Laufzeit, Monatsrate, verlängert sich automatisch.</summary>
    public sealed class Lease
    {
        public long MonthlyCents;
        public int NextPaymentTick;
        public int EndTick;
    }

    /// <summary>Verschleiß, Wartung und Pannen eines eigenen Fahrzeugs (T2.6).</summary>
    public sealed class Upkeep
    {
        public int Condition = MaintenanceConfig.FullCondition;
        public int WearRest;
        public int BrokenTicks;
        public int Breakdowns;
        public int? LastBreakdownTick;
        public int? LastServiceTick;
        public bool ServiceRequested;
        public bool WarnedNoWorkshop;
        public int? WorkshopId;
    }

    public enum TruckIdleReason { noJob, noRoute, noDestination, noTour }
    public enum TruckPhase { idle, toPickup, loading, toDropoff, unloading, toWorkshop, servicing }

    /// <summary>Eigener LKW bzw. Transporter: Automatik oder feste Tour (TourId, null = Automatik).</summary>
    public sealed class Truck : Vehicle
    {
        public override string Kind => "truck";
        public TruckPhase Phase;
        public Job Job;
        public TruckIdleReason? IdleReason;
        /// <summary>Gefahrene Tausendstel Felder, noch nicht als Kilometerkosten gebucht.</summary>
        public long Odometer;
        public int? TourId;
        public int TourIndex;
        public VehicleModel Model = VehicleModel.truck;
        public VehicleDrive Drive = VehicleDrive.diesel;
        /// <summary>Kaufpreis (bei Leasing der Listenpreis).</summary>
        public long PriceCents;
        public int BoughtTick;
        /// <summary>null = gekauft.</summary>
        public Lease Lease;
        public Upkeep Upkeep = new Upkeep();
    }
}
