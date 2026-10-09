using System;
using System.Collections.Generic;

namespace Logistikum.Sim
{
    /// <summary>
    /// Ereignis-Bus: Systeme melden Ereignisse während eines Schritts mit Emit. Erst Flush am
    /// Schrittende verteilt sie in Meldereihenfolge. So sieht kein Zuhörer einen halben Schritt.
    /// </summary>
    public sealed class EventBus
    {
        List<SimEvent> pending = new List<SimEvent>();
        readonly Dictionary<Type, List<Action<SimEvent>>> listeners = new Dictionary<Type, List<Action<SimEvent>>>();
        readonly List<Action<SimEvent>> allListeners = new List<Action<SimEvent>>();

        public void Emit(SimEvent e) { pending.Add(e); }

        /// <summary>Meldet einen Zuhörer an; der Rückgabewert meldet ihn wieder ab.</summary>
        public Action On<T>(Action<T> listener) where T : SimEvent
        {
            if (!listeners.TryGetValue(typeof(T), out var list))
            {
                list = new List<Action<SimEvent>>();
                listeners[typeof(T)] = list;
            }
            Action<SimEvent> wrapped = e => listener((T)e);
            list.Add(wrapped);
            return () => list.Remove(wrapped);
        }

        public Action OnAny(Action<SimEvent> listener)
        {
            allListeners.Add(listener);
            return () => allListeners.Remove(listener);
        }

        /// <summary>Verteilt alle gesammelten Ereignisse; dabei gemeldete kommen danach dran.</summary>
        public void Flush()
        {
            while (pending.Count > 0)
            {
                var batch = pending;
                pending = new List<SimEvent>();
                foreach (var e in batch)
                {
                    if (listeners.TryGetValue(e.GetType(), out var list))
                        foreach (var l in list.ToArray()) l(e);
                    foreach (var l in allListeners.ToArray()) l(e);
                }
            }
        }

        public int PendingCount => pending.Count;
    }
}
