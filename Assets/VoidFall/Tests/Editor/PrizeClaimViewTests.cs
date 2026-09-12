using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    public sealed class PrizeClaimViewTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject _host;
        private PrizeRevealView _view;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Prize claim test", typeof(RectTransform));
            ((RectTransform)_host.transform).sizeDelta = new Vector2(1280f, 820f);
            _view = _host.AddComponent<PrizeRevealView>();
            _view.Initialize(null);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [Test]
        public void Claim_invokes_callback_once_hides_the_view_and_disables_the_button()
        {
            var calls = 0;
            var hiddenBeforeCallback = false;
            var detachedBeforeCallback = false;
            var disabledBeforeCallback = false;
            _view.ShowClaim("ORBIT BLADES", "+1 orbiting blade.", RouletteTier.Premium,
                null, 2, 3, 1, 1, () =>
                {
                    calls++;
                    hiddenBeforeCallback = !_view.IsVisible;
                    detachedBeforeCallback = typeof(PrizeRevealView)
                        .GetField("_onContinue", Flags).GetValue(_view) == null;
                    disabledBeforeCallback = !ClaimButton().interactable;
                });
            UnlockClaim();

            ClaimButton().onClick.Invoke();
            ClaimButton().onClick.Invoke();

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(hiddenBeforeCallback, Is.True);
            Assert.That(detachedBeforeCallback, Is.True);
            Assert.That(disabledBeforeCallback, Is.True);
            Assert.That(_view.IsVisible, Is.False);
            Assert.That(ClaimButton().interactable, Is.False);
        }

        [Test]
        public void Showing_the_next_card_inside_the_callback_restarts_the_click_guard()
        {
            var firstCalls = 0;
            var secondCalls = 0;
            _view.ShowClaim("FIRST", "First reward.", RouletteTier.Standard,
                null, 0, 1, 1, 2, () =>
                {
                    firstCalls++;
                    _view.ShowClaim("SECOND", "Second reward.", RouletteTier.Legendary,
                        null, 1, 2, 2, 2, () => secondCalls++);
                });
            UnlockClaim();

            ClaimButton().onClick.Invoke();
            ClaimButton().onClick.Invoke();

            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.Zero, "A held or double click must not claim the next card.");
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(ClaimButton().interactable, Is.False);

            UnlockClaim();
            ClaimButton().onClick.Invoke();
            Assert.That(secondCalls, Is.EqualTo(1));
        }

        [Test]
        public void Elapsed_time_enables_claim_without_automatically_accepting_it()
        {
            var calls = 0;
            _view.ShowClaim("PARTS CACHE", "+90 Parts added to the run.", RouletteTier.Standard,
                null, 0, 0, 1, 1, () => calls++);

            SetElapsed(60f);
            InvokeUpdate();

            Assert.That(calls, Is.Zero);
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(ClaimButton().interactable, Is.True);
        }

        [Test]
        public void Claim_shows_actual_icon_progress_and_rank_transition()
        {
            var texture = new Texture2D(4, 4);
            var icon = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            try
            {
                _view.ShowClaim("ORBIT BLADES", "+1 blade and +12% orbit damage.", RouletteTier.Premium,
                    icon, 2, 3, 1, 2, null);

                Assert.That(TextAt("Reveal/Heading").text, Is.EqualTo("CLAIM YOUR REWARD"));
                Assert.That(TextAt("Reveal/Prize Card/Claim Progress").text, Is.EqualTo("CARD 1 OF 2"));
                Assert.That(TextAt("Reveal/Prize Card/Rank Transition").text, Is.EqualTo("RANK II \u2192 III"));
                var renderedIcon = ImageAt("Reveal/Prize Card/Reward Icon");
                Assert.That(renderedIcon.gameObject.activeSelf, Is.True);
                Assert.That(renderedIcon.sprite, Is.SameAs(icon));
                Assert.That(TextAt("Reveal/Prize Card/Detail").text,
                    Is.EqualTo("+1 blade and +12% orbit damage."));
                Assert.That(ClaimButton().GetComponentInChildren<Text>().text, Is.EqualTo("CLAIM"));
            }
            finally
            {
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void First_rank_claim_reads_new_to_rank_one()
        {
            _view.ShowClaim("CLOCK", "Adds a clock weapon.", RouletteTier.Standard,
                null, 0, 1, 1, 1, null);

            Assert.That(TextAt("Reveal/Prize Card/Rank Transition").text, Is.EqualTo("NEW \u2192 RANK I"));
        }

        [Test]
        public void Long_weapon_titles_wrap_with_a_readable_best_fit_floor()
        {
            const string title = "ANTI-MATTER RETURNING ORBITAL BOOMERANG ARRAY";
            _view.ShowClaim(title, "Adds a returning projectile and improves its recovery speed.",
                RouletteTier.Legendary, null, 5, 6, 1, 1, null);

            var titleLabel = TextAt("Reveal/Prize Card/Title");
            Assert.That(titleLabel.text, Is.EqualTo(title));
            Assert.That(titleLabel.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(titleLabel.resizeTextForBestFit, Is.True);
            Assert.That(titleLabel.resizeTextMinSize, Is.GreaterThanOrEqualTo(18));
            Assert.That(titleLabel.resizeTextMaxSize, Is.GreaterThanOrEqualTo(28));
            Assert.That(titleLabel.rectTransform.rect.height, Is.GreaterThanOrEqualTo(70f));
            var detailLabel = TextAt("Reveal/Prize Card/Detail");
            Assert.That(detailLabel.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(detailLabel.resizeTextForBestFit, Is.True);
            Assert.That(detailLabel.resizeTextMinSize, Is.GreaterThanOrEqualTo(13));
        }

        [Test]
        public void Legacy_show_keeps_its_single_continue_presentation()
        {
            _view.ShowClaim("CLOCK", "Adds a clock weapon.", RouletteTier.Standard,
                null, 0, 1, 1, 2, null);
            _view.Show("PARTS CACHE", "+60 Parts.", RouletteTier.Mediocre, null);

            Assert.That(TextAt("Reveal/Kicker").text, Is.EqualTo("FORTUNE ANSWERS"));
            Assert.That(TextAt("Reveal/Heading").text, Is.EqualTo("It belongs to you."));
            Assert.That(TextAt("Reveal/Prize Card/Card Kicker").gameObject.activeSelf, Is.True);
            Assert.That(TextAt("Reveal/Prize Card/Claim Progress").gameObject.activeSelf, Is.False);
            Assert.That(TextAt("Reveal/Prize Card/Rank Transition").gameObject.activeSelf, Is.False);
            Assert.That(ImageAt("Reveal/Prize Card/Reward Icon").gameObject.activeSelf, Is.False);
            Assert.That(ClaimButton().GetComponentInChildren<Text>().text, Is.EqualTo("CONTINUE"));
        }

        private void UnlockClaim()
        {
            SetElapsed(.45f);
            InvokeUpdate();
        }

        private void SetElapsed(float elapsed) =>
            typeof(PrizeRevealView).GetField("_revealElapsed", Flags).SetValue(_view, elapsed);

        private void InvokeUpdate() =>
            typeof(PrizeRevealView).GetMethod("Update", Flags).Invoke(_view, null);

        private Button ClaimButton() =>
            (Button)typeof(PrizeRevealView).GetField("_continueButton", Flags).GetValue(_view);

        private Text TextAt(string path) => _view.transform.Find(path).GetComponent<Text>();

        private Image ImageAt(string path) => _view.transform.Find(path).GetComponent<Image>();
    }
}
