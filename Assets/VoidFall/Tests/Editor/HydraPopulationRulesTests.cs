using System.Linq;
using NUnit.Framework;
using VoidFall.Core;
namespace VoidFall.Tests.Editor
{
    public sealed class HydraPopulationRulesTests
    {
        [Test]
        public void Ten_approved_specimens_have_stable_names_and_parent_pairs()
        {
            var kinds=Enumerable.Range(0,HydraPopulationRules.Count).ToArray();
            Assert.That(kinds.Select(HydraPopulationRules.Name),Is.EqualTo(new[]{"Cleft","Hook","Rachis","Bloat","Graft","Bastion","Aegis","Riftkin","Reclaimer","Broodsmith"}));
            Assert.That(kinds.Select(HydraPopulationRules.StableId).Distinct().Count(),Is.EqualTo(10));
            Assert.That(kinds.Count(HydraPopulationRules.IsVirus),Is.EqualTo(5));
            Assert.That(kinds.Skip(5).Select(HydraPopulationRules.Parents),Is.EqualTo(new[]{"brute+gunner","runner+guard","dasher+splitter","harvester+mortar","technician+carrier"}));
            Assert.That(HydraPopulationRules.BaseId(8),Is.EqualTo("harvester"));
        }
        [Test]
        public void Selection_cycles_without_rng_and_split_capabilities_are_explicit()
        {
            Assert.That(Enumerable.Range(0,20).Select(HydraPopulationRules.KindForAttempt),Is.EqualTo(Enumerable.Range(0,10).Concat(Enumerable.Range(0,10))));
            Assert.That(Enumerable.Range(0,10).Where(HydraPopulationRules.Splits),Is.EqualTo(new[]{0,7}));
            Assert.That(HydraPopulationRules.RepairDroneLimit,Is.EqualTo(2));
            Assert.That(HydraPopulationRules.IncomingDamageMultiplier(6,1),Is.EqualTo(.15f));
            Assert.That(HydraPopulationRules.IncomingDamageMultiplier(6,-1),Is.EqualTo(1f));
            Assert.That(HydraPopulationRules.IncomingDamageMultiplier(5,1),Is.EqualTo(1f));
        }
    }
}
