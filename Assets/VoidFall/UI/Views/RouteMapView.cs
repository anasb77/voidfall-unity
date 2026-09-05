using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>A paused route overview. Planning is presentation only; portals commit travel.</summary>
    public sealed class RouteMapView : UIViewBase
    {
        private const float PanelWidth = 1160f;
        private const float PanelHeight = 840f;
        private const float CardWidth = 404f;
        private readonly List<NodeWidgets> _cards = new List<NodeWidgets>();
        private readonly List<EdgeWidgets> _edges = new List<EdgeWidgets>();
        private readonly Dictionary<string, NodeWidgets> _byId = new Dictionary<string, NodeWidgets>(StringComparer.Ordinal);
        private readonly HashSet<string> _reachable = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<VoidRouteNode> _ordered = new List<VoidRouteNode>();
        private readonly List<int> _depths = new List<int>();
        private RectTransform _panel;
        private RectTransform _nodeLayer;
        private RectTransform _edgeLayer;
        private Text _current;
        private Text _progress;
        private Text _plan;
        private VoidRouteRun _run;
        private string _plannedId;
        private Action<string> _onPlan;
        private Action _onClose;

        private sealed class NodeWidgets
        {
            public VoidRouteNode Node;
            public RectTransform Root;
            public Image Fill;
            public Image Border;
            public Image Accent;
            public Text Name;
            public Text State;
            public Text Hint;
            public Button Button;
            public bool Sealed;
            public bool Visited;
            public bool Current;
            public bool CanPlan;
            public bool Mystery;
        }

        private sealed class EdgeWidgets
        {
            public Image Line;
            public NodeWidgets From;
            public NodeWidgets To;
            public bool Traversed;
        }

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Scrim", UITheme.WithAlpha(UITheme.Void, 0.9f));
            _panel = UIBuilder.CreatePanel(Root, "Route Map", new Vector2(PanelWidth, PanelHeight));
            Label(_panel, "Title", "VOID MAP", 30f, Color.white,
                new Vector2(-300f, 369f), new Vector2(448f, 48f), FontStyle.Bold);
            _progress = Label(_panel, "Progress", string.Empty, 14f, UITheme.CyanPale,
                new Vector2(300f, 370f), new Vector2(448f, 30f), FontStyle.Bold, TextAnchor.MiddleRight);
            _current = Label(_panel, "Current Void", string.Empty, 13f, UITheme.CyanLight,
                new Vector2(-220f, 333f), new Vector2(608f, 24f));
            Label(_panel, "Pause Status", "PAUSED  /  PLAN YOUR ESCAPE", 11f, UITheme.TextStrong,
                new Vector2(310f, 333f), new Vector2(428f, 24f), FontStyle.Normal, TextAnchor.MiddleRight);
            Rule("Header Rule", 311f);
            Label(_panel, "Escape", "ESCAPE", 11f, UITheme.CyanPale,
                new Vector2(0f, 294f), new Vector2(300f, 20f), FontStyle.Bold, TextAnchor.MiddleCenter);

            // Separate layers keep every connection behind every node, including pooled additions.
            _edgeLayer = UIBuilder.Stretch(UIBuilder.CreateRect(_panel, "Connections"));
            _nodeLayer = UIBuilder.Stretch(UIBuilder.CreateRect(_panel, "Voids"));

            Rule("Footer Rule", -294f);
            Legend("Current", UITheme.Cyan, -452f);
            Legend("Cleared path", UITheme.CyanLight, -242f);
            Legend("Future", UITheme.WithAlpha(UITheme.TextStrong, 0.7f), -20f);
            Legend("Sealed", UITheme.WithAlpha(UITheme.TextStrong, 0.28f), 150f);
            Legend("Planned", UITheme.GoldLight, 324f);
            _plan = Label(_panel, "Plan", string.Empty, 13f, UITheme.TextStrong,
                new Vector2(-130f, -349f), new Vector2(788f, 24f));
            Label(_panel, "Travel Hint", "Mark a destination. Enter its portal to travel.", 12f,
                UITheme.WithAlpha(UITheme.TextStrong, 0.72f), new Vector2(-130f, -377f), new Vector2(788f, 22f));
            var close = UIBuilder.CreateSecondaryAction(_panel, "Close Map", "TAB / ESC   CLOSE", string.Empty, Close, 46f);
            Place(close.GetComponent<RectTransform>(), new Vector2(404f, -366f), new Vector2(240f, 46f));
        }

        public void Show(VoidRouteRun run, string plannedId, Action<string> onPlan, Action onClose)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            _run = run;
            _plannedId = plannedId;
            _onPlan = onPlan;
            _onClose = onClose;
            _ordered.Clear();
            _depths.Clear();
            foreach (var node in run.Nodes) _ordered.Add(node);
            _ordered.Sort((a, b) =>
            {
                var depth = a.Depth.CompareTo(b.Depth);
                return depth != 0 ? depth : string.CompareOrdinal(a.Id, b.Id);
            });
            foreach (var node in _ordered)
                if (_depths.Count == 0 || _depths[_depths.Count - 1] != node.Depth) _depths.Add(node.Depth);

            FindReachable();
            LayoutNodes();
            LayoutEdges();
            RefreshPlan();

            var cleared = 0;
            foreach (var node in _ordered)
                if (run.StateOf(node.Id) == RouteNodeState.Completed) cleared++;
            _current.text = "CURRENT  /  " + run.Node(run.CurrentVoidId).DisplayName.ToUpperInvariant();
            _progress.text = cleared + " / " + _depths.Count + " VOIDS CLEARED";
            FitPanel();
            SetVisible(true);
        }

        private void FindReachable()
        {
            _reachable.Clear();
            var pending = new Stack<string>();
            pending.Push(_run.CurrentVoidId);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (_run.StateOf(id) == RouteNodeState.Locked || !_reachable.Add(id)) continue;
                foreach (var child in _run.Node(id).Outgoing) pending.Push(child);
            }
        }

        private void LayoutNodes()
        {
            _byId.Clear();
            var compact = _depths.Count >= 6;
            var height = compact ? 78f : 96f;
            for (var index = 0; index < _ordered.Count; index++)
            {
                if (_cards.Count <= index) _cards.Add(BuildNode(index));
                var card = _cards[index];
                var node = _ordered[index];
                var row = _depths.IndexOf(node.Depth);
                var first = index;
                while (first > 0 && _ordered[first - 1].Depth == node.Depth) first--;
                var last = index;
                while (last + 1 < _ordered.Count && _ordered[last + 1].Depth == node.Depth) last++;
                var count = last - first + 1;
                var width = count <= 2 ? CardWidth : Mathf.Min(CardWidth, 1008f / count - 20f);
                var x = (index - first - (count - 1) * 0.5f) * (width + 76f);
                var y = _depths.Count <= 1 ? 0f : Mathf.Lerp(-230f, 233f, row / (float)(_depths.Count - 1));
                Place(card.Root, new Vector2(x, y), new Vector2(width, height));
                card.Root.gameObject.SetActive(true);
                card.Node = node;
                var state = _run.StateOf(node.Id);
                card.Current = node.Id == _run.CurrentVoidId;
                card.Visited = state == RouteNodeState.Selected || state == RouteNodeState.Completed;
                card.Sealed = state == RouteNodeState.Locked || (!card.Visited && !_reachable.Contains(node.Id));
                card.CanPlan = !card.Visited && !card.Sealed && _reachable.Contains(node.Id);
                card.Mystery = node.IsMystery && !card.Visited;
                card.Name.text = card.Mystery ? "? UNKNOWN VOID" : node.DisplayName.ToUpperInvariant();
                // Objective and description can name an exclusive boss, so neither is shown for a mystery.
                card.Hint.text = card.Mystery
                    ? node.ThreatLabel + "  /  Revealed on entry"
                    : ShortHint(string.IsNullOrEmpty(node.Description) ? node.ObjectiveSummary : node.Description, compact ? 47 : 88);
                card.Button.interactable = card.CanPlan;
                var top = height * 0.5f;
                Place(card.State.rectTransform, new Vector2(0f, top - 15f), new Vector2(width - 32f, 18f));
                Place(card.Name.rectTransform, new Vector2(0f, top - 39f), new Vector2(width - 32f, 28f));
                Place(card.Hint.rectTransform, new Vector2(0f, compact ? -24f : -28f), new Vector2(width - 32f, compact ? 20f : 34f));
                card.Hint.alignment = compact ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
                _byId.Add(node.Id, card);
            }
            for (var index = _ordered.Count; index < _cards.Count; index++) _cards[index].Root.gameObject.SetActive(false);
        }

        private NodeWidgets BuildNode(int index)
        {
            var root = UIBuilder.CreateRect(_nodeLayer, "Void " + index);
            var fill = UIBuilder.CreateSurface(root, "Fill", UISprites.Rounded(
                UITheme.RadiusCard, Color.white, Color.white, Color.clear), true);
            UIBuilder.Stretch(fill.rectTransform);
            var border = UIBuilder.CreateSurface(root, "Border", UISprites.Rounded(
                UITheme.RadiusCard, Color.clear, Color.clear, Color.white, 2f));
            UIBuilder.Stretch(border.rectTransform);
            var accent = UIBuilder.CreateFill(root, "State Marker", UITheme.Cyan);
            accent.rectTransform.anchorMin = new Vector2(0f, 0.18f);
            accent.rectTransform.anchorMax = new Vector2(0f, 0.82f);
            accent.rectTransform.anchoredPosition = new Vector2(1f, 0f);
            accent.rectTransform.sizeDelta = new Vector2(3f, 0f);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.45f, 1.45f, 1.45f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(1.8f, 1.8f, 1.8f, 1f);
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            var card = new NodeWidgets
            {
                Root = root, Fill = fill, Border = border, Accent = accent, Button = button,
                State = Label(root, "State", string.Empty, 10.5f, UITheme.CyanLight, Vector2.zero, Vector2.zero, FontStyle.Bold),
                Name = Label(root, "Name", string.Empty, 19f, Color.white, Vector2.zero, Vector2.zero, FontStyle.Bold),
                Hint = Label(root, "Mechanic", string.Empty, 12f, UITheme.TextStrong, Vector2.zero, Vector2.zero)
            };
            card.Hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            button.onClick.AddListener(() => Plan(card));
            return card;
        }

        private void LayoutEdges()
        {
            var index = 0;
            foreach (var node in _ordered)
            {
                foreach (var child in node.Outgoing)
                {
                    if (!_byId.TryGetValue(child, out var to)) continue;
                    if (_edges.Count <= index)
                        _edges.Add(new EdgeWidgets { Line = UIBuilder.CreateFill(_edgeLayer, "Connection " + index, Color.white) });
                    var edge = _edges[index++];
                    edge.From = _byId[node.Id];
                    edge.To = to;
                    edge.Traversed = Traversed(node.Id, child);
                    edge.Line.gameObject.SetActive(true);
                    var direction = to.Root.anchoredPosition - edge.From.Root.anchoredPosition;
                    var start = edge.From.Root.anchoredPosition + Boundary(edge.From.Root, direction);
                    var end = to.Root.anchoredPosition + Boundary(to.Root, -direction);
                    var delta = end - start;
                    Place(edge.Line.rectTransform, (start + end) * 0.5f, new Vector2(delta.magnitude, edge.Traversed ? 3f : 2f));
                    edge.Line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
            }
            for (; index < _edges.Count; index++) _edges[index].Line.gameObject.SetActive(false);
        }

        private bool Traversed(string from, string to)
        {
            for (var index = 1; index < _run.History.Count; index++)
                if (_run.History[index - 1] == from && _run.History[index] == to) return true;
            return false;
        }

        private void RefreshPlan()
        {
            if (!_byId.TryGetValue(_plannedId ?? string.Empty, out var planned) || !planned.CanPlan) planned = null;
            foreach (var card in _cards)
            {
                if (!card.Root.gameObject.activeSelf) continue;
                var focused = card == planned;
                var accent = card.Current ? UITheme.Cyan : focused ? UITheme.GoldLight
                    : card.Visited ? UITheme.CyanLight : UITheme.TextStrong;
                card.Fill.color = card.Current ? UITheme.Rgba(8, 42, 54, 1f)
                    : focused ? UITheme.Rgba(43, 37, 23, 1f) : UITheme.Rgba(11, 17, 29, 1f);
                card.Border.color = UITheme.WithAlpha(accent,
                    card.Current || focused ? 1f : card.Sealed ? 0.12f : card.Visited ? 0.65f : 0.32f);
                card.Accent.color = UITheme.WithAlpha(accent, card.Sealed ? 0.16f : 1f);
                card.Name.color = UITheme.WithAlpha(card.Current ? UITheme.CyanPale : UITheme.TextStrong, card.Sealed ? 0.35f : 1f);
                card.Hint.color = UITheme.WithAlpha(UITheme.TextStrong, card.Sealed ? 0.26f : 0.78f);
                card.State.color = UITheme.WithAlpha(accent, card.Sealed ? 0.35f : 0.95f);
                var state = card.Current ? "YOU ARE HERE" : card.Visited ? "CLEARED" : card.Sealed ? "SEALED"
                    : focused ? "PLANNED" : _run.Node(_run.CurrentVoidId).Outgoing.Contains(card.Node.Id) ? "NEXT CHOICE" : "FUTURE";
                card.State.text = state + (card.Node.Outgoing.Count == 0 ? "  /  ESCAPE" : string.Empty)
                    + (card.Current && _run.StateOf(card.Node.Id) == RouteNodeState.Completed ? "  /  CLEARED" : string.Empty);
            }
            foreach (var edge in _edges)
            {
                if (!edge.Line.gameObject.activeSelf) continue;
                var sealedEdge = edge.From.Sealed || edge.To.Sealed || (edge.From.Visited && !edge.From.Current && !edge.Traversed);
                edge.Line.color = edge.Traversed ? UITheme.WithAlpha(UITheme.Cyan, 0.95f)
                    : sealedEdge ? UITheme.WithAlpha(UITheme.TextStrong, 0.09f)
                    : edge.To == planned ? UITheme.WithAlpha(UITheme.GoldLight, 0.78f)
                    : UITheme.WithAlpha(UITheme.CyanLight, edge.From.Current ? 0.48f : 0.24f);
            }
            _plan.text = planned == null ? "Choose a future Void to mark your route."
                : "PLANNED  /  " + (planned.Mystery ? "? UNKNOWN VOID" : planned.Node.DisplayName.ToUpperInvariant());
            _plan.color = planned == null ? UITheme.TextStrong : UITheme.GoldLight;
        }

        private void Plan(NodeWidgets card)
        {
            if (!IsVisible || !card.CanPlan) return;
            _plannedId = card.Node.Id;
            RefreshPlan();
            _onPlan?.Invoke(_plannedId);
        }

        private void Close()
        {
            if (!IsVisible) return;
            var close = _onClose;
            _onClose = null;
            SetVisible(false);
            close?.Invoke();
        }

        private void OnRectTransformDimensionsChange()
        {
            FitPanel();
        }

        private void FitPanel()
        {
            if (_panel == null || Root == null || Root.rect.width <= 0f || Root.rect.height <= 0f) return;
            var scale = Mathf.Min(1f, (Root.rect.width - 40f) / PanelWidth, (Root.rect.height - 40f) / PanelHeight);
            _panel.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        private void Legend(string text, Color color, float x)
        {
            var mark = UIBuilder.CreateFill(_panel, text + " Legend Marker", color);
            Place(mark.rectTransform, new Vector2(x, -319f), new Vector2(14f, 3f));
            Label(_panel, text + " Legend", text, 11f, UITheme.TextStrong,
                new Vector2(x + 91f, -319f), new Vector2(150f, 22f));
        }

        private void Rule(string name, float y)
        {
            var rule = UIBuilder.CreateFill(_panel, name, UITheme.BorderRule);
            Place(rule.rectTransform, new Vector2(0f, y), new Vector2(1048f, 1f));
        }

        private static Vector2 Boundary(RectTransform rect, Vector2 direction)
        {
            var x = Mathf.Abs(direction.x) < 0.01f ? float.PositiveInfinity : rect.sizeDelta.x * 0.5f / Mathf.Abs(direction.x);
            var y = Mathf.Abs(direction.y) < 0.01f ? float.PositiveInfinity : rect.sizeDelta.y * 0.5f / Mathf.Abs(direction.y);
            return direction * Mathf.Min(x, y);
        }

        private static string ShortHint(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "Clear this Void's escape condition.";
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            if (value.Length <= maxLength) return value;
            var end = value.LastIndexOf(' ', maxLength - 1, maxLength);
            return value.Substring(0, end > maxLength / 2 ? end : maxLength - 1).TrimEnd(' ', '.', ',') + "…";
        }

        private static Text Label(Transform parent, string name, string text, float size, Color color,
            Vector2 position, Vector2 dimensions, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var label = UIBuilder.CreateText(parent, name, text, size, color, anchor, true, style);
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            Place(label.rectTransform, position, dimensions);
            return label;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
