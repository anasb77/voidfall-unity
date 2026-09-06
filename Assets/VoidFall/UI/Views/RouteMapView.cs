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
        private const float PanelHeight = 720f;
        private const float CardWidth = 158f;
        private const float CardHeight = 142f;
        private const string ThumbnailResourceRoot = "VoidFall/RouteThumbnails/";

        private readonly List<NodeWidgets> _cards = new List<NodeWidgets>();
        private readonly List<EdgeWidgets> _edges = new List<EdgeWidgets>();
        private readonly Dictionary<string, NodeWidgets> _byId =
            new Dictionary<string, NodeWidgets>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _thumbnails =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<string> _reachable = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _plannedNodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _plannedEdges = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<VoidRouteNode> _ordered = new List<VoidRouteNode>();
        private readonly List<int> _depths = new List<int>();
        private RectTransform _panel;
        private RectTransform _nodeLayer;
        private RectTransform _edgeLayer;
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
            public Image Thumbnail;
            public Text Name;
            public Text State;
            public Button Button;
            public bool Sealed;
            public bool Visited;
            public bool Current;
            public bool CanPlan;
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
            var scrim = UIBuilder.CreateScrim(Root, "Scrim", UITheme.WithAlpha(UITheme.Void, 0.92f));
            var dismiss = scrim.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = scrim;
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(Close);

            _panel = UIBuilder.CreatePanel(Root, "Route Map", new Vector2(PanelWidth, PanelHeight));
            Label(_panel, "Title", "VOID MAP", 30f, Color.white,
                new Vector2(0f, 319f), new Vector2(560f, 48f), FontStyle.Bold, TextAnchor.MiddleCenter);
            var rule = UIBuilder.CreateFill(_panel, "Header Rule", UITheme.BorderRule);
            Place(rule.rectTransform, new Vector2(0f, 284f), new Vector2(1060f, 1f));

            // Separate layers keep every connection behind every node, including pooled additions.
            _edgeLayer = UIBuilder.Stretch(UIBuilder.CreateRect(_panel, "Connections"));
            _nodeLayer = UIBuilder.Stretch(UIBuilder.CreateRect(_panel, "Voids"));
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
            for (var index = 0; index < _ordered.Count; index++)
            {
                if (_cards.Count <= index) _cards.Add(BuildNode(index));
                var card = _cards[index];
                var node = _ordered[index];
                var column = _depths.IndexOf(node.Depth);
                var first = index;
                while (first > 0 && _ordered[first - 1].Depth == node.Depth) first--;
                var last = index;
                while (last + 1 < _ordered.Count && _ordered[last + 1].Depth == node.Depth) last++;
                var count = last - first + 1;
                var lane = index - first;
                var x = _depths.Count <= 1
                    ? 0f
                    : Mathf.Lerp(-490f, 490f, column / (float)(_depths.Count - 1));
                var y = count <= 1 ? 0f : lane == 0 ? 126f : -126f;
                Place(card.Root, new Vector2(x, y - 8f), new Vector2(CardWidth, CardHeight));
                card.Root.gameObject.SetActive(true);
                card.Node = node;
                var state = _run.StateOf(node.Id);
                card.Current = node.Id == _run.CurrentVoidId;
                card.Visited = state == RouteNodeState.Selected || state == RouteNodeState.Completed;
                card.Sealed = state == RouteNodeState.Locked || (!card.Visited && !_reachable.Contains(node.Id));
                card.CanPlan = !card.Visited && !card.Sealed && _reachable.Contains(node.Id);
                card.Name.text = node.DisplayName.ToUpperInvariant();
                card.State.text = card.Current ? "YOU ARE HERE" : string.Empty;
                card.State.gameObject.SetActive(card.Current);
                card.Button.interactable = card.CanPlan;
                card.Thumbnail.sprite = Thumbnail(node.ArenaId);
                card.Thumbnail.enabled = card.Thumbnail.sprite != null;
                _byId.Add(node.Id, card);
            }
            for (var index = _ordered.Count; index < _cards.Count; index++)
                _cards[index].Root.gameObject.SetActive(false);
        }

        private NodeWidgets BuildNode(int index)
        {
            var root = UIBuilder.CreateRect(_nodeLayer, "Void " + index);
            var fill = UIBuilder.CreateSurface(root, "Fill", UISprites.Rounded(
                UITheme.RadiusCard, UITheme.Rgba(7, 12, 23, 1f), UITheme.Rgba(7, 12, 23, 1f), Color.clear), true);
            UIBuilder.Stretch(fill.rectTransform);
            var border = UIBuilder.CreateSurface(root, "Border", UISprites.Rounded(
                UITheme.RadiusCard, Color.clear, Color.clear, Color.white, 2f));
            UIBuilder.Stretch(border.rectTransform);
            var thumbnail = UIBuilder.CreateSurface(root, "Arena Thumbnail", null);
            Place(thumbnail.rectTransform, new Vector2(0f, 13f), new Vector2(148f, 84f));
            thumbnail.preserveAspect = true;
            thumbnail.raycastTarget = false;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            var card = new NodeWidgets
            {
                Root = root,
                Fill = fill,
                Border = border,
                Thumbnail = thumbnail,
                Button = button,
                Name = Label(root, "Name", string.Empty, 14f, Color.white,
                    new Vector2(0f, -50f), new Vector2(CardWidth - 8f, 32f), FontStyle.Bold, TextAnchor.MiddleCenter),
                State = Label(root, "State", string.Empty, 10f, UITheme.CyanPale,
                    new Vector2(0f, 63f), new Vector2(CardWidth - 8f, 18f), FontStyle.Bold, TextAnchor.MiddleCenter),
            };
            button.onClick.AddListener(() => Plan(card));
            return card;
        }

        private Sprite Thumbnail(string arenaId)
        {
            if (string.IsNullOrEmpty(arenaId)) return null;
            if (_thumbnails.TryGetValue(arenaId, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>(ThumbnailResourceRoot + arenaId);
            _thumbnails.Add(arenaId, sprite);
            return sprite;
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
                        _edges.Add(new EdgeWidgets
                        {
                            Line = UIBuilder.CreateFill(_edgeLayer, "Connection " + index, Color.white)
                        });
                    var edge = _edges[index++];
                    edge.From = _byId[node.Id];
                    edge.To = to;
                    edge.Traversed = Traversed(node.Id, child);
                    edge.Line.gameObject.SetActive(true);
                    var direction = to.Root.anchoredPosition - edge.From.Root.anchoredPosition;
                    var start = edge.From.Root.anchoredPosition + Boundary(edge.From.Root, direction);
                    var end = to.Root.anchoredPosition + Boundary(to.Root, -direction);
                    var delta = end - start;
                    Place(edge.Line.rectTransform, (start + end) * 0.5f,
                        new Vector2(delta.magnitude, edge.Traversed ? 3f : 2f));
                    edge.Line.rectTransform.localRotation = Quaternion.Euler(
                        0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
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
            if (!_byId.TryGetValue(_plannedId ?? string.Empty, out var planned) || !planned.CanPlan)
                planned = null;
            BuildPlannedRoute(planned);

            foreach (var card in _cards)
            {
                if (!card.Root.gameObject.activeSelf) continue;
                var plannedPath = _plannedNodes.Contains(card.Node.Id) && !card.Current;
                var accent = card.Current ? UITheme.Cyan : plannedPath ? UITheme.GoldLight
                    : card.Visited ? UITheme.CyanLight : UITheme.TextStrong;
                card.Fill.color = card.Current ? UITheme.Rgba(8, 42, 54, 1f)
                    : plannedPath ? UITheme.Rgba(43, 37, 23, 1f) : UITheme.Rgba(7, 12, 23, 1f);
                card.Border.color = UITheme.WithAlpha(accent,
                    card.Current || plannedPath ? 1f : card.Sealed ? 0.12f : card.Visited ? 0.58f : 0.34f);
                card.Name.color = UITheme.WithAlpha(card.Current ? UITheme.CyanPale : accent,
                    card.Sealed ? 0.3f : card.Visited ? 0.72f : 1f);
                card.Thumbnail.color = UITheme.WithAlpha(Color.white,
                    card.Sealed ? 0.16f : card.Visited && !card.Current ? 0.52f : 0.9f);
            }

            foreach (var edge in _edges)
            {
                if (!edge.Line.gameObject.activeSelf) continue;
                var plannedPath = _plannedEdges.Contains(EdgeKey(edge.From.Node.Id, edge.To.Node.Id));
                var available = _reachable.Contains(edge.From.Node.Id) && _reachable.Contains(edge.To.Node.Id) &&
                                !edge.From.Sealed && !edge.To.Sealed;
                edge.Line.color = plannedPath ? UITheme.WithAlpha(UITheme.GoldLight, 0.92f)
                    : edge.Traversed ? UITheme.WithAlpha(UITheme.Cyan, 0.95f)
                    : available ? UITheme.WithAlpha(UITheme.CyanLight, 0.36f)
                    : UITheme.WithAlpha(UITheme.TextStrong, 0.08f);
            }
        }

        private void BuildPlannedRoute(NodeWidgets planned)
        {
            _plannedNodes.Clear();
            _plannedEdges.Clear();
            if (planned == null) return;
            AddPath(_run.PlannedPathThrough(planned.Node.Id));
        }

        private void AddPath(IReadOnlyList<string> path)
        {
            for (var index = 0; index < path.Count; index++)
            {
                _plannedNodes.Add(path[index]);
                if (index > 0) _plannedEdges.Add(EdgeKey(path[index - 1], path[index]));
            }
        }

        private static string EdgeKey(string from, string to) => from + "\n" + to;

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

        private static Vector2 Boundary(RectTransform rect, Vector2 direction)
        {
            var x = Mathf.Abs(direction.x) < 0.01f
                ? float.PositiveInfinity
                : rect.sizeDelta.x * 0.5f / Mathf.Abs(direction.x);
            var y = Mathf.Abs(direction.y) < 0.01f
                ? float.PositiveInfinity
                : rect.sizeDelta.y * 0.5f / Mathf.Abs(direction.y);
            return direction * Mathf.Min(x, y);
        }

        private static Text Label(Transform parent, string name, string text, float size, Color color,
            Vector2 position, Vector2 dimensions, FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.MiddleLeft)
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
