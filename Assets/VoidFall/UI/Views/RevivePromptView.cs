using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// The second-chance prompt shown when integrity hits zero, rebuilt from the
    /// browser build's revive .overlay-card.
    /// </summary>
    public sealed class RevivePromptView : UIViewBase
    {
        private Text _acceptLabel;

        protected override void Build()
        {
            UIBuilder.CreateScrim(Root, "Scrim", UITheme.OverlayScrim);

            var content = UIBuilder.CreateOverlayCard(
                Root,
                "Card",
                new Vector2(430f, 268f),
                "Integrity zero",
                "Revive?",
                out _);

            var stack = UIBuilder.Stretch(UIBuilder.CreateRect(content, "Actions"));
            UIBuilder.AddVerticalLayout(stack, 9f);

            var accept = UIBuilder.CreatePrimaryAction(
                stack,
                "Revive",
                "Revive",
                null,
                () => Callbacks?.AcceptRevive?.Invoke(),
                52f);
            UIBuilder.SetHeight(accept.GetComponent<RectTransform>(), 52f);
            _acceptLabel = accept.transform.Find("Label")?.GetComponent<Text>();

            var decline = UIBuilder.CreateSecondaryAction(
                stack,
                "EndRun",
                "End run",
                null,
                () => Callbacks?.DeclineRevive?.Invoke(),
                46f);
            UIBuilder.SetHeight(decline.GetComponent<RectTransform>(), 46f);
        }

        /// <summary>
        /// Opens the prompt, surfacing how many revives are left when more than
        /// one remains, as the browser build does.
        /// </summary>
        public void Show(int revivesRemaining)
        {
            if (_acceptLabel != null)
            {
                _acceptLabel.text = revivesRemaining > 1
                    ? "Revive (" + revivesRemaining.ToString() + " left)"
                    : "Revive";
            }
            SetVisible(true);
        }
    }
}
