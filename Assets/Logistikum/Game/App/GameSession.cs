using System.Diagnostics;
using Logistikum.Sim;

namespace Logistikum.Game
{
    /// <summary>Simulation + Takt + Geschwindigkeit (0 = Pause; merkt die letzte Stufe für „Weiter“).</summary>
    public sealed class GameSession
    {
        public readonly Simulation Sim;
        readonly StepClock clock = new StepClock();
        public int Speed { get; private set; }
        public int LastSpeed { get; private set; } = 1;
        /// <summary>Gleitender Mittelwert der Rechenzeit je Schritt (Leistungsanzeige).</summary>
        public float MsPerTick { get; private set; }

        public GameSession(GameState state) { Sim = new Simulation(state); }

        public GameState State => Sim.State;
        public float Alpha => Speed == 0 ? 0 : clock.Alpha;
        public bool Paused => Speed == 0;

        public void SetSpeed(int speed)
        {
            if (speed > 0) LastSpeed = speed;
            Speed = speed;
        }

        public void TogglePause() => SetSpeed(Paused ? LastSpeed : 0);

        /// <summary>Rechnet die fälligen Schritte für die vergangene Echtzeit.</summary>
        public int Advance(float realSeconds)
        {
            int ticks = clock.Advance(realSeconds * 1000.0, Speed);
            if (ticks == 0) return 0;
            var watch = Stopwatch.StartNew();
            Sim.Run(ticks);
            float ms = (float)watch.Elapsed.TotalMilliseconds / ticks;
            MsPerTick = MsPerTick == 0 ? ms : MsPerTick * 0.95f + ms * 0.05f;
            return ticks;
        }

        public CommandResult Execute(Command c) => Sim.Execute(c);

        public void Replace(GameState state)
        {
            Sim.ReplaceState(state);
            clock.Reset();
        }
    }

    /// <summary>Beispiel-Campus für das Hauptmenü („lebender Campus“, Entscheidung 07.10.2026).</summary>
    public static class DemoCampus
    {
        public static GameState Create()
        {
            var s = new Simulation(GameState.CreateInitial(2026));
            void Do(Command c) => s.Execute(c);
            Do(new DemolishHallCommand { HallId = 1 });
            Do(new BuildRoadCommand { FromX = 0, FromZ = 61, ToX = 60, ToZ = 61, XFirst = true });
            Do(new BuildRoadCommand { FromX = 30, FromZ = 61, ToX = 30, ToZ = 40, XFirst = false });
            Do(new BuildRoadCommand { FromX = 60, FromZ = 61, ToX = 60, ToZ = 80, XFirst = false });
            Do(new BuildRoadCommand { FromX = 60, FromZ = 80, ToX = 70, ToZ = 80, XFirst = true });
            Do(new BuildRoadCommand { FromX = 70, FromZ = 80, ToX = 70, ToZ = 123, XFirst = false });
            Do(new SetPriorityCommand { FromX = 0, FromZ = 61, ToX = 60, ToZ = 61, XFirst = true, Priority = true });
            Do(new PlaceZoneCommand { Kind = ZoneKind.A, FromX = 10, FromZ = 56, ToX = 15, ToZ = 60 });
            Do(new PlaceZoneCommand { Kind = ZoneKind.B, FromX = 20, FromZ = 62, ToX = 26, ToZ = 67 });
            Do(new PlaceZoneCommand { Kind = ZoneKind.C, FromX = 31, FromZ = 44, ToX = 36, ToZ = 50, Gate = Side.W });
            Do(new PlaceZoneCommand { Kind = ZoneKind.W, FromX = 40, FromZ = 57, ToX = 44, ToZ = 60 });
            Do(new PlaceHallCommand { FromX = 61, FromZ = 64, ToX = 74, ToZ = 76 });
            var hall = s.State.Halls[s.State.Halls.Count - 1];
            hall.Gate = Side.W;
            Do(new PlaceAreaCommand { Kind = AreaKind.inbound, FromX = 61, FromZ = 64, ToX = 63, ToZ = 68 });
            Do(new PlaceAreaCommand { Kind = AreaKind.outbound, FromX = 61, FromZ = 72, ToX = 63, ToZ = 76 });
            Do(new PlaceAreaCommand { Kind = AreaKind.packing, FromX = 66, FromZ = 64, ToX = 70, ToZ = 68 });
            Do(new PlaceAreaCommand { Kind = AreaKind.labeling, FromX = 66, FromZ = 72, ToX = 70, ToZ = 76 });
            Do(new BuyForkliftCommand { HallId = hall.Id });
            Do(new BuyForkliftCommand { HallId = hall.Id });
            Do(new PlaceBuildingCommand { Type = BuildingTypeId.exportExit, X = 69, Z = 124 });
            Do(new CreateOrderCommand { Product = ProductId.rawA, Quantity = 50, Interval = OrderInterval.daily });
            Do(new CreateOrderCommand { Product = ProductId.rawB, Quantity = 50, Interval = OrderInterval.daily });
            for (int i = 0; i < 4; i++) Do(new BuyVehicleCommand { Model = i % 2 == 0 ? VehicleModel.truck : VehicleModel.van });
            Stock.Add(s.State.Zones.Find(z => z.Kind == ZoneKind.C), ProductId.final, 40);
            s.State.Tick = GameTime.TicksPerHour * 7;
            s.Run(1500);
            return s.State;
        }
    }
}
