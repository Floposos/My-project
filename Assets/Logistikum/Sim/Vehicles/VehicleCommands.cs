using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Fahrzeug kaufen oder leasen (T2.5).</summary>
    public sealed class BuyVehicleCommand : Command
    {
        public VehicleModel Model = VehicleModel.truck; public VehicleDrive Drive = VehicleDrive.diesel; public bool Lease;
        public override string Name => "vehicle/buy";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var t = Fleet.Buy(s, bus, Model, Drive, Lease, out var r);
            return t == null ? CommandResult.Fail(r) : CommandResult.Success(t.Id);
        }
    }

    /// <summary>Verkaufen bzw. Leasing zurückgeben.</summary>
    public sealed class DisposeVehicleCommand : Command
    {
        public int TruckId;
        public override string Name => "vehicle/dispose";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Fleet.Dispose(s, bus, TruckId) ? CommandResult.Success() : CommandResult.Fail("notFound");
    }

    public sealed class ServiceVehicleCommand : Command
    {
        public int TruckId;
        public override string Name => "vehicle/service";
        public override CommandResult Apply(GameState s, EventBus bus) => Commands.FromReason(Tours.RequestService(s, TruckId));
    }

    /// <summary>Tour zuweisen; null = Automatik.</summary>
    public sealed class AssignTourCommand : Command
    {
        public int TruckId; public int? TourId;
        public override string Name => "vehicle/assignTour";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Tours.Assign(s, TruckId, TourId) ? CommandResult.Success() : CommandResult.Fail("notFound");
    }

    /// <summary>Tour anlegen (T2.1). Halte werden immer als Ganzes geschickt.</summary>
    public sealed class CreateTourCommand : Command
    {
        public string TourName; public int? Color; public List<TourStop> Stops;
        public override string Name => "tour/create";
        public override CommandResult Apply(GameState s, EventBus bus)
        {
            var t = Tours.Create(s, TourName, Color, Stops, out var r);
            return t == null ? CommandResult.Fail(r) : CommandResult.Success(t.Id);
        }
    }

    public sealed class UpdateTourCommand : Command
    {
        public int TourId; public string TourName; public int? Color; public List<TourStop> Stops;
        public override string Name => "tour/update";
        public override CommandResult Apply(GameState s, EventBus bus) => Commands.FromReason(Tours.Update(s, TourId, TourName, Color, Stops));
    }

    public sealed class DeleteTourCommand : Command
    {
        public int TourId;
        public override string Name => "tour/delete";
        public override CommandResult Apply(GameState s, EventBus bus) =>
            Tours.Delete(s, TourId) ? CommandResult.Success() : CommandResult.Fail("notFound");
    }
}
