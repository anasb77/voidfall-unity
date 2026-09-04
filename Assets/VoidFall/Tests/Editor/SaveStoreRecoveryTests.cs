using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Tests.Editor
{
    public sealed class SaveStoreRecoveryTests
    {
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "VoidFall-save-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, SaveStore.SaveKey + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_directory, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Lost_primary_recovers_last_good_backup_and_survives_another_restart(bool corrupt)
        {
            var store = new SaveStore(_path);
            var profile = SaveStore.CreateDefault();
            profile.parts = 125;
            profile.stats.totalRuns = 7;
            profile.workshop[0].rank = 2;
            store.Save(profile);
            profile.parts = 150;
            store.Save(profile);
            var backup = File.ReadAllText(_path + ".bak");
            if (corrupt) File.WriteAllText(_path, "{ invalid json");
            else File.Delete(_path);

            var recovered = new SaveStore(_path).Load();

            Assert.That(recovered.parts, Is.EqualTo(125));
            Assert.That(recovered.stats.totalRuns, Is.EqualTo(7));
            Assert.That(recovered.workshop[0].rank, Is.EqualTo(2));
            Assert.That(File.ReadAllText(_path + ".bak"), Is.EqualTo(backup),
                "Recovery must not replace the last good backup with the corrupt primary.");
            Assert.That(new SaveStore(_path).Load().parts, Is.EqualTo(125));
        }

        [Test]
        public void Backup_takes_precedence_over_stale_legacy_profile_when_primary_is_missing()
        {
            var profile = SaveStore.CreateDefault();
            profile.parts = 125;
            File.WriteAllText(_path + ".bak", JsonUtility.ToJson(profile));
            File.WriteAllText(Path.Combine(_directory, "voidfall_save_v3.json"), "{\"version\":3,\"parts\":10}");

            Assert.That(new SaveStore(_path).Load().parts, Is.EqualTo(125));
        }

        [Test]
        public void Failed_recovery_write_keeps_last_good_backup_on_the_next_save()
        {
            var profile = SaveStore.CreateDefault();
            profile.parts = 125;
            var backup = JsonUtility.ToJson(profile);
            File.WriteAllText(_path + ".bak", backup);
            File.WriteAllText(_path, "{ invalid json");
            Directory.CreateDirectory(_path + ".tmp");
            var store = new SaveStore(_path);

            var recovered = store.Load();
            Assert.That(recovered.parts, Is.EqualTo(125));
            Directory.Delete(_path + ".tmp");
            recovered.parts = 150;
            store.Save(recovered);

            Assert.That(File.ReadAllText(_path + ".bak"), Is.EqualTo(backup));
            Assert.That(new SaveStore(_path).Load().parts, Is.EqualTo(150));
        }

        [Test]
        public void Locked_backup_with_missing_primary_blocks_default_profile_saves()
        {
            var profile = SaveStore.CreateDefault();
            profile.parts = 125;
            File.WriteAllText(_path + ".bak", JsonUtility.ToJson(profile));
            var store = new SaveStore(_path);
            using (var locked = new FileStream(_path + ".bak", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("VoidFall save backup could not be read"));
                store.Load();
                Assert.That(store.StorageUnreadable, Is.True);
                Assert.Throws<IOException>(() => store.Save(SaveStore.CreateDefault()));
                Assert.That(File.Exists(_path), Is.False);
            }
            Assert.That(new SaveStore(_path).Load().parts, Is.EqualTo(125));
        }

        [Test]
        public void Legacy_backup_migration_refunds_protocol_only_once()
        {
            File.WriteAllText(_path + ".bak",
                "{\"version\":4,\"parts\":10,\"workshop\":[{\"id\":\"protocol\",\"rank\":3}]}");
            Assert.That(new SaveStore(_path).Load().parts, Is.EqualTo(370));
            Assert.That(new SaveStore(_path).Load().parts, Is.EqualTo(370));
        }

        [Test]
        public void Healthy_primary_takes_precedence_over_backup()
        {
            var profile = SaveStore.CreateDefault();
            profile.parts = 125;
            var store = new SaveStore(_path);
            store.Save(profile);
            profile.parts = 150;
            store.Save(profile);
            Assert.That(new SaveStore(_path).Load().parts, Is.EqualTo(150));
        }

        [Test]
        public void Locked_primary_is_not_treated_as_corruption_or_overwritten()
        {
            var store = new SaveStore(_path);
            var profile = SaveStore.CreateDefault();
            profile.parts = 125;
            store.Save(profile);
            using (var locked = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("VoidFall save could not be read"));
                store.Load();
                Assert.That(store.StorageUnreadable, Is.True);
                Assert.Throws<IOException>(() => store.Save(SaveStore.CreateDefault()));
            }
            Assert.That(new SaveStore(_path).Load().parts, Is.EqualTo(125));
        }

        [Test]
        public void New_enemies_and_bosses_keep_their_discovery_after_save_and_reload()
        {
            var ids = MonochromeContent.Enemies.Select(enemy => enemy.Id)
                .Concat(new[] { HydraContent.Boss.Id, MonochromeContent.BlackBoss.Id, MonochromeContent.WhiteBoss.Id })
                .ToArray();
            var profile = SaveStore.CreateDefault();
            foreach (var id in ids)
            {
                var entry = profile.bestiary.SingleOrDefault(item => item.id == id);
                Assert.That(entry, Is.Not.Null, "Missing discoverable entry: " + id);
                entry.discovered = true;
            }
            var store = new SaveStore(_path);
            store.Save(profile);
            var loaded = new SaveStore(_path).Load();
            foreach (var id in ids)
                Assert.That(loaded.bestiary.Single(item => item.id == id).discovered, Is.True, id);
            Assert.That(loaded.bestiary.Single(item => item.id == "chaser").discovered, Is.False);
        }
    }
}
