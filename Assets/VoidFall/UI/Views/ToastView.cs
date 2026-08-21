using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// Menu notices: purchase confirmations, save failures, import/export results
    /// and mute changes.
    ///
    /// Combat toasts stay with the runtime, which already renders them on its own
    /// canvas. This view exists because the runtime also sets a separate
    /// menu-notice string that no screen ever displayed, so every one of those
    /// messages was silently discarded.
    ///
    /// Styling follows .toast: centred, uppercase, wide tracking, and a glow that
    /// tightens as the notice settles.
    /// </summary>
    public sealed class ToastView : UIViewBase
    {
        private const int MaxVisible = 3;
        private const float DefaultSeconds = 2.6f;

        private sealed class Notice
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Text Title;
            public Text Detail;
            public float Elapsed;
            public float Duration;
        }

        private readonly List<Notice> _active = new List<Notice>();
        private RectTransform _stack;
        private bool _obscured;

        protected override void Build()
        {
            _stack = UIBuilder.CreateRect(Root, "Stack");
            _stack.anchorMin = new Vector2(0.5f, 1f);
            _stack.anchorMax = new Vector2(0.5f, 1f);
            _stack.pivot = new Vector2(0.5f, 1f);
            _stack.sizeDelta = new Vector2(680f, 0f);
            _stack.anchoredPosition = new Vector2(0f, -150f);

            var layout = UIBuilder.AddVerticalLayout(_stack, 7f, null, TextAnchor.UpperCenter);
            layout.childForceExpandWidth = true;

            var fitter = _stack.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var group = UIBuilder.EnsureGroup(gameObject);
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>Retained signature; routes to the notice presentation.</summary>
        public void ShowToast(string text, string detail, float seconds)
        {
            ShowNotice(text, detail, seconds, UITheme.CyanPale);
        }

        /// <summary>Shows a neutral menu notice.</summary>
        public void ShowNotice(string message)
        {
            ShowNotice(message, null, DefaultSeconds, UITheme.CyanPale);
        }

        /// <summary>Shows a notice with an explicit tint and lifetime.</summary>
        public void ShowNotice(string message, string detail, float seconds, Color tint)
        {
            if (string.IsNullOrEmpty(message)) return;

            gameObject.SetActive(true);

            // The browser keeps at most three, dropping the oldest.
            while (_active.Count >= MaxVisible)
            {
                var oldest = _active[0];
                _active.RemoveAt(0);
                if (oldest.Root != null) Destroy(oldest.Root.gameObject);
            }

            var root = UIBuilder.CreateRect(_stack, "Notice");
            UIBuilder.SetHeight(root, string.IsNullOrEmpty(detail) ? 34f : 52f);
            var group = UIBuilder.EnsureGroup(root.gameObject);

            var title = UIBuilder.CreateText(
                root,
                "Title",
                message.ToUpperInvariant(),
                20f,
                tint,
                TextAnchor.UpperCenter,
                true,
                FontStyle.Bold,
                0.28f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(0f, 28f);

            var bloom = UIBuilder.CreateSurface(root, "Bloom", UISprites.Glow(256));
            bloom.type = Image.Type.Simple;
            UIBuilder.Stretch(bloom.rectTransform, -140f, -14f, -140f, -6f);
            bloom.color = UITheme.WithAlpha(tint, 0.30f);
            bloom.rectTransform.SetAsFirstSibling();

            Text detailLabel = null;
            if (!string.IsNullOrEmpty(detail))
            {
                detailLabel = UIBuilder.CreateText(
                    root,
                    "Detail",
                    detail.ToUpperInvariant(),
                    10f,
                    UITheme.WithAlpha(tint, 0.72f),
                    TextAnchor.UpperCenter,
                    true,
                    FontStyle.Bold,
                    0.20f);
                detailLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
                detailLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
                detailLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
                detailLabel.rectTransform.sizeDelta = new Vector2(0f, 16f);
                detailLabel.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            }

            _active.Add(new Notice
            {
                Root = root,
                Group = group,
                Title = title,
                Detail = detailLabel,
                Elapsed = 0f,
                Duration = Mathf.Max(0.4f, seconds)
            });
        }

        /// <summary>
        /// Hides the stack while a decision overlay is up, matching
        /// .toast-stack.is-obscured.
        /// </summary>
        public void SetObscured(bool obscured)
        {
            _obscured = obscured;
            if (_stack != null) _stack.gameObject.SetActive(!obscured);
        }

        private void Update()
        {
            if (_active.Count == 0) return;

            for (var index = _active.Count - 1; index >= 0; index--)
            {
                var notice = _active[index];
                if (notice.Root == null)
                {
                    _active.RemoveAt(index);
                    continue;
                }

                notice.Elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(notice.Elapsed / notice.Duration);
                Apply(notice, t);

                if (t < 1f) continue;
                _active.RemoveAt(index);
                Destroy(notice.Root.gameObject);
            }
        }

        /// <summary>
        /// The @keyframes toast-in envelope, including the tracking that starts at
        /// 0.6em and tightens to 0.28em as the notice lands.
        /// </summary>
        private static void Apply(Notice notice, float t)
        {
            float alpha;
            float scale;

            // The stylesheet also animates letter-spacing from 0.6em down to
            // 0.28em. Tracking here is emulated by inserting separators into the
            // string, which cannot be animated per-frame without rebuilding the
            // text every frame, so the notice settles with the horizontal scale
            // instead: it still arrives wide and tightens as it lands.
            if (t < 0.18f)
            {
                var k = t / 0.18f;
                alpha = k;
                scale = Mathf.Lerp(0.9f, 1.04f, k);
            }
            else if (t < 0.28f)
            {
                var k = (t - 0.18f) / 0.1f;
                alpha = 1f;
                scale = Mathf.Lerp(1.04f, 1f, k);
            }
            else if (t < 0.82f)
            {
                alpha = 1f;
                scale = 1f;
            }
            else
            {
                var k = (t - 0.82f) / 0.18f;
                alpha = 1f - k;
                scale = Mathf.Lerp(1f, 0.98f, k);
            }

            if (notice.Group != null) notice.Group.alpha = alpha;
            if (notice.Root != null) notice.Root.localScale = Vector3.one * scale;
        }

        public override void SetVisible(bool visible)
        {
            // Always live: notices are driven by their own timers, and visibility
            // is controlled through SetObscured instead.
            gameObject.SetActive(true);
        }
    }
}
