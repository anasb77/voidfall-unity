using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;
using VoidFall.UI;

namespace VoidFall.Tests.PlayMode
{
    public sealed class DealerIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private object _sim, _oldStore, _oldProfile, _oldExportDirectory;
        private bool _enabled, _inactive;
        private string _folder;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>(); Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _oldStore = Get("_saveStore"); _oldProfile = Get("_saveData"); _inactive = (bool)Get("_applicationInactive");
            _oldExportDirectory = Get("_runExportDirectoryOverride");
            _folder = Path.Combine(Path.GetTempPath(), "voidfall-dealer-tests-" + Guid.NewGuid().ToString("N"));
            var store = new SaveStore(Path.Combine(_folder, "profile.json")); var profile = SaveStore.CreateDefault(); profile.directorOnboardingSeen = true;
            store.Save(profile); Set("_saveStore", store); Set("_saveData", profile); Set("_runSaved", true); Set("_applicationInactive", false);
            Call("StartRunInternal", false, false); _sim = Get("_gameSim");
            Call("DestroyEnemiesForVoidTransition"); SetPlayer("Iframes", 0f); Set("_partsEarned", 100);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_finished"); Set("_runExportDirectoryOverride", _oldExportDirectory);
                Call("CloseDealer"); Set("_runSaved", true); Call("EnterMainMenu");
                Set("_saveStore", _oldStore); Set("_saveData", _oldProfile); Set("_applicationInactive", _inactive); _runtime.enabled = _enabled;
            }
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            yield return null;
        }
        private void Crossing()
        {
            Call("OnVoidObjectiveCompleted"); Call("BeginPortalJunction");
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
            SetPlayer("Position", (Vector2)Get("_dealerPosition"));
            Call("OpenDealer"); Set("_dealerOpenedFrame", Time.frameCount - 2);
        }
        [Test]
        public void BrowseIsFreeReopeningPreservesStockAndPurchaseIsOnce()
        {
            Crossing(); var session = (DealerSession)Get("_dealerSession");
            Assert.That(((UIManager)Get("_ui")).CurrentScreen, Is.EqualTo(UIScreen.Dealer));
            Assert.That((int)Get("_partsEarned"), Is.EqualTo(100));
            Call("CloseDealer"); Call("OpenDealer"); Set("_dealerOpenedFrame", Time.frameCount - 2);
            Assert.That(Get("_dealerSession"), Is.SameAs(session));
            var index = Array.FindIndex(session.Offers, o => o.Kind == DealerOfferKind.Fragment);
            Call("BuyDealerOffer", index); Assert.That((int)Get("_partsEarned"), Is.Zero);
            var saved = ((SaveStore)Get("_saveStore")).Load(); Assert.That(saved.soundBladeFragments, Is.EqualTo(1));
            Set("_partsEarned", 100); Call("BuyDealerOffer", index); Assert.That((int)Get("_partsEarned"), Is.EqualTo(100));
        }
        [Test]
        public void ControllerOpenCannotAlsoBuyInTheOpeningFrame()
        {
            Crossing(); Set("_dealerOpenedFrame", Time.frameCount);
            Call("BuyDealerOffer", 0); Assert.That((int)Get("_partsEarned"), Is.EqualTo(100));
            Assert.That(((DealerSession)Get("_dealerSession")).PurchasedIndex, Is.EqualTo(-1));
        }
        [Test]
        public void CompletingFragmentsEquipsAndNewRunResetsOnlyEquipment()
        {
            var profile = (SaveData)Get("_saveData"); profile.soundBladeFragments = 3;
            Crossing(); var session = (DealerSession)Get("_dealerSession");
            var index = Array.FindIndex(session.Offers, o => o.Kind == DealerOfferKind.Fragment);
            Call("BuyDealerOffer", index);
            Assert.That(Get("_legendaryWeapon"), Is.EqualTo(LegendaryWeaponId.SoundBlade));
            Assert.That(((SaveData)Get("_saveData")).soundBladeFragments, Is.EqualTo(7));
            Call("CloseDealer"); Call("StartRunInternal", false, false);
            Assert.That(Get("_legendaryWeapon"), Is.EqualTo(LegendaryWeaponId.None));
            Assert.That(((SaveData)Get("_saveData")).soundBladeFragments, Is.EqualTo(7));
        }
        [Test]
        public void NativeShieldConsumesShieldBeforeApplyingHpDamage()
        {
            Set("_dealerShield", 20f); var health = (float)Field(Field(_sim, "Player"), "Health");
            Call("DamagePlayer", 15f, Vector2.zero, "chaser", false);
            Assert.That(Field(Field(_sim, "Player"), "Health"), Is.EqualTo(health)); Assert.That(Get("_dealerShield"), Is.EqualTo(5f));
            SetPlayer("Iframes", 0f); Call("DamagePlayer", 12f, Vector2.zero, "chaser", false);
            Assert.That(Field(Field(_sim, "Player"), "Health"), Is.EqualTo(health - 7));
        }
        [Test]
        public void RecoveryChargesHealOnEarnedLevelsAndConsumeAtFullHealth()
        {
            var maximum = (float)Field(Field(_sim, "Player"), "MaxHealth");
            Set("_dealerRecoveryCharges", 5); SetPlayer("Health", maximum * .5f); Call("ApplyLevelRecovery");
            Assert.That(Field(Field(_sim, "Player"), "Health"), Is.EqualTo(maximum * .53f).Within(.001f));
            Assert.That(Get("_dealerRecoveryCharges"), Is.EqualTo(4));
            SetPlayer("Health", maximum); Call("ApplyLevelRecovery"); Assert.That(Get("_dealerRecoveryCharges"), Is.EqualTo(3));
        }
        [Test]
        public void DealerAndLegendaryOutcomesAppearInExistingJsonAndJournal()
        {
            var directory = Path.Combine(_folder, "RunExports"); Set("_runExportDirectoryOverride", directory);
            ((SaveData)Get("_saveData")).soundBladeFragments = 3; Call("BeginRunExport", true, true);
            Crossing(); var session = (DealerSession)Get("_dealerSession");
            Call("BuyDealerOffer", Array.FindIndex(session.Offers, o => o.Kind == DealerOfferKind.Fragment));
            Call("CloseDealer"); Call("HideJunction");
            var stage = typeof(VoidFallGameRuntime).GetField("_journeyStage", Flags); stage.SetValue(_runtime, Enum.Parse(stage.FieldType, "Combat"));
            SetPlayer("Position", Vector2.zero); Enemy(new Vector2(100, 0));
            Set("_legendaryHeld", false); Call("StepLegendaries", .01f);
            Set("_legendaryHeld", true); Set("_legendaryAim", 0f); Set("_time", 1f); Call("StepLegendaries", .01f);
            Call("FinishRunExport", "test_complete");
            var history = File.ReadAllLines(Directory.GetFiles(directory, "*.jsonl").Single()).Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
            Assert.That(history.Any(e => e.kind == "dealer_stock" && e.options.Length == 3), Is.True);
            Assert.That(history.Any(e => e.kind == "dealer_purchase" && e.id == "sound-blade-fragment-2" && e.amount == 100), Is.True);
            Assert.That(history.Any(e => e.kind == "legendary_equipped" && e.id == "sound-blade"), Is.True);
            Assert.That(history.Any(e => e.kind == "weapon_damage_window" && e.id == "sound-blade" && e.amount > 0), Is.True);
            var report = JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(Directory.GetFiles(directory, "*.json").Single()));
            Assert.That(report.summary.weaponDamage.Any(d => d.id == "sound-blade" && d.value > 0), Is.True);
            Assert.That(report.summary.progress.legendaryId, Is.EqualTo("sound-blade"));
            Assert.That(report.summary.progress.soundBladeFragments, Is.EqualTo(7));
        }
        [Test]
        public void NativeSoundBladeHitsThroughTheSharedDamagePathWithRepeatGate()
        {
            SetPlayer("Position", Vector2.zero); Enemy(new Vector2(100, 0)); Call("EquipLegendary", LegendaryWeaponId.SoundBlade, 1);
            Set("_legendaryHeld", false); Call("StepLegendaries", .01f);
            Set("_legendaryHeld", true); Set("_legendaryAim", 0f); Set("_time", 1f); Call("StepLegendaries", .01f);
            var after = EnemyHealth(); Assert.That(after, Is.LessThan(1000));
            Set("_time", 1.01f); Call("StepLegendaries", .01f); Assert.That(EnemyHealth(), Is.EqualTo(after));
        }
        [Test]
        public void NativeChargedRifleDamageOccursOncePerReleasedBeam()
        {
            SetPlayer("Position", Vector2.zero); Enemy(new Vector2(180, 0)); Call("EquipLegendary", LegendaryWeaponId.ChargedRifle, 1);
            Set("_legendaryHeld", false); Call("StepLegendaries", .01f); Set("_legendaryHeld", true);
            for (var i = 0; i < 120; i++) Call("StepLegendaries", 1f / 60);
            ((LegendaryState)Get("_legendaryState")).Angle = 0;
            Set("_legendaryHeld", false); Call("StepLegendaries", .001f);
            var health = EnemyHealth(); Assert.That(health, Is.LessThan(1000));
            Call("StepLegendaries", .1f); Assert.That(EnemyHealth(), Is.EqualTo(health));
        }
        [Test]
        public void SoundBladeHitCooldownDoesNotLeakAcrossEquipmentOrSlotReuse()
        {
            SetPlayer("Position", Vector2.zero); Enemy(new Vector2(100, 0)); Call("EquipLegendary", LegendaryWeaponId.SoundBlade, 1);
            Set("_legendaryHeld", false); Call("StepLegendaries", .01f);
            Set("_time", 200f); Set("_legendaryHeld", true); Call("StepLegendaries", .01f); Assert.That(EnemyHealth(), Is.LessThan(1000));
            Enemy(new Vector2(100, 0), 322);
            Set("_time", 200.01f); Set("_legendaryHeld", false); Call("StepLegendaries", .01f);
            Set("_legendaryHeld", true); Call("StepLegendaries", .01f); Assert.That(EnemyHealth(), Is.LessThan(1000));
            Call("StartRunInternal", false, false); SetPlayer("Position", Vector2.zero); Enemy(new Vector2(100, 0));
            Call("EquipLegendary", LegendaryWeaponId.SoundBlade, 1); Set("_time", .05f);
            Set("_legendaryHeld", false); Call("StepLegendaries", .01f);
            Set("_legendaryHeld", true); Call("StepLegendaries", .01f); Assert.That(EnemyHealth(), Is.LessThan(1000));
        }
        private void Enemy(Vector2 position, int identity = 321)
        {
            var enemies = (Array)Field(_sim, "Enemies"); Array.Clear(enemies, 0, enemies.Length);
            _sim.GetType().GetMethod("ResetEnemyOrder").Invoke(_sim, null);
            var enemy = Activator.CreateInstance(enemies.GetType().GetElementType());
            Put(enemy,"Active",true); Put(enemy,"Position",position); Put(enemy,"Id","chaser"); Put(enemy,"View",0);
            Put(enemy,"SpawnId",identity); Put(enemy,"Health",1000f); Put(enemy,"MaxHealth",1000f); Put(enemy,"Radius",14f); Put(enemy,"Age",1f);
            enemies.SetValue(enemy,0); _sim.GetType().GetMethod("AppendEnemyOrder").Invoke(_sim,new object[]{0}); Call("RebuildEnemyGrid");
        }
        private float EnemyHealth() => (float)Field(((Array)Field(_sim,"Enemies")).GetValue(0),"Health");
        private object Get(string name) => typeof(VoidFallGameRuntime).GetField(name,Flags).GetValue(_runtime);
        private void Set(string name,object value) => typeof(VoidFallGameRuntime).GetField(name,Flags).SetValue(_runtime,value);
        private static object Field(object target,string name) => target.GetType().GetField(name,Flags).GetValue(target);
        private static void Put(object target,string name,object value) => target.GetType().GetField(name,Flags).SetValue(target,value);
        private void SetPlayer(string name,object value) { var p=Field(_sim,"Player"); Put(p,name,value); Put(_sim,"Player",p); }
        private object Call(string name,params object[] args)
        {
            foreach(var method in typeof(VoidFallGameRuntime).GetMethods(Flags)) if(method.Name==name && method.GetParameters().Length==args.Length)
                try{return method.Invoke(_runtime,args);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw();throw;}
            throw new MissingMethodException(name);
        }
    }
}
