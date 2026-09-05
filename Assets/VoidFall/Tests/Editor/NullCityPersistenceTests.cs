using System;
using System.IO;
using NUnit.Framework;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Tests.Editor
{
    public sealed class NullCityPersistenceTests
    {
        [Test]
        public void City_discoveries_round_trip_without_losing_legacy_discovery()
        {
            var path = Path.Combine(Path.GetTempPath(), "voidfall-null-city-save-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var profile = SaveStore.CreateDefault();
                foreach (var definition in NullCityContent.Enemies)
                {
                    var entry = Array.Find(profile.bestiary, e => e.id == definition.Id);
                    Assert.That(entry, Is.Not.Null, definition.Id);
                    entry.discovered = true;
                }
                var boss = Array.Find(profile.bestiary, e => e.id == NullCityContent.MotherloadId);
                Assert.That(boss, Is.Not.Null);
                boss.discovered = true;
                Array.Find(profile.bestiary, e => e.id == "chaser").discovered = true;
                var store = new SaveStore(path);
                store.Save(profile);
                var loaded = store.Load();
                foreach (var definition in NullCityContent.Enemies)
                    Assert.That(Array.Find(loaded.bestiary, e => e.id == definition.Id)?.discovered, Is.True, definition.Id);
                Assert.That(Array.Find(loaded.bestiary, e => e.id == NullCityContent.MotherloadId)?.discovered, Is.True);
                Assert.That(Array.Find(loaded.bestiary, e => e.id == "chaser")?.discovered, Is.True);
            }
            finally { DeleteTestFile(path); }
        }

        [Test]
        public void Legacy_profiles_gain_undiscovered_city_entries_without_resetting_progress()
        {
            var path = Path.Combine(Path.GetTempPath(), "voidfall-null-city-save-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var profile = SaveStore.CreateDefault();
                profile.parts = 123;
                profile.bestiary = new[] { new BestiaryEntry { id = "chaser", discovered = true } };
                var store = new SaveStore(path);
                store.Save(profile);
                var loaded = store.Load();
                Assert.That(loaded.parts, Is.EqualTo(123));
                Assert.That(Array.Find(loaded.bestiary, e => e.id == "chaser")?.discovered, Is.True);
                foreach (var definition in NullCityContent.Enemies)
                    Assert.That(Array.Find(loaded.bestiary, e => e.id == definition.Id)?.discovered, Is.False, definition.Id);
                Assert.That(Array.Find(loaded.bestiary, e => e.id == NullCityContent.MotherloadId)?.discovered, Is.False);
            }
            finally { DeleteTestFile(path); }
        }

        private static void DeleteTestFile(string path)
        {
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }
}
