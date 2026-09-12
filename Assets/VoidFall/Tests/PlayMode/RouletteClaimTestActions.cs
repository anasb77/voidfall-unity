using System.Reflection;
using NUnit.Framework;
using VoidFall.Runtime;
using VoidFall.UI;
using UnityEngine.UI;

namespace VoidFall.Tests.PlayMode
{
    internal static class RouletteClaimTestActions
    {
        internal static void ClaimOne(VoidFallGameRuntime runtime, bool take = true)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var ui = (UIManager)typeof(VoidFallGameRuntime).GetField("_ui", flags).GetValue(runtime);
            var view = ui.LevelUp;
            typeof(LevelUpView).GetField("_rewardElapsed", flags).SetValue(view, 1f);
            typeof(LevelUpView).GetMethod("Update", flags).Invoke(view, null);
            var action = view.transform.Find("Content/RewardActions/" + (take ? "Take" : "Leave"));
            var button = action != null && action.gameObject.activeInHierarchy ? action.GetComponent<Button>() : null;
            if (button == null)
                foreach (var candidate in view.GetComponentsInChildren<Button>())
                    if (candidate.name == "Card0") { button = candidate; break; }
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
        }

        internal static void ClaimAll(VoidFallGameRuntime runtime)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var active = typeof(VoidFallGameRuntime).GetField("_prizeRevealActive", flags);
            var ui = (UIManager)typeof(VoidFallGameRuntime).GetField("_ui", flags).GetValue(runtime);
            for (var count = 0; count < 3 && (bool)active.GetValue(runtime); count++)
            {
                ClaimOne(runtime);
            }
            Assert.That((bool)active.GetValue(runtime), Is.False, "All presented rewards must be explicitly claimed.");
        }
    }
}
