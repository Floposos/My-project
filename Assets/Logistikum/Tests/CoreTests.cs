using System.Collections.Generic;
using Logistikum.Sim;
using NUnit.Framework;

namespace Logistikum.Tests
{
    public class CoreTests
    {
        [Test]
        public void Rng_MatchesBrowserVersion()
        {
            var rng = new RngState(42);
            // Werte aus der Browser-Version (Mulberry32, Seed 42).
            Assert.AreEqual(0.6011037519201636, rng.NextFloat(), 1e-15);
            Assert.AreEqual(0.44829055899754167, rng.NextFloat(), 1e-15);
            Assert.AreEqual(0.8524657934904099, rng.NextFloat(), 1e-15);
        }

        [Test]
        public void Calendar_StartsJanFirst2000_AndCountsDays()
        {
            var c = GameTime.CalendarAt(0);
            Assert.AreEqual((2000, 1, 1, 0, 0), (c.Year, c.Month, c.Day, c.Hour, c.Minute));
            Assert.AreEqual(6, c.Weekday); // Samstag
            var noon = GameTime.CalendarAt(GameTime.TicksPerDay / 2);
            Assert.AreEqual(12, noon.Hour);
            var feb1 = GameTime.CalendarAt(GameTime.TicksPerDay * 31);
            Assert.AreEqual((2, 1), (feb1.Month, feb1.Day));
        }

        [Test]
        public void AddOneMonth_ClampsToMonthEnd()
        {
            int jan31 = GameTime.TicksPerDay * 30;
            var c = GameTime.CalendarAt(GameTime.AddOneMonth(jan31));
            Assert.AreEqual((2, 29), (c.Month, c.Day)); // 2000 ist ein Schaltjahr
        }

        [Test]
        public void SameSeedAndCommands_GiveSameState()
        {
            string Run()
            {
                var s = TestKit.World();
                TestKit.Ok(s, new CreateOrderCommand { Product = ProductId.rawA, Quantity = 20, Interval = OrderInterval.daily });
                TestKit.Ok(s, new BuyVehicleCommand());
                s.Run(10_000);
                return SaveFormat.ToJson(s.State);
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void StepClock_FourTimesSpeed_EqualsFourSecondsAtOne()
        {
            var a = new StepClock();
            var b = new StepClock();
            int ta = 0, tb = 0;
            for (int i = 0; i < 60; i++) ta += a.Advance(1000.0 / 60, 4);
            for (int i = 0; i < 240; i++) tb += b.Advance(1000.0 / 60, 1);
            Assert.AreEqual(40, ta);
            Assert.AreEqual(ta, tb);
            Assert.AreEqual(0, new StepClock().Advance(5000, 0));
        }

        [Test]
        public void StepClock_CapsTicksAfterStall()
        {
            Assert.AreEqual(TimeConfig.MaxTicksPerFrame, new StepClock().Advance(60_000, 4));
        }

        [Test]
        public void Calendar_EmitsDayMonthYearEvents()
        {
            var s = new Simulation(GameState.CreateInitial(1));
            var seen = new List<string>();
            s.Bus.On<DayStarted>(e => seen.Add("day"));
            s.Bus.On<MonthStarted>(e => seen.Add("month"));
            s.Run(GameTime.TicksPerDay * 31);
            Assert.AreEqual(31, seen.FindAll(x => x == "day").Count);
            Assert.AreEqual(1, seen.FindAll(x => x == "month").Count);
        }

        [Test]
        public void EventBus_DeliversOnlyOnFlush_InOrder()
        {
            var bus = new EventBus();
            var got = new List<int>();
            bus.On<DayStarted>(e => got.Add(e.Day));
            bus.Emit(new DayStarted { Day = 1 });
            bus.Emit(new DayStarted { Day = 2 });
            Assert.AreEqual(0, got.Count);
            bus.Flush();
            CollectionAssert.AreEqual(new[] { 1, 2 }, got);
        }

        [Test]
        public void Json_RoundTrip_IsIdentical()
        {
            var s = TestKit.World();
            TestKit.Ok(s, new BuyVehicleCommand { Model = VehicleModel.van, Drive = VehicleDrive.electric, Lease = true });
            s.Run(500);
            var json = SaveFormat.ToJson(s.State);
            Assert.AreEqual(json, SaveFormat.ToJson(SaveFormat.Clone(s.State)));
        }

        [Test]
        public void Ledger_BooksCategoriesAndPeriods()
        {
            var s = new Simulation(GameState.CreateInitial(1));
            long start = s.State.Finance.BalanceCents;
            TestKit.Ok(s, new BuildRoadCommand { FromX = 0, FromZ = 61, ToX = 9, ToZ = 61, XFirst = true });
            Assert.AreEqual(start - 10 * BuildConfig.RoadCostPerTileCents, s.State.Finance.BalanceCents);
            var today = Ledger.Current(s.State.Finance, s.State.Tick, true);
            Assert.AreEqual(10 * BuildConfig.RoadCostPerTileCents, today.Expense(BookingCategory.build));
            s.Run(GameTime.TicksPerDay);
            Assert.AreEqual(0, Ledger.Current(s.State.Finance, s.State.Tick, true).Expense(BookingCategory.build));
        }
    }
}
