using System.Diagnostics;
using Logistikum.Sim;
using NUnit.Framework;

namespace Logistikum.Tests
{
    /// <summary>
    /// Lasttest (M2 T2.9 / M3 T3.7): 300 Fahrzeuge, 20 Hallen mit je 5 Staplern, Förderbänder, laufender
    /// Warenfluss. Ziel: Simulationsschritt im Mittel unter 4 ms.
    /// </summary>
    public class LoadTests
    {
        public static Simulation BigWorld()
        {
            var s = TestKit.World();
            TestKit.Ok(s, new AdjustBalanceCommand { DeltaCents = 10_000_000_000 });
            TestKit.Ok(s, new CreateOrderCommand { Product = ProductId.rawA, Quantity = 100, Interval = OrderInterval.daily });
            TestKit.Ok(s, new CreateOrderCommand { Product = ProductId.rawB, Quantity = 100, Interval = OrderInterval.daily });
            for (int i = 0; i < 10; i++)
            {
                int z = 4 + i * 11;
                var left = TestKit.Ok(s, new PlaceHallCommand { FromX = 50, FromZ = z, ToX = 57, ToZ = z + 5 }).Id.Value;
                var right = TestKit.Ok(s, new PlaceHallCommand { FromX = 110, FromZ = z, ToX = 117, ToZ = z + 5 }).Id.Value;
                foreach (var (id, x0) in new[] { (left, 50), (right, 110) })
                {
                    var hall = s.State.HallById(id);
                    hall.Gate = Side.E;
                    TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.inbound, FromX = x0 + 6, FromZ = z, ToX = x0 + 7, ToZ = z + 2 });
                    TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.outbound, FromX = x0 + 6, FromZ = z + 3, ToX = x0 + 7, ToZ = z + 5 });
                    TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.packing, FromX = x0, FromZ = z, ToX = x0 + 2, ToZ = z + 2 });
                    TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.labeling, FromX = x0, FromZ = z + 3, ToX = x0 + 2, ToZ = z + 5 });
                    for (int k = 0; k < 5; k++) TestKit.Ok(s, new BuyForkliftCommand { HallId = id });
                    Stock.Add(hall.FirstArea(AreaKind.inbound).Stock, ProductId.final, 50);
                }
                // Band vom Warenausgang der linken zur rechten Halle (52 Felder).
                TestKit.Ok(s, new BuildConveyorCommand { FromX = 58, FromZ = z + 4, ToX = 109, ToZ = z + 4, XFirst = true });
            }
            for (int i = 0; i < 300; i++)
                TestKit.Ok(s, new BuyVehicleCommand { Model = i % 2 == 0 ? VehicleModel.truck : VehicleModel.van });
            return s;
        }

        [Test, Category("Load")]
        public void SimulationStep_StaysUnderFourMilliseconds()
        {
            var s = BigWorld();
            s.Run(1500);
            var watch = Stopwatch.StartNew();
            const int steps = 1000;
            s.Run(steps);
            double ms = watch.Elapsed.TotalMilliseconds / steps;
            TestContext.Out.WriteLine("Mittlere Schrittzeit: " + ms.ToString("0.000") + " ms, Fahrzeuge: " + s.State.Vehicles.Count);
            Assert.Less(ms, 4.0);
        }
    }
}
