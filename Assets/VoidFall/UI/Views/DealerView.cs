using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    public sealed class DealerView : UIViewBase
    {
        private RectTransform _content, _grid;
        private DealerPortraitView _portrait;
        private Action<int> _buy;
        private Action _close;
        private float _look;
        private int _variation;
        private bool _reduced;
        private float _smileUntil;
        private Text _notice;
        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Dealer Scrim", new Color(.015f, .023f, .043f, .985f));
            _content = UIBuilder.CreateRect(Root, "Dealer Content");
            _content.anchorMin = _content.anchorMax = Vector2.one * .5f;
            _content.sizeDelta = new Vector2(930, 790);
            _portrait = DealerPortraitView.Create(_content, "Dealer Face", 10.26f, new Vector2(620, 405));
            _portrait.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 205);
            _grid = UIBuilder.CreateRect(_content, "Dealer Cards");
            _grid.anchorMin = _grid.anchorMax = Vector2.one * .5f;
            _grid.sizeDelta = new Vector2(930, 330); _grid.anchoredPosition = new Vector2(0, -215);
            var layout = UIBuilder.AddHorizontalLayout(_grid, 16, null, TextAnchor.UpperCenter);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var close = UIBuilder.CreateSecondaryAction(Root, "Close Dealer", "×", null, () => _close?.Invoke(), 40);
            var rect = close.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-25, -20); rect.sizeDelta = new Vector2(42, 42);
            _notice = UIBuilder.CreateText(_content, "Purchase Error", "", 13, UITheme.RosePale, TextAnchor.MiddleCenter, false);
            _notice.rectTransform.anchoredPosition = new Vector2(0, -390); _notice.rectTransform.sizeDelta = new Vector2(800, 30);
        }
        public void Show(DealerSession session, int wallet, int soundMask, int rifleMask, int variation, bool reduced, Action<int> buy, Action close)
        {
            _buy = buy; _close = close; _variation = variation; _reduced = reduced; _look = 0; _notice.text = "";
            ClearChildren(_grid);
            for (var i = 0; i < session.Offers.Length; i++) BuildOffer(session, i, wallet, soundMask, rifleMask);
            SetVisible(true);
            RestoreFocus();
        }
        public void Purchased(int index) { _look = index - 1; _smileUntil = Time.unscaledTime + 3.5f; }
        public void SetNotice(string message) { if (_notice != null) _notice.text = message; }
        private void Update()
        {
            if (_portrait == null) return;
            _portrait.SetPose(_look, Time.unscaledTime < _smileUntil, _reduced, _variation);
            var width = Screen.height > 0 ? Screen.width * 900f / Screen.height : 1600f;
            var scale = Mathf.Min(1, (width - 36) / 930f); _content.localScale = Vector3.one * Mathf.Max(.35f, scale);
        }
        private void BuildOffer(DealerSession session, int index, int wallet, int soundMask, int rifleMask)
        {
            var offer = session.Offers[index]; var bought = session.PurchasedIndex >= 0;
            var accent = offer.Kind == DealerOfferKind.Fragment || offer.Kind == DealerOfferKind.EquipLegendary || offer.Kind == DealerOfferKind.UpgradeLegendary
                ? new Color(.88f, .56f, 1) : offer.Cursed ? UITheme.Rose : UITheme.GreenLight;
            var column = UIBuilder.CreateRect(_grid, "Offer " + index);
            var surface = UIBuilder.CreateSurface(column, "Card", UISprites.Rounded(12, UITheme.UpgradeCardTop, UITheme.UpgradeCardBottom,
                UITheme.Mix(accent, 27, UITheme.Hex("#334155")), 1, 165, true));
            UIBuilder.Stretch(surface.rectTransform); surface.rectTransform.offsetMin = new Vector2(0, 57);
            surface.raycastTarget = true;
            var group = UIBuilder.EnsureGroup(surface.gameObject);
            group.alpha = bought && session.PurchasedIndex != index ? .2f : 1;
            var tick = UIBuilder.CreateFill(surface.transform, "Accent", accent);
            var tr = tick.rectTransform; tr.anchorMin = new Vector2(.1f, 1); tr.anchorMax = new Vector2(.42f, 1); tr.pivot = new Vector2(.5f, 1); tr.sizeDelta = new Vector2(0, 2);
            var image = UIBuilder.CreateRect(surface.transform, "Icon").gameObject.AddComponent<RawImage>();
            image.raycastTarget = false; image.texture = Resources.Load<Texture2D>("VoidFall/Dealer/" + offer.Art);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, 1);
            image.rectTransform.pivot = new Vector2(.5f, 1); image.rectTransform.anchoredPosition = new Vector2(0, -20);
            image.rectTransform.sizeDelta = new Vector2(100, 100);
            if (offer.Art.EndsWith("complete")) image.rectTransform.sizeDelta = new Vector2(210, 92);
            var description = offer.Description;
            if (offer.Kind == DealerOfferKind.Fragment)
            {
                var mask = offer.Legendary == LegendaryWeaponId.SoundBlade ? soundMask : rifleMask;
                description = LegendaryRules.Name(offer.Legendary) + " fragment. " + DealerRules.PieceCount(mask) + "/3 collected.";
                if (DealerRules.PieceCount(mask) == 2 && (mask & (1 << offer.Piece)) == 0) description += " Completes and equips the weapon.";
                if (mask == 7 && session.PurchasedIndex == index)
                {
                    image.texture = Resources.Load<Texture2D>("VoidFall/Dealer/" + LegendaryRules.ArtId(offer.Legendary) + "-complete");
                    image.rectTransform.sizeDelta = new Vector2(210, 92); description = LegendaryRules.Name(offer.Legendary) + " assembled and equipped.";
                }
            }
            var title = UIBuilder.CreateText(surface.transform, "Name", offer.Title, 18, UITheme.TextBody, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            var titleRect = title.rectTransform; titleRect.anchorMin = titleRect.anchorMax = new Vector2(.5f, 1); titleRect.pivot = new Vector2(.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -132); titleRect.sizeDelta = new Vector2(265, 40);
            var desc = UIBuilder.CreateText(surface.transform, "Description", description, 12.5f, UITheme.TextDescription, TextAnchor.UpperCenter, false);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;
            var dr = desc.rectTransform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = new Vector2(20, 14); dr.offsetMax = new Vector2(-20, -187);
            UIBuilder.FitText(desc, 10, 12.5f);
            var price = UIBuilder.CreateSecondaryAction(column, "Price", "100 Scraps", null, () => _buy?.Invoke(index), 40);
            var pr = price.GetComponent<RectTransform>(); pr.anchorMin = pr.anchorMax = new Vector2(.5f, 0); pr.pivot = new Vector2(.5f, 0); pr.sizeDelta = new Vector2(170, 42); pr.anchoredPosition = Vector2.zero;
            price.interactable = !bought && wallet >= DealerRules.Price;
            var priceText = price.GetComponentInChildren<Text>(); if (priceText != null) priceText.color = UITheme.GoldLight;
            var hover = surface.gameObject.AddComponent<DealerHover>(); hover.Enter = () => _look = index - 1; hover.Exit = () => _look = 0;
            var priceHover = price.gameObject.AddComponent<DealerHover>(); priceHover.Enter = hover.Enter; priceHover.Exit = hover.Exit;
        }
        private sealed class DealerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
        {
            public Action Enter, Exit;
            public void OnPointerEnter(PointerEventData data) => Enter?.Invoke();
            public void OnPointerExit(PointerEventData data) => Exit?.Invoke();
            public void OnSelect(BaseEventData data) => Enter?.Invoke();
        }
    }
}
