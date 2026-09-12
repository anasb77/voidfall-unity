using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class DirectorDiagnosticsTests
    {
        [UnityTest]
        public IEnumerator Active_benchmark_exposes_progress_and_does_not_leave_an_upgrade_pause()
        {
            var runtime = Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(runtime, Is.Not.Null);
            yield return null;
            using var profile = new SimulationProfileScope(runtime);
            var type = runtime.GetType();
            var prepare = type.GetMethod("PrepareBenchmarkFrame");
            var ticks = type.GetProperty("DiagnosticSimulationTicks");
            var seconds = type.GetProperty("DiagnosticCombatSeconds");
            var damage = type.GetProperty("DiagnosticDamageDealt");
            Assert.That(prepare, Is.Not.Null, "Benchmark must explicitly handle diagnostic upgrade prompts.");
            Assert.That(ticks, Is.Not.Null, "Wall time cannot establish combat advancement.");
            Assert.That(seconds, Is.Not.Null);
            Assert.That(damage, Is.Not.Null);
            Assert.That(runtime.ApplyStressScenario("productionMax", 0x5f1dc0deu), Is.True);
            var simulate = type.GetMethod("Simulate", BindingFlags.Instance | BindingFlags.NonPublic);
            var start = (float)seconds.GetValue(runtime);
            var paused = type.GetField("_paused", BindingFlags.NonPublic | BindingFlags.Instance);
            var blockedFrames = 0;
            for (var step = 0; step < 1200; step++)
            {
                prepare.Invoke(runtime, null);
                if ((bool)paused.GetValue(runtime)) blockedFrames++;
                else simulate.Invoke(runtime, new object[] { 1.0 / 60.0 });
            }
            prepare.Invoke(runtime, null);
            Debug.Log("Director diagnostic fixture: combat delta=" + ((float)seconds.GetValue(runtime) - start) +
                " ticks=" + ticks.GetValue(runtime) + " damage=" + damage.GetValue(runtime) + " blocked=" + blockedFrames);
            Assert.That(blockedFrames, Is.Zero, "A diagnostic upgrade prompt must not stall the real Update gate.");
            // Real combat includes deliberate hit-stop; zero-dt steps are not active ticks.
            Assert.That((long)ticks.GetValue(runtime), Is.GreaterThan(0));
            Assert.That((float)seconds.GetValue(runtime), Is.GreaterThan(start + 1f));
            Assert.That((double)damage.GetValue(runtime), Is.GreaterThan(0), "The benchmark must run attacking combat.");
            Assert.That((bool)type.GetField("_levelUpActive", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime), Is.False);
        }
    }
}
