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
        public void Planning_future_node_reports_destination_without_entering_or_revealing_it()
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
        public void Sealed_branch_and_its_unreachable_descendant_cannot_be_planned()
        {
            var run = Route();
            run.NotifyVoidCompleted("start-id");
            run.SelectNextVoid("known-id");
            var calls = 0;
            _view.Show(run, null, _ => calls++, null);

            var sealedBranch = ButtonWithLabel("? UNKNOWN VOID");
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
        public void Planning_and_reopening_mystery_never_discloses_name_or_objective_until_entry()
        {
            var run = Route();
            string planned = null;
            _view.Show(run, null, id => planned = id, null);
            ButtonWithLabel("? UNKNOWN VOID").onClick.Invoke();
            _view.Show(run, planned, _ => { }, null);

            var visible = VisibleText();
            Assert.That(visible, Does.Not.Contain("Secret Destination").IgnoreCase);
            Assert.That(visible, Does.Not.Contain("Secret Boss").IgnoreCase);
            Assert.That(visible, Does.Not.Contain("mystery-id"));
            Assert.That(visible, Does.Contain("VOLATILE"));
            Assert.That(run.StateOf("mystery-id"), Is.EqualTo(RouteNodeState.Revealed));

            run.NotifyVoidCompleted("start-id");
            run.SelectNextVoid("mystery-id");
            _view.Show(run, null, null, null);

            Assert.That(VisibleText(), Does.Contain("SECRET DESTINATION"));
            Assert.That(ButtonWithLabel("SECRET DESTINATION").interactable, Is.False);
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
