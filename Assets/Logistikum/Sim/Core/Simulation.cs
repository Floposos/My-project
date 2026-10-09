using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>Ein Spielsystem: bearbeitet pro Schritt seinen Teil des Zustands.</summary>
    public interface ISimSystem
    {
        void Update(GameState state, EventBus bus);
    }

    /// <summary>
    /// Hält den Spielzustand und rechnet ihn Schritt für Schritt weiter. Ablauf je Schritt:
    /// eingereichte Befehle → Zähler erhöhen → Systeme in fester Reihenfolge → Ereignisse verteilen.
    /// </summary>
    public sealed class Simulation
    {
        public readonly EventBus Bus = new EventBus();
        public GameState State;
        readonly IReadOnlyList<ISimSystem> systems;
        List<Command> queue = new List<Command>();

        public Simulation(GameState state, IReadOnlyList<ISimSystem> systems = null)
        {
            State = state;
            this.systems = systems ?? Systems.Default;
        }

        /// <summary>Reicht einen Befehl ein; er wird im nächsten Schritt geprüft und ausgeführt.</summary>
        public void Submit(Command command) { queue.Add(command); }

        /// <summary>Führt einen Befehl sofort zwischen zwei Schritten aus (Spieleraktionen, auch in der Pause).</summary>
        public CommandResult Execute(Command command)
        {
            var result = Commands.Run(State, command, Bus);
            Bus.Flush();
            return result;
        }

        public void Step()
        {
            var commands = queue;
            queue = new List<Command>();
            foreach (var c in commands) Commands.Run(State, c, Bus);
            State.Tick += 1;
            foreach (var s in systems) s.Update(State, Bus);
            Bus.Flush();
        }

        public void Run(int ticks) { for (int i = 0; i < ticks; i++) Step(); }

        /// <summary>Ersetzt den Zustand (Laden eines Spielstands); offene Befehle verfallen.</summary>
        public void ReplaceState(GameState state)
        {
            State = state;
            queue = new List<Command>();
        }
    }
}
