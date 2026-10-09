using System;
using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>Einstellungen; gespeichert in PlayerPrefs, ungültige Werte fallen auf den Standard zurück.</summary>
    [Serializable]
    public sealed class Settings
    {
        public int autosaveMinutes = SettingsConfig.AutosaveMinutesDefault;
        public float cameraSensitivity = SettingsConfig.CameraSensitivityDefault;
        public bool edgeScroll = SettingsConfig.EdgeScrollDefault;

        const string Key = "logistikum.settings.v1";

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                var text = PlayerPrefs.GetString(Key, "");
                if (text.Length > 0) JsonUtility.FromJsonOverwrite(text, s);
            }
            catch (Exception) { s = new Settings(); }
            s.Sanitize();
            return s;
        }

        public void Sanitize()
        {
            if (Array.IndexOf(SettingsConfig.AutosaveMinutesOptions, autosaveMinutes) < 0) autosaveMinutes = SettingsConfig.AutosaveMinutesDefault;
            if (float.IsNaN(cameraSensitivity)) cameraSensitivity = SettingsConfig.CameraSensitivityDefault;
            cameraSensitivity = Mathf.Clamp(cameraSensitivity, SettingsConfig.CameraSensitivityMin, SettingsConfig.CameraSensitivityMax);
        }

        public void Save()
        {
            Sanitize();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
