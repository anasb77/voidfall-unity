using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Tests.Editor
{
    public sealed class SaveTransferTests
    {
        private string _directory, _path;
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-save-transfer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "profile.json");
        }
        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        [Test]
        public void Transfer_retains_complete_profile_including_forms_and_video_preferences()
        {
            var profile = SaveStore.CreateDefault();
            profile.parts = 427;
            profile.workshop[0].rank = 2;
            profile.soundBladeFragments = 3;
            profile.form = PlayerForms.DasherId;
            profile.unlockedForms = new[] { PlayerForms.DefaultId, PlayerForms.DasherId, PlayerForms.BruteId };
            profile.voidsCleared = new[] { "abyss", "null-city", "hydra" };
            profile.settings.resolutionWidth = 1280;
            profile.settings.resolutionHeight = 720;
            profile.settings.fullscreenMode = 3;
            profile.settings.monitorIndex = 2;
            profile.settings.bloom = .7f;
            profile.settings.chromatic = .15f;
            profile.settings.masterVolume = .35f;
            profile.settings.effectsVolume = .4f;
            profile.settings.musicVolume = .55f;
            profile.settings.shake = .25f;
            profile.settings.reducedMotion = true;
            profile.settings.highContrast = true;
            profile.settings.touchSize = 1.2f;
            profile.settings.quality = "low";
            profile.stats.totalRuns = 7;
            profile.stats.totalDamageDealt = 12345678901L;
            profile = SaveStore.Sanitize(profile);
            var before = JsonUtility.ToJson(profile);
            var exported = BrowserSaveExporter.Export(profile);
            Assert.That(BrowserSaveImporter.TryConvert(exported, out var converted), Is.True);
            Assert.That(JsonUtility.ToJson(SaveStore.Sanitize(converted)), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(before), "Export never mutates the live source.");
            Assert.That(new SaveStore(_path).TryImportBrowserSave(exported, out _, out var error), Is.True, error);
            Assert.That(JsonUtility.ToJson(new SaveStore(_path).Load()), Is.EqualTo(before));
        }

        [Test]
        public void Old_browser_profile_keeps_compatible_form_and_video_defaults()
        {
            Assert.That(BrowserSaveImporter.TryConvert("{\"version\":5,\"workshop\":{},\"bestiary\":{}}", out var profile), Is.True);
            Assert.That(profile.form, Is.EqualTo(PlayerForms.DefaultId));
            Assert.That(profile.unlockedForms, Is.EqualTo(new[] { PlayerForms.DefaultId }));
            Assert.That(profile.voidsCleared, Is.Empty);
            Assert.That(profile.settings.monitorIndex, Is.EqualTo(-1));
            Assert.That(profile.settings.bloom, Is.EqualTo(-1));
            Assert.That(profile.settings.chromatic, Is.EqualTo(-1));
            Assert.That(profile.settings.resolutionWidth, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Future_primary_is_quarantined_without_rewrite_or_backup_fallback(bool browserShape)
        {
            var store = new SaveStore(_path);
            store.Save(SaveStore.CreateDefault());
            var original = File.ReadAllText(_path);
            File.Copy(_path, _path + ".bak");
            var future = FutureJson(browserShape);
            File.WriteAllText(_path, future);
            ExpectFutureWarning();
            var visible = store.Load();
            Assert.That(visible.parts, Is.EqualTo(427));
            Assert.That(store.UnsupportedSaveVersion, Is.EqualTo(999));
            Assert.That(store.StorageUnreadable, Is.True);
            Assert.Throws<IOException>(() => store.Save(visible));
            ExpectFutureWarning();
            Assert.That(store.TryReloadExisting(out _), Is.False);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(future));
            Assert.That(File.ReadAllText(_path + ".bak"), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(_directory, "*.corrupt*"), Is.Empty);
        }

        [Test]
        public void Future_backup_is_preserved_when_primary_is_missing()
        {
            var future = FutureJson(false);
            File.WriteAllText(_path + ".bak", future);
            var store = new SaveStore(_path);
            ExpectFutureWarning();
            Assert.That(store.Load().parts, Is.EqualTo(427));
            Assert.That(File.Exists(_path), Is.False);
            Assert.That(File.ReadAllText(_path + ".bak"), Is.EqualTo(future));
            Assert.Throws<IOException>(() => store.Save(SaveStore.CreateDefault()));
        }

        [Test]
        public void Future_import_is_rejected_and_explicit_compatible_import_can_replace_quarantine()
        {
            var store = new SaveStore(_path);
            var future = FutureJson(false);
            File.WriteAllText(_path, future);
            ExpectFutureWarning();
            store.Load();
            Assert.That(store.TryImportBrowserSave(FutureJson(true), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("newer game version"));
            Assert.That(File.ReadAllText(_path), Is.EqualTo(future));
            Assert.That(store.TryImportBrowserSave(BrowserSaveExporter.Export(SaveStore.CreateDefault()), out _, out error), Is.True, error);
            Assert.That(store.StorageUnreadable, Is.False);
            Assert.That(store.UnsupportedSaveVersion, Is.Zero);
            Assert.That(File.ReadAllText(_path + ".pre-import.bak"), Is.EqualTo(future));
        }

        [Test]
        public void Atomic_export_keeps_last_good_file_on_failure()
        {
            var profile = SaveStore.CreateDefault();
            BrowserSaveExporter.WriteFile(_path, profile);
            var original = File.ReadAllText(_path);
            Directory.CreateDirectory(_path + ".tmp");
            profile.parts = 427;
            Assert.That(() => BrowserSaveExporter.WriteFile(_path, profile), Throws.Exception);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(original));
            Directory.Delete(_path + ".tmp");
            BrowserSaveExporter.WriteFile(_path, profile);
            Assert.That(File.ReadAllText(_path + ".bak"), Is.EqualTo(original));
            Assert.That(BrowserSaveImporter.TryConvert(File.ReadAllText(_path), out var loaded), Is.True);
            Assert.That(loaded.parts, Is.EqualTo(427));
        }

        private static void ExpectFutureWarning() => LogAssert.Expect(LogType.Warning, new Regex("^VoidFall profile uses a newer save version"));
        private static string FutureJson(bool browser)
        {
            var profile = SaveStore.CreateDefault();
            profile.parts = 427;
            var json = browser ? BrowserSaveExporter.Export(profile) : JsonUtility.ToJson(profile);
            return json.Replace("\"version\":6", "\"version\":999").Insert(1, "\"futureInventory\":{\"relic\":12},");
        }
    }
}
