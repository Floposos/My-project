using System;
using System.Linq;
using Logistikum.Sim;
using NUnit.Framework;

namespace Logistikum.Tests
{
    public class SaveTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void BrowserSaveV4_LoadsAndMigrates()
        {
            var error = SaveFormat.Parse(TestKit.Fixture("v4-beispiel.json"), out var save);
            Assert.AreEqual(SaveError.none, error);
            Assert.AreEqual(SaveFormat.CurrentVersion, save.SaveVersion);
            Assert.AreEqual(1337, save.State.Tick);
            Assert.AreEqual(4, save.State.Vehicles.Count);
            Assert.IsInstanceOf<Truck>(save.State.Vehicles[0]);
            Assert.AreEqual(VehicleDrive.electric, ((Truck)save.State.Vehicles[1]).Drive);
            Assert.AreEqual("Frühschicht", save.State.Tours[0].Name);
            Assert.IsNotNull(save.State.Halls);
            // Weiterspielen funktioniert.
            var s = new Simulation(save.State);
            s.Run(3000);
        }

        [Test]
        public void SaveAndLoad_GivesIdenticalState()
        {
            var s = TestKit.World();
            TestKit.Ok(s, new BuyVehicleCommand());
            TestKit.Ok(s, new CreateOrderCommand { Product = ProductId.rawA, Quantity = 20, Interval = OrderInterval.weekly });
            s.Run(1234);
            var repo = new SaveRepository(new MemorySaveStorage(), "test");
            var key = repo.Save(s.State, "Mein Spiel", Now);
            Assert.AreEqual(SaveError.none, repo.Load(key, out var loaded));
            Assert.AreEqual(SaveFormat.ToJson(s.State), SaveFormat.ToJson(loaded.State));
            Assert.AreEqual("Mein Spiel", repo.List().Single().Meta.Name);
        }

        [Test]
        public void ContinuingAfterLoad_MatchesUninterruptedRun()
        {
            var a = TestKit.World();
            TestKit.Ok(a, new BuyVehicleCommand());
            TestKit.Ok(a, new CreateOrderCommand { Product = ProductId.rawA, Quantity = 20, Interval = OrderInterval.daily });
            a.Run(2000);
            SaveFormat.Parse(SaveFormat.Serialize(SaveFormat.Create(a.State, "x", "t", Now)), out var save);
            var b = new Simulation(save.State);
            a.Run(3000);
            b.Run(3000);
            Assert.AreEqual(SaveFormat.ToJson(a.State), SaveFormat.ToJson(b.State));
        }

        [TestCase("kein json", SaveError.notJson)]
        [TestCase("{\"format\":\"anderes\",\"saveVersion\":1}", SaveError.wrongFormat)]
        [TestCase("{\"format\":\"logistikum-save\",\"saveVersion\":99}", SaveError.tooNew)]
        [TestCase("{\"format\":\"logistikum-save\",\"saveVersion\":2}", SaveError.tooOld)]
        [TestCase("{\"format\":\"logistikum-save\",\"saveVersion\":\"5\"}", SaveError.invalidVersion)]
        [TestCase("{\"format\":\"logistikum-save\",\"saveVersion\":5,\"meta\":{\"name\":\"x\",\"tick\":0,\"balanceCents\":0},\"state\":{}}", SaveError.invalidState)]
        public void BrokenFiles_GiveReadableError(string text, SaveError expected)
        {
            Assert.AreEqual(expected, SaveFormat.Parse(text, out var save));
            Assert.IsNull(save);
        }

        [Test]
        public void FailedWrite_KeepsOldSave()
        {
            var storage = new MemorySaveStorage();
            var repo = new SaveRepository(storage, "test");
            var s = new Simulation(GameState.CreateInitial(3));
            var key = repo.Save(s.State, "Stand", Now);
            s.Run(500);
            storage.FailNextWrite = true;
            Assert.Throws<System.IO.IOException>(() => repo.Save(s.State, "Stand", Now));
            repo.Load(key, out var loaded);
            Assert.AreEqual(0, loaded.State.Tick);
        }

        [Test]
        public void Autosave_KeepsExactlyBackupCount()
        {
            var storage = new MemorySaveStorage();
            var repo = new SaveRepository(storage, "test");
            var s = new Simulation(GameState.CreateInitial(3));
            for (int i = 0; i < 6; i++)
            {
                s.Run(10);
                repo.Autosave(s.State, Now.AddMinutes(i));
            }
            var autos = repo.List().Where(e => e.IsAutosave).ToList();
            Assert.AreEqual(SaveConfig.BackupCount, autos.Count);
            repo.Load(autos[0].Key, out var newest);
            Assert.AreEqual(60, newest.State.Tick);
        }

        [Test]
        public void TestHallBuilding_BecomesHall_OnMigration()
        {
            var text = "{\"format\":\"logistikum-save\",\"saveVersion\":4,\"gameVersion\":\"0.3.0\",\"createdAt\":\"x\"," +
                "\"meta\":{\"name\":\"a\",\"tick\":0,\"balanceCents\":1}," + "\"state\":" +
                TestKit.Fixture("v4-beispiel.json").Split(new[] { "\"state\": " }, StringSplitOptions.None)[1].TrimEnd().TrimEnd('}') + "}";
            text = text.Replace("\"buildings\": [", "\"buildings\": [{\"id\":99,\"type\":\"testHall\",\"x\":12,\"z\":58,\"builtTick\":0,\"paidCents\":0},");
            Assert.AreEqual(SaveError.none, SaveFormat.Parse(text, out var save));
            Assert.AreEqual(1, save.State.Halls.Count);
            Assert.AreEqual(99, save.State.Halls[0].Id);
            Assert.IsFalse(save.State.Buildings.Exists(b => b.Type == BuildingTypeId.testHall));
        }
    }
}
