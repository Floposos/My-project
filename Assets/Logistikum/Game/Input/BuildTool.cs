using System.Collections.Generic;
using Logistikum.Sim;
using UnityEngine;
using Grid = Logistikum.Sim.Grid;

namespace Logistikum.Game
{
    public enum ToolKind
    {
        None, Road, Priority, PriorityRemove, Zone, ExportExit, Hall, Area, Conveyor, Demolish,
    }

    /// <summary>Aktives Werkzeug der Bauleiste.</summary>
    public sealed class Tool
    {
        public ToolKind Kind;
        public ZoneKind Zone;
        public AreaKind Area;

        public bool Drags => Kind == ToolKind.Road || Kind == ToolKind.Priority || Kind == ToolKind.PriorityRemove ||
                             Kind == ToolKind.Zone || Kind == ToolKind.Hall || Kind == ToolKind.Area || Kind == ToolKind.Conveyor;

        public bool Equals(Tool o) => o != null && o.Kind == Kind && o.Zone == Zone && o.Area == Area;
    }

    /// <summary>Ergebnis der Vorschau: Felder, ob baubar, Hinweistext am Mauszeiger.</summary>
    public sealed class Preview
    {
        public List<Cell> Cells = new List<Cell>();
        public HashSet<Cell> Blocked = new HashSet<Cell>();
        public Footprint Rect;
        public bool Ok;
        public string Tip;
        public Command Command;
    }

    /// <summary>
    /// Bauwerkzeuge (T1.1 ff.): Ziehen von Start nach Ziel (gerade oder L-Form) bzw. Rechteck aufziehen,
    /// Vorschau grün/rot mit Grund, Kosten vorab. Prüfung über dieselben Funktionen wie die Befehle.
    /// </summary>
    public static class BuildTool
    {
        public static Preview Evaluate(GameState state, Tool tool, Cell start, Cell end, bool dragging)
        {
            var p = new Preview();
            var from = dragging ? start : end;
            bool xFirst = RoadLine.PrefersXFirst(from, end);
            switch (tool.Kind)
            {
                case ToolKind.Road:
                {
                    var c = Roads.CheckBuild(state, from, end, xFirst);
                    p.Cells = RoadLine.Line(from, end, xFirst);
                    p.Blocked = new HashSet<Cell>(c.Blocked);
                    p.Ok = c.Ok;
                    p.Tip = c.Ok ? T.RoadCost(c.NewCells.Count, Fmt.Euro(c.CostCents)) : T.Reason(c.Reason) + " · " + T.RoadCost(c.NewCells.Count, Fmt.Euro(c.CostCents));
                    p.Command = new BuildRoadCommand { FromX = from.X, FromZ = from.Z, ToX = end.X, ToZ = end.Z, XFirst = xFirst };
                    break;
                }
                case ToolKind.Priority:
                case ToolKind.PriorityRemove:
                {
                    bool on = tool.Kind == ToolKind.Priority;
                    var tiles = Roads.TilesOnLine(state, from, end, xFirst);
                    p.Cells = tiles.ConvertAll(t => new Cell(t.X, t.Z));
                    if (p.Cells.Count == 0) p.Cells = RoadLine.Line(from, end, xFirst);
                    p.Ok = tiles.Count > 0;
                    p.Tip = p.Ok ? T.PriorityCells(tiles.Count, on) + " · " + T.Free : T.NoRoadHere;
                    p.Command = new SetPriorityCommand { FromX = from.X, FromZ = from.Z, ToX = end.X, ToZ = end.Z, XFirst = xFirst, Priority = on };
                    break;
                }
                case ToolKind.Zone:
                {
                    var f = Grid.RectBetween(from, end);
                    var c = Zones.CheckPlace(state, tool.Zone, f);
                    p.Rect = f;
                    p.Ok = c.Ok;
                    string info = tool.Zone == ZoneKind.W
                        ? T.WorkshopSize(f.Width, f.Depth, Bays.ForArea(ZoneKind.W, f.Area), Fmt.Euro(c.CostCents))
                        : T.ZoneSize(f.Width, f.Depth, f.Area * ZoneConfig.CapacityPerField, Fmt.Euro(c.CostCents));
                    if (c.Ok && Zones.NeighboursOf(state, tool.Zone, f).Count > 0) info += " · " + T.ZoneMerges;
                    p.Tip = c.Ok ? info : T.Reason(c.Reason) + " · " + info;
                    p.Command = new PlaceZoneCommand { Kind = tool.Zone, FromX = from.X, FromZ = from.Z, ToX = end.X, ToZ = end.Z };
                    break;
                }
                case ToolKind.ExportExit:
                {
                    var t = BuildingTypes.ExportExit;
                    int x = end.X - t.Width / 2, z = end.Z - t.Depth / 2;
                    var c = Build.CheckPlaceBuilding(state, BuildingTypeId.exportExit, x, z);
                    p.Rect = new Footprint(x, z, t.Width, t.Depth);
                    p.Ok = c.Ok;
                    p.Tip = (c.Ok ? "" : T.Reason(c.Reason) + " · ") + T.ExportExit + " · " + T.Cost(Fmt.Euro(c.CostCents));
                    p.Command = new PlaceBuildingCommand { Type = BuildingTypeId.exportExit, X = x, Z = z };
                    break;
                }
                case ToolKind.Hall:
                {
                    var f = Grid.RectBetween(from, end);
                    var c = Halls.CheckPlace(state, f);
                    p.Rect = f;
                    p.Ok = c.Ok;
                    p.Tip = (c.Ok ? "" : T.Reason(c.Reason) + " · ") + T.HallSize(f.Width, f.Depth, Fmt.Euro(c.CostCents));
                    p.Command = new PlaceHallCommand { FromX = from.X, FromZ = from.Z, ToX = end.X, ToZ = end.Z };
                    break;
                }
                case ToolKind.Area:
                {
                    var f = Grid.RectBetween(from, end);
                    var c = Halls.CheckArea(state, tool.Area, f, out _);
                    p.Rect = f;
                    p.Ok = c.Ok;
                    var info = T.AreaSize(T.Area(tool.Area), f.Width, f.Depth, f.Area * ZoneConfig.CapacityPerField, Fmt.Euro(c.CostCents));
                    p.Tip = (c.Ok ? "" : T.Reason(c.Reason) + " · ") + info;
                    p.Command = new PlaceAreaCommand { Kind = tool.Area, FromX = from.X, FromZ = from.Z, ToX = end.X, ToZ = end.Z };
                    break;
                }
                case ToolKind.Conveyor:
                {
                    var c = Conveyors.CheckBuild(state, from, end, xFirst);
                    p.Cells = c.Cells;
                    p.Blocked = new HashSet<Cell>(c.Blocked);
                    p.Ok = c.Ok;
                    p.Tip = c.Ok
                        ? T.ConveyorInfo(c.Cells.Count, Names.Site(state, c.FromSiteId), Names.Site(state, c.ToSiteId), Fmt.Euro(c.CostCents))
                        : T.Reason(c.Reason) + " · " + T.Cost(Fmt.Euro(c.CostCents));
                    p.Command = new BuildConveyorCommand { FromX = from.X, FromZ = from.Z, ToX = end.X, ToZ = end.Z, XFirst = xFirst };
                    break;
                }
                case ToolKind.Demolish:
                    EvaluateDemolish(state, end, p);
                    break;
            }
            return p;
        }

        /// <summary>Abriss: Straße, Förderband, Feld einer Zone, Gebäude, Hallenbereich oder Halle unter dem Mauszeiger.</summary>
        static void EvaluateDemolish(GameState state, Cell c, Preview p)
        {
            p.Cells = new List<Cell> { c };
            var road = Roads.RefundAt(state, c.X, c.Z);
            if (road != null) { Done(p, T.Refund(Fmt.Euro(road.Value)), new DemolishRoadCommand { X = c.X, Z = c.Z }); return; }
            var belt = Sim.Conveyors.At(state, c.X, c.Z);
            if (belt != null)
            {
                p.Cells = belt.Cells;
                Done(p, T.DemolishConveyor + " · " + T.Refund(Fmt.Euro(Build.DemolishRefund(state, belt.BuiltTick, belt.PaidCents))), new DemolishConveyorCommand { X = c.X, Z = c.Z });
                return;
            }
            var zone = state.Zones.Find(z => ZoneShape.Contains(z.Parts, c.X, c.Z));
            if (zone != null)
            {
                var refund = Zones.CellRefund(state, zone, c.X, c.Z) ?? 0;
                Done(p, T.Refund(Fmt.Euro(refund)), new DemolishZoneCellCommand { ZoneId = zone.Id, X = c.X, Z = c.Z });
                return;
            }
            var building = state.Buildings.Find(b => Buildings.FootprintOf(b).Contains(c.X, c.Z));
            if (building != null)
            {
                p.Rect = Buildings.FootprintOf(building);
                Done(p, T.Refund(Fmt.Euro(Build.DemolishRefund(state, building.BuiltTick, building.PaidCents))), new DemolishBuildingCommand { BuildingId = building.Id });
                return;
            }
            var hall = Halls.HallAt(state, c.X, c.Z);
            if (hall != null)
            {
                var area = hall.Areas.Find(a => a.Rect.Contains(c.X, c.Z));
                if (area != null)
                {
                    p.Rect = area.Rect;
                    Done(p, T.DemolishArea + " · " + T.Refund(Fmt.Euro(Build.DemolishRefund(state, area.BuiltTick, area.PaidCents))), new DemolishAreaCommand { AreaId = area.Id });
                    return;
                }
                p.Rect = hall.Rect;
                Done(p, T.DemolishHall + " · " + T.Refund(Fmt.Euro(Halls.Refund(state, hall))), new DemolishHallCommand { HallId = hall.Id });
                return;
            }
            p.Ok = false;
            p.Tip = T.NothingToDemolish;
        }

        static void Done(Preview p, string tip, Command c)
        {
            p.Ok = true;
            p.Tip = tip;
            p.Command = c;
        }
    }

    /// <summary>Namen von Orten und Fahrzeugen für die Oberfläche.</summary>
    public static class Names
    {
        public static string Site(GameState state, int id)
        {
            var z = state.ZoneById(id);
            if (z != null) return T.Zone(z.Kind) + " " + Number(state, id);
            if (state.HallById(id) != null) return T.Hall + " " + Number(state, id);
            var b = state.BuildingById(id);
            if (b != null && b.Type == BuildingTypeId.exportExit) return T.ExportExit + " " + Number(state, id);
            return "–";
        }

        /// <summary>Laufende Nummer je Art (1, 2, …) statt der internen Id.</summary>
        static int Number(GameState state, int id)
        {
            var z = state.ZoneById(id);
            if (z != null) return state.Zones.FindAll(x => x.Kind == z.Kind && x.Id <= id).Count;
            if (state.HallById(id) != null) return state.Halls.FindAll(x => x.Id <= id).Count;
            return state.Buildings.FindAll(x => x.Type == BuildingTypeId.exportExit && x.Id <= id).Count;
        }

        public static string Vehicle(GameState state, Vehicle v)
        {
            if (v is Supplier) return T.Supplier;
            var t = (Truck)v;
            int n = 0;
            foreach (var o in state.Vehicles) if (o is Truck ot && ot.Model == t.Model && ot.Id <= t.Id) n++;
            return T.Model(t.Model) + " " + n;
        }

        public static string VehicleById(GameState state, int? id)
        {
            var v = id.HasValue ? state.VehicleById(id.Value) : null;
            return v == null ? "Ein Fahrzeug" : Vehicle(state, v);
        }
    }
}
