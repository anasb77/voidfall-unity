using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class VoidObjectiveTests
    {
        private static void Tick(IVoidObjective objective, double dt, in VoidObjectiveFeed feed)
        {
            objective.TickObjective(dt, feed);
        }

        [Test]
        public void Survive_completes_exactly_at_target_and_clamps_progress()
        {
            var objective = new SurviveObjective(180, "Survive");
            objective.BeginObjective();

            Tick(objective, 60, default(VoidObjectiveFeed));
            Assert.That(objective.IsComplete, Is.False);
            Assert.That(objective.Progress01, Is.EqualTo(1.0 / 3.0).Within(1e-9));

            Tick(objective, 120, default(VoidObjectiveFeed));
            Assert.That(objective.IsComplete, Is.True);
            Assert.That(objective.Progress01, Is.EqualTo(1.0));

            // Extra ticks after completion must not push progress past 1.
            Tick(objective, 30, default(VoidObjectiveFeed));
            Assert.That(objective.Progress01, Is.EqualTo(1.0));
            Assert.That(objective.GetObjectiveText(), Does.Contain("03:00"));
        }

        [Test]
        public void Capture_zone_pauses_when_outside_and_never_resets()
        {
            var objective = new CaptureZoneObjective(75, "Stabilize Rift");
            objective.BeginObjective();
            var feed = new VoidObjectiveFeed();

            feed.ZoneHoldSeconds = 30;
            Tick(objective, 30, feed);
            Assert.That(objective.Progress01, Is.EqualTo(0.4).Within(1e-9));

            // Outside the zone: time passes, progress holds (spec §13).
            feed.Reset();
            Tick(objective, 45, feed);
            Assert.That(objective.Progress01, Is.EqualTo(0.4).Within(1e-9));
            Assert.That(objective.IsComplete, Is.False);

            feed.ZoneHoldSeconds = 45;
            Tick(objective, 45, feed);
            Assert.That(objective.IsComplete, Is.True);
        }

        [Test]
        public void Destroy_targets_counts_and_ignores_overflow()
        {
            var objective = new DestroyTargetsObjective(3, "Gene Nodes");
            objective.BeginObjective();
            var feed = new VoidObjectiveFeed { StructuresDestroyed = 2 };

            Tick(objective, 1, feed);
            Assert.That(objective.Destroyed, Is.EqualTo(2));
            Assert.That(objective.IsComplete, Is.False);
            Assert.That(objective.GetObjectiveText(), Is.EqualTo("Gene Nodes: 2 / 3"));

            feed.Reset();
            feed.StructuresDestroyed = 5; // chain reaction overkill
            Tick(objective, 1, feed);
            Assert.That(objective.IsComplete, Is.True);
            Assert.That(objective.GetObjectiveText(), Is.EqualTo("Gene Nodes: 3 / 3"));
        }

        [Test]
        public void Kill_target_only_matches_its_named_id()
        {
            var objective = new KillTargetObjective("gatekeeper", "Kill the Gatekeeper");
            objective.BeginObjective();
            var feed = new VoidObjectiveFeed { KilledId = "elite-siege" };

            Tick(objective, 1, feed);
            Assert.That(objective.IsComplete, Is.False);

            feed.KilledId = "gatekeeper";
            Tick(objective, 1, feed);
            Assert.That(objective.IsComplete, Is.True);
        }

        [Test]
        public void Charge_with_kills_accumulates_feed_counts()
        {
            var objective = new ChargeWithKillsObjective(200, "Charge the Rift");
            objective.BeginObjective();
            var feed = new VoidObjectiveFeed { Kills = 120 };

            Tick(objective, 1, feed);
            feed.Reset();
            feed.Kills = 80;
            Tick(objective, 1, feed);
            Assert.That(objective.IsComplete, Is.True);
            Assert.That(objective.GetObjectiveText(), Does.Contain("200 kills"));
        }

        [Test]
        public void Boss_objective_requires_spawn_then_kill_in_order()
        {
            var objective = new BossObjective("hydra-prime", "Kill Hydra Prime");
            objective.BeginObjective();
            var feed = new VoidObjectiveFeed { KilledId = "hydra-prime" };

            // A kill before the spawn is not credited: the objective is
            // waiting for its own boss to enter.
            Tick(objective, 1, feed);
            Assert.That(objective.IsComplete, Is.False);

            feed.Reset();
            feed.SpawnedId = "hydra-prime";
            Tick(objective, 1, feed);
            Assert.That(objective.Spawned, Is.True);
            Assert.That(objective.IsComplete, Is.False);

            feed.Reset();
            feed.KilledId = "hydra-prime";
            Tick(objective, 1, feed);
            Assert.That(objective.IsComplete, Is.True);
        }

        [Test]
        public void Multi_phase_runs_children_in_order_without_sharing_a_feed()
        {
            var survive = new SurviveObjective(60, "Survive");
            var boss = new BossObjective("gatekeeper", "Kill the Gatekeeper");
            var objective = new MultiPhaseObjective("ABYSS", survive, boss);
            objective.BeginObjective();
            var feed = new VoidObjectiveFeed { Kills = 500, KilledId = "gatekeeper" };

            // Phase 1 ticking with a feed that would complete phase 2 must
            // not leak into phase 2: the boss has not spawned.
            Tick(objective, 60, feed);
            Assert.That(survive.IsComplete, Is.True);
            Assert.That(objective.IsComplete, Is.False);
            Assert.That(objective.PhaseIndex, Is.EqualTo(1));

            // Transition tick: phase 2 begins with this tick's batch (empty
            // here); the old batch from phase 1's final tick is gone.
            Tick(objective, 1, default(VoidObjectiveFeed));
            Assert.That(boss.Spawned, Is.False);

            feed.Reset();
            feed.SpawnedId = "gatekeeper";
            Tick(objective, 1, feed);
            feed.Reset();
            feed.KilledId = "gatekeeper";
            Tick(objective, 1, feed);

            Assert.That(objective.IsComplete, Is.True);
            Assert.That(objective.Progress01, Is.EqualTo(1.0));
            Assert.That(objective.GetObjectiveText(), Does.Contain("COMPLETE"));
        }

        [Test]
        public void Multi_phase_progress_averages_across_children()
        {
            var first = new SurviveObjective(100, "Survive");
            var second = new SurviveObjective(100, "Survive");
            var objective = new MultiPhaseObjective("", first, second);
            objective.BeginObjective();

            Tick(objective, 50, default(VoidObjectiveFeed));
            Assert.That(objective.Progress01, Is.EqualTo(0.25).Within(1e-9));

            Tick(objective, 50, default(VoidObjectiveFeed)); // first completes
            Tick(objective, 50, default(VoidObjectiveFeed)); // boundary batch begins + ticks phase 2
            Assert.That(objective.Progress01, Is.EqualTo(0.75).Within(1e-9));
        }

        [Test]
        public void Abyss_objective_from_the_spec_composes_cleanly()
        {
            // Spec §11: survive ~3 minutes, then the Gatekeeper enters.
            var abyss = new MultiPhaseObjective(
                "ABYSS",
                new SurviveObjective(180, "Survive"),
                new BossObjective("gatekeeper", "Kill the Gatekeeper"));
            abyss.BeginObjective();

            for (var tick = 0; tick < 600; tick++)
                Tick(abyss, 0.5, default(VoidObjectiveFeed));
            Assert.That(abyss.CurrentPhase, Is.InstanceOf<BossObjective>());

            var feed = new VoidObjectiveFeed { SpawnedId = "gatekeeper" };
            Tick(abyss, 0.5, feed);
            feed.Reset();
            feed.KilledId = "gatekeeper";
            Tick(abyss, 0.5, feed);
            Assert.That(abyss.IsComplete, Is.True);
        }
    }
}
