using System.Collections.Generic;
using Logistikum.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Logistikum.Tests
{
    /// <summary>
    /// Vergleich mit der Browser-Version 0.3.0: dasselbe Szenario (Straßen, Zonen, Bestellungen, Kauf und
    /// Leasing, Tour, Werkstatt, Zonen-Zerfall, unterbrochene Straße) muss nach 18.000 Schritten exakt
    /// denselben Zustand ergeben. Der Referenzzustand stammt aus dem TypeScript-Original.
    /// </summary>
    public class ParityTests
    {
        static void Ok(Simulation s, Command c) => TestKit.Ok(s, c);

        [Test]
        public void Scenario_MatchesTypeScriptOriginal()
        {
            var s = new Simulation(GameState.CreateInitial(42));
            Ok(s, new DemolishHallCommand { HallId = 1 });
            Ok(s, new BuildRoadCommand { FromX = 0, FromZ = 61, ToX = 40, ToZ = 61, XFirst = true });
            Ok(s, new PlaceZoneCommand { Kind = ZoneKind.A, FromX = 10, FromZ = 58, ToX = 12, ToZ = 60 });
            Ok(s, new PlaceZoneCommand { Kind = ZoneKind.B, FromX = 20, FromZ = 62, ToX = 22, ToZ = 64 });
            Ok(s, new PlaceZoneCommand { Kind = ZoneKind.C, FromX = 30, FromZ = 58, ToX = 32, ToZ = 60 });
            Ok(s, new BuildRoadCommand { FromX = 40, FromZ = 61, ToX = 40, ToZ = 123, XFirst = false });
            Ok(s, new PlaceBuildingCommand { Type = BuildingTypeId.exportExit, X = 39, Z = 124 });
            Ok(s, new BuildRoadCommand { FromX = 25, FromZ = 61, ToX = 25, ToZ = 70, XFirst = false });
            Ok(s, new BuildRoadCommand { FromX = 25, FromZ = 70, ToX = 40, ToZ = 70, XFirst = true });
            Ok(s, new PlaceZoneCommand { Kind = ZoneKind.W, FromX = 26, FromZ = 71, ToX = 29, ToZ = 72 });
            Ok(s, new SetPriorityCommand { FromX = 0, FromZ = 61, ToX = 40, ToZ = 61, XFirst = true, Priority = true });
            Ok(s, new CreateOrderCommand { Product = ProductId.rawA, Quantity = 50, Interval = OrderInterval.daily });
            Ok(s, new CreateOrderCommand { Product = ProductId.rawB, Quantity = 50, Interval = OrderInterval.daily });
            Ok(s, new BuyVehicleCommand { Model = VehicleModel.truck, Drive = VehicleDrive.diesel });
            var van = s.Execute(new BuyVehicleCommand { Model = VehicleModel.van, Drive = VehicleDrive.electric, Lease = true });
            Ok(s, new BuyVehicleCommand { Model = VehicleModel.truck, Drive = VehicleDrive.electric });
            Ok(s, new BuyVehicleCommand { Model = VehicleModel.van, Drive = VehicleDrive.diesel });
            var tour = s.Execute(new CreateTourCommand
            {
                TourName = "Früh",
                Stops = TestKit.Stops((2, TourAction.load, ProductId.rawA), (3, TourAction.unload, ProductId.rawA)),
            });
            Ok(s, new AssignTourCommand { TruckId = van.Id.Value, TourId = tour.Id });
            s.Run(4000);
            Ok(s, new ServiceVehicleCommand { TruckId = 9 });
            Ok(s, new PlaceZoneCommand { Kind = ZoneKind.C, FromX = 33, FromZ = 58, ToX = 34, ToZ = 60 });
            Ok(s, new DemolishZoneCellCommand { ZoneId = 2, X = 11, Z = 58 });
            s.Run(5000);
            Ok(s, new DemolishRoadCommand { X = 40, Z = 100 });
            s.Run(3000);
            Ok(s, new BuildRoadCommand { FromX = 40, FromZ = 100, ToX = 40, ToZ = 100, XFirst = true });
            s.Run(6000);

            var actual = JObject.Parse(SaveFormat.ToJson(s.State));
            // Nur in Unity (M3): Hallen, Förderbänder, „Lager voll“-Liste.
            foreach (var key in new[] { "halls", "conveyors", "warnedFull" }) actual.Remove(key);
            var expected = JObject.Parse(TestKit.Fixture("ts-golden-0.3.0.json"));
            var diff = FirstDifference(expected, actual, "state");
            Assert.IsNull(diff, diff);
        }

        static string FirstDifference(JToken a, JToken b, string path)
        {
            if (a.Type == JTokenType.Object && b.Type == JTokenType.Object)
            {
                var ao = (JObject)a; var bo = (JObject)b;
                var keys = new HashSet<string>();
                foreach (var p in ao.Properties()) keys.Add(p.Name);
                foreach (var p in bo.Properties()) keys.Add(p.Name);
                foreach (var k in keys)
                {
                    if (ao[k] == null || bo[k] == null) return path + "." + k + ": fehlt " + (ao[k] == null ? "im Original" : "in Unity");
                    var d = FirstDifference(ao[k], bo[k], path + "." + k);
                    if (d != null) return d;
                }
                return null;
            }
            if (a.Type == JTokenType.Array && b.Type == JTokenType.Array)
            {
                var aa = (JArray)a; var ba = (JArray)b;
                if (aa.Count != ba.Count) return path + ": Länge " + aa.Count + " ≠ " + ba.Count;
                for (int i = 0; i < aa.Count; i++)
                {
                    var d = FirstDifference(aa[i], ba[i], path + "[" + i + "]");
                    if (d != null) return d;
                }
                return null;
            }
            return JToken.DeepEquals(a, b) ? null : path + ": " + a.ToString(Newtonsoft.Json.Formatting.None) + " ≠ " + b.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
