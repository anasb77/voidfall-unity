using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the tracker that owns the active objective and its per-tick
    /// feed, plus the arena escape-condition factory.
    /// </summary>
    public sealed class VoidObjectiveTrackerTests
    {
        [Test]
        public void Arena_factory_maps_the_built_voids()
        {
            foreach (var voidId in new[] { "abyss", "red-nebula", "white-sakura", "hydra", "monochrome-court" })
            {
                var objective = VoidObjectives.ForArena(voidId);
                Assert.That(objective, Is.Not.Null, voidId + " must open a rift");
                Assert.That(objective, Is.InstanceOf<MultiPhaseObjective>(),
                    voidId + " composes survive + encounter phases");
            }

            // Layer II and beyond stay endless until their Voids are built.
            Assert.That(VoidObjectives.ForArena("null-city"), Is.Null);
            Assert.That(VoidObjectives.ForArena("last-gate"), Is.Null);
            Assert.That(VoidObjectives.ForArena(null), Is.Null);
        }

        [Test]
        public void Abyss_composition_matches_the_spec_shape()
        {
            var abyss = (MultiPhaseObjective)VoidObjectives.ForArena("abyss");
            Assert.That(abyss.PhaseCount, Is.EqualTo(2));
            Assert.That(abyss.CurrentPhase, Is.InstanceOf<SurviveObjective>());
            abyss.BeginObjective();
            Assert.That(abyss.GetObjectiveText(), Does.Contain("ABYSS"));
            Assert.That(abyss.GetObjectiveText(), Does.Contain("Phase 1/2"));
        }

        [Test]
        public void Feed_accumulates_in_report_order_and_resets_after_the_tick()
        {
            var tracker = new VoidObjectiveTracker();
            tracker.Begin(new ChargeWithKillsObjective(10, "Charge"));
            tracker.NotifyKill();
            tracker.NotifyKill();
            tracker.NotifyKill();
            tracker.Step(1.0 / 60.0);
            Assert.That(tracker.Progress01, Is.EqualTo(0.3).Within(1e-9));

            // No further reports: the batch was consumed, not accumulated.
            tracker.Step(1.0 / 60.0);
            Assert.That(tracker.Progress01, Is.EqualTo(0.3).Within(1e-9));

            tracker.NotifyKill();
            tracker.Step(1.0 / 60.0);
            Assert.That(tracker.Progress01, Is.EqualTo(0.4).Within(1e-9));
        }

        [Test]
        public void Named_events_route_to_killing_objectives()
        {
            var tracker = new VoidObjectiveTracker();
            tracker.Begin(new KillTargetObjective("herald", "Kill the Gatekeeper"));
            Assert.That(tracker.IsComplete, Is.False);

            tracker.NotifyNamedKilled("warden");
            tracker.Step(1.0 / 60.0);
            Assert.That(tracker.IsComplete, Is.False, "only the named target counts");

            tracker.NotifyNamedSpawned("herald");
            tracker.NotifyNamedKilled("herald");
            tracker.Step(1.0 / 60.0);
            Assert.That(tracker.IsComplete, Is.True);
            Assert.That(tracker.Text, Does.Contain("DOWN"));
        }

        [Test]
        public void Completed_objectives_stop_consuming_late_events()
        {
            var tracker = new VoidObjectiveTracker();
            tracker.Begin(new SurviveObjective(1, "Survive"));
            tracker.Step(1.0);
            Assert.That(tracker.IsComplete, Is.True);

            tracker.NotifyKill();
            tracker.Step(1.0);
            Assert.That(tracker.IsComplete, Is.True);
            Assert.That(tracker.Progress01, Is.EqualTo(1.0));
        }

        [Test]
        public void Every_built_void_survives_five_minutes_before_its_boss_encounter()
        {
            foreach (var voidId in new[] { "abyss", "red-nebula", "white-sakura", "hydra", "monochrome-court" })
            {
                var objective = (MultiPhaseObjective)VoidObjectives.ForArena(voidId);
                objective.BeginObjective();
                objective.TickObjective(299.75, new VoidObjectiveFeed());
                Assert.That(objective.PhaseIndex, Is.EqualTo(0), voidId);
                Assert.That(objective.GetObjectiveText(), Does.Contain("04:59"), voidId);

                objective.TickObjective(0.25, new VoidObjectiveFeed());
                Assert.That(objective.PhaseIndex, Is.EqualTo(1), voidId);
            }
        }

        [Test]
        public void Boss_encounter_completes_only_after_every_spawned_boss_dies()
        {
            var tracker = new VoidObjectiveTracker();
            tracker.Begin(VoidObjectives.ForArena("abyss"));
            tracker.Step(300);

            tracker.NotifyNamedSpawned("warden");
            tracker.NotifyNamedSpawned("matriarch");
            tracker.Step(0);
            Assert.That(tracker.Text, Does.Contain("Defeat the Void Boss"));

            tracker.NotifyNamedKilled("warden");
            tracker.Step(0);
            Assert.That(tracker.IsComplete, Is.False, "second boss still lives");

            tracker.NotifyNamedKilled("matriarch");
            tracker.Step(0);
            Assert.That(tracker.IsComplete, Is.True);
            Assert.That(tracker.Text, Does.Contain("COMPLETE"));
        }

        [Test]
        public void Clear_detaches_the_objective()
        {
            var tracker = new VoidObjectiveTracker();
            tracker.Begin(new SurviveObjective(60, "Survive"));
            tracker.NotifyKill();
            tracker.Step(1.0 / 60.0);
            Assert.That(tracker.HasObjective, Is.True);

            tracker.Clear();
            Assert.That(tracker.HasObjective, Is.False);
            Assert.That(tracker.IsComplete, Is.False);
            Assert.That(tracker.Text, Is.Null);

            // Late notifications after Clear are dropped, not thrown.
            tracker.NotifyKill();
            tracker.NotifyNamedKilled("herald");
            tracker.Step(1.0 / 60.0);
            Assert.That(tracker.HasObjective, Is.False);
        }
    }
}
