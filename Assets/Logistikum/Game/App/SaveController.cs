using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>
    /// Speichern, Laden, Export und Import als Dateien sowie Autosave im Echtzeit-Intervall (Entscheidung
    /// 07.10.2026: alle 5 Minuten, änderbar). Autosave nur bei Fortschritt, mit rotierenden Sicherungen.
    /// ANNAHME (Unity-Version): Die Browser-Sicherungsdatei und die Export-Erinnerung entfallen, weil die
    /// Spielstände ohnehin als Dateien auf dem Rechner liegen.
    /// </summary>
    public sealed class SaveController
    {
        readonly GameApp app;
        public readonly SaveRepository Repo;
        public readonly string SaveDir, ExportDir;
        float realSeconds;
        int lastSavedTick = -1;

        public SaveController(GameApp app)
        {
            this.app = app;
            SaveDir = Path.Combine(Application.persistentDataPath, "Spielstaende");
            ExportDir = Path.Combine(Application.persistentDataPath, "Exporte");
            Directory.CreateDirectory(ExportDir);
            Repo = new SaveRepository(new FileSaveStorage(SaveDir), Application.version);
        }

        public static string GameVersion => Application.version;

        public void ResetTimer(int tick)
        {
            realSeconds = 0;
            lastSavedTick = tick;
        }

        /// <summary>Echtzeit im laufenden Spiel zählen; im Intervall automatisch sichern.</summary>
        public void Update(float dt)
        {
            if (!app.InGame) return;
            realSeconds += dt;
            if (realSeconds < app.Settings.autosaveMinutes * 60f) return;
            realSeconds = 0;
            if (app.State.Tick == lastSavedTick) return;
            Autosave();
        }

        public void Autosave()
        {
            try
            {
                Repo.Autosave(app.State, DateTime.UtcNow);
                lastSavedTick = app.State.Tick;
                app.Toasts.Show(T.Autosaved, SaveConfig.SavedToastSeconds);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Autosave fehlgeschlagen: " + e.Message);
                app.Toasts.Show(T.AutosaveFailed, 4f);
            }
        }

        public bool Save(string name)
        {
            try
            {
                Repo.Save(app.State, name, DateTime.UtcNow);
                lastSavedTick = app.State.Tick;
                app.Toasts.Show(T.Saved, SaveConfig.SavedToastSeconds);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Speichern fehlgeschlagen: " + e.Message);
                app.Dialogs.Message(T.SaveTitle, T.SaveFailed);
                return false;
            }
        }

        public void LoadKey(string key)
        {
            var error = Repo.Load(key, out var save);
            Apply(error, save);
        }

        public void LoadFile(string path)
        {
            SaveError error;
            SaveFile save = null;
            try { error = SaveFormat.Parse(File.ReadAllText(path), out save); }
            catch (Exception) { error = SaveError.notJson; }
            Apply(error, save);
        }

        void Apply(SaveError error, SaveFile save)
        {
            if (error != SaveError.none)
            {
                app.Dialogs.Message(T.ErrorTitle, T.SaveError(error));
                return;
            }
            app.StartGame(save.State);
            app.Toasts.Show(T.Loaded);
        }

        public string Export()
        {
            var name = "logistikum-" + DateTime.Now.ToString("yyyy-MM-dd-HHmm") + ".json";
            var path = Path.Combine(ExportDir, name);
            File.WriteAllText(path, Repo.ExportText(app.State, T.DefaultSaveName(Fmt.GameDate(app.State.Tick)), DateTime.UtcNow));
            app.Toasts.Show(T.Exported(path), 5f, T.OpenFolder, () => OpenFolder(ExportDir));
            return path;
        }

        /// <summary>Spielstand-Dateien zum Importieren: Ordner „Exporte“ und „Downloads“ (auch aus der Browser-Version).</summary>
        public List<(string path, SaveMeta meta)> ImportCandidates()
        {
            var dirs = new List<string> { ExportDir };
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) dirs.Add(Path.Combine(home, "Downloads"));
            var list = new List<(string, SaveMeta)>();
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                foreach (var f in Directory.GetFiles(d, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(40))
                {
                    try
                    {
                        if (new FileInfo(f).Length > 20_000_000) continue;
                        var text = File.ReadAllText(f);
                        if (!text.Contains("logistikum-save")) continue;
                        var meta = SaveFormat.ReadMeta(text);
                        if (meta != null) list.Add((f, meta));
                    }
                    catch (Exception) { }
                }
            }
            return list;
        }

        public static void OpenFolder(string dir) => Application.OpenURL("file://" + dir);
    }
}
