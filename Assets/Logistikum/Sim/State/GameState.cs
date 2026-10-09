using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Ein Gebäude auf dem Raster. X/Z = Feld der linken oberen Ecke.</summary>
    public sealed class Building
    {
        public int Id;
        public BuildingTypeId Type;
        public int X, Z;
        /// <summary>Schritt, in dem gebaut wurde (Abriss-Erstattung am selben Spieltag).</summary>
        public int BuiltTick;
        public long PaidCents;
    }

    /// <summary>Ein Straßenfeld. Verbindungsstücke ergeben sich aus den Nachbarn.</summary>
    public sealed class RoadTile
    {
        public int X, Z;
        public int BuiltTick;
        public long PaidCents;
        /// <summary>Als Vorfahrtsstraße markiert (T2.2).</summary>
        public bool Priority;
    }

    /// <summary>Ein Rechteck einer Zone mit eigenem Bautag und Preis.</summary>
    public sealed class ZonePart : Footprint
    {
        public int BuiltTick;
        public long PaidCents;
        public ZonePart() { }
        public ZonePart(Footprint f, int builtTick, long paidCents) : base(f.X, f.Z, f.Width, f.Depth)
        { BuiltTick = builtTick; PaidCents = paidCents; }
    }

    /// <summary>Frei aufgezogene Zone (Lieferort A, B, C oder Werkstatt) mit Lager.</summary>
    public sealed class Zone
    {
        public int Id;
        public ZoneKind Kind;
        /// <summary>Fläche: ein Rechteck oder mehrere angrenzende (verschmolzen).</summary>
        public List<ZonePart> Parts = new List<ZonePart>();
        /// <summary>Tor-Seite, über die LKW ein- und ausfahren.</summary>
        public Side Gate;
        /// <summary>Bestand je Ware (nur Waren, die die Zone lagert).</summary>
        public Dictionary<ProductId, int> Stock = new Dictionary<ProductId, int>();
        /// <summary>Fortschritt der laufenden Verarbeitung (B und C).</summary>
        public int Work;
    }

    public enum OrderInterval { once, daily, weekly, monthly }
    /// <summary>Warum eine fällige Lieferung wartet.</summary>
    public enum OrderBlock { noSite, noRoute, full, noMoney }

    /// <summary>Rohware-Bestellung: einmalig oder als Dauerauftrag.</summary>
    public sealed class Order
    {
        public int Id;
        public ProductId Product;
        public int Quantity;
        public OrderInterval Interval;
        public int NextTick;
        public OrderBlock? Blocked;
    }

    /// <summary>Einfahrt: nächste erlaubte Durchfahrt je Richtung (T2.7).</summary>
    public sealed class EntranceState
    {
        public int NextInTick;
        public int NextOutTick;
    }

    /// <summary>
    /// Der komplette Spielzustand: nur JSON-fähige Werte, Tabellen nach ID geordnet, Verweise nur
    /// über IDs. Speichern = diesen Zustand serialisieren.
    /// </summary>
    public sealed class GameState
    {
        public uint Seed;
        public int Tick;
        public RngState Rng;
        /// <summary>Eigener Zufallsstrom für Ereignisse wie Pannen (T2.6).</summary>
        public RngState EventRng;
        public int NextId;
        public Finance Finance;
        public List<Building> Buildings = new List<Building>();
        public List<RoadTile> Roads = new List<RoadTile>();
        public List<Zone> Zones = new List<Zone>();
        public List<Order> Orders = new List<Order>();
        public List<Vehicle> Vehicles = new List<Vehicle>();
        public List<Tour> Tours = new List<Tour>();
        public List<Notice> Notices = new List<Notice>();
        public EntranceState Entrance = new EntranceState();
        /// <summary>Hallen (M3) mit Bereichen, Staplern und Bandkisten.</summary>
        public List<Hall> Halls = new List<Hall>();
        /// <summary>Förderbänder (M3).</summary>
        public List<Conveyor> Conveyors = new List<Conveyor>();
        /// <summary>Orte bzw. Hallenbereiche, für die „Lager voll“ schon gemeldet ist (T3.6).</summary>
        public List<int> WarnedFull = new List<int>();

        /// <summary>Lage der Test-Halle aus M0 (nahe der Eingangsstraße).</summary>
        public const int TestHallX = 12, TestHallZ = 58;

        public static uint EventSeed(uint seed) => seed ^ 0x5bd1e995u;

        public static GameState CreateInitial(uint seed)
        {
            return new GameState
            {
                Seed = seed,
                Tick = 0,
                Rng = new RngState(seed),
                EventRng = new RngState(EventSeed(seed)),
                NextId = 2,
                Finance = Finance.Create(EconomyConfig.StartingBalanceCents, 0),
                // M3 (ANNAHME): Die Test-Halle aus M0 ist eine echte, leere Halle.
                Halls = new List<Hall>
                {
                    new Hall { Id = 1, X = TestHallX, Z = TestHallZ, Width = 8, Depth = 6, Gate = Side.S },
                },
            };
        }

        public Zone ZoneById(int id) => Zones.Find(z => z.Id == id);
        public Vehicle VehicleById(int id) => Vehicles.Find(v => v.Id == id);
        public Building BuildingById(int id) => Buildings.Find(b => b.Id == id);
        public Hall HallById(int id) => Halls.Find(h => h.Id == id);
    }
}
