# Spieldesign – Unity-Version

Alle Entscheidungen der Browser-Version (DESIGN.md dort, Stand 08.10.2026) gelten unverändert.

## Neue Entscheidungen

| Datum | Thema | Entscheidung |
|---|---|---|
| 09.10.2026 | Plattform | Blender für Modelle, Unity als Engine (Florian) |
| 09.10.2026 | Auto-Prototyp | bleibt erhalten, Logistikum bekommt eigene Szene und Ordner |
| 09.10.2026 | M3-Fragen (fragenm3.md) | empfohlene Antworten übernommen, siehe unten |

## M3 Produktion & Lager (empfohlene Antworten)

1. Halle als Rechteck frei aufziehen, innen Bereiche einzeichnen.
2. Dach blendet sich beim Heranzoomen (unter 45 Felder Abstand) und per Taste H aus.
3. A, B, C bleiben Außenzonen; Hallen sind die Stufe nach C.
4. Stufen Lagern, Verpacken, Etikettieren, Qualitätsprüfung; jede macht die Ware wertvoller.
5. Gabelstapler einzeln sichtbar, je Halle gekauft, fahren selbst.
6. Förderbänder auf dem Raster wie Straßen, eine Ebene, Laufrichtung als Pfeil, keine Kreuzungen.
7. Kapazität je Bereich, je Ware getrennt (10 je Feld wie Zonen).
8. LKW halten am Hallentor, Stapler übernehmen drinnen.
9. Tempo einer Stufe wächst mit der Bereichsgröße.
10. Qualitätsprüfung sortiert Ausschuss aus.
11. Feste Reihenfolge, Stufen dürfen fehlen.
12. Fenster „Lager“ mit Bestand je Ware über alle Orte, Sprung zum Ort.

## Annahmen (bitte bestätigen oder ändern; Werte in `Sim/Config/HallConfig.cs`, `EconomyConfig.cs`)

- Halle 600 €/Feld, mindestens 3 × 3; Bereich einrichten 100 €/Feld; angrenzende Hallen verschmelzen nicht.
- Die Test-Halle aus M0 ist eine echte, leere Halle (Tor Süd); alte Spielstände werden umgestellt.
- Waren: Verpackt 145 €, Etikettiert 155 €, Geprüft 170 € (Endprodukt 130 €). „Ungeprüfte Ware bringt weniger“ ist über diese Preise abgebildet.
- Arbeit je Einheit (Feld-Schritte): Verpacken 360, Etikettieren 240, Prüfen 300; Ausschuss 5 %.
- Stapler 25.000 €, 80 €/Tag (Betrieb), Verkauf zum halben Preis, 2,5 Felder/s, trägt 4 Einheiten.
- Das Lager im Wareneingang wird nur übersprungen, wenn es voll ist; eine vorhandene, volle Stufe wird nicht übersprungen (Stapler warten).
- LKW-Automatik: Endprodukt lieber in den Wareneingang einer Halle als in den Export; fertige Hallenware hat Vorrang.
- Förderband 200 €/Feld, 2 Felder/s, Kisten mit halbem Feld Abstand; Quelle gibt ab: A Rohware A, B Kombi, C Endprodukt, Halle die am weitesten verarbeitete Ware im Warenausgang.
- Meldung „Lager voll“ einmal je Ort bzw. Hallenbereich, bis wieder Platz ist; Hinweis „Halle ohne Stapler“ einmal je Halle.
- Unity-Version: Sicherungsdatei (Chrome/Edge) und Export-Erinnerung entfallen, weil Spielstände ohnehin Dateien sind.
