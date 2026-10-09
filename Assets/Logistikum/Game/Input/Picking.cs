using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    public enum PickKind { None, Vehicle, Zone, Building, Hall, Conveyor }

    public struct Pick
    {
        public PickKind Kind;
        public int Id;
        public static readonly Pick None = new Pick { Kind = PickKind.None };
    }

    /// <summary>Objekt an einem Bodenpunkt: zuerst Fahrzeuge in der Nähe, dann Bauwerk auf dem Feld.</summary>
    public static class Picking
    {
        public static Pick At(GameState state, Vector3 ground, float alpha)
        {
            Vehicle best = null;
            float bestD = 0.45f;
            foreach (var v in state.Vehicles)
            {
                if (v.Route.Count == 0 || v.Here.X < -1) continue;
                var p = VehiclePose.WorldPosition(v, alpha, out _);
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(ground.x, ground.z));
                if (d < bestD) { bestD = d; best = v; }
            }
            if (best != null) return new Pick { Kind = PickKind.Vehicle, Id = best.Id };
            var c = Coords.CellAt(ground);
            var occ = new Occupancy(state);
            int id = occ.At(c.X, c.Z);
            if (id == Occupancy.ConveyorCell)
            {
                var belt = Sim.Conveyors.At(state, c.X, c.Z);
                return belt != null ? new Pick { Kind = PickKind.Conveyor, Id = belt.Id } : Pick.None;
            }
            if (id <= 0) return Pick.None;
            if (state.ZoneById(id) != null) return new Pick { Kind = PickKind.Zone, Id = id };
            if (state.HallById(id) != null) return new Pick { Kind = PickKind.Hall, Id = id };
            if (state.BuildingById(id) != null) return new Pick { Kind = PickKind.Building, Id = id };
            return Pick.None;
        }
    }
}
