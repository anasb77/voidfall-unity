using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    public sealed class RouteMapViewTests
    {
        private GameObject _host;
        private RouteMapView _view;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Route map test", typeof(RectTransform));
            ((RectTransform)_host.transform).sizeDelta = new Vector2(1600f, 900f);
            _view = _host.AddComponent<RouteMapView>();
            _view.Initialize(null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void Map_shows_only_title_void_names_and_current_status()
        {
            _view.Show(Route(), null, null, null);

            Assert.That(VisibleLabels(), Is.EquivalentTo(new[]
            {
                "VOID MAP",
                "YOU ARE HERE",
                "STARTING VOID",
                "KNOWN VOID",
                "SECRET DESTINATION",
                "STRANDED VOID",
                "ESCAPE VOID",
            }));
        }

        [Test]
        public void Planning_future_node_reports_destination_without_entering_it()
        {
            var run = Route();
            string planned = null;
            _view.Show(run, null, id => planned = id, null);

            ButtonWithLabel("ESCAPE VOID").onClick.Invoke();

            Assert.That(planned, Is.EqualTo("escape-id"));
            Assert.That(run.CurrentVoidId, Is.EqualTo("start-id"));
            Assert.That(run.History, Is.EqualTo(new[] { "start-id" }));
            Assert.That(run.StateOf("escape-id"), Is.EqualTo(RouteNodeState.Hidden));
            Assert.That(run.StateOf("start-id"), Is.EqualTo(RouteNodeState.Selected));
            Assert.That(_view.IsVisible, Is.True);
        }

        [Test]
        public void Planning_node_highlights_connected_route_through_terminal()
        {
            _view.Show(Route(), null, _ => { }, null);

            ButtonWithLabel("KNOWN VOID").onClick.Invoke();

            var plannedColor = UITheme.WithAlpha(UITheme.GoldLight, 0.92f);
            Assert.That(ConnectionImages().Count(image => Approximately(image.color, plannedColor)), Is.EqualTo(2),
                "The selected node and its deterministic continuation to the terminal should form one route.");
        }

        [Test]
        public void Sealed_branch_and_its_unreachable_descendant_cannot_be_planned()
        {
            var run = Route();
            run.NotifyVoidCompleted("start-id");
            run.SelectNextVoid("known-id");
            var calls = 0;
            _view.Show(run, null, _ => calls++, null);

            var sealedBranch = ButtonWithLabel("SECRET DESTINATION");
            var stranded = ButtonWithLabel("STRANDED VOID");
            Assert.That(sealedBranch.interactable, Is.False);
            Assert.That(stranded.interactable, Is.False);
            sealedBranch.onClick.Invoke();
            stranded.onClick.Invoke();

            Assert.That(calls, Is.Zero);
            Assert.That(run.CurrentVoidId, Is.EqualTo("known-id"));
            Assert.That(ButtonWithLabel("ESCAPE VOID").interactable, Is.True,
                "A reconverging destination must remain reachable through the chosen branch.");
        }

        [Test]
        public void Map_never_discloses_objective_or_description_copy()
        {
            var run = Route();
            _view.Show(run, null, null, null);

            var visible = VisibleText();
            Assert.That(visible, Does.Not.Contain("Secret Boss").IgnoreCase);
            Assert.That(visible, Does.Not.Contain("conceals a threat").IgnoreCase);
            Assert.That(visible, Does.Not.Contain("VOLATILE"));
            Assert.That(visible, Does.Not.Contain("CLEARED"));
            Assert.That(visible, Does.Not.Contain("PLANNED"));
            Assert.That(visible, Does.Not.Contain("FUTURE"));
        }

        [Test]
        public void Thumbnail_uses_stable_arena_identity_when_route_node_id_is_unique()
        {
            var node = new VoidRouteNode(
                "route-red-branch",
                "red-nebula",
                "Red Nebula",
                0,
                1,
                "VOLATILE",
                "Description",
                "Objective",
                "Reward");
            _view.Show(new VoidRouteRun(new[] { node }, node.Id), null, null, null);

            var thumbnail = _host.GetComponentsInChildren<Image>()
                .Single(image => image.name == "Arena Thumbnail");
            Assert.That(thumbnail.sprite, Is.Not.Null);
        }

        private Button ButtonWithLabel(string label)
        {
            return _host.GetComponentsInChildren<Button>()
                .Single(button => button.GetComponentsInChildren<Text>()
                    .Any(text => text.text == label));
        }

        private string VisibleText()
        {
            return string.Join("\n", _host.GetComponentsInChildren<Text>()
                .Select(text => text.text));
        }

        private string[] VisibleLabels()
        {
            return _host.GetComponentsInChildren<Text>()
                .Select(text => text.text)
                .Where(text => !string.IsNullOrEmpty(text))
                .ToArray();
        }

        private Image[] ConnectionImages()
        {
            return _host.GetComponentsInChildren<Image>()
                .Where(image => image.name.StartsWith("Connection "))
                .ToArray();
        }

        private static bool Approximately(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.001f &&
                   Mathf.Abs(left.g - right.g) < 0.001f &&
                   Mathf.Abs(left.b - right.b) < 0.001f &&
                   Mathf.Abs(left.a - right.a) < 0.001f;
        }

        private static VoidRouteRun Route()
        {
            return new VoidRouteRun(new[]
            {
                Node("start-id", "Starting Void", 0, "known-id", "mystery-id"),
                Node("known-id", "Known Void", 1, "escape-id"),
                new VoidRouteNode("mystery-id", "Secret Destination", 1, 1.2,
                    "VOLATILE", "Secret Destination conceals a threat.",
                    "Defeat the Secret Boss", "Reward", "stranded-id") { IsMystery = true },
                Node("stranded-id", "Stranded Void", 2, "escape-id"),
                Node("escape-id", "Escape Void", 3)
            }, "start-id");
        }

        private static VoidRouteNode Node(string id, string name, int depth, params string[] outgoing)
        {
            return new VoidRouteNode(id, name, depth, 1, "BASELINE",
                "Keep moving through the hazard.", "Clear the objective", "Reward", outgoing);
        }
    }
}
