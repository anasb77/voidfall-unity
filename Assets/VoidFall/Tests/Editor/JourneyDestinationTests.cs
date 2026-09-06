using System.Reflection;
using NUnit.Framework;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.Editor
{
    public sealed class JourneyDestinationTests
    {
        [TestCase("crascendo", ArenaId.Crascendo)]
        [TestCase("eon-sea", ArenaId.EonSea)]
        [TestCase("hydra", ArenaId.Hydra)]
        public void Travel_resolves_the_actual_destination_instead_of_falling_back_to_Abyss(string id, ArenaId expected)
        {
            var resolver = typeof(VoidFallGameRuntime).GetMethod("ArenaIdForVoidId", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(resolver.Invoke(null, new object[] { id }), Is.EqualTo(expected));
        }
    }
}
