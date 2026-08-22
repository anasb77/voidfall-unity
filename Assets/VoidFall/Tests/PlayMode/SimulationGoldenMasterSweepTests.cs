using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    /// <summary>
    /// Multi-seed golden-master sweep. The pinned single-seed test locks the
    /// contract; this locks the property behind it: any seed, run twice
    /// through the full productionMax scenario, must reproduce the same
    /// state hash bit-exactly — which also proves ApplyStressScenario leaves
    /// no cross-run residue — and distinct seeds must diverge.
    ///
    /// A failure here with a green single-seed test means nondeterminism or
    /// dirty reset state that the canonical seed happens not to exercise.
    /// </summary>
    public sealed class SimulationGoldenMasterSweepTests
    {
        private const int SeedCount = 32;
        private const int Ticks = 600;
        private const double FixedDt = 1.0 / 60.0;
        private const uint BaseSeed = 0x5f1dc0deu;
        private const string ScenarioId = "productionMax";

        [UnityTest]
        public IEnumerator Production_max_is_bit_stable_across_32_seeds()
        {
            var runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null, "The parity probe did not create the game runtime.");
            yield return null;
            yield return null;

            SimulationGoldenMasterTests.PinHermeticPresentationState(runtime);

            var apply = typeof(VoidFallGameRuntime).GetMethod(
                "ApplyStressScenario",
                BindingFlags.Public | BindingFlags.Instance);
            var simulate = typeof(VoidFallGameRuntime).GetMethod(
                "Simulate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(apply, Is.Not.Null, "ApplyStressScenario disappeared; update this test.");
            Assert.That(simulate, Is.Not.Null, "Simulate(double) disappeared; update this test.");

            var seenHashes = new HashSet<ulong>();
            for (var index = 0; index < SeedCount; index++)
            {
                var seed = SeedFor(index);
                var first = RunScenario(runtime, apply, simulate, seed);
                var second = RunScenario(runtime, apply, simulate, seed);

                Assert.That(
                    second,
                    Is.EqualTo(first),
                    $"seed 0x{seed:X8} did not reproduce bit-exactly across two consecutive " +
                    "runs; the scenario reset leaves state behind or the sim is nondeterministic");

                Assert.That(
                    seenHashes.Add(first),
                    Is.True,
                    $"seed 0x{seed:X8} hashed identically to an earlier seed; the seed is " +
                    "not actually driving the simulation");
            }
            Assert.That(seenHashes.Count, Is.EqualTo(SeedCount));
            yield break;
        }

        private static uint SeedFor(int index)
        {
            if (index == 0) return BaseSeed;
            unchecked
            {
                return BaseSeed + 0x9e3779b9u * (uint)index;
            }
        }

        private static ulong RunScenario(
            VoidFallGameRuntime runtime,
            MethodInfo apply,
            MethodInfo simulate,
            uint seed)
        {
            Assert.That(
                (bool)apply.Invoke(runtime, new object[] { ScenarioId, seed }),
                Is.True,
                "The stress scenario could not be applied for seed 0x" + seed.ToString("X8"));
            for (var tick = 0; tick < Ticks; tick++)
                simulate.Invoke(runtime, new object[] { FixedDt });
            return SimulationGoldenMasterTests.HashRuntimeState(runtime);
        }
    }
}
