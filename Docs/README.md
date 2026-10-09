# Logistikum (Unity)

3D-Logistik-Managementspiel. Nachbau der Browser-Version (`Floposos/Warehouse-Management`, 0.3.0) in **Unity 6** mit Modellen aus **Blender**, plus **M3 Produktion & Lager** (Version 0.4.0).

## Starten

1. Projekt im Unity Hub öffnen (Unity 6000.6.5f1).
2. Szene `Assets/Logistikum/Scenes/Logistikum.unity` öffnen und auf **Play** drücken.
3. Spielbare Mac-Version: Menü **Logistikum → macOS-Build** (landet in `Builds/Logistikum.app`).

Der Auto-Prototyp (`Assets/Scenes/SampleScene.unity`, SUV) bleibt unverändert erhalten.

## Steuerung

| Taste/Maus | Wirkung |
|---|---|
| Rechte Maustaste ziehen | drehen/neigen |
| Mittlere Maustaste ziehen, WASD, Bildschirmrand | verschieben |
| Mausrad, Q/E | zoomen |
| R/F · Y/X | neigen · drehen |
| Linke Maustaste | bauen bzw. auswählen |
| Leertaste · 1/2/3 | Pause · 1x/2x/4x |
| H | Hallendächer ein/aus |
| F3 | Leistungsanzeige |
| Esc | Werkzeug abbrechen → Fenster schließen → Menü |

## Aufbau

```
Assets/Logistikum/
├─ Sim/        Spiellogik, reines C# ohne Unity (asmdef noEngineReferences) – Port der TS-Simulation + M3
├─ Game/       Unity-Schicht: App (Ablauf, Speichern), Render (Ansichten), Input (Bauwerkzeuge), UI (UI Toolkit)
├─ Editor/     Vorschaubilder rendern, macOS-Build
├─ Tests/      NUnit-Tests (laufen im Unity Test Runner und mit Tools/SimTests)
├─ Resources/  Modelle (FBX aus Blender), Materialien, Stylesheet
└─ Scenes/     Logistikum.unity
Blender/Logistikum.blend   Quelle aller Logistikum-Modelle
Tools/SimTests             Logik-Tests ohne Unity: ./Tools/SimTests/run.sh
Tools/GameCheck            Compiler-Prüfung der Unity-Schicht ohne Editor
```

Schichtregel wie in der Browser-Version: `Sim` kennt keine Darstellung; Oberfläche und Eingabe ändern den Zustand nur über Befehle.

## Spielstände

- Speicherort: `~/Library/Application Support/DefaultCompany/Logistikum/Spielstaende` (Ordner über „Laden → Ordner öffnen“).
- Format identisch zur Browser-Version; **Spielstände der Browser-Version 0.3.0 lassen sich importieren** (Datei nach `Downloads` legen, dann „Laden → Datei importieren“). Ältere Browser-Stände (vor 0.3.0) nicht.
- Autosave im Echtzeit-Intervall mit 3 rotierenden Sicherungen.

## Tests

- `Tools/SimTests/run.sh` – 38 Tests, darunter ein Vergleich mit dem TypeScript-Original: 18.000 Schritte, Zustand Feld für Feld identisch.
- Unity: Window → General → Test Runner → EditMode → `Logistikum.Tests`.
