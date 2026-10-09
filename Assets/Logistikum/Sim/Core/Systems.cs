using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Meldet Tages-, Monats- und Jahreswechsel als Ereignisse.</summary>
    public sealed class CalendarSystem : ISimSystem
    {
        public void Update(GameState state, EventBus bus)
        {
            if (!GameTime.IsDayStart(state.Tick)) return;
            var c = GameTime.CalendarAt(state.Tick);
            if (c.Month == 1 && c.Day == 1) bus.Emit(new YearStarted { Year = c.Year });
            if (c.Day == 1) bus.Emit(new MonthStarted { Year = c.Year, Month = c.Month });
            bus.Emit(new DayStarted { Year = c.Year, Month = c.Month, Day = c.Day });
        }
    }

    sealed class FuncSystem : ISimSystem
    {
        readonly System.Action<GameState, EventBus> update;
        public FuncSystem(System.Action<GameState, EventBus> update) { this.update = update; }
        public void Update(GameState state, EventBus bus) => update(state, bus);
    }

    public static class Systems
    {
        /// <summary>Alle Systeme in fester Reihenfolge (wichtig für Determinismus).</summary>
        public static readonly IReadOnlyList<ISimSystem> Default = new ISimSystem[]
        {
            new CalendarSystem(),
            new FuncSystem(Orders.Update),
            new VehicleSystem(),
            new FuncSystem(Production.Update),
            new FuncSystem(HallProcessing.Update),
            new FuncSystem(Forklifts.Update),
            new FuncSystem(Conveyors.Update),
            new FuncSystem(StorageWarnings.Update),
        };
    }
}
