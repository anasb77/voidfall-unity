using NUnit.Framework;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    /// <summary>
    /// Covers the route-selection domain: card projection of the pending
    /// choice set, guarded confirmation with per-state notices, banners per
    /// layer, and route-history projection.
    /// </summary>
    public sealed class RouteSelectControllerTests
    {
        private static VoidRouteRun CompletedStart()
        {
            var run = VoidRouteRun.PrototypeGraph();
            run.NotifyVoidCompleted("abyss");
            return run;
        }

        [Test]
        public void Choice_set_projects_full_route_cards()
        {
            var controller = new RouteSelectController();
            var run = CompletedStart();

            var cards = controller.BuildCards(run);

            Assert.That(cards.Count, Is.EqualTo(3));
            foreach (var card in cards)
            {
                Assert.That(card.Selectable, Is.True, card.Id + " must be choosable");
                Assert.That(card.StateLabel, Is.EqualTo("AVAILABLE"));
                Assert.That(card.ThreatLabel, Is.Not.Empty, card.Id + " needs a threat label");
                Assert.That(card.ObjectiveSummary, Is.Not.Empty);
                Assert.That(card.RewardSummary, Is.Not.Empty);
                Assert.That(card.Depth, Is.EqualTo(1));
                Assert.That(card.ThreatMultiplier, Is.EqualTo(1.20));
            }
            // Sorted for stable focus order.
            Assert.That(cards[0].Id, Is.EqualTo("hydra"));
            Assert.That(cards[1].Id, Is.EqualTo("red-nebula"));
            Assert.That(cards[2].Id, Is.EqualTo("white-sakura"));
        }

        [Test]
        public void No_cards_while_a_void_is_in_progress()
        {
            var controller = new RouteSelectController();
            var run = VoidRouteRun.PrototypeGraph();

            Assert.That(controller.BuildCards(run).Count, Is.EqualTo(0),
                "abyss has not been completed; layer I is preview only");
            Assert.That(controller.BuildBanner(run),
                Is.EqualTo("ABYSS — THE DESCENT BEGINS"));
        }

        [Test]
        public void Choosing_seals_the_siblings_and_presents_the_next_set()
        {
            var controller = new RouteSelectController();
            var run = CompletedStart();

            Assert.That(controller.Confirm(run, "hydra", out var notice), Is.True);
            Assert.That(notice, Is.EqualTo("Entering Hydra."));
            Assert.That(run.CurrentVoidId, Is.EqualTo("hydra"));

            // Mid-void: no pending choice.
            Assert.That(controller.BuildCards(run).Count, Is.EqualTo(0));

            run.NotifyVoidCompleted("hydra");
            var cards = controller.BuildCards(run);
            Assert.That(cards.Count, Is.EqualTo(2));
            Assert.That(cards[0].Id, Is.EqualTo("monochrome-court"));
            Assert.That(cards[1].Id, Is.EqualTo("null-city"));
            Assert.That(controller.BuildBanner(run), Is.EqualTo("LAYER II — THE LABYRINTH DEEPENS"));
        }

        [Test]
        public void Hidden_revealed_and_locked_voids_fail_with_distinct_notices()
        {
            var controller = new RouteSelectController();
            var run = CompletedStart();

            Assert.That(controller.Confirm(run, "null-city", out var hidden), Is.False);
            Assert.That(hidden, Does.Contain("beyond the veil"));
            Assert.That(controller.Confirm(run, "abyss", out var cleared), Is.False);
            Assert.That(cleared, Does.Contain("conquered"));

            // Layer I previews start as revealed (not available) before the
            // start void completes.
            var fresh = VoidRouteRun.PrototypeGraph();
            Assert.That(controller.Confirm(fresh, "hydra", out var revealed), Is.False);
            Assert.That(revealed, Does.Contain("rift is not open"));

            var chosen = CompletedStart();
            controller.Confirm(chosen, "hydra", out _);
            Assert.That(controller.Confirm(chosen, "red-nebula", out var locked), Is.False);
            Assert.That(locked, Does.Contain("sealed"));
            Assert.That(chosen.CurrentVoidId, Is.EqualTo("hydra"), "failed confirms must not move the run");
        }

        [Test]
        public void Locked_sibling_rides_along_greyed_out_in_later_layers()
        {
            var controller = new RouteSelectController();
            var run = CompletedStart();
            controller.Confirm(run, "red-nebula", out _);
            run.NotifyVoidCompleted("red-nebula");

            var cards = controller.BuildCards(run);
            Assert.That(cards.Count, Is.EqualTo(2));
            foreach (var card in cards)
            {
                Assert.That(card.Selectable, Is.True);
                Assert.That(card.Depth, Is.EqualTo(2));
            }

            // Choosing dead-orbit seals null-city; if the screen reopens the
            // sealed sibling appears as a non-selectable card.
            controller.Confirm(run, "dead-orbit", out _);
            run.NotifyVoidCompleted("dead-orbit");
            var gateCards = controller.BuildCards(run);
            Assert.That(gateCards.Count, Is.EqualTo(1));
            Assert.That(gateCards[0].Id, Is.EqualTo("last-gate"));
            Assert.That(controller.BuildBanner(run), Is.EqualTo("THE LAST GATE AWAITS"));
        }

        [Test]
        public void Route_line_spells_out_the_journey()
        {
            var controller = new RouteSelectController();
            var run = CompletedStart();
            Assert.That(controller.BuildRouteLine(run), Is.EqualTo("ABYSS"));

            controller.Confirm(run, "hydra", out _);
            run.NotifyVoidCompleted("hydra");
            controller.Confirm(run, "null-city", out _);
            Assert.That(controller.BuildRouteLine(run),
                Is.EqualTo("ABYSS → HYDRA → NULL CITY"));
        }

        [Test]
        public void Final_void_banner_and_escape()
        {
            var controller = new RouteSelectController();
            var run = CompletedStart();
            controller.Confirm(run, "hydra", out _);
            run.NotifyVoidCompleted("hydra");
            controller.Confirm(run, "null-city", out _);
            run.NotifyVoidCompleted("null-city");
            controller.Confirm(run, "last-gate", out _);
            run.NotifyVoidCompleted("last-gate");

            var cards = controller.BuildCards(run);
            Assert.That(cards.Count, Is.EqualTo(1));
            Assert.That(cards[0].Id, Is.EqualTo("final-void"));
            Assert.That(controller.BuildBanner(run), Is.EqualTo("THE FINAL VOID"));
        }
    }
}
