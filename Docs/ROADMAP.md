# Roadmap (Unity)

| Meilenstein | Version | Status |
|---|---|---|
| M0 Grundgerüst | 0.1.0 | fertig (Unity-Port) |
| M1 Bauen & erster Warenfluss | 0.2.0 | fertig (Unity-Port) |
| M2 Flotte & Verkehr | 0.3.0 | fertig (Unity-Port, Logik identisch zum Original) |
| M3 Produktion & Lager | 0.4.0 | im Test |
| M4 Export | 0.5.0 | geplant |
| M5 Aufträge & Finanzen | 0.6.0 | geplant |
| M6 Personal | 0.7.0 | geplant |
| M7 Firmenpolitik & Ereignisse | 0.8.0 | geplant |
| M8 Züge, Kühl-/Gefahrgut, Produktkatalog | 0.9.0 | geplant |
| M9 Spielmodi & Feinschliff | 0.10.0 | geplant |

## M3 (planm3.md)

| Aufgabe | Stand |
|---|---|
| T3.1 Hallen bauen | Rechteck, Mindestgröße, Kosten je Feld, Tor-Seite, Dach ausblendbar, Abriss, Test-Halle umgestellt, Spielstand v5 |
| T3.2 Hallenbereiche | sechs Bereichsarten, Farbe und Schild, Eingang/Ausgang am Tor, LKW-Automatik kennt Hallen |
| T3.3 Verarbeitungsstufen | verpackt/etikettiert/geprüft, feste Reihenfolge, Tempo nach Fläche, Ausschuss deterministisch |
| T3.4 Gabelstapler | je Halle kaufen/verkaufen, holen und bringen selbst, ein Stapler je Feld, Ausweichen |
| T3.5 Förderbänder | ziehen wie Straßen, Pfeile, Kisten sichtbar, volles Ziel staut |
| T3.6 Fenster „Lager“ | Summen je Ware, je Ort, Füllstand, Sprung zum Ort, Meldung „Lager voll“ |
| T3.7 Leistung | Lasttest 300 Fahrzeuge, 20 Hallen, 100 Stapler, 520 Bandfelder: 0,5 ms (.NET) bzw. 2,8 ms (Unity-Editor) je Schritt |
| T3.8 Migration und Abnahme | v4 → v5 mit Test, Browser-Spielstände 0.3.0 ladbar |
