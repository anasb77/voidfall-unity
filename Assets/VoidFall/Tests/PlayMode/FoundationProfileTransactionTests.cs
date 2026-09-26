using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class FoundationProfileTransactionTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SaveData _previousProfile;
        private SaveStore _previousStore, _store;
        private object _previousExport, _previousSeed;
        private bool _enabled, _inactive;
        private string _directory;
        private SaveData Profile => (SaveData)Get("_saveData");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled;
            _runtime.enabled = false;
            _previousProfile = Profile;
            _previousStore = (SaveStore)Get("_saveStore");
            _previousExport = Get("_runExportDirectoryOverride");
            _previousSeed = Get("_diagnosticRunSeedOverride");
            _inactive = (bool)Get("_applicationInactive");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-profile-transactions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _store = new SaveStore(Path.Combine(_directory, "profile.json"));
            var profile = SaveStore.CreateDefault();
            profile.parts = 100;
            profile.directorOnboardingSeen = true;
            _store.Save(profile);
            Set("_saveStore", _store);
            Set("_saveData", profile);
            Set("_runExportDirectoryOverride", Path.Combine(_directory, "exports"));
            Set("_runSaved", true);
            Set("_applicationInactive", false);
            Set("_diagnosticRunSeedOverride", 2848592627u);
            Call("StartRunInternal", false, false);
            Call("DestroyEnemiesForVoidTransition");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_complete");
                Call("StopMajorIncident");
                Set("_runSaved", true);
                Set("_gameOver", false);
                Call("EnterMainMenu");
                Set("_saveData", _previousProfile);
                Set("_saveStore", _previousStore);
                Set("_runExportDirectoryOverride", _previousExport);
                Set("_diagnosticRunSeedOverride", _previousSeed);
                Set("_applicationInactive", _inactive);
                _runtime.enabled = _enabled;
            }
            if (_directory != null && Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        [Test]
        public void Purchase_persists_rank_and_wallet_before_publishing()
        {
            Call("TryBuyWorkshop", "integrity");
            Assert.That(Profile.parts, Is.EqualTo(65));
            Assert.That(_store.Load().parts, Is.EqualTo(65));
            Assert.That(_store.Load().workshop[0].rank, Is.EqualTo(1));
        }

        [Test]
        public void Failed_purchase_keeps_live_identity_rank_wallet_and_disk()
        {
            var previous = Profile;
            var disk = File.ReadAllText(_store.PathOnDisk);
            BlockWrites();
            LogAssert.Expect(LogType.Warning, new Regex("^VoidFall profile could not be saved:"));
            Call("TryBuyWorkshop", "integrity");
            Assert.That(Profile, Is.SameAs(previous));
            Assert.That(Profile.parts, Is.EqualTo(100));
            Assert.That(Profile.workshop[0].rank, Is.Zero);
            Assert.That(File.ReadAllText(_store.PathOnDisk), Is.EqualTo(disk));
        }

        [Test]
        public void Refund_persists_wallet_and_removal_in_one_commit()
        {
            GivePurchasedRank();
            Call("RefundAllWorkshop");
            Assert.That(Profile.parts, Is.EqualTo(100));
            Assert.That(Profile.workshop[0].rank, Is.Zero);
            Assert.That(_store.Load().parts, Is.EqualTo(100));
            Assert.That(_store.Load().workshop[0].rank, Is.Zero);
        }

        [Test]
        public void Failed_refund_keeps_owned_rank_and_both_wallets()
        {
            GivePurchasedRank();
            var previous = Profile;
            var disk = File.ReadAllText(_store.PathOnDisk);
            BlockWrites();
            LogAssert.Expect(LogType.Warning, new Regex("^VoidFall profile could not be saved:"));
            Call("RefundAllWorkshop");
            Assert.That(Profile, Is.SameAs(previous));
            Assert.That(Profile.parts, Is.EqualTo(65));
            Assert.That(Profile.workshop[0].rank, Is.EqualTo(1));
            Assert.That(File.ReadAllText(_store.PathOnDisk), Is.EqualTo(disk));
        }

        [Test]
        public void Terminal_run_unlock_records_and_totals_use_one_atomic_commit()
        {
            Call("BeginRunExport", true, true);
            var diskBefore = File.ReadAllText(_store.PathOnDisk);
            PrepareTerminalRun();
            Call("SaveRun");
            AssertCompleteRun();
            Assert.That(File.ReadAllText(_store.PathOnDisk + ".bak"), Is.EqualTo(diskBefore),
                "A nested write would rotate a partially committed run into the backup.");
            var diskAfter = File.ReadAllText(_store.PathOnDisk);
            Call("SaveRun");
            Assert.That(File.ReadAllText(_store.PathOnDisk), Is.EqualTo(diskAfter));
            Assert.That(File.ReadAllText(_store.PathOnDisk + ".bak"), Is.EqualTo(diskBefore));
            Call("FinishRunExport", "test_complete");
            var history = string.Join("\n", Directory.GetFiles(Path.Combine(_directory, "exports"), "*.jsonl", SearchOption.AllDirectories).Select(File.ReadAllText));
            Assert.That(history, Does.Contain("form_unlocked"));
            Assert.That(history, Does.Contain(PlayerForms.DasherId));
        }

        [Test]
        public void Failed_terminal_save_publishes_nothing_and_retry_awards_exactly_once()
        {
            var previous = Profile;
            var diskBefore = File.ReadAllText(_store.PathOnDisk);
            PrepareTerminalRun();
            BlockWrites();
            LogAssert.Expect(LogType.Error, new Regex("^VoidFall run save failed:"));
            Call("SaveRun");
            Assert.That(Profile, Is.SameAs(previous));
            Assert.That(Profile.stats.totalRuns, Is.Zero);
            Assert.That(Profile.parts, Is.EqualTo(100));
            Assert.That(Profile.unlockedForms, Does.Not.Contain(PlayerForms.DasherId));
            Assert.That(Get("_runSaved"), Is.False);
            Assert.That(File.ReadAllText(_store.PathOnDisk), Is.EqualTo(diskBefore));
            Directory.Delete(_store.PathOnDisk + ".tmp");
            Call("SaveRun");
            AssertCompleteRun();
            Assert.That(File.ReadAllText(_store.PathOnDisk + ".bak"), Is.EqualTo(diskBefore));
        }

        [Test]
        public void Failed_void_clear_is_recovered_from_route_facts_on_terminal_retry()
        {
            var route = (VoidRouteRun)Get("_voidRoute");
            Assert.That(route.NotifyVoidCompleted(route.CurrentVoidId), Is.True);
            BlockWrites();
            LogAssert.Expect(LogType.Warning, new Regex("^VoidFall profile could not be saved:"));
            Call("RecordVoidClearedForForms");
            Assert.That(Profile.voidsCleared, Is.Empty);
            Directory.Delete(_store.PathOnDisk + ".tmp");
            PrepareTerminalRun();
            Call("SaveRun");
            Assert.That(_store.Load().voidsCleared, Does.Contain(route.CurrentArenaId));
            AssertCompleteRun();
        }

        private void BlockWrites() => Directory.CreateDirectory(_store.PathOnDisk + ".tmp");
        private void GivePurchasedRank()
        {
            Profile.parts = 65;
            Profile.workshop[0].rank = 1;
            _store.Save(Profile);
        }
        private void PrepareTerminalRun()
        {
            Set("_bossKills", 1);
            Set("_partsEarned", 30);
            Set("_gameOver", true);
            Set("_runSaved", false);
        }
        private void AssertCompleteRun()
        {
            var disk = _store.Load();
            Assert.That(Get("_runSaved"), Is.True);
            Assert.That(disk.stats.totalRuns, Is.EqualTo(1));
            Assert.That(disk.stats.totalBossKills, Is.EqualTo(1));
            Assert.That(disk.parts, Is.EqualTo(130));
            Assert.That(disk.recentRuns, Has.Length.EqualTo(1));
            Assert.That(disk.highScores, Has.Length.EqualTo(1));
            Assert.That(disk.unlockedForms, Does.Contain(PlayerForms.DasherId));
            Assert.That(JsonUtility.ToJson(Profile), Is.EqualTo(JsonUtility.ToJson(disk)));
        }
        private object Get(string name) => typeof(VoidFallGameRuntime).GetField(name, Flags).GetValue(_runtime);
        private void Set(string name, object value) => typeof(VoidFallGameRuntime).GetField(name, Flags).SetValue(_runtime, value);
        private object Call(string name, params object[] args) => typeof(VoidFallGameRuntime).GetMethod(name, Flags).Invoke(_runtime, args);
    }
}
