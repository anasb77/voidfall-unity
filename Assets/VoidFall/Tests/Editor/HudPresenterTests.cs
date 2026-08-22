using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    public sealed class HudPresenterTests
    {
        private sealed class FakeSink : IHudViewSink
        {
            public readonly List<string> Calls = new List<string>();
            public float HealthFill;
            public float HealthGhostFill;
            public float XpFill;

            public void SetHudFade(float alpha, bool visible) { }
            public void SetHealthFill(float fraction) { HealthFill = fraction; }
            public void SetHealthGhostFill(float fraction) { HealthGhostFill = fraction; }
            public void SetHealthText(string text) { Calls.Add("health:" + text); }
            public void SetHealthValueText(string text) { Calls.Add("value:" + text); }
            public void SetXpFill(float fraction) { XpFill = fraction; }
            public void SetTimeText(string text) { Calls.Add("time:" + text); }
            public void SetLevelText(string text) { Calls.Add("level:" + text); }
            public void SetMetricsSummary(string text) { Calls.Add("metrics:" + text); }
            public void SetMetricValue(int index, string text) { Calls.Add("metric" + index + ":" + text); }
            public void SetBoostPanel(bool active, int powerTier, float fillFraction, float punch)
            {
                Calls.Add("boost:" + active + ":" + powerTier + ":" + fillFraction.ToString("F2"));
            }
            public void SetBossBar(bool visible, float fraction)
            {
                Calls.Add("bossbar:" + visible);
            }
            public void SetBossNameText(string text) { Calls.Add("bossname:" + text); }
            public void SetBossHealthText(string text) { Calls.Add("bosshp:" + text); }

            public int CountOf(string prefix)
            {
                var count = 0;
                foreach (var call in Calls)
                {
                    if (call.StartsWith(prefix)) count++;
                }
                return count;
            }

            public bool AnyCall(string exact)
            {
                return Calls.Contains(exact);
            }
        }

        private static HudSnapshot Snapshot(
            float health = 80f,
            float maxHealth = 100f,
            float time = 65f,
            int level = 3,
            int kills = 10,
            int parts = 2,
            int xp = 5,
            int xpNeed = 20,
            int score = 1000,
            bool overclockActive = false,
            int overclockTier = 0,
            int overclockStreak = 0,
            float overclockRemaining = 0f,
            int bosses = 0,
            float bossHealth = 0f,
            float bossMax = 0f,
            string bossName = "herald",
            bool visible = true)
        {
            return new HudSnapshot(
                health, maxHealth, time, level, kills, parts, xp, xpNeed, score,
                overclockActive, overclockTier, overclockStreak, overclockRemaining,
                bosses, bossHealth, bossMax, bossName, visible);
        }

        [Test]
        public void Identical_snapshots_rewrite_no_text()
        {
            var sink = new FakeSink();
            var presenter = new HudPresenter(sink);
            presenter.Tick(Snapshot(), 0.016f);
            var textCallsAfterFirst = CountTextCalls(sink);
            presenter.Tick(Snapshot(), 0.016f);
            Assert.That(CountTextCalls(sink), Is.EqualTo(textCallsAfterFirst),
                "an unchanged snapshot must not rewrite any label (VF-009)");
        }

        // Per-frame bar/fill writes are legitimate; VF-009 covers text only.
        private static int CountTextCalls(FakeSink sink)
        {
            var count = 0;
            foreach (var call in sink.Calls)
            {
                if (!call.StartsWith("boost:") && !call.StartsWith("bossbar:")) count++;
            }
            return count;
        }

        [Test]
        public void Health_change_rewrites_integrity_labels_once()
        {
            var sink = new FakeSink();
            var presenter = new HudPresenter(sink);
            presenter.Tick(Snapshot(health: 80f), 0.016f);
            presenter.Tick(Snapshot(health: 80f), 0.016f);
            presenter.Tick(Snapshot(health: 61f), 0.016f);
            Assert.That(sink.CountOf("health:"), Is.EqualTo(2), "first tick plus the change");
            Assert.That(sink.CountOf("value:61/100"), Is.EqualTo(1));
        }

        [Test]
        public void Clock_formats_M_SS_and_rewrites_once_per_second()
        {
            var sink = new FakeSink();
            var presenter = new HudPresenter(sink);
            presenter.Tick(Snapshot(time: 65f), 0.016f);
            presenter.Tick(Snapshot(time: 65.9f), 0.016f);
            presenter.Tick(Snapshot(time: 66.1f), 0.016f);
            Assert.That(sink.CountOf("time:1:05"), Is.EqualTo(1));
            Assert.That(sink.CountOf("time:1:06"), Is.EqualTo(1));
        }

        [Test]
        public void Ghost_bar_never_trails_below_health_and_snaps_on_heal()
        {
            var sink = new FakeSink();
            var presenter = new HudPresenter(sink);
            presenter.Tick(Snapshot(health: 100f, maxHealth: 100f), 0.016f);
            Assert.That(sink.HealthGhostFill, Is.EqualTo(1f).Within(1e-4));
            presenter.Tick(Snapshot(health: 50f, maxHealth: 100f), 0.016f);
            Assert.That(sink.HealthGhostFill, Is.GreaterThan(0.5f), "ghost lags above health");
            presenter.Tick(Snapshot(health: 50f, maxHealth: 100f), 0.5f);
            Assert.That(presenter.HealthGhostFraction, Is.LessThan(0.6f),
                "a long frame lets the ghost finish chasing down");
            presenter.Tick(Snapshot(health: 90f, maxHealth: 100f), 0.016f);
            Assert.That(sink.HealthGhostFill, Is.EqualTo(0.9f).Within(1e-4),
                "heal must snap the ghost up instantly");
        }

        [Test]
        public void Boss_bar_shows_header_pluralization_and_hides_when_clear()
        {
            var sink = new FakeSink();
            var presenter = new HudPresenter(sink);
            presenter.Tick(Snapshot(bosses: 1, bossHealth: 300f, bossMax: 1500f,
                bossName: "herald"), 0.016f);
            Assert.That(sink.AnyCall("bossname:HERALD"), Is.True);
            Assert.That(sink.AnyCall("bosshp:300"), Is.True);
            Assert.That(sink.HealthFill, Is.EqualTo(0.8f).Within(1e-4));

            presenter.Tick(Snapshot(bosses: 2, bossHealth: 900f, bossMax: 1500f,
                bossName: "herald"), 0.016f);
            Assert.That(sink.AnyCall("bossname:2 BOSSES"), Is.True);

            presenter.Tick(Snapshot(bosses: 0), 0.016f);
            Assert.That(sink.AnyCall("bossbar:False"), Is.True);
        }

        [Test]
        public void Overdrive_panel_activates_and_resets_when_inactive()
        {
            var sink = new FakeSink();
            var presenter = new HudPresenter(sink);
            presenter.Tick(Snapshot(overclockActive: true, overclockTier: 3,
                overclockStreak: 2, overclockRemaining: 30f), 0.016f);
            Assert.That(sink.AnyCall("boost:True:3:1.00"), Is.True,
                "two stacks = full bar denominator");

            presenter.Tick(Snapshot(overclockActive: false), 0.016f);
            Assert.That(sink.AnyCall("boost:False:0:0.00"), Is.True);
            Assert.That(presenter.OverclockPunch, Is.EqualTo(0f));
        }

        [Test]
        public void Xp_fill_clamps_to_need()
        {
            var sink = new FakeSink();
            var presenter = new HudPresenter(sink);
            presenter.Tick(Snapshot(xp: 5, xpNeed: 20), 0.016f);
            Assert.That(sink.XpFill, Is.EqualTo(0.25f).Within(1e-4));
            presenter.Tick(Snapshot(xp: 50, xpNeed: 20), 0.016f);
            Assert.That(sink.XpFill, Is.EqualTo(1f).Within(1e-4));
            presenter.Tick(Snapshot(xp: 5, xpNeed: 0), 0.016f);
            Assert.That(sink.XpFill, Is.EqualTo(0f).Within(1e-4));
        }
    }
}