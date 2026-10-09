using System.Collections.Generic;
using System.IO;
using System.Linq;
using Logistikum.Sim;

namespace Logistikum.Game
{
    /// <summary>
    /// Spielstände als Dateien (ein Eintrag = eine .json-Datei). Schreiben über eine Temporärdatei, die erst
    /// am Ende das Original ersetzt: Ein Absturz mitten im Speichern lässt den alten Stand heil.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        readonly string dir;

        public FileSaveStorage(string dir)
        {
            this.dir = dir;
            Directory.CreateDirectory(dir);
        }

        public string Directory_ => dir;

        string PathOf(string key) => Path.Combine(dir, key + ".json");

        public IEnumerable<string> Keys() =>
            Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension).ToList();

        public string Read(string key)
        {
            var p = PathOf(key);
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        public void Write(string key, string text)
        {
            var target = PathOf(key);
            var tmp = target + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(target)) File.Replace(tmp, target, null);
            else File.Move(tmp, target);
        }

        public void Delete(string key)
        {
            var p = PathOf(key);
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
