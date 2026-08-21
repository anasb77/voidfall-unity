using UnityEngine;

namespace VoidFall.UI
{
    /// <summary>
    /// An intentionally inert seam for the gameplay HUD.
    ///
    /// The in-run HUD (integrity bar and ghost, XP strip, run clock, compact
    /// metrics, boss bar, overclock meter, weapon and upgrade strips) is authored
    /// directly by VoidFallGameRuntime on its own canvas. It is already uGUI, it
    /// already matches the browser layout, and it is driven by a dirty-checked
    /// update path that only touches what changed.
    ///
    /// The IMGUI-to-uGUI migration therefore deliberately left it alone: moving a
    /// working HUD would risk regressions for no visual gain. This type keeps the
    /// runtime's per-frame HUD calls valid so those call sites did not have to be
    /// stripped out, and gives the HUD an obvious home should it ever move here.
    /// </summary>
    public sealed class HUDView : UIViewBase
    {
        protected override void Build()
        {
            // No hierarchy: the runtime owns the HUD.
        }

        public void UpdateHealth(float current, float max) { }

        public void UpdateShield(float current, float max) { }

        public void UpdateXP(int current, int required, int level) { }

        public void UpdateStats(int score, int kills, float elapsedSeconds) { }

        public void SetBossWarning(bool active) { }

        public void SetRusherWarning(bool active) { }

        public override void SetVisible(bool visible)
        {
            // Nothing to show or hide.
        }
    }
}
