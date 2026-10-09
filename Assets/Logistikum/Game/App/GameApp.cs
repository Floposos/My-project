using System;
using Logistikum.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Logistikum.Game
{
    /// <summary>
    /// Einstieg: baut Kamera, Licht, Welt und Oberfläche auf, schaltet zwischen Hauptmenü (Kamera kreist über
    /// den Beispiel-Campus) und Spiel um und verteilt Eingaben auf Kamera, Bauwerkzeuge und Auswahl.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        [NonSerialized] public GameSession Session;
        [NonSerialized] public WorldRenderer World;
        [NonSerialized] public CameraController Cam;
        [NonSerialized] public UiRoot Ui;
        [NonSerialized] public Dialogs Dialogs;
        [NonSerialized] public Toasts Toasts;
        [NonSerialized] public WindowManager Windows;
        [NonSerialized] public SaveController Saves;
        [NonSerialized] public Settings Settings;
        TopBar topBar;
        BuildBar buildBar;
        Overlays overlays;
        InfoPanel info;
        MainMenu mainMenu;

        public bool InGame { get; private set; }
        public Tool Tool { get; private set; }
        /// <summary>„Orte anklicken“ für diese Tour (null = aus).</summary>
        [NonSerialized] public int? PickStopsFor;
        public GameState State => Session.State;

        Cell dragStart;
        bool dragging, pressedOnWorld, pausedByMenu;
        Vector2 pressPos;

        /// <summary>Startet das Spiel automatisch in der Szene „Logistikum“.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name != "Logistikum" || FindAnyObjectByType<GameApp>() != null) return;
            new GameObject("Logistikum").AddComponent<GameApp>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Settings = Settings.Load();
            SetupCameraAndLight();
            Session = new GameSession(DemoCampus.Create());
            World = new WorldRenderer();
            World.Root.SetParent(transform, false);
            World.Hook(Session.Sim.Bus);
            Ui = new UiRoot(transform);
            Dialogs = new Dialogs(Ui.Dialogs);
            Toasts = new Toasts(Ui.Overlay);
            Windows = new WindowManager(this);
            Windows.Build(Ui.Windows);
            info = new InfoPanel(this, Ui.Windows);
            topBar = new TopBar(this, Ui.Hud);
            buildBar = new BuildBar(this, Ui.Hud);
            overlays = new Overlays(this, Ui.Overlay);
            mainMenu = new MainMenu(this, Ui.Hud);
            Saves = new SaveController(this);
            Session.Sim.Bus.On<Booked>(e => { if (InGame && e.At.HasValue) overlays.Amount(e.AmountCents, e.At.Value); });
            ApplySettings();
            ShowMainMenu();
            SelfTest.StartIfRequested(this);
        }

        void SetupCameraAndLight()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Kamera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Hex(0xcfe6f5);
            cam.fieldOfView = 45;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 900;
            Cam = new CameraController(cam);

            Light sun = null;
            foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) sun = l;
            if (sun == null) sun = new GameObject("Sonne").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            sun.intensity = 1.0f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.6f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.45f, 0.4f);
            RenderSettings.fog = false;
            RenderSettings.reflectionIntensity = 0.2f;
        }

        public void ApplySettings()
        {
            Cam.Sensitivity = Settings.cameraSensitivity;
            Cam.EdgeScroll = Settings.edgeScroll;
        }

        // ---------------- Spielablauf ----------------

        public void ShowMainMenu()
        {
            InGame = false;
            Dialogs.CloseAll();
            Windows.CloseSide();
            SetTool(null);
            Select(Pick.None);
            Session.Replace(DemoCampus.Create());
            World.Clear();
            Session.SetSpeed(1);
            overlays.ClearAmounts();
            SetHud(false);
            Cam.Target = new Vector3(40, 0, -66);
            Cam.Distance = 75; Cam.Pitch = 38;
        }

        public void NewGame() => StartGame(GameState.CreateInitial((uint)Environment.TickCount));

        public void StartGame(GameState state)
        {
            Dialogs.CloseAll();
            Windows.CloseSide();
            SetTool(null);
            Select(Pick.None);
            Session.Replace(state);
            World.Clear();
            InGame = true;
            pausedByMenu = false;
            Session.SetSpeed(1);
            overlays.ClearAmounts();
            SetHud(true);
            Cam.Reset();
            Saves.ResetTimer(state.Tick);
        }

        void SetHud(bool inGame)
        {
            mainMenu.SetVisible(!inGame);
            topBar.Root.style.display = inGame ? DisplayStyle.Flex : DisplayStyle.None;
            buildBar.Root.style.display = inGame ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void OpenPauseMenu()
        {
            if (!Session.Paused) { Session.SetSpeed(0); pausedByMenu = true; }
            Windows.OpenPause();
        }

        // ---------------- Befehle und Auswahl ----------------

        /// <summary>Führt eine Spieleraktion aus; Ablehnungen erscheinen als kurze Meldung.</summary>
        public CommandResult Run(Command c)
        {
            var r = Session.Execute(c);
            if (!r.Ok) Toasts.Show(ReasonText(r.Reason), 3f);
            if (c is SetZoneGateCommand || c is SetHallGateCommand) World.MarkAll();
            info.Refresh();
            return r;
        }

        public static string ReasonText(string reason)
        {
            if (Enum.TryParse<BuildRejection>(reason, out var br)) return T.Reason(br);
            switch (reason)
            {
                case "insufficientFunds": return T.NoMoney;
                case "noWorkshop": return T.NoWorkshop;
                case "notFound": return "Nicht mehr vorhanden.";
                default: return T.TourRejected(reason);
            }
        }

        public void BuyVehicle(VehicleModel model, VehicleDrive drive, bool lease)
        {
            var r = Run(new BuyVehicleCommand { Model = model, Drive = drive, Lease = lease });
            if (r.Ok) Toasts.Show(T.Model(model) + " " + T.Drive(drive) + " angeschafft, fährt an der Einfahrt los.");
        }

        public void Select(Pick p)
        {
            info?.Show(p);
            int? vehicle = p.Kind == PickKind.Vehicle ? p.Id : (int?)null;
            World.Vehicles.Selected = vehicle;
            World.Routes.Selected = vehicle;
        }

        public void ShowVehicle(int id)
        {
            var v = State.VehicleById(id);
            if (v == null) return;
            Cam.Focus(VehiclePose.WorldPosition(v, 0, out _), 30);
            Select(new Pick { Kind = PickKind.Vehicle, Id = id });
        }

        public void SetTool(Tool t)
        {
            Tool = t;
            dragging = false;
            World?.Ghost.Hide();
            overlays?.SetTip(null, false, Vector2.zero);
            buildBar?.OnToolChanged();
        }

        // ---------------- Bild für Bild ----------------

        bool Typing()
        {
            var focused = Ui.Root.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            var kb = Keyboard.current;
            bool typing = Typing();
            if (kb != null && !typing) Shortcuts(kb);
            if (pausedByMenu && !Dialogs.Open) { pausedByMenu = false; Session.SetSpeed(Session.LastSpeed); }

            Session.Advance(dt);
            var mouse = Mouse.current;
            var pos = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            bool overUi = Ui.PointerOverUi(pos) || Dialogs.Open;
            if (InGame)
            {
                Cam.HandleInput(dt, overUi, !typing && !Dialogs.Open);
                if (!Dialogs.Open) WorldInput(mouse, pos, overUi);
                else { World.Ghost.Hide(); overlays.SetTip(null, false, pos); overlays.SetHover(null, pos); }
            }
            else Cam.Orbit(dt);

            World.Sync(State, Session.Alpha, Cam, dt);
            if (InGame)
            {
                topBar.Update();
                info.Update(dt);
                Windows.Update(dt);
            }
            overlays.Update(dt);
            Toasts.Update();
            Saves.Update(dt);
        }

        void Shortcuts(Keyboard kb)
        {
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (PickStopsFor != null) { PickStopsFor = null; Windows.ToggleTours(); Windows.ToggleTours(); }
                else if (Tool != null) SetTool(null);
                else if (Dialogs.Open) Dialogs.CloseTop();
                else if (Windows.AnySideOpen) Windows.CloseSide();
                else if (info.Current.Kind != PickKind.None) Select(Pick.None);
                else if (InGame) OpenPauseMenu();
                return;
            }
            if (!InGame || Dialogs.Open) return;
            if (kb.spaceKey.wasPressedThisFrame) Session.TogglePause();
            if (kb.digit1Key.wasPressedThisFrame) Session.SetSpeed(1);
            if (kb.digit2Key.wasPressedThisFrame) Session.SetSpeed(2);
            if (kb.digit3Key.wasPressedThisFrame) Session.SetSpeed(4);
            if (kb.f3Key.wasPressedThisFrame) overlays.ShowPerf = !overlays.ShowPerf;
            if (kb.hKey.wasPressedThisFrame) World.Halls.RoofsForcedOff = !World.Halls.RoofsForcedOff;
        }

        void WorldInput(Mouse mouse, Vector2 pos, bool overUi)
        {
            if (mouse == null) return;
            var ground = Cam.GroundPoint(pos);
            if (Tool != null)
            {
                overlays.SetHover(null, pos);
                if (ground == null || (overUi && !dragging)) { World.Ghost.Hide(); overlays.SetTip(null, false, pos); return; }
                var cell = Coords.CellAt(ground.Value);
                if (mouse.leftButton.wasPressedThisFrame && !overUi)
                {
                    dragStart = cell;
                    dragging = Tool.Drags;
                    if (!Tool.Drags)
                    {
                        var p0 = BuildTool.Evaluate(State, Tool, cell, cell, false);
                        if (p0.Ok && p0.Command != null) Run(p0.Command); else Toasts.Show(p0.Tip, 2.5f);
                    }
                }
                var preview = BuildTool.Evaluate(State, Tool, dragStart, cell, dragging);
                if (preview.Rect != null) World.Ghost.Rect(preview.Rect, preview.Ok, Tool.Kind == ToolKind.Hall ? 1.2f : 0.25f);
                else World.Ghost.Cells(preview.Cells, preview.Ok, preview.Blocked);
                overlays.SetTip(preview.Tip, !preview.Ok, pos);
                if (dragging && mouse.leftButton.wasReleasedThisFrame)
                {
                    dragging = false;
                    if (preview.Ok && preview.Command != null) Run(preview.Command);
                    else if (!string.IsNullOrEmpty(preview.Tip)) Toasts.Show(preview.Tip, 2.5f);
                }
                return;
            }
            // Auswahl: Klick ohne Ziehen; Name beim Überfahren.
            var hoverPick = ground.HasValue && !overUi ? Picking.At(State, ground.Value, Session.Alpha) : Pick.None;
            overlays.SetHover(hoverPick.Kind == PickKind.None ? null : PickName(hoverPick) + " · Klicken für Details", pos);
            if (mouse.leftButton.wasPressedThisFrame) { pressedOnWorld = !overUi; pressPos = pos; }
            if (mouse.leftButton.wasReleasedThisFrame && pressedOnWorld && (pos - pressPos).sqrMagnitude < 36)
            {
                pressedOnWorld = false;
                if (PickStopsFor != null)
                {
                    if (hoverPick.Kind == PickKind.Zone || hoverPick.Kind == PickKind.Hall || hoverPick.Kind == PickKind.Building)
                        Windows.AppendStopAt(PickStopsFor.Value, hoverPick.Id);
                    return;
                }
                Select(hoverPick);
            }
        }

        string PickName(Pick p)
        {
            switch (p.Kind)
            {
                case PickKind.Vehicle: return Names.VehicleById(State, p.Id);
                case PickKind.Conveyor: return T.Conveyor;
                default: return Names.Site(State, p.Id);
            }
        }
    }
}
