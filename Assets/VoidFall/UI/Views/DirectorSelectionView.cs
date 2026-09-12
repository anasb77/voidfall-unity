using System;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;

namespace VoidFall.UI
{
    public sealed class DirectorSelectionView : UIViewBase
    {
        private Action<DirectorProfileId> _choose;
        private Action _cancel;
        private Text _notice;
        private readonly Text[] _labels = new Text[3];

        protected override void Build()
        {
            var card = UIBuilder.CreatePanel(Root, "DirectorCard", new Vector2(580, 480));
            var body = card.Find("Body") as RectTransform ?? card;
            var stack = UIBuilder.Stretch(UIBuilder.CreateRect(body, "Content"), 24f);
            UIBuilder.AddVerticalLayout(stack, 12f);
            var heading = UIBuilder.CreateHeading(stack, "Heading", "Choose your Director", TextAnchor.MiddleCenter);
            UIBuilder.SetHeight(heading.rectTransform, 42);
            var intro = UIBuilder.CreateText(stack, "Introduction",
                "The Director shapes combat intensity. Pressure grows as you advance and multiplies your final score.",
                14, UITheme.TextChip, TextAnchor.MiddleCenter, false);
            UIBuilder.SetHeight(intro.rectTransform, 56);
            for (var i = 0; i < 3; i++)
            {
                var id = (DirectorProfileId)i;
                var button = UIBuilder.CreateSecondaryAction(stack, "Director" + i, "", null, () => _choose?.Invoke(id), 65);
                UIBuilder.SetHeight(button.GetComponent<RectTransform>(), 65);
                _labels[i] = button.GetComponentInChildren<Text>();
            }
            _notice = UIBuilder.CreateText(stack, "Notice", "", 12, UITheme.RosePale, TextAnchor.MiddleCenter, false);
            UIBuilder.SetHeight(_notice.rectTransform, 32);
            var back = UIBuilder.CreateSecondaryAction(stack, "Back", "Back", null, () => _cancel?.Invoke(), 36);
            UIBuilder.SetHeight(back.GetComponent<RectTransform>(), 36);
        }

        public void Show(DirectorProfileId selected, Action<DirectorProfileId> choose, Action cancel)
        {
            _choose = choose;
            _cancel = cancel;
            SetNotice("");
            for (var i = 0; i < _labels.Length; i++)
            {
                var profile = DirectorProfiles.For((DirectorProfileId)i);
                if (_labels[i] != null) _labels[i].text = profile.Name + (profile.Id == selected ? " · Selected" : "") +
                    "\n" + profile.Recommendation;
            }
            SetVisible(true);
        }

        public void SetNotice(string message) { if (_notice != null) _notice.text = message; }
    }
}
