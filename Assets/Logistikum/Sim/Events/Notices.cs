using System;

namespace Logistikum.Sim
{
    /// <summary>Arten von Meldungen; Texte in der Oberfläche.</summary>
    public enum NoticeKind { jam, breakdown, noWorkshop, leaseRenewed, storageFull, noForklift }

    /// <summary>Meldung mit Ort (Sprung dorthin) und optional dem betroffenen Fahrzeug.</summary>
    public sealed class Notice
    {
        public int Id;
        public int Tick;
        public NoticeKind Kind;
        public int X, Z;
        public int? VehicleId;
    }

    public static class Notices
    {
        /// <summary>
        /// Meldung aufnehmen. Dieselbe Art in der Nähe kurz hintereinander wird zusammengefasst.
        /// Liefert die Meldung oder null, wenn sie zusammengefasst wurde.
        /// </summary>
        public static Notice Add(GameState state, EventBus bus, NoticeKind kind, int x, int z, int? vehicleId)
        {
            foreach (var n in state.Notices)
            {
                if (n.Kind == kind && state.Tick - n.Tick < EventsConfig.NoticeMergeTicks &&
                    Math.Abs(n.X - x) + Math.Abs(n.Z - z) <= EventsConfig.NoticeMergeDistance) return null;
            }
            var notice = new Notice { Id = state.NextId++, Tick = state.Tick, Kind = kind, X = x, Z = z, VehicleId = vehicleId };
            state.Notices.Add(notice);
            if (state.Notices.Count > EventsConfig.MaxNotices) state.Notices.RemoveRange(0, state.Notices.Count - EventsConfig.MaxNotices);
            bus.Emit(new NoticeAdded { Notice = notice });
            return notice;
        }
    }
}
