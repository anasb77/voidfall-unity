namespace VoidFall.Core
{
    public static class CrascendoContent
    {
        public const string StableId = "crascendo";
        public static readonly ArenaDefinition Arena = new ArenaDefinition
        {
            Id = StableId, Name = "Crascendo",
            Description = "Crying obsidian awakens as every hit makes your enemies larger.",
            Modifier = "+20% size per hit · 5× maximum · Giant death pulses",
            StarTint = "#ba7bed", WeightMultipliers = new WeightedValueDefinition[0],
            Features = new[] { "crascendoGrowth" },
            Cycles = new[] { new ArenaCycleDefinition { Id = "resonance", Name = "Resonance", Seconds = 300, FlashRate = 0 } },
            EliteCadenceMultiplier = 1, EliteRewardMultiplier = 1,
        };
        public static ArenaDefinition FindArena(string id) => id == StableId ? Arena : null;
    }
}
