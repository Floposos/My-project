using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>
    /// Bereiche einer Halle (M3). Feste Reihenfolge der Stufen: Wareneingang → Lager → Verpackung →
    /// Etikettierung → Qualitätsprüfung → Warenausgang; Stufen dürfen fehlen (Frage 11, Empfehlung).
    /// </summary>
    public enum AreaKind { inbound, storage, packing, labeling, inspection, outbound }

    /// <summary>Ein Bereich als Rechteck innerhalb einer Halle, mit Lager je Ware.</summary>
    public sealed class HallArea
    {
        public int Id;
        public AreaKind Kind;
        public Footprint Rect;
        public Dictionary<ProductId, int> Stock = new Dictionary<ProductId, int>();
        /// <summary>Fortschritt der laufenden Verarbeitung (Verpackung, Etikettierung, Prüfung).</summary>
        public int Work;
        public int BuiltTick;
        public long PaidCents;
    }

    public enum ForkliftPhase { idle, toPickup, loading, toDropoff, unloading }

    /// <summary>Gabelstapler einer Halle (M3, Frage 5: einzeln sichtbar, je Halle gekauft).</summary>
    public sealed class Forklift
    {
        public int Id;
        /// <summary>Weg über Hallenfelder; Route[0] ist das aktuelle Feld.</summary>
        public List<Cell> Route = new List<Cell>();
        public int Progress;
        public ForkliftPhase Phase;
        public int Timer = 1;
        public Cargo Cargo;
        /// <summary>Laufender Auftrag: Ware von Bereich (FromId) zu Bereich (ToId).</summary>
        public Job Job;
        public long PriceCents;
        public int BoughtTick;
        public int WaitTicks;
    }

    /// <summary>Halle (M3): frei aufgezogenes Rechteck mit Tor-Seite, Bereichen und Staplern.</summary>
    public sealed class Hall
    {
        public int Id;
        public int X, Z, Width, Depth;
        public Side Gate = Side.S;
        public int BuiltTick;
        public long PaidCents;
        public List<HallArea> Areas = new List<HallArea>();
        public List<Forklift> Forklifts = new List<Forklift>();
        /// <summary>Hinweis „keine Stapler“ schon gegeben (bis ein Stapler gekauft wird).</summary>
        public bool WarnedNoForklift;

        [Newtonsoft.Json.JsonIgnore]
        public Footprint Rect => new Footprint(X, Z, Width, Depth);

        public HallArea AreaById(int id) => Areas.Find(a => a.Id == id);
        public HallArea FirstArea(AreaKind kind) => Areas.Find(a => a.Kind == kind);
    }

    /// <summary>Eine Kiste auf einem Förderband; Position in Tausendstel Feld ab dem ersten Feld.</summary>
    public sealed class BeltItem
    {
        public ProductId Product;
        public int Position;
    }

    /// <summary>
    /// Förderband (M3, Frage 6): auf dem Raster gezogen, eine Ebene, Laufrichtung vom ersten zum
    /// letzten Feld. Anfang grenzt an die Quelle, Ende an das Ziel (Zone, Halle oder Export-Ausfahrt).
    /// </summary>
    public sealed class Conveyor
    {
        public int Id;
        public List<Cell> Cells = new List<Cell>();
        public int FromSiteId, ToSiteId;
        public List<BeltItem> Items = new List<BeltItem>();
        public int BuiltTick;
        public long PaidCents;
        /// <summary>Schritte bis zur nächsten Aufnahme an der Quelle.</summary>
        public int Cooldown;
    }
}
