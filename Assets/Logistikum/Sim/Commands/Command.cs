namespace Logistikum.Sim
{
    /// <summary>Ergebnis eines Befehls; Id ist bei Befehlen gesetzt, die etwas Neues anlegen.</summary>
    public struct CommandResult
    {
        public bool Ok;
        public int? Id;
        public string Reason;

        public static CommandResult Success(int? id = null) => new CommandResult { Ok = true, Id = id };
        public static CommandResult Fail(string reason) => new CommandResult { Ok = false, Reason = reason };
    }

    /// <summary>
    /// Spieleraktion als Befehl. Darstellung, Oberfläche und Eingabe ändern den Zustand nie direkt,
    /// sondern reichen Befehle ein; die Simulation prüft und führt sie aus.
    /// </summary>
    public abstract class Command
    {
        /// <summary>Kurzname für Meldungen, z. B. "road/build".</summary>
        public abstract string Name { get; }
        /// <summary>Prüft und führt den Befehl aus. Abgelehnte Befehle ändern nichts.</summary>
        public abstract CommandResult Apply(GameState state, EventBus bus);
    }

    public static class Commands
    {
        public static CommandResult Run(GameState state, Command command, EventBus bus)
        {
            var result = command.Apply(state, bus);
            if (!result.Ok) bus.Emit(new CommandRejected { Command = command.Name, Reason = result.Reason });
            return result;
        }

        public static CommandResult FromReason(string reason, int? id = null) =>
            reason == null ? CommandResult.Success(id) : CommandResult.Fail(reason);
    }
}
