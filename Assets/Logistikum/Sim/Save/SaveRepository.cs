using System;
using System.Collections.Generic;
using System.Linq;

namespace Logistikum.Sim
{
    /// <summary>Ablage für Spielstände (Dateien im Spiel, Arbeitsspeicher in Tests).</summary>
    public interface ISaveStorage
    {
        IEnumerable<string> Keys();
        string Read(string key);
        /// <summary>Schreibt atomar: entweder ganz oder gar nicht (alter Stand bleibt sonst heil).</summary>
        void Write(string key, string text);
        void Delete(string key);
    }

    public sealed class MemorySaveStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        /// <summary>Für Tests: lässt den nächsten Schreibvorgang mitten im Vorgang scheitern.</summary>
        public bool FailNextWrite;

        public IEnumerable<string> Keys() => Data.Keys.ToList();
        public string Read(string key) => Data.TryGetValue(key, out var t) ? t : null;
        public void Write(string key, string text)
        {
            if (FailNextWrite) { FailNextWrite = false; throw new System.IO.IOException("Simulierter Fehler"); }
            Data[key] = text;
        }
        public void Delete(string key) { Data.Remove(key); }
    }

    public sealed class SaveEntry
    {
        public string Key;
        public SaveMeta Meta;
        public string CreatedAt;
        public bool IsAutosave;
    }

    /// <summary>
    /// Beliebig viele benannte Spielstände (Entscheidung 07.10.2026) und rotierende Autosave-Backups:
    /// neues zuerst schreiben, dann das älteste löschen.
    /// </summary>
    public sealed class SaveRepository
    {
        public const string SlotPrefix = "slot-";
        public const string AutoPrefix = "auto-";
        readonly ISaveStorage storage;
        readonly string gameVersion;

        public SaveRepository(ISaveStorage storage, string gameVersion)
        {
            this.storage = storage;
            this.gameVersion = gameVersion;
        }

        /// <summary>Dateiname aus dem Spielstand-Namen (nur sichere Zeichen).</summary>
        public static string KeyFor(string name)
        {
            var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray();
            var safe = new string(chars);
            return SlotPrefix + (safe.Length == 0 ? "spielstand" : safe);
        }

        public List<SaveEntry> List()
        {
            var list = new List<SaveEntry>();
            foreach (var key in storage.Keys())
            {
                if (!key.StartsWith(SlotPrefix) && !key.StartsWith(AutoPrefix)) continue;
                var text = storage.Read(key);
                var meta = text == null ? null : SaveFormat.ReadMeta(text);
                if (meta == null) continue;
                string created = null;
                try { created = (string)Newtonsoft.Json.Linq.JObject.Parse(text)["createdAt"]; } catch (Exception) { }
                list.Add(new SaveEntry { Key = key, Meta = meta, CreatedAt = created, IsAutosave = key.StartsWith(AutoPrefix) });
            }
            return list.OrderByDescending(e => e.CreatedAt).ToList();
        }

        public bool Exists(string name) => storage.Read(KeyFor(name)) != null;

        public string Save(GameState state, string name, DateTime nowUtc)
        {
            var text = SaveFormat.Serialize(SaveFormat.Create(state, name, gameVersion, nowUtc));
            var key = KeyFor(name);
            storage.Write(key, text);
            return key;
        }

        /// <summary>Autosave: neues Backup schreiben, dann über BackupCount hinaus die ältesten löschen.</summary>
        public string Autosave(GameState state, DateTime nowUtc, int backupCount = SaveConfig.BackupCount)
        {
            var key = AutoPrefix + nowUtc.ToString("yyyyMMdd-HHmmss-fff");
            var text = SaveFormat.Serialize(SaveFormat.Create(state, "Autosave", gameVersion, nowUtc));
            storage.Write(key, text);
            var autos = storage.Keys().Where(k => k.StartsWith(AutoPrefix)).OrderByDescending(k => k, StringComparer.Ordinal).ToList();
            foreach (var old in autos.Skip(backupCount)) storage.Delete(old);
            return key;
        }

        public SaveError Load(string key, out SaveFile save)
        {
            save = null;
            var text = storage.Read(key);
            if (text == null) return SaveError.notJson;
            return SaveFormat.Parse(text, out save);
        }

        public void Delete(string key) => storage.Delete(key);

        public string ExportText(GameState state, string name, DateTime nowUtc) =>
            SaveFormat.Serialize(SaveFormat.Create(state, name, gameVersion, nowUtc), true);
    }
}
