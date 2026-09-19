using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoidFall.Runtime
{
    // One view per owned slot, never per combat entity. All geometry is cached.
    public sealed class ApprovedHudSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        public Action<ApprovedHudSlot> Inspect;
        public Action Dismiss;
        public string Title, Detail;
        private Image[] _pips;
        private Text _cooldown;
        private Color _accent;

        public void Initialize(Font font)
        {
            _pips = new Image[6];
            for (var i = 0; i < _pips.Length; i++)
            {
                var go = new GameObject("Rank Pip " + i, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _pips[i] = go.AddComponent<Image>();
                _pips[i].raycastTarget = false;
            }
            var label = new GameObject("Cooldown", typeof(RectTransform));
            label.transform.SetParent(transform, false);
            _cooldown = label.AddComponent<Text>();
            _cooldown.font = font; _cooldown.alignment = TextAnchor.MiddleCenter;
            _cooldown.raycastTarget = false; _cooldown.fontSize = 12;
            var r = _cooldown.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            _cooldown.enabled = false;
        }

        public void Bind(string title, string detail, int rank, int maxRank, Color accent, float width, float scale)
        {
            Title = title; Detail = detail; _accent = accent;
            var count = Mathf.Clamp(maxRank, 0, 6);
            for (var i = 0; i < _pips.Length; i++)
            {
                _pips[i].enabled = i < count;
                if (i >= count) continue;
                var rect = _pips[i].rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                var step = (width - 17 * scale) / count;
                rect.anchoredPosition = new Vector2(4 * scale + i * step, 5 * scale);
                rect.sizeDelta = new Vector2(Mathf.Max(1, step - 2 * scale), 2 * scale);
                _pips[i].color = i < rank ? accent : new Color(.38f, .45f, .54f, .32f);
            }
        }

        public void SetCooldown(int seconds)
        {
            _cooldown.enabled = seconds > 0;
            if (seconds > 0) _cooldown.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }
        public void OnPointerEnter(PointerEventData e) => Inspect?.Invoke(this);
        public void OnPointerExit(PointerEventData e) => Dismiss?.Invoke();
        public void OnSelect(BaseEventData e) => Inspect?.Invoke(this);
        public void OnDeselect(BaseEventData e) => Dismiss?.Invoke();
        private void OnDisable() => Dismiss?.Invoke();
    }
}
