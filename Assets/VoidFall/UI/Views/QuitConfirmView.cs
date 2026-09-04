using UnityEngine;
using UnityEngine.UI;

namespace VoidFall.UI
{
    /// <summary>
    /// The desktop-style exit confirmation opened by the home screen's corner
    /// close button. Modal over the menu layer with a near-opaque scrim, so the
    /// menu reads as fully covered while the dialog is up; the runtime hides it
    /// on every SetScreen sweep, and the menu music muffle tracks visibility.
    /// </summary>
    public sealed class QuitConfirmView : UIViewBase
    {
        protected override void Build()
        {
            // Denser than the gameplay overlay scrim: the main menu must not
            // show through, so this is a true cover rather than a tint.
            UIBuilder.CreateScrim(Root, "Scrim", UITheme.MenuDialogScrim);

            var content = UIBuilder.CreateOverlayCard(
                Root,
                "Card",
                new Vector2(559f, 390f),
                "Exit VoidFall",
                "Quit game?",
                out _);

            // The question copy sits between the card header and the action
            // stack, mirroring the overlay-card rhythm the revive prompt uses.
            var prompt = UIBuilder.CreateParagraph(
                content,
                "Prompt",
                "Do you want to quit the game?",
                13f,
                UITheme.TextLabel,
                TextAnchor.MiddleCenter);
            prompt.rectTransform.anchorMin = new Vector2(0f, 1f);
            prompt.rectTransform.anchorMax = new Vector2(1f, 1f);
            prompt.rectTransform.pivot = new Vector2(0.5f, 1f);
            prompt.rectTransform.sizeDelta = new Vector2(0f, 72f);
            prompt.rectTransform.anchoredPosition = new Vector2(0f, -6f);

            var stack = UIBuilder.CreateRect(content, "Actions");
            stack.anchorMin = new Vector2(0f, 0f);
            stack.anchorMax = new Vector2(1f, 0f);
            stack.pivot = new Vector2(0.5f, 0f);
            stack.sizeDelta = new Vector2(0f, 107f);
            UIBuilder.AddVerticalLayout(stack, 9f);

            var quit = UIBuilder.CreatePrimaryAction(
                stack,
                "Quit",
                "Quit",
                null,
                () => Callbacks?.QuitGame?.Invoke(),
                52f);
            UIBuilder.SetHeight(quit.GetComponent<RectTransform>(), 52f);

            var cancel = UIBuilder.CreateSecondaryAction(
                stack,
                "Cancel",
                "Cancel",
                null,
                Hide,
                46f);
            UIBuilder.SetHeight(cancel.GetComponent<RectTransform>(), 46f);
        }

        /// <summary>Opens the confirmation over the main menu.</summary>
        public void Show() => SetVisible(true);

        /// <summary>Closes the confirmation, returning focus to the menu.</summary>
        public void Hide() => SetVisible(false);

        public override void SetVisible(bool visible)
        {
            base.SetVisible(visible);
            // The menu-theme muffle tracks dialog visibility exactly, including
            // SetScreen sweeps that dismiss the dialog without calling Hide().
            Callbacks?.SetQuitDialogOpen?.Invoke(visible);
        }
    }
}
