using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Basis aller Ereignisse der Simulation. Neue Systeme ergänzen hier ihre Typen.</summary>
    public abstract class SimEvent { }

    public sealed class DayStarted : SimEvent { public int Year, Month, Day; }
    public sealed class MonthStarted : SimEvent { public int Year, Month; }
    public sealed class YearStarted : SimEvent { public int Year; }
    public sealed class BalanceChanged : SimEvent { public long BalanceCents, DeltaCents; }
    public sealed class Booked : SimEvent { public BookingCategory Category; public long AmountCents; public Place? At; }
    public sealed class CommandRejected : SimEvent { public string Command; public string Reason; }
    public sealed class BuildingPlaced : SimEvent { public int Id; public BuildingTypeId Type; public int X, Z; public long CostCents; }
    public sealed class BuildingDemolished : SimEvent { public int Id; public long RefundCents; }
    public sealed class ZonePlaced : SimEvent { public int Id; public ZoneKind Kind; public long CostCents; }
    public sealed class ZoneDemolished : SimEvent { public int Id; public long RefundCents; }
    public sealed class ZoneCellDemolished : SimEvent { public int Id, X, Z; public long RefundCents; }
    public sealed class GoodsDelivered : SimEvent { public int SiteId; public ProductId Product; public int Quantity; }
    public sealed class GoodsProduced : SimEvent { public int SiteId; public ProductId Product; }
    public sealed class GoodsSold : SimEvent { public int ExitId; public ProductId Product; public int Quantity; public long RevenueCents; }
    public sealed class VehicleBought : SimEvent { public int Id; }
    public sealed class VehicleBrokeDown : SimEvent { public int Id, X, Z; }
    /// <summary>Verkauft bzw. Leasing zurückgegeben (Betrag: + Erlös, − Strafe).</summary>
    public sealed class VehicleDisposed : SimEvent { public int Id; public long AmountCents; }
    public sealed class TrafficJam : SimEvent { public int VehicleId, X, Z; }
    public sealed class NoticeAdded : SimEvent { public Notice Notice; }
    public sealed class RoadBuilt : SimEvent { public List<Cell> Cells; public long CostCents; }
    public sealed class RoadDemolished : SimEvent { public int X, Z; public long RefundCents; }
    public sealed class RoadPriorityChanged : SimEvent { public List<Cell> Cells; public bool Priority; }
}
