using NUnit.Framework;
using UnityEditor;
using VoidFall.Core;
using VoidFall.Runtime;
namespace VoidFall.Tests.Editor
{
    public sealed class CrascendoTests
    {
        [Test] public void Stable_identity_and_shared_encounter_are_prepared()
        {
            Assert.That((int)ArenaId.Crascendo, Is.EqualTo(7));
            Assert.That(ArenaCatalogRules.StableId(ArenaId.Crascendo), Is.EqualTo("crascendo"));
            Assert.That(ArenaCatalogRules.LegacyArena("crascendo"), Is.EqualTo(ArenaId.Crascendo));
            Assert.That(PlayableVoidRoutes.Create(42).Node("crascendo").ObjectiveSummary, Does.Contain("random boss"));
            var plate = AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>("Assets/VoidFall/Generated/ArenaPackages/Crascendo/Plate.asset");
            Assert.That(plate.IsValidFor(ArenaId.Crascendo), Is.True); Assert.That(plate.CrascendoVisuals.IsValid, Is.True);
        }
        [Test] public void Intensity_finishes_once_without_looping_and_stays_at_boss_maximum()
        {
            Assert.That(CrascendoRules.Intensity(0, false), Is.Zero); Assert.That(CrascendoRules.Intensity(150, false), Is.EqualTo(.5f));
            Assert.That(CrascendoRules.Intensity(900, false), Is.EqualTo(1)); Assert.That(CrascendoRules.Intensity(10, true), Is.EqualTo(1));
            Assert.That(CrascendoRules.Growth(1), Is.EqualTo(1.2f)); Assert.That(CrascendoRules.Growth(20), Is.EqualTo(5)); Assert.That(CrascendoRules.Growth(999), Is.EqualTo(5));
        }
    }
}
