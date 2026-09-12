using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VoidFall.UI;

namespace VoidFall.Tests.Editor
{
    public sealed class LevelUpRewardTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _host;
        private LevelUpView _view;
        private GameObject _eventSystemHost;
        private EventSystem _previousEventSystem;

        [SetUp]
        public void SetUp()
        {
            _previousEventSystem = EventSystem.current;
            _eventSystemHost = new GameObject("Reward navigation test", typeof(EventSystem));
            var eventSystem = _eventSystemHost.GetComponent<EventSystem>();
            // EditMode does not run this component's registration lifecycle.
            typeof(EventSystem).GetMethod("OnEnable", Flags).Invoke(eventSystem, null);
            EventSystem.current = eventSystem;
            _host = new GameObject("Level up reward test", typeof(RectTransform));
            _view = _host.AddComponent<LevelUpView>();
            _view.Initialize(null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            if (_eventSystemHost != null)
                typeof(EventSystem).GetMethod("OnDisable", Flags)
                    .Invoke(_eventSystemHost.GetComponent<EventSystem>(), null);
            Object.DestroyImmediate(_eventSystemHost);
            if (_previousEventSystem != null) EventSystem.current = _previousEventSystem;
        }

        [Test]
        public void Controller_focus_follows_each_reward_and_returns_to_normal_upgrades()
        {
            var nextCalls = 0;
            _view.ShowReward(Card("FIRST"), () =>
                _view.ShowReward(Card("SECOND"), () => nextCalls++));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(CardButton().gameObject));
            Assert.That(CardButton().interactable, Is.False);
            UnlockReward();
            CardButton().onClick.Invoke();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(CardButton().gameObject));
            Assert.That(CardButton().interactable, Is.False, "Focus must not bypass the new stage guard.");
            Assert.That(nextCalls, Is.Zero);
            _view.ShowUpgrades(new[] { Card("ONE"), Card("TWO") }, _ => { });
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(CardButton().gameObject));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Controller_can_navigate_from_initial_take_focus_to_either_wildcard_decision(bool take)
        {
            var taken = 0;
            var left = 0;
            _view.ShowReward(Card("WILD"), () => taken++, () => left++);
            var takeButton = At("Content/RewardActions/Take").GetComponent<Button>();
            var leaveButton = At("Content/RewardActions/Leave").GetComponent<Button>();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(takeButton.gameObject));
            Assert.That(takeButton.interactable, Is.False);
            UnlockReward();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)At("Content/RewardActions"));
            takeButton.OnMove(new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right });
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(leaveButton.gameObject));
            if (take)
            {
                leaveButton.OnMove(new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Left });
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(takeButton.gameObject));
            }
            EventSystem.current.currentSelectedGameObject.GetComponent<Button>().onClick.Invoke();
            Assert.That(taken, Is.EqualTo(take ? 1 : 0));
            Assert.That(left, Is.EqualTo(take ? 0 : 1));
        }

        [Test]
        public void Reward_waits_for_explicit_card_click_even_after_time_passes()
        {
            var calls = 0;
            _view.ShowReward(Card("ORBIT BLADES"), () => calls++);
            CardButton().onClick.Invoke();
            Assert.That(calls, Is.Zero, "The opening click must not claim a reward.");
            UnlockReward(60f);
            Assert.That(calls, Is.Zero);
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(CardButton().interactable, Is.True);
            Assert.That(At("Content/RerollRow").gameObject.activeSelf, Is.False);
            Assert.That(At("Content/Grid").childCount, Is.EqualTo(1));
            CardButton().onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Card_claim_detaches_callbacks_before_hiding_and_invokes_only_once()
        {
            var calls = 0;
            _view.ShowReward(Card("CLOCK"), () =>
            {
                calls++;
                Assert.That(_view.IsVisible, Is.False);
                Assert.That(typeof(LevelUpView).GetField("_onTake", Flags).GetValue(_view), Is.Null);
                Assert.That(CardButton().interactable, Is.False);
            });
            UnlockReward();
            CardButton().onClick.Invoke();
            CardButton().onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Next_reward_restarts_guard_and_old_card_cannot_claim_new_reward()
        {
            var secondCalls = 0;
            _view.ShowReward(Card("FIRST"), () =>
                _view.ShowReward(Card("SECOND"), () => secondCalls++, claimIndex: 2, claimCount: 2));
            UnlockReward();
            var firstClick = CardButton().onClick;
            firstClick.Invoke();
            CardButton().onClick.Invoke();
            Assert.That(secondCalls, Is.Zero);
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(CardButton().interactable, Is.False);
            UnlockReward();
            firstClick.Invoke();
            Assert.That(secondCalls, Is.Zero, "A stale card must not resolve a later stage.");
            CardButton().onClick.Invoke();
            Assert.That(secondCalls, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Wildcard_requires_explicit_take_or_leave_and_resolves_once(bool take)
        {
            var taken = 0;
            var left = 0;
            _view.ShowReward(Card("WILD CARD"), () => taken++, () => left++);
            At("Content/RewardActions/Take").GetComponent<Button>().onClick.Invoke();
            At("Content/RewardActions/Leave").GetComponent<Button>().onClick.Invoke();
            Assert.That(taken + left, Is.Zero, "Both wildcard decisions share the opening input guard.");
            UnlockReward();
            CardButton().onClick.Invoke();
            Assert.That(taken + left, Is.Zero, "The wildcard card body is not a take button.");
            Assert.That(CardButton().interactable, Is.False);
            var chosen = At("Content/RewardActions/" + (take ? "Take" : "Leave")).GetComponent<Button>();
            chosen.onClick.Invoke();
            chosen.onClick.Invoke();
            At("Content/RewardActions/" + (take ? "Leave" : "Take")).GetComponent<Button>().onClick.Invoke();
            Assert.That(taken, Is.EqualTo(take ? 1 : 0));
            Assert.That(left, Is.EqualTo(take ? 0 : 1));
            Assert.That(_view.IsVisible, Is.False);
        }

        [Test]
        public void Normal_upgrades_restore_header_grid_reroll_and_selection_after_reward()
        {
            var rewards = 0;
            var selected = -1;
            _view.ShowReward(Card("WILD CARD"), () => rewards++, () => rewards++);
            _view.ShowUpgrades(new[] { Card("ONE"), Card("TWO"), Card("THREE") }, 2, index => selected = index);
            Assert.That(At("Content/Header/Kicker").GetComponent<Text>().text, Is.EqualTo("LEVEL UP"));
            Assert.That(At("Content/Header/Title").GetComponent<Text>().text, Is.EqualTo("CHOOSE AN UPGRADE"));
            Assert.That(At("Content/RewardActions").gameObject.activeSelf, Is.False);
            Assert.That(At("Content/RerollRow").gameObject.activeSelf, Is.True);
            Assert.That(At("Content/RerollRow/Reroll").GetComponent<Button>().interactable, Is.True);
            Assert.That(At("Content/Grid").childCount, Is.EqualTo(3));
            var grid = (RectTransform)At("Content/Grid");
            Assert.That(grid.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(grid.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(grid.sizeDelta.x, Is.Zero);
            At("Content/RewardActions/Take").GetComponent<Button>().onClick.Invoke();
            At("Content/Grid/Card1").GetComponent<Button>().onClick.Invoke();
            Assert.That(selected, Is.EqualTo(1));
            Assert.That(rewards, Is.Zero);
        }

        private static UpgradeCardData Card(string title) => new UpgradeCardData
        {
            Title = title, Description = "Reward effect.", Category = "WEAPON", CurrentRank = 1, MaxRank = 6
        };

        private Transform At(string path) => _view.transform.Find(path);
        private Button CardButton() => At("Content/Grid/Card0").GetComponent<Button>();

        private void UnlockReward(float elapsed = .45f)
        {
            typeof(LevelUpView).GetField("_rewardElapsed", Flags).SetValue(_view, elapsed);
            typeof(LevelUpView).GetMethod("Update", Flags).Invoke(_view, null);
        }
    }
}
