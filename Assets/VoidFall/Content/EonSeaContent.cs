namespace VoidFall.Core
{
    public static class EonSeaContent
    {
        public const string StableId = "eon-sea";
        public static readonly ArenaDefinition Arena = new ArenaDefinition
        {
            Id = StableId,
            Name = "Eon Sea",
            Description = "An endless frozen sea whose glaciers crack, thaw and release a slowing frost.",
            Modifier = "Melting glaciers · Slippery ice · Freezing pulses",
            StarTint = "#a7d2eb",
            WeightMultipliers = new WeightedValueDefinition[0],
            Features = new[] { "eonGlaciers", "slipperyIce" },
            Cycles = new[]
            {
                new ArenaCycleDefinition { Id = "stillwater", Name = "Stillwater", Seconds = 25, FlashRate = 0.01 },
                new ArenaCycleDefinition { Id = "snowdrift", Name = "Snowdrift", Seconds = 24, FlashRate = 0.02 },
                new ArenaCycleDefinition { Id = "afterglow", Name = "Afterglow", Seconds = 20, FlashRate = 0.01 },
            },
            EliteCadenceMultiplier = 1.1,
            EliteRewardMultiplier = 1.15,
        };
        public static ArenaDefinition FindArena(string id) => id == StableId ? Arena : null;
    }
}
