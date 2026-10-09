using System;
using System.Collections.Generic;
using System.IO;
using Logistikum.Sim;
using NUnit.Framework;

namespace Logistikum.Tests
{
    /// <summary>Testaufbau und Hilfen für Logiktests (laufen in Unity und mit Tools/SimTests).</summary>
    public static class TestKit
    {
        public static CommandResult Ok(Simulation s, Command c)
        {
            var r = s.Execute(c);
            Assert.IsTrue(r.Ok, c.Name + " abgelehnt: " + r.Reason);
            return r;
        }

        /// <summary>
        /// Straße von der Einfahrt nach Osten (z = 61, x 0–40), A nördlich, B südlich, C weiter östlich,
        /// Export-Ausfahrt am Südrand über eine Stichstraße (wie testWorld der Browser-Version).
        /// </summary>
        public static Simulation World()
        {
            var s = new Simulation(GameState.CreateInitial(42));
            Ok(s, new DemolishHallCommand { HallId = 1 });
            Ok(s, new BuildRoadCommand { FromX = 0, FromZ = 61, ToX = 40, ToZ = 61, XFirst = true });
            Ok(s, new PlaceZoneCommand { Kind = ZoneKind.A, FromX = 10, FromZ = 58, ToX = 12, ToZ = 60 });
            Ok(s, new PlaceZoneCommand { Kind = ZoneKind.B, FromX = 20, FromZ = 62, ToX = 22, ToZ = 64 });
            Ok(s, new PlaceZoneCommand { Kind = ZoneKind.C, FromX = 30, FromZ = 58, ToX = 32, ToZ = 60 });
            Ok(s, new BuildRoadCommand { FromX = 40, FromZ = 61, ToX = 40, ToZ = 123, XFirst = false });
            Ok(s, new PlaceBuildingCommand { Type = BuildingTypeId.exportExit, X = 39, Z = 124 });
            return s;
        }

        public static Zone ZoneOf(Simulation s, ZoneKind kind) => s.State.Zones.Find(z => z.Kind == kind);

        public static string Fixture(string name)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var path = Path.Combine(dir.FullName, "Assets", "Logistikum", "Tests", "Fixtures", name);
                    if (File.Exists(path)) return File.ReadAllText(path);
                    dir = dir.Parent;
                }
            }
            throw new FileNotFoundException(name);
        }

        public static List<TourStop> Stops(params (int site, TourAction action, ProductId product)[] stops)
        {
            var list = new List<TourStop>();
            foreach (var x in stops) list.Add(new TourStop { SiteId = x.site, Action = x.action, Product = x.product });
            return list;
        }
    }
}
