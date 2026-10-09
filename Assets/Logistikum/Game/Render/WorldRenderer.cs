using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>
    /// Verbindet alle Ansichten. Bauwerke werden nur bei Änderungen neu gebaut (Ereignisse oder nach Befehlen),
    /// Fahrzeuge, Stapler und Bandkisten jedes Bild, Lagerbestände mehrmals je Sekunde.
    /// </summary>
    public sealed class WorldRenderer
    {
        public readonly Transform Root;
        public readonly RoadsView Roads;
        public readonly ZonesView Zones;
        public readonly HallsView Halls;
        public readonly ConveyorsView Conveyors;
        public readonly VehiclesView Vehicles;
        public readonly RouteLinesView Routes;
        public readonly ExternalTrafficView Traffic;
        public readonly GhostView Ghost;
        bool roadsDirty = true, sitesDirty = true, hallsDirty = true, conveyorsDirty = true;
        float refreshTimer;

        public WorldRenderer()
        {
            Root = new GameObject("Welt").transform;
            new TerrainView(Root);
            Roads = new RoadsView(Root);
            Zones = new ZonesView(Root);
            Halls = new HallsView(Root);
            Conveyors = new ConveyorsView(Root);
            Vehicles = new VehiclesView(Root);
            Routes = new RouteLinesView(Root);
            Traffic = new ExternalTrafficView(Root);
            Ghost = new GhostView(Root);
        }

        public void MarkAll() { roadsDirty = sitesDirty = hallsDirty = conveyorsDirty = true; }

        public void Hook(EventBus bus)
        {
            bus.On<RoadBuilt>(_ => roadsDirty = true);
            bus.On<RoadDemolished>(_ => roadsDirty = true);
            bus.On<RoadPriorityChanged>(_ => roadsDirty = true);
            bus.On<ZonePlaced>(_ => sitesDirty = true);
            bus.On<ZoneDemolished>(_ => sitesDirty = true);
            bus.On<ZoneCellDemolished>(_ => sitesDirty = true);
            bus.On<BuildingPlaced>(_ => sitesDirty = true);
            bus.On<BuildingDemolished>(_ => sitesDirty = true);
            bus.On<HallChanged>(_ => hallsDirty = true);
            bus.On<ConveyorChanged>(_ => conveyorsDirty = true);
        }

        public void Sync(GameState state, float alpha, CameraController cam, float dt)
        {
            if (roadsDirty) { Roads.Rebuild(state); roadsDirty = false; }
            if (sitesDirty) { Zones.Rebuild(state); sitesDirty = false; refreshTimer = 0; }
            if (hallsDirty) { Halls.Rebuild(state); hallsDirty = false; refreshTimer = 0; }
            if (conveyorsDirty) { Conveyors.Rebuild(state); conveyorsDirty = false; }
            refreshTimer -= dt;
            if (refreshTimer <= 0)
            {
                refreshTimer = 0.25f;
                Zones.Refresh(state, cam.Camera);
            }
            Halls.Refresh(state, cam.Distance);
            Vehicles.Sync(state, alpha, cam.Camera);
            Halls.SyncForklifts(state, alpha);
            Conveyors.SyncItems(state, alpha);
            Routes.Sync(state, alpha);
            Traffic.Sync(state.Tick, alpha);
        }

        public void Clear()
        {
            Vehicles.Clear();
            MarkAll();
        }
    }
}
