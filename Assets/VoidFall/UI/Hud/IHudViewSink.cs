namespace VoidFall.UI
{
    /// <summary>
    /// View operations HudPresenter drives. The runtime provides the concrete
    /// implementation wrapping its uGUI fields (RecordsView-style); tests
    /// provide a recording fake. Text setters are called only when the source
    /// value changes - that is the VF-009 contract the presenter owns.
    /// Fill setters may be called every frame; they are cheap.
    /// </summary>
    public interface IHudViewSink
    {
        void SetHudFade(float alpha, bool visible);
        void SetHealthFill(float fraction);
        void SetHealthGhostFill(float fraction);
        void SetHealthText(string text);
        void SetHealthValueText(string text);
        void SetXpFill(float fraction);
        void SetTimeText(string text);
        void SetLevelText(string text);
        void SetMetricsSummary(string text);
        void SetMetricValue(int index, string text);
        void SetBoostPanel(bool active, int powerTier, float fillFraction, float punch);
        void SetBossBar(bool visible, float fraction);
        void SetBossNameText(string text);
        void SetBossHealthText(string text);
    }
}