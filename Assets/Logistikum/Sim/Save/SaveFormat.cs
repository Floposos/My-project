using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Logistikum.Sim
{
    /// <summary>Kurzinfos für Listen, ohne den ganzen Zustand lesen zu müssen.</summary>
    public sealed class SaveMeta
    {
        public string Name;
        public int Tick;
        public long BalanceCents;
    }

    /// <summary>Spielstand. Format wie in der Browser-Version (dort bis Version 4), hier ab Version 5 mit Hallen.</summary>
    public sealed class SaveFile
    {
        public string Format = SaveFormat.FormatName;
        public int SaveVersion = SaveFormat.CurrentVersion;
        public string GameVersion;
        /// <summary>ISO-Zeitstempel (Echtzeit) des Speicherns.</summary>
        public string CreatedAt;
        public SaveMeta Meta;
        public GameState State;
    }

    public enum SaveError { none, notJson, wrongFormat, tooOld, tooNew, invalidVersion, migrationFailed, invalidState }

    public static class SaveFormat
    {
        public const string FormatName = "logistikum-save";
        /// <summary>Steigt bei jeder Änderung am Zustandsmodell; dazu Migration + Beispiel-Spielstand.</summary>
        public const int CurrentVersion = 5;
        /// <summary>Älteste ladbare Version: 4 = Browser-Version 0.3.0 (Spielstände von dort importierbar).</summary>
        public const int OldestVersion = 4;

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            Converters = new List<JsonConverter> { new StringEnumConverter(), new VehicleConverter() },
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            FloatParseHandling = FloatParseHandling.Double,
        };

        static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        public static string ToJson(object value, bool indented = false) =>
            JsonConvert.SerializeObject(value, indented ? Formatting.Indented : Formatting.None, Settings);

        /// <summary>Tiefe Kopie des Zustands (spätere Änderungen am Spiel wirken nicht hinein).</summary>
        public static GameState Clone(GameState state) => JsonConvert.DeserializeObject<GameState>(ToJson(state), Settings);

        public static SaveFile Create(GameState state, string name, string gameVersion, DateTime nowUtc)
        {
            var copy = Clone(state);
            return new SaveFile
            {
                GameVersion = gameVersion,
                CreatedAt = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Meta = new SaveMeta { Name = name, Tick = copy.Tick, BalanceCents = copy.Finance.BalanceCents },
                State = copy,
            };
        }

        /// <summary>Wandelt einen Spielstand in Text und prüft ihn sofort durch Zurücklesen.</summary>
        public static string Serialize(SaveFile save, bool indented = false)
        {
            var text = ToJson(save, indented);
            var error = Parse(text, out _);
            if (error != SaveError.none) throw new InvalidOperationException("Spielstand ungültig: " + error);
            return text;
        }

        /// <summary>Liest einen Spielstand, führt nötige Migrationen aus und prüft das Ergebnis.</summary>
        public static SaveError Parse(string text, out SaveFile save)
        {
            save = null;
            JObject obj;
            try { obj = JObject.Parse(text); }
            catch (Exception) { return SaveError.notJson; }
            if ((string)obj["format"] != FormatName) return SaveError.wrongFormat;
            var v = obj["saveVersion"];
            if (v == null || v.Type != JTokenType.Integer || (int)v < 1) return SaveError.invalidVersion;
            int version = (int)v;
            if (version > CurrentVersion) return SaveError.tooNew;
            if (version < OldestVersion) return SaveError.tooOld;
            try { Migrations.Run(obj, version); }
            catch (Exception) { return SaveError.migrationFailed; }
            try { save = obj.ToObject<SaveFile>(Serializer); }
            catch (Exception) { return SaveError.invalidState; }
            if (!Validate.IsValid(save)) { save = null; return SaveError.invalidState; }
            return SaveError.none;
        }

        /// <summary>Liest nur die Kurzinfos (für Listen); null = unlesbar.</summary>
        public static SaveMeta ReadMeta(string text)
        {
            try { return JObject.Parse(text)["meta"]?.ToObject<SaveMeta>(Serializer); }
            catch (Exception) { return null; }
        }
    }

    /// <summary>Liest Fahrzeuge anhand von "kind" als Zulieferer oder LKW.</summary>
    public sealed class VehicleConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Vehicle);
        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var obj = JObject.Load(reader);
            var kind = (string)obj["kind"];
            Vehicle v = kind == "supplier" ? new Supplier() : kind == "truck" ? (Vehicle)new Truck() : throw new JsonSerializationException("kind");
            serializer.Populate(obj.CreateReader(), v);
            return v;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();
    }

    /// <summary>Migrationen: eine Methode pro Versionssprung, nacheinander angewendet.</summary>
    public static class Migrations
    {
        public static void Run(JObject save, int from)
        {
            for (int v = from; v < SaveFormat.CurrentVersion; v++)
            {
                if (v == 4) V4ToV5(save);
                save["saveVersion"] = v + 1;
            }
        }

        /// <summary>
        /// 4 → 5 (M3): Hallen, Förderbänder, „Lager voll“-Liste; die Test-Halle (Gebäude) wird eine echte, leere
        /// Halle mit Tor im Süden (ANNAHME).
        /// </summary>
        static void V4ToV5(JObject save)
        {
            var state = (JObject)save["state"];
            var halls = new JArray();
            var buildings = (JArray)state["buildings"];
            foreach (var b in buildings.ToArray())
            {
                if ((string)b["type"] != "testHall") continue;
                halls.Add(new JObject
                {
                    ["id"] = b["id"], ["x"] = b["x"], ["z"] = b["z"], ["width"] = 8, ["depth"] = 6, ["gate"] = "S",
                    ["builtTick"] = b["builtTick"], ["paidCents"] = b["paidCents"],
                    ["areas"] = new JArray(), ["forklifts"] = new JArray(), ["warnedNoForklift"] = false,
                });
                b.Remove();
            }
            state["halls"] = halls;
            state["conveyors"] = new JArray();
            state["warnedFull"] = new JArray();
        }
    }

    /// <summary>Prüft die Form des geladenen Zustands (gegen kaputte oder fremde Dateien).</summary>
    public static class Validate
    {
        public static bool IsValid(SaveFile s)
        {
            if (s?.Meta == null || s.Meta.Name == null) return false;
            var st = s.State;
            if (st == null || st.Rng == null || st.EventRng == null || st.Finance == null || st.Entrance == null) return false;
            if (st.Finance.Recent == null || st.Finance.Today == null || st.Finance.Month == null) return false;
            if (st.Buildings == null || st.Roads == null || st.Zones == null || st.Orders == null || st.Vehicles == null) return false;
            if (st.Tours == null || st.Notices == null || st.Halls == null || st.Conveyors == null || st.WarnedFull == null) return false;
            if (st.Tick < 0 || st.NextId < 1) return false;
            foreach (var z in st.Zones) if (z.Parts == null || z.Parts.Count == 0 || z.Stock == null) return false;
            foreach (var v in st.Vehicles) if (v == null || v.Route == null || v.Route.Count == 0) return false;
            foreach (var v in st.Vehicles) if (v is Truck t && t.Upkeep == null) return false;
            foreach (var h in st.Halls)
            {
                if (h.Areas == null || h.Forklifts == null || h.Width < 1 || h.Depth < 1) return false;
                foreach (var a in h.Areas) if (a.Rect == null || a.Stock == null) return false;
                foreach (var f in h.Forklifts) if (f.Route == null || f.Route.Count == 0) return false;
            }
            foreach (var c in st.Conveyors) if (c.Cells == null || c.Cells.Count == 0 || c.Items == null) return false;
            return true;
        }
    }
}
