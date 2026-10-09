using System.Linq;
using Logistikum.Sim;
using NUnit.Framework;

namespace Logistikum.Tests
{
    public class HallTests
    {
        /// <summary>Testwelt plus Halle östlich der Stichstraße (x 41–50, z 64–71), Tor im Westen.</summary>
        static (Simulation s, Hall hall) WorldWithHall(bool stages = true)
        {
            var s = TestKit.World();
            var r = TestKit.Ok(s, new PlaceHallCommand { FromX = 41, FromZ = 64, ToX = 50, ToZ = 71 });
            var hall = s.State.HallById(r.Id.Value);
            Assert.AreEqual(Side.W, hall.Gate);
            TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.inbound, FromX = 41, FromZ = 64, ToX = 42, ToZ = 67 });
            TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.outbound, FromX = 41, FromZ = 68, ToX = 42, ToZ = 71 });
            if (stages)
            {
                TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.packing, FromX = 45, FromZ = 64, ToX = 47, ToZ = 66 });
                TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.labeling, FromX = 45, FromZ = 68, ToX = 47, ToZ = 70 });
            }
            return (s, hall);
        }

        static int Count(Hall h, AreaKind kind, ProductId p) => h.Areas.Where(a => a.Kind == kind).Sum(a => Stock.Of(a.Stock, p));

        [Test]
        public void Hall_CostsPerField_AndRefundsOnSameDay()
        {
            var s = TestKit.World();
            long before = s.State.Finance.BalanceCents;
            var r = TestKit.Ok(s, new PlaceHallCommand { FromX = 60, FromZ = 20, ToX = 69, ToZ = 27 });
            Assert.AreEqual(before - 80 * HallConfig.CostPerFieldCents, s.State.Finance.BalanceCents);
            TestKit.Ok(s, new DemolishHallCommand { HallId = r.Id.Value });
            Assert.AreEqual(before, s.State.Finance.BalanceCents);
        }

        [Test]
        public void Hall_TooSmallOrOccupied_IsRejected()
        {
            var s = TestKit.World();
            Assert.AreEqual("tooSmall", s.Execute(new PlaceHallCommand { FromX = 60, FromZ = 20, ToX = 61, ToZ = 25 }).Reason);
            Assert.AreEqual("occupied", s.Execute(new PlaceHallCommand { FromX = 0, FromZ = 60, ToX = 5, ToZ = 65 }).Reason);
        }

        [Test]
        public void Areas_MustBeInsideHall_InboundAtGate_NoOverlap()
        {
            var (s, _) = WorldWithHall(false);
            Assert.AreEqual("notAtGate", s.Execute(new PlaceAreaCommand { Kind = AreaKind.inbound, FromX = 48, FromZ = 64, ToX = 49, ToZ = 65 }).Reason);
            Assert.AreEqual("notInHall", s.Execute(new PlaceAreaCommand { Kind = AreaKind.storage, FromX = 60, FromZ = 64, ToX = 61, ToZ = 65 }).Reason);
            Assert.AreEqual("occupied", s.Execute(new PlaceAreaCommand { Kind = AreaKind.storage, FromX = 42, FromZ = 65, ToX = 44, ToZ = 66 }).Reason);
            TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.storage, FromX = 48, FromZ = 64, ToX = 49, ToZ = 65 });
        }

        [Test]
        public void WithoutForklifts_GoodsStayInInbound_WithNotice()
        {
            var (s, hall) = WorldWithHall();
            Stock.Add(hall.FirstArea(AreaKind.inbound).Stock, ProductId.final, 10);
            s.Run(500);
            Assert.AreEqual(10, Count(hall, AreaKind.inbound, ProductId.final));
            Assert.IsTrue(s.State.Notices.Exists(n => n.Kind == NoticeKind.noForklift));
        }

        [Test]
        public void Forklifts_CarryGoodsThroughStages_ToOutbound()
        {
            var (s, hall) = WorldWithHall();
            TestKit.Ok(s, new BuyForkliftCommand { HallId = hall.Id });
            TestKit.Ok(s, new BuyForkliftCommand { HallId = hall.Id });
            Stock.Add(hall.FirstArea(AreaKind.inbound).Stock, ProductId.final, 20);
            s.Run(4000);
            Assert.AreEqual(20, Count(hall, AreaKind.outbound, ProductId.labeled));
            Assert.AreEqual(0, Count(hall, AreaKind.inbound, ProductId.final));
        }

        [Test]
        public void MissingStages_AreSkipped()
        {
            var (s, hall) = WorldWithHall(false);
            TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.labeling, FromX = 45, FromZ = 68, ToX = 47, ToZ = 70 });
            TestKit.Ok(s, new BuyForkliftCommand { HallId = hall.Id });
            Stock.Add(hall.FirstArea(AreaKind.inbound).Stock, ProductId.final, 8);
            s.Run(3000);
            Assert.AreEqual(8, Count(hall, AreaKind.outbound, ProductId.labeled));
        }

        [Test]
        public void StageSpeed_GrowsWithAreaSize()
        {
            int Produced(int size)
            {
                var (s, hall) = WorldWithHall(false);
                TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.packing, FromX = 45, FromZ = 64, ToX = 44 + size, ToZ = 63 + size });
                var packing = hall.FirstArea(AreaKind.packing);
                Stock.Add(packing.Stock, ProductId.final, 30);
                s.Run(400);
                return Stock.Of(packing.Stock, ProductId.packed);
            }
            Assert.Greater(Produced(4), Produced(2));
        }

        [Test]
        public void Inspection_ScrapsSmallShare_Deterministically()
        {
            int Checked()
            {
                var (s, hall) = WorldWithHall(false);
                TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.inspection, FromX = 44, FromZ = 64, ToX = 50, ToZ = 71 });
                var area = hall.FirstArea(AreaKind.inspection);
                Stock.Add(area.Stock, ProductId.labeled, 200);
                s.Run(3000);
                Assert.AreEqual(0, Stock.Of(area.Stock, ProductId.labeled));
                return Stock.Of(area.Stock, ProductId.@checked);
            }
            int a = Checked();
            Assert.AreEqual(a, Checked());
            Assert.That(a, Is.InRange(170, 199));
        }

        [Test]
        public void Trucks_BringFinalToHall_AndHallGoodsToExport()
        {
            var (s, hall) = WorldWithHall();
            TestKit.Ok(s, new BuyForkliftCommand { HallId = hall.Id });
            TestKit.Ok(s, new BuyForkliftCommand { HallId = hall.Id });
            TestKit.Ok(s, new BuyVehicleCommand());
            TestKit.Ok(s, new BuyVehicleCommand());
            Stock.Add(TestKit.ZoneOf(s, ZoneKind.C), ProductId.final, 40);
            long sold = 0;
            int labeledSold = 0;
            s.Bus.On<GoodsSold>(e => { sold += e.RevenueCents; if (e.Product == ProductId.labeled) labeledSold += e.Quantity; });
            s.Run(12_000);
            Assert.Greater(labeledSold, 0, "etikettierte Ware wurde exportiert");
            Assert.Greater(sold, 0);
        }

        [Test]
        public void ProcessedGoods_SellForMore()
        {
            Assert.Greater(Export.Price(ProductId.packed), Export.Price(ProductId.final));
            Assert.Greater(Export.Price(ProductId.labeled), Export.Price(ProductId.packed));
            Assert.Greater(Export.Price(ProductId.@checked), Export.Price(ProductId.labeled));
        }

        [Test]
        public void Conveyor_RunsGoodsFromCToHall_AndStallsWhenFull()
        {
            var s = TestKit.World();
            var hall = s.State.HallById(TestKit.Ok(s, new PlaceHallCommand { FromX = 30, FromZ = 48, ToX = 37, ToZ = 54 }).Id.Value);
            Assert.AreEqual(Side.S, hall.Gate);
            TestKit.Ok(s, new PlaceAreaCommand { Kind = AreaKind.inbound, FromX = 30, FromZ = 54, ToX = 30, ToZ = 54 });
            var r = TestKit.Ok(s, new BuildConveyorCommand { FromX = 31, FromZ = 57, ToX = 31, ToZ = 55, XFirst = false });
            var belt = s.State.Conveyors.Single();
            Assert.AreEqual(TestKit.ZoneOf(s, ZoneKind.C).Id, belt.FromSiteId);
            Assert.AreEqual(hall.Id, belt.ToSiteId);
            Stock.Add(TestKit.ZoneOf(s, ZoneKind.C), ProductId.final, 15);
            s.Run(400);
            Assert.AreEqual(10, Stock.Of(hall.FirstArea(AreaKind.inbound).Stock, ProductId.final));
            Assert.IsTrue(Conveyors.IsStalled(belt));
            Assert.Greater(belt.Items.Count, 1);
            Assert.AreEqual(r.Id, belt.Id);
        }

        [Test]
        public void Conveyor_NeedsSourceAndTarget()
        {
            var s = TestKit.World();
            Assert.AreEqual("noSource", s.Execute(new BuildConveyorCommand { FromX = 70, FromZ = 10, ToX = 75, ToZ = 10, XFirst = true }).Reason);
            Assert.AreEqual("noTarget", s.Execute(new BuildConveyorCommand { FromX = 31, FromZ = 57, ToX = 31, ToZ = 50, XFirst = false }).Reason);
        }

        [Test]
        public void StorageFull_NoticeOncePerPlace()
        {
            var s = TestKit.World();
            var a = TestKit.ZoneOf(s, ZoneKind.A);
            Stock.Add(a, ProductId.rawA, Stock.ZoneCapacity(a));
            s.Run(100);
            Assert.AreEqual(1, s.State.Notices.Count(n => n.Kind == NoticeKind.storageFull));
            Stock.Add(a, ProductId.rawA, -5);
            s.Run(20);
            Stock.Add(a, ProductId.rawA, 5);
            s.Run(20);
            Assert.AreEqual(1, s.State.Notices.Count(n => n.Kind == NoticeKind.storageFull), "zusammengefasst in der Nähe");
            Assert.Contains(a.Id, s.State.WarnedFull);
        }

        [Test]
        public void HallSave_RoundTrips()
        {
            var (s, hall) = WorldWithHall();
            TestKit.Ok(s, new BuyForkliftCommand { HallId = hall.Id });
            Stock.Add(hall.FirstArea(AreaKind.inbound).Stock, ProductId.final, 12);
            s.Run(700);
            var text = SaveFormat.Serialize(SaveFormat.Create(s.State, "h", "t", System.DateTime.UtcNow));
            Assert.AreEqual(SaveError.none, SaveFormat.Parse(text, out var save));
            var b = new Simulation(save.State);
            s.Run(1500);
            b.Run(1500);
            Assert.AreEqual(SaveFormat.ToJson(s.State), SaveFormat.ToJson(b.State));
        }
    }
}
