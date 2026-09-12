using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;
using VoidFall.UI;

namespace VoidFall.Tests.PlayMode
{
    public sealed class RouletteClaimIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private object _oldStore, _oldProfile;
        private bool _oldEnabled, _oldInactive;
        private string _directory;
        private UpgradeProgress Progress => (UpgradeProgress)Get(_runtime, "_upgradeProgress");
        private LevelUpView View => ((UIManager)Get(_runtime, "_ui")).LevelUp;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            _oldEnabled = _runtime.enabled;
            _runtime.enabled = false;
            _oldStore = Get(_runtime, "_saveStore");
            _oldProfile = Get(_runtime, "_saveData");
            _oldInactive = (bool)Get(_runtime, "_applicationInactive");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-claims-" + Guid.NewGuid().ToString("N"));
            Set(_runtime, "_saveStore", new SaveStore(Path.Combine(_directory, "profile.json")));
            Set(_runtime, "_saveData", SaveStore.CreateDefault());
            Set(_runtime, "_runExportDirectoryOverride", Path.Combine(_directory, "RunExports"));
            Set(_runtime, "_applicationInactive", false);
            Set(_runtime, "_visualCaptureIssued", true);
            Call("StartRunInternal", true, false);
            Array.Clear(Progress.WeaponRanks, 0, Progress.WeaponRanks.Length);
            Array.Clear(Progress.SupportRanks, 0, Progress.SupportRanks.Length);
            Progress.WeaponRanks[0] = 2;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Call("FinishRunExport", "test_finished");
            Set(_runtime, "_runSaved", true);
            Call("EnterMainMenu");
            Set(_runtime, "_runExportDirectoryOverride", null);
            Set(_runtime, "_saveStore", _oldStore);
            Set(_runtime, "_saveData", _oldProfile);
            Set(_runtime, "_applicationInactive", _oldInactive);
            _runtime.enabled = _oldEnabled;
            yield return null;
        }

        [Test]
        public void Two_weapon_ranks_require_two_claims_and_old_callbacks_cannot_claim_again()
        {
            Open(RoulettePrizeKind.WeaponUpgradeQuality);
            Assert.That(Progress.WeaponRanks[0], Is.EqualTo(2), "Landing is not a grant.");
            Assert.That(Get(_runtime, "_paused"), Is.True);
            Assert.That(Get(_runtime, "_prizeRevealActive"), Is.True);
            var stale = (Action)Get(View, "_onTake");
            Claim();
            Assert.That(Progress.WeaponRanks[0], Is.EqualTo(3));
            Assert.That(Get(_runtime, "_paused"), Is.True);
            stale();
            Assert.That(Progress.WeaponRanks[0], Is.EqualTo(3));
            Claim();
            Assert.That(Progress.WeaponRanks[0], Is.EqualTo(4));
            Assert.That(Get(_runtime, "_prizeRevealActive"), Is.False);
            Assert.That(Get(_runtime, "_paused"), Is.False);
            stale();
            Assert.That(Progress.WeaponRanks[0], Is.EqualTo(4));
        }

        [Test]
        public void Legacy_boon_slot_awards_500_parts_only_after_claim()
        {
            Set(_runtime, "_partsEarned", 100);
            Set(_runtime, "_score", 123);
            var sim = Get(_runtime, "_gameSim");
            var player = Get(sim, "Player");
            Set(player, "Health", 13f);
            Set(sim, "Player", player);
            Open(RoulettePrizeKind.RareBoon);
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(100));
            Claim();
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(600));
            Assert.That(Get(_runtime, "_score"), Is.EqualTo(123));
            Assert.That(Get(Get(sim, "Player"), "Health"), Is.EqualTo(13f));
        }

        [Test]
        public void Claim_deltas_are_exported_once_for_each_real_card()
        {
            Open(RoulettePrizeKind.WeaponUpgradeQuality);
            Claim();
            Claim();
            Call("FinishRunExport", "test_finished");
            var historyPath = Directory.GetFiles(Path.Combine(_directory, "RunExports"), "*.jsonl").Single();
            var history = File.ReadAllLines(historyPath).Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
            Assert.That(history.Count(e => e.kind == "roulette_claimed"), Is.EqualTo(2));
            Assert.That(history.Count(e => e.kind == "roulette_granted"), Is.EqualTo(1));
            Assert.That(history.Where(e => e.kind == "roulette_claimed").All(e => e.progress != null), Is.True);
        }

        [Test]
        public void Support_ranks_and_new_cards_are_deferred_until_their_claims()
        {
            Progress.SupportRanks[0] = 1;
            Open(RoulettePrizeKind.SupportUpgradeQuality);
            Assert.That(Progress.SupportRanks[0], Is.EqualTo(1));
            Claim();
            Assert.That(Progress.SupportRanks[0], Is.EqualTo(2));
            Claim();
            Assert.That(Progress.SupportRanks[0], Is.EqualTo(3));
            var owned = Progress.WeaponRanks.Count(r => r > 0) + Progress.SupportRanks.Count(r => r > 0);
            Open(RoulettePrizeKind.NewRandomCard);
            Assert.That(Progress.WeaponRanks.Count(r => r > 0) + Progress.SupportRanks.Count(r => r > 0), Is.EqualTo(owned));
            Claim();
            Assert.That(Progress.WeaponRanks.Count(r => r > 0) + Progress.SupportRanks.Count(r => r > 0), Is.EqualTo(owned + 1));
        }

        [Test]
        public void Power_up_is_a_real_drop_created_only_when_claimed()
        {
            var pickups = (Array)Get(Get(_runtime, "_gameSim"), "Pickups");
            var before = pickups.Cast<object>().Count(p => (bool)Get(p, "Active"));
            Open(RoulettePrizeKind.PowerUp);
            Assert.That(pickups.Cast<object>().Count(p => (bool)Get(p, "Active")), Is.EqualTo(before));
            Claim();
            Assert.That(pickups.Cast<object>().Count(p => (bool)Get(p, "Active")), Is.EqualTo(before + 1));
        }

        [Test]
        public void Capped_rewards_show_only_real_ranks_and_empty_eligibility_claims_parts()
        {
            Progress.WeaponRanks[0] = ProgressionRules.MaxWeaponRank - 1;
            Open(RoulettePrizeKind.WeaponUpgradeQuality);
            Claim();
            Assert.That(Progress.WeaponRanks[0], Is.EqualTo(ProgressionRules.MaxWeaponRank));
            Assert.That(Get(_runtime, "_prizeRevealActive"), Is.False);
            var before = (int)Get(_runtime, "_partsEarned");
            Open(RoulettePrizeKind.WeaponUpgradeQuality);
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(before));
            Claim();
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(before + 40));
        }

        [Test]
        public void Escape_clock_and_close_shortcuts_cannot_skip_unclaimed_cards()
        {
            Set(_runtime, "_voidCompletionPending", true);
            Set(_runtime, "_voidCompletionDelayRemaining", 12f);
            Open(RoulettePrizeKind.WeaponUpgradeQuality);
            Call("ClosePrizeReveal");
            Call("StepVoidCompletionDelay", 10f);
            Assert.That(Get(_runtime, "_prizeRevealActive"), Is.True);
            Assert.That(Get(_runtime, "_voidCompletionDelayRemaining"), Is.EqualTo(12f));
            Claim();
            Call("StepVoidCompletionDelay", 10f);
            Assert.That(Get(_runtime, "_voidCompletionDelayRemaining"), Is.EqualTo(12f));
            Claim();
            Assert.That(Get(_runtime, "_voidCompletionDelayRemaining"), Is.EqualTo(12f));
        }

        [Test]
        public void Wager_cost_is_settled_at_landing_once_even_before_reward_claim()
        {
            Set(_runtime, "_partsEarned", 100);
            Open(RoulettePrizeKind.RareBoon, 25, 5);
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(80));
            Claim();
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(580));
        }

        [Test]
        public void Old_ceremony_callback_cannot_claim_a_new_ceremony()
        {
            Open(RoulettePrizeKind.RareBoon);
            var oldCallback = (Action)Get(View, "_onTake");
            Call("StartRunInternal", true, false);
            Open(RoulettePrizeKind.RareBoon);
            var before = (int)Get(_runtime, "_partsEarned");
            oldCallback();
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(before));
            Assert.That(Get(_runtime, "_prizeRevealActive"), Is.True);
            Claim();
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(before + 500));
        }

        [Test]
        public void Wild_card_and_all_owned_fallback_wait_for_claim()
        {
            var cards = (HashSet<WildCardId>)Get(_runtime, "_activeWildCards");
            Open(RoulettePrizeKind.WildCard);
            Assert.That(cards.Count, Is.Zero);
            Claim();
            Assert.That(cards.Count, Is.EqualTo(1));
            foreach (WildCardId id in Enum.GetValues(typeof(WildCardId)))
                if (id != WildCardId.None && WildCardRules.IsImplemented(id)) cards.Add(id);
            var parts = (int)Get(_runtime, "_partsEarned");
            var score = (int)Get(_runtime, "_score");
            Open(RoulettePrizeKind.WildCard);
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(parts));
            Claim();
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(parts + 80));
            Assert.That(Get(_runtime, "_score"), Is.EqualTo(score + 750));
        }

        [Test]
        public void Full_xp_pool_keeps_a_powerup_claimable_without_granting_it_early()
        {
            var pickups = (Array)Get(Get(_runtime, "_gameSim"), "Pickups");
            for (var slot = 0; slot < pickups.Length + 30; slot++)
                Call("SpawnPickup", new Vector2(9000 + slot, 0), 1f);
            var special = _runtime.GetType().GetMethod("SpawnSpecialPickup", Flags);
            var partKind = Enum.Parse(special.GetParameters()[2].ParameterType, "Part");
            for (var fill = 0; fill < pickups.Length && Enumerable.Range(0, pickups.Length - 1).Any(i => !(bool)Get(pickups.GetValue(i), "Active")); fill++)
                special.Invoke(_runtime, new object[] { new Vector2(9000, 0), 1f, partKind });
            var before = (int)Get(_runtime, "_partsEarned");
            Open(RoulettePrizeKind.PowerUp);
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(before));
            Assert.That(pickups.Cast<object>().Any(p => (bool)Get(p, "Active") && Get(p, "Kind").ToString() != "Xp" && Get(p, "Kind").ToString() != "Part"), Is.False);
            Claim();
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(before));
            Assert.That(pickups.Cast<object>().Any(p => (bool)Get(p, "Active") && Get(p, "Kind").ToString() != "Xp" && Get(p, "Kind").ToString() != "Part"), Is.True);
        }

        [Test]
        public void Another_open_request_cannot_replace_unclaimed_rewards()
        {
            Open(RoulettePrizeKind.WeaponUpgradeQuality);
            var session = Get(_runtime, "_rouletteSession");
            Call("OpenBossRoulette");
            Assert.That(Get(_runtime, "_rouletteSession"), Is.SameAs(session));
            Claim();
            Claim();
            Assert.That(Progress.WeaponRanks[0], Is.EqualTo(4));
        }

        private void Open(RoulettePrizeKind kind, int spent = 0, int refunded = 0)
        {
            Call("OpenBossRoulette");
            var session = (RouletteSession)Get(_runtime, "_rouletteSession");
            typeof(RouletteSession).GetProperty("Wedges").SetValue(session, new[] {
                new RouletteWedgeDefinition(kind, RouletteTier.Legendary, 1, "Test reward", "", "") });
            typeof(RouletteSession).GetProperty("PartsSpent").SetValue(session, spent);
            typeof(RouletteSession).GetProperty("PartsRefunded").SetValue(session, refunded);
            RouletteRules.Spin(session, new Rng(1));
            Call("OnRouletteComplete", session);
        }

        [Test]
        public void Rewards_use_the_existing_upgrade_menu_instead_of_a_separate_reveal_screen()
        {
            Open(RoulettePrizeKind.RareBoon);
            var ui = (UIManager)Get(_runtime, "_ui");
            Assert.That(ui.LevelUp.IsVisible, Is.True);
            Assert.That(ui.PrizeReveal.IsVisible, Is.False);
            Assert.That(ui.LevelUp.transform.Find("Content/Grid/Card0"), Is.Not.Null);
        }

        [Test]
        public void Wild_card_leave_preserves_the_build_and_exports_a_decline_without_an_award()
        {
            var cards = (HashSet<WildCardId>)Get(_runtime, "_activeWildCards");
            foreach (WildCardId id in Enum.GetValues(typeof(WildCardId)))
                if (id != WildCardId.None && id != WildCardId.Greed && WildCardRules.IsImplemented(id)) cards.Add(id);
            Set(_runtime, "_partsEarned", 100);
            Open(RoulettePrizeKind.WildCard, 25);
            Assert.That(View.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.text.Contains("Magnet")), Is.True,
                "The rule-changing drawback must be visible before Take/Leave.");
            var staleTake = (Action)Get(View, "_onTake");
            RouletteClaimTestActions.ClaimOne(_runtime, false);
            staleTake();
            Assert.That(cards.Contains(WildCardId.Greed), Is.False);
            Assert.That(Get(_runtime, "_partsEarned"), Is.EqualTo(75), "Leaving is not a refund or another prize.");
            Assert.That(Get(_runtime, "_prizeRevealActive"), Is.False);
            Assert.That(Get(_runtime, "_paused"), Is.False);
            Call("FinishRunExport", "test_finished");
            var path = Directory.GetFiles(Path.Combine(_directory, "RunExports"), "*.jsonl").Single();
            var history = File.ReadAllLines(path).Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
            Assert.That(history.Count(e => e.kind == "roulette_declined"), Is.EqualTo(1));
            Assert.That(history.Any(e => e.kind == "roulette_claimed" || e.kind == "roulette_granted"), Is.False);
        }

        [Test]
        public void Normal_upgrade_waiting_under_reward_is_restored_and_cannot_be_selected_early()
        {
            var options = (UpgradeOptionDefinition[])Call("RollLevelOptions");
            Set(_runtime, "_levelOptions", options);
            Set(_runtime, "_levelUpActive", true);
            Set(_runtime, "_rerollsRemaining", 3);
            Open(RoulettePrizeKind.RareBoon);
            Call("RerollLevelOptions");
            Call("SelectLevelOption", 0);
            Assert.That(Get(_runtime, "_levelOptions"), Is.SameAs(options));
            Assert.That(Get(_runtime, "_rerollsRemaining"), Is.EqualTo(3));
            Assert.That(Get(_runtime, "_levelUpActive"), Is.True);
            Claim();
            Assert.That(Get(_runtime, "_levelUpActive"), Is.True);
            Assert.That(Get(_runtime, "_paused"), Is.True);
            Assert.That(View.transform.Find("Content/RerollRow").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Reset_tolerates_reward_views_already_destroyed_during_shutdown()
        {
            Open(RoulettePrizeKind.RareBoon);
            var ui = (UIManager)Get(_runtime, "_ui");
            var liveUpgrade = ui.LevelUp;
            var liveReveal = ui.PrizeReveal;
            var deadUpgrade = new GameObject("Destroyed upgrade view").AddComponent<LevelUpView>();
            var deadReveal = new GameObject("Destroyed reward view").AddComponent<PrizeRevealView>();
            UnityEngine.Object.DestroyImmediate(deadUpgrade.gameObject);
            UnityEngine.Object.DestroyImmediate(deadReveal.gameObject);
            try
            {
                typeof(UIManager).GetProperty("LevelUp", Flags).SetValue(ui, deadUpgrade);
                typeof(UIManager).GetProperty("PrizeReveal", Flags).SetValue(ui, deadReveal);
                Assert.DoesNotThrow(() => Call("ResetRouletteClaims"));
            }
            finally
            {
                typeof(UIManager).GetProperty("LevelUp", Flags).SetValue(ui, liveUpgrade);
                typeof(UIManager).GetProperty("PrizeReveal", Flags).SetValue(ui, liveReveal);
            }
        }
        private void Claim()
        {
            RouletteClaimTestActions.ClaimOne(_runtime);
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        private object Call(string name, params object[] args) => _runtime.GetType().GetMethods(Flags)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(_runtime, args);
    }
}
