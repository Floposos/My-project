namespace Logistikum.Sim
{
    public enum BuildingTypeId { testHall, exportExit }

    /// <summary>Gebäudetyp mit fester Grundfläche. Lieferorte sind Zonen.</summary>
    public sealed class BuildingType
    {
        public BuildingTypeId Id;
        /// <summary>Grundfläche in Feldern (x = Breite, z = Tiefe).</summary>
        public int Width, Depth;
        /// <summary>Höhe in Feldbreiten (nur Darstellung).</summary>
        public float Height;
        /// <summary>Muss am Geländerand stehen (Export-Ausfahrt).</summary>
        public bool AtEdge;
        /// <summary>Braucht eine Zufahrt von der Straße.</summary>
        public bool NeedsAccess;
    }

    public static class BuildingTypes
    {
        public static readonly BuildingType TestHall = new BuildingType
        { Id = BuildingTypeId.testHall, Width = 8, Depth = 6, Height = 2.5f };

        /// <summary>Export-Ausfahrt: frei platzierbar am Geländerand (Entscheidung 07.10.2026).</summary>
        public static readonly BuildingType ExportExit = new BuildingType
        { Id = BuildingTypeId.exportExit, Width = 4, Depth = 4, Height = 1.6f, AtEdge = true, NeedsAccess = true };

        public static BuildingType Get(BuildingTypeId id) => id == BuildingTypeId.testHall ? TestHall : ExportExit;
    }
}
