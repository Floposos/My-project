using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Logistikum.EditorTools
{
    /// <summary>Spielbare Version bauen (Menü „Logistikum/macOS-Build“ oder Batch: -executeMethod … -buildPath Pfad).</summary>
    public static class BuildTools
    {
        [MenuItem("Logistikum/macOS-Build")]
        public static void BuildMacMenu() => BuildMac(Path.Combine(Application.dataPath, "../Builds/Logistikum.app"));

        public static void BuildMacBatch()
        {
            var args = Environment.GetCommandLineArgs();
            string path = Path.Combine(Application.dataPath, "../Builds/Logistikum.app");
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-buildPath") path = args[i + 1];
            EditorApplication.Exit(BuildMac(path) ? 0 : 1);
        }

        public static bool BuildMac(string path)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Logistikum/Scenes/Logistikum.unity" },
                locationPathName = path,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            Debug.Log("Build: " + report.summary.result + " → " + path);
            return report.summary.result == BuildResult.Succeeded;
        }
    }
}
