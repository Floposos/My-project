using System;
using System.Collections;
using System.IO;
using Logistikum.Sim;
using UnityEngine;

namespace Logistikum.Game
{
    /// <summary>
    /// Selbsttest der fertigen Version: Start mit „-logistikumSelftest Ordner“ macht Bildschirmfotos von Menü
    /// und Spiel (Bauleiste, Infofenster, Lager-Fenster, offene Halle) und beendet das Spiel.
    /// </summary>
    public sealed class SelfTest : MonoBehaviour
    {
        string dir;

        public static void StartIfRequested(GameApp app)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-logistikumSelftest") continue;
                Application.runInBackground = true;
                var t = app.gameObject.AddComponent<SelfTest>();
                t.dir = args[i + 1];
                Directory.CreateDirectory(t.dir);
                t.StartCoroutine(t.Run(app));
            }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return new WaitForSecondsRealtime(0.5f);
        }

        IEnumerator Run(GameApp app)
        {
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("1-hauptmenue");
            app.StartGame(DemoCampus.Create());
            app.Session.SetSpeed(4);
            yield return Shot("2-spiel");
            var hall = app.State.Halls.Count > 0 ? app.State.Halls[0] : null;
            if (hall != null)
            {
                app.Cam.Focus(Coords.Center(hall.Rect), 28);
                app.Select(new Pick { Kind = PickKind.Hall, Id = hall.Id });
            }
            yield return Shot("3-halle-info");
            app.Windows.ToggleStock();
            app.SetTool(new Tool { Kind = ToolKind.Zone, Zone = ZoneKind.C });
            yield return Shot("4-lager-bauen");
            app.Windows.CloseSide();
            app.SetTool(null);
            var truck = app.State.Vehicles.Find(v => v is Truck);
            if (truck != null) app.ShowVehicle(truck.Id);
            yield return Shot("5-fahrzeug");
            app.Windows.OpenCash();
            yield return Shot("6-kasse");
            app.Dialogs.CloseAll();
            app.OpenPauseMenu();
            yield return Shot("7-pause");
            Application.Quit();
        }
    }
}
