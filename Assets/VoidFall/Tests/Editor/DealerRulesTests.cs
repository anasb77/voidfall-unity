using System;
using NUnit.Framework;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Tests
{
    public sealed class DealerRulesTests
    {
        [Test]
        public void FragmentPurchaseCostsOneHundredAndCannotRepeat()
        {
            var session = new DealerSession(new[] { DealerRules.Fragment(LegendaryWeaponId.SoundBlade, 0) });
            Assert.That(session.TryBuy(0, 99, 0, 0, null, out _), Is.EqualTo(DealerPurchaseResult.Unaffordable));
            Assert.That(session.TryBuy(0, 100, 0, 0, _ => true, out var receipt), Is.EqualTo(DealerPurchaseResult.Success));
            Assert.That(receipt.WalletAfter, Is.Zero); Assert.That(receipt.SoundMask, Is.EqualTo(1));
            Assert.That(session.TryBuy(0, 100, 1, 0, null, out _), Is.EqualTo(DealerPurchaseResult.AlreadyBought));
        }
        [Test]
        public void FailedSaveAndDuplicateFragmentNeverCommitPurchase()
        {
            var session = new DealerSession(new[] { DealerRules.Fragment(LegendaryWeaponId.SoundBlade, 0) });
            Assert.That(session.TryBuy(0, 100, 0, 0, _ => false, out _), Is.EqualTo(DealerPurchaseResult.SaveFailed));
            Assert.That(session.PurchasedIndex, Is.EqualTo(-1));
            Assert.That(session.TryBuy(0, 100, 1, 0, _ => true, out _), Is.EqualTo(DealerPurchaseResult.DuplicateFragment));
        }
        [Test]
        public void ThreeDistinctFragmentsCompleteOnlyTheirOwnWeapon()
        {
            var mask = 0;
            for (var part = 0; part < 3; part++)
            {
                var session = new DealerSession(new[] { DealerRules.Fragment(LegendaryWeaponId.SoundBlade, part) });
                Assert.That(session.TryBuy(0, 100, mask, 0, _ => true, out var receipt), Is.EqualTo(DealerPurchaseResult.Success));
                mask = receipt.SoundMask; Assert.That(receipt.RifleMask, Is.Zero);
            }
            Assert.That(mask, Is.EqualTo(7)); Assert.That(DealerRules.MissingPiece(mask), Is.EqualTo(-1));
        }
        [Test]
        public void OldSaveDefaultsAndCorruptMasksDoNotGrantWeapons()
        {
            var save = SaveStore.CreateDefault();
            Assert.That(save.soundBladeFragments, Is.Zero); Assert.That(save.chargedRifleFragments, Is.Zero);
            save.soundBladeFragments = -1; save.chargedRifleFragments = 9;
            SaveStore.Sanitize(save);
            Assert.That(save.soundBladeFragments, Is.Zero); Assert.That(save.chargedRifleFragments, Is.EqualTo(1));
        }
        [TestCase(239.9, .8)] [TestCase(240, 1.4)]
        public void DelayedPowerChangesAtFourCombatMinutes(double elapsed, double expected)
            => Assert.That(DealerRules.DelayedMultiplier(true, elapsed), Is.EqualTo(expected));
        [Test]
        public void ExportAndImportPreservePermanentFragments()
        {
            var save = SaveStore.CreateDefault(); save.soundBladeFragments = 7; save.chargedRifleFragments = 3;
            Assert.That(BrowserSaveImporter.TryConvert(BrowserSaveExporter.Export(save), out var loaded), Is.True);
            Assert.That(loaded.soundBladeFragments, Is.EqualTo(7)); Assert.That(loaded.chargedRifleFragments, Is.EqualTo(3));
        }
        [Test]
        public void ExhaustedCombatEligibilityStillOffersThreeDistinctUsefulDeals()
        {
            var offers = DealerRules.CreateOffers(1, 0, 0, LegendaryWeaponId.None, 0, -1, "", true, true, true, true);
            Assert.That(offers.Length, Is.EqualTo(3));
            var ids = Array.ConvertAll(offers, DealerRules.Id);
            Assert.That(new System.Collections.Generic.HashSet<string>(ids).Count, Is.EqualTo(3));
            Assert.That(Array.Exists(offers, o => o.Kind == DealerOfferKind.RecoveryPlan), Is.True);
        }
        [Test]
        public void RecentRunDamageRetainsManualWeaponAttribution()
        {
            var save = SaveStore.CreateDefault();
            save.recentRuns = new[] { new RunRecordEntry { date = 1, weaponDamage = new[] {
                new WeaponDamageEntry { id = "sound-blade", damage = 38 }, new WeaponDamageEntry { id = "charged-rifle", damage = 310 } } } };
            SaveStore.Sanitize(save);
            Assert.That(save.recentRuns[0].weaponDamage.Length, Is.EqualTo(2));
        }
        [Test]
        public void ShieldAbsorbsBeforeHpAndDoesNotManufactureDamage()
        {
            var shield = 20f;
            Assert.That(DealerRules.Absorb(ref shield, 15f), Is.Zero); Assert.That(shield, Is.EqualTo(5));
            Assert.That(DealerRules.Absorb(ref shield, 12f), Is.EqualTo(7)); Assert.That(shield, Is.Zero);
        }
    }
}
