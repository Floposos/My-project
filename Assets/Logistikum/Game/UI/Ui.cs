using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Logistikum.Game
{
    /// <summary>Kleine Bausteine für die Oberfläche (UI Toolkit, aus Code gebaut).</summary>
    public static class Ui
    {
        public static VisualElement Div(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            if (classes.Length == 0) l.AddToClassList("lg-text");
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("lg-btn");
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        public static VisualElement Row(params VisualElement[] children)
        {
            var r = Div("lg-row");
            foreach (var c in children) r.Add(c);
            return r;
        }

        public static VisualElement Spacer()
        {
            var s = new VisualElement();
            s.style.flexGrow = 1;
            return s;
        }

        public static VisualElement Swatch(Color c)
        {
            var s = Div("lg-swatch");
            s.style.backgroundColor = c;
            return s;
        }

        /// <summary>Füllstandsbalken (rot, wenn voll).</summary>
        public static VisualElement Bar(float fill)
        {
            var bar = Div("lg-bar");
            var f = Div("lg-bar-fill");
            f.style.width = Length.Percent(Mathf.Clamp01(fill) * 100);
            if (fill >= 0.999f) f.AddToClassList("lg-full");
            bar.Add(f);
            return bar;
        }

        public static DropdownField Dropdown(string label, List<string> choices, int index, Action<int> onChange)
        {
            var d = new DropdownField(label, choices, Mathf.Clamp(index, 0, Math.Max(0, choices.Count - 1)));
            d.AddToClassList("lg-field");
            d.RegisterValueChangedCallback(e => onChange(choices.IndexOf(e.newValue)));
            return d;
        }

        public static ScrollView Scroll(float maxHeight = 0)
        {
            var s = new ScrollView(ScrollViewMode.Vertical);
            if (maxHeight > 0) s.style.maxHeight = maxHeight;
            return s;
        }
    }

    /// <summary>Wurzel der Oberfläche: UIDocument mit zur Laufzeit erzeugten PanelSettings und dem Stylesheet.</summary>
    public sealed class UiRoot
    {
        public readonly UIDocument Document;
        public readonly VisualElement Root;
        public readonly VisualElement Hud, Windows, Dialogs, Overlay;

        public UiRoot(Transform parent)
        {
            var go = new GameObject("Oberfläche");
            go.transform.SetParent(parent, false);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/LogistikumTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.match = 0.5f;
            settings.clearDepthStencil = true;
            Document = go.AddComponent<UIDocument>();
            Document.panelSettings = settings;
            Root = Document.rootVisualElement;
            Root.AddToClassList("root");
            var sheet = Resources.Load<StyleSheet>("UI/Logistikum");
            if (sheet != null) Root.styleSheets.Add(sheet);
            Root.pickingMode = PickingMode.Ignore;
            Hud = Layer("HUD");
            Windows = Layer("Fenster");
            Dialogs = Layer("Dialoge");
            Overlay = Layer("Overlay");
        }

        VisualElement Layer(string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
            Root.Add(e);
            return e;
        }

        /// <summary>Liegt die Maus über einem Bedienelement? (Dann keine Kamera- und Bau-Aktionen.)</summary>
        public bool PointerOverUi(Vector2 screen)
        {
            var panel = Root.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = panel.Pick(p);
            return picked != null && picked.pickingMode == PickingMode.Position && picked != Root;
        }

        public Vector2 WorldToPanel(Camera cam, Vector3 world, out bool visible)
        {
            var sp = cam.WorldToScreenPoint(world);
            visible = sp.z > 0;
            var panel = Root.panel;
            return panel == null ? Vector2.zero : RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, Screen.height - sp.y));
        }

        public Vector2 ScreenToPanel(Vector2 screen)
        {
            var panel = Root.panel;
            return panel == null ? screen : RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
        }
    }

    /// <summary>Modale Dialoge mit Hintergrund; Esc schließt den obersten.</summary>
    public sealed class Dialogs
    {
        readonly VisualElement layer;
        readonly List<VisualElement> stack = new List<VisualElement>();

        public Dialogs(VisualElement layer) { this.layer = layer; }

        public bool Open => stack.Count > 0;

        public VisualElement Show(string title, Action<VisualElement> fill, float width = 0)
        {
            var backdrop = Ui.Div("lg-backdrop");
            var dlg = Ui.Div("lg-panel", "lg-dialog");
            if (width > 0) dlg.style.width = width;
            var head = Ui.Row(Ui.Text(title, "lg-title"), Ui.Spacer(), Ui.Btn("✕", CloseTop));
            dlg.Add(head);
            var body = new VisualElement();
            dlg.Add(body);
            fill(body);
            backdrop.Add(dlg);
            layer.Add(backdrop);
            stack.Add(backdrop);
            return body;
        }

        public void CloseTop()
        {
            if (stack.Count == 0) return;
            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            top.RemoveFromHierarchy();
        }

        public void CloseAll() { while (stack.Count > 0) CloseTop(); }

        public void Confirm(string title, string message, string okText, Action onOk)
        {
            Show(title, body =>
            {
                body.Add(Ui.Text(message));
                var row = Ui.Row(Ui.Spacer(), Ui.Btn(T.Cancel, CloseTop), Ui.Btn(okText, () => { CloseTop(); onOk(); }, "lg-primary"));
                row.style.marginTop = 12;
                body.Add(row);
            }, 460);
        }

        public void Message(string title, string message) =>
            Show(title, body => { body.Add(Ui.Text(message)); body.Add(Ui.Row(Ui.Spacer(), Ui.Btn(T.Ok, CloseTop, "lg-primary"))); }, 460);
    }

    /// <summary>Kurze Meldungen oben in der Mitte, verschwinden von selbst.</summary>
    public sealed class Toasts
    {
        readonly VisualElement box;
        readonly List<(VisualElement e, float until)> items = new List<(VisualElement, float)>();

        public Toasts(VisualElement layer)
        {
            box = Ui.Div("lg-toasts");
            box.pickingMode = PickingMode.Ignore;
            layer.Add(box);
        }

        public void Show(string text, float seconds = 2.5f, string buttonText = null, Action onButton = null)
        {
            var t = Ui.Div("lg-toast", "lg-row");
            t.Add(Ui.Text(text, "lg-text"));
            if (buttonText != null) t.Add(Ui.Btn(buttonText, onButton));
            box.Add(t);
            items.Add((t, Time.unscaledTime + seconds));
            if (items.Count > 4) { items[0].e.RemoveFromHierarchy(); items.RemoveAt(0); }
        }

        public void Update()
        {
            for (int i = items.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > items[i].until) { items[i].e.RemoveFromHierarchy(); items.RemoveAt(i); }
        }
    }
}
