using System;
using System.IO;
using Logistikum.Game;
using Logistikum.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Logistikum.EditorTools
{
    /// <summary>
    /// Rendert den Beispiel-Campus ohne Play-Modus in Bilder (Menü „Logistikum/Vorschaubilder“ oder im
    /// Batch-Modus: -executeMethod Logistikum.EditorTools.Snapshot.RenderBatch -snapshotDir Pfad).
    /// </summary>
    public static class Snapshot
    {
        [MenuItem("Logistikum/Vorschaubilder rendern")]
        public static void RenderMenu() => Render(Path.Combine(Application.dataPath, "../Screenshots/Logistikum"));

        public static void RenderBatch()
        {
            var args = Environment.GetCommandLineArgs();
            string dir = Path.Combine(Application.dataPath, "../Screenshots/Logistikum");
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-snapshotDir") dir = args[i + 1];
            try { Render(dir); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Render(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/Logistikum/Scenes/Logistikum.unity");
            var camGo = new GameObject("Kamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Hex(0xcfe6f5);
            cam.fieldOfView = 45;
            cam.farClipPlane = 900;
            var sun = new GameObject("Sonne").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.6f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.45f, 0.4f);
            RenderSettings.fog = false;
            RenderSettings.reflectionIntensity = 0.2f;

            var state = DemoCampus.Create();
            var world = new WorldRenderer();
            var rig = new CameraController(cam);
            float alpha = 0.3f;

            void Shot(string name, Vector3 target, float distance, float pitch, float yaw, bool roofs = true)
            {
                rig.Target = target; rig.Distance = distance; rig.Pitch = pitch; rig.Yaw = yaw;
                rig.Apply();
                world.Halls.RoofsForcedOff = !roofs;
                world.Sync(state, alpha, rig, 1f);
                var rt = new RenderTexture(1600, 900, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                RenderTexture.active = null;
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }

            Shot("00-aufwaermen", new Vector3(40, 0, -70), 95, 50, 45);
            Shot("01-uebersicht", new Vector3(40, 0, -70), 95, 50, 45);
            Shot("02-halle-mit-dach", new Vector3(67, 0, -70), 50, 50, 30);
            Shot("03-halle-offen", new Vector3(67, 0, -70), 26, 60, 30, false);
            var truck = state.Vehicles.Find(v => v is Truck);
            var tp = truck != null ? VehiclePose.WorldPosition(truck, alpha, out _) : new Vector3(20, 0, -61);
            Shot("04-fahrzeuge", tp, 9, 35, 20);
            Shot("05-einfahrt", new Vector3(4, 0, -61.5f), 22, 40, 60);
            Shot("06-zonen", new Vector3(20, 0, -60), 30, 55, 0);
            Debug.Log("Vorschaubilder gespeichert in " + dir);
        }
    }
}
