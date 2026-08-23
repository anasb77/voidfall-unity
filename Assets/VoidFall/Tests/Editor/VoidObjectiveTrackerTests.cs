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
            var abyss = VoidObjectives.ForArena("abyss");
            Assert.That(abyss, Is.Not.Null, "the mandatory beginning must have an objective");
            Assert.That(abyss, Is.InstanceOf<MultiPhaseObjective>());

            // Voids without a built escape condition stay endless.
            Assert.That(VoidObjectives.ForArena("red-nebula"), Is.Null);
            Assert.That(VoidObjectives.ForArena("white-sakura"), Is.Null);
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
        public void End_to_end_abyss_flow_reaches_checkmate_style_completion()
        {
            var tracker = new VoidObjectiveTracker();
            tracker.Begin(VoidObjectives.ForArena("abyss"));

            // 180 seconds of opening escalation at 4 Hz (0.25 s is exact in
            // binary, so the survive phase completes without float drift);
            // kills happen but only the boss matters for the escape.
            var dt = 0.25;
            for (var tick = 0; tick < 720; tick++)
            {
                tracker.NotifyKill();
                if (tick == 480) tracker.NotifyNamedSpawned("herald");
                tracker.Step(dt);
            }
            Assert.That(tracker.IsComplete, Is.False, "the Gatekeeper still lives");
            Assert.That(tracker.Text, Does.Contain("Kill the Gatekeeper"));

            // The Herald dies only after the survive phase completes: the
            // kill lands in phase 2 and completes the escape.
            tracker.NotifyNamedKilled("herald");
            tracker.Step(dt);
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
