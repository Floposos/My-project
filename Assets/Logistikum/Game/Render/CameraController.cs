using Logistikum.Sim;
using UnityEngine;
using Grid = Logistikum.Sim.Grid;
using UnityEngine.InputSystem;

namespace Logistikum.Game
{
    /// <summary>
    /// Kamera im Aufbauspiel-Stil (Entscheidung 07.10.2026): rechte Taste drehen/neigen, mittlere Taste
    /// verschieben, Rad zoomen, WASD verschieben, Q/E zoomen, R/F neigen, Y/X drehen, Rand-Scrollen.
    /// Grenzen: Neigung 15–85°, Abstand 8–170, Blickpunkt bleibt auf dem Gelände.
    /// </summary>
    public sealed class CameraController
    {
        public readonly Camera Camera;
        public Vector3 Target = new Vector3(Grid.Width / 2f, 0, -Grid.Depth / 2f);
        public float Distance = CameraConfig.StartDistance, Yaw = CameraConfig.StartYawDeg, Pitch = CameraConfig.StartPitchDeg;
        public float Sensitivity = 1f;
        public bool EdgeScroll = true;

        public CameraController(Camera camera) { Camera = camera; Apply(); }

        public void Reset()
        {
            Target = new Vector3(20, 0, -WorldConfig.EntranceZ);
            Distance = CameraConfig.StartDistance; Yaw = CameraConfig.StartYawDeg; Pitch = CameraConfig.StartPitchDeg;
            Apply();
        }

        public void Focus(Vector3 world, float distance = 30f)
        {
            Target = new Vector3(world.x, 0, world.z);
            Distance = Mathf.Min(Distance, distance);
            Apply();
        }

        /// <summary>Hauptmenü: langsame Kreisfahrt über den Beispiel-Campus.</summary>
        public void Orbit(float dt)
        {
            Yaw += CameraConfig.MenuOrbitDegPerSecond * dt;
            Apply();
        }

        /// <summary>Eingaben eines Bilds; pointerOverUi sperrt Maus-Aktionen über der Oberfläche.</summary>
        public void HandleInput(float dt, bool pointerOverUi, bool keyboardFree)
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            float s = Sensitivity;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed)
                {
                    Yaw += delta.x * CameraConfig.RotateDegPerPixel * s;
                    Pitch -= delta.y * CameraConfig.TiltDegPerPixel * s;
                }
                if (mouse.middleButton.isPressed) PanScreen(-delta.x / Screen.height, -delta.y / Screen.height);
                float wheel = mouse.scroll.ReadValue().y;
                if (!pointerOverUi && Mathf.Abs(wheel) > 0.01f)
                {
                    float steps = Mathf.Clamp(wheel / 120f, -3f, 3f);
                    if (Mathf.Abs(steps) < 0.05f) steps = Mathf.Sign(wheel) * 0.25f;
                    Distance /= Mathf.Pow(CameraConfig.ZoomPerWheelStep, steps);
                }
                if (EdgeScroll && !pointerOverUi && Application.isFocused && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed)
                {
                    var p = mouse.position.ReadValue();
                    float e = CameraConfig.EdgeScrollPixels;
                    float ex = p.x <= e ? -1 : p.x >= Screen.width - e ? 1 : 0;
                    float ey = p.y <= e ? -1 : p.y >= Screen.height - e ? 1 : 0;
                    if (p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height && (ex != 0 || ey != 0))
                        PanScreen(ex * CameraConfig.PanScreensPerSecond * dt * s, ey * CameraConfig.PanScreensPerSecond * dt * s);
                }
            }
            if (kb != null && keyboardFree)
            {
                float px = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                float py = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                if (px != 0 || py != 0) PanScreen(px * CameraConfig.PanScreensPerSecond * dt * s, py * CameraConfig.PanScreensPerSecond * dt * s);
                if (kb.eKey.isPressed) Distance /= Mathf.Pow(CameraConfig.KeyZoomPerSecond, dt);
                if (kb.qKey.isPressed) Distance *= Mathf.Pow(CameraConfig.KeyZoomPerSecond, dt);
                if (kb.rKey.isPressed) Pitch += CameraConfig.KeyTiltDegPerSecond * dt;
                if (kb.fKey.isPressed) Pitch -= CameraConfig.KeyTiltDegPerSecond * dt;
                // Deutsche Tastatur: Y/X drehen (Input System benennt Tasten nach US-Layout: Y = zKey).
                if (KeyPressed(kb, "y")) Yaw -= CameraConfig.KeyRotateDegPerSecond * dt;
                if (KeyPressed(kb, "x")) Yaw += CameraConfig.KeyRotateDegPerSecond * dt;
            }
            Apply();
        }

        static bool KeyPressed(Keyboard kb, string displayName)
        {
            var key = kb.FindKeyOnCurrentKeyboardLayout(displayName);
            return key != null && key.isPressed;
        }

        /// <summary>Verschiebt um Bildschirmhöhen (x nach rechts, y nach oben im Bild).</summary>
        void PanScreen(float sx, float sy)
        {
            float worldPerScreen = 2f * Distance * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var right = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
            var forward = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            Target += (right * sx + forward * sy) * worldPerScreen;
        }

        public void Apply()
        {
            Pitch = Mathf.Clamp(Pitch, CameraConfig.MinPitchDeg, CameraConfig.MaxPitchDeg);
            Distance = Mathf.Clamp(Distance, CameraConfig.MinDistance, CameraConfig.MaxDistance);
            Target.x = Mathf.Clamp(Target.x, 0, Grid.Width);
            Target.z = Mathf.Clamp(Target.z, -Grid.Depth, 0);
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            Camera.transform.position = Target - rot * Vector3.forward * Distance;
            Camera.transform.rotation = rot;
        }

        /// <summary>Bodenpunkt unter einer Bildschirmposition (Ebene y = 0); null = Himmel.</summary>
        public Vector3? GroundPoint(Vector2 screen)
        {
            var ray = Camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return null;
            float t = -ray.origin.y / ray.direction.y;
            return t > 0 ? ray.origin + ray.direction * t : (Vector3?)null;
        }
    }
}
