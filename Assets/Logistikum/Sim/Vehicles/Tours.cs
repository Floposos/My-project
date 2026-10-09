using System;
using System.Collections.Generic;
using System.Linq;

namespace Logistikum.Sim
{
    /// <summary>Touren (T2.1) und LKW-Befehle.</summary>
    public static class Tours
    {
        public static Tour ById(GameState state, int? id) => id == null ? null : state.Tours.Find(t => t.Id == id);
        public static Tour TourOf(GameState state, Truck t) => ById(state, t.TourId);

        public static Truck TruckOf(GameState state, int id) => state.VehicleById(id) as Truck;

        /// <summary>Laufenden Auftrag verwerfen und neu beginnen; Ladung bleibt an Bord.</summary>
        public static void RestartTruck(Truck t)
        {
            t.Job = null;
            t.Phase = TruckPhase.idle;
            t.IdleReason = null;
            t.Timer = 1;
            t.TourIndex = 0;
            t.Upkeep.WorkshopId = null;
        }

        /// <summary>Ist der Halt sinnvoll? Laden nur dort, wo die Ware liegen kann; Abladen auch am Export.</summary>
        public static bool IsValidStop(GameState state, TourStop stop)
        {
            var site = Sites.ById(state, stop.SiteId);
            if (site == null) return false;
            if (site.Kind == SiteKind.export) return stop.Action == TourAction.unload && Export.Price(stop.Product) != null;
            if (site.Kind == SiteKind.hall)
                return stop.Action == TourAction.unload ? Stock.HallAccepts(stop.Product) : Stock.PickStock(state, site.Id) != null;
            var zone = state.ZoneById(site.Id);
            return zone != null && Stock.Stores(zone, stop.Product);
        }

        static string Check(GameState state, string name, int? color, List<TourStop> stops)
        {
            if (name != null)
            {
                var n = name.Trim();
                if (n.Length == 0 || n.Length > VehicleConfig.TourNameMaxLength) return "invalidName";
            }
            if (color != null && (color < 0 || color >= TourColors.All.Length)) return "invalidColor";
            if (stops != null)
            {
                if (stops.Count > VehicleConfig.TourMaxStops) return "tooManyStops";
                if (!stops.All(s => IsValidStop(state, s))) return "invalidStop";
            }
            return null;
        }

        /// <summary>Farbe für eine neue Tour: die am wenigsten benutzte, bei Gleichstand die erste.</summary>
        public static int SuggestColor(GameState state)
        {
            int best = 0, bestCount = int.MaxValue;
            for (int i = 0; i < TourColors.All.Length; i++)
            {
                int count = state.Tours.Count(t => t.Color == i);
                if (count < bestCount) { best = i; bestCount = count; }
            }
            return best;
        }

        public static Tour Create(GameState state, string name, int? color, List<TourStop> stops, out string rejection)
        {
            rejection = Check(state, name, color, stops);
            if (rejection != null) return null;
            int id = state.NextId++;
            var tour = new Tour
            {
                Id = id,
                Name = name?.Trim() ?? "Tour " + id,
                Color = color ?? SuggestColor(state),
                Stops = (stops ?? new List<TourStop>()).Select(s => s.Copy()).ToList(),
            };
            state.Tours.Add(tour);
            return tour;
        }

        /// <summary>Ändert Name, Farbe oder Halte. Neue Halte: alle LKW der Tour beginnen beim ersten Halt.</summary>
        public static string Update(GameState state, int id, string name, int? color, List<TourStop> stops)
        {
            var tour = ById(state, id);
            if (tour == null) return "notFound";
            var rejection = Check(state, name, color, stops);
            if (rejection != null) return rejection;
            if (name != null) tour.Name = name.Trim();
            if (color != null) tour.Color = color.Value;
            if (stops != null)
            {
                tour.Stops = stops.Select(s => s.Copy()).ToList();
                foreach (var t in TrucksOn(state, id)) RestartTruck(t);
            }
            return null;
        }

        /// <summary>Löscht eine Tour. ANNAHME (T2.1): Ihre LKW fahren danach in der Automatik.</summary>
        public static bool Delete(GameState state, int id)
        {
            if (ById(state, id) == null) return false;
            foreach (var t in TrucksOn(state, id))
            {
                t.TourId = null;
                RestartTruck(t);
            }
            state.Tours.RemoveAll(t => t.Id == id);
            return true;
        }

        public static List<Truck> TrucksOn(GameState state, int tourId) =>
            state.Vehicles.OfType<Truck>().Where(t => t.TourId == tourId).ToList();

        /// <summary>Tour zuweisen; null = Automatik. Ladung bleibt an Bord, der Auftrag entfällt.</summary>
        public static bool Assign(GameState state, int truckId, int? tourId)
        {
            var t = TruckOf(state, truckId);
            if (t == null || (tourId != null && ById(state, tourId) == null)) return false;
            if (t.TourId == tourId) return true;
            t.TourId = tourId;
            RestartTruck(t);
            return true;
        }

        /// <summary>„Zur Werkstatt“: nach dem laufenden Auftrag zur nächsten Werkstatt. null = angenommen.</summary>
        public static string RequestService(GameState state, int truckId)
        {
            var t = TruckOf(state, truckId);
            if (t == null) return "notFound";
            if (!state.Zones.Exists(z => z.Kind == ZoneKind.W)) return "noWorkshop";
            t.Upkeep.ServiceRequested = true;
            t.Upkeep.WarnedNoWorkshop = false;
            return null;
        }
    }

    /// <summary>
    /// Alle Fahrzeuge eines Schritts in fester Reihenfolge (nach Id) mit gemeinsamer Verkehrslage;
    /// Leasingraten bei Fälligkeit, am Tageswechsel die Fahrzeugkosten.
    /// </summary>
    public sealed class VehicleSystem : ISimSystem
    {
        public void Update(GameState state, EventBus bus)
        {
            var traffic = new Traffic(new RoadNetwork(state), state) { Gate = new EntranceGate(state) };
            var ctx = new VehicleCtx { State = state, Bus = bus, Traffic = traffic };
            var remaining = new List<Vehicle>(state.Vehicles.Count);
            foreach (var v in state.Vehicles.ToArray())
            {
                if (v is Truck t) Trucks.Step(ctx, t);
                else if (!Suppliers.Step(ctx, (Supplier)v)) continue;
                remaining.Add(v);
            }
            state.Vehicles = remaining;
            Fleet.UpdateLeases(state, bus);
            if (GameTime.IsDayStart(state.Tick)) Trucks.BookCosts(state, bus);
        }
    }
}
