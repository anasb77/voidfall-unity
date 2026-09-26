using System.Collections.Generic;

namespace VoidFall.Core
{
    public static class SurvivalSupportCatalog
    {
        public static SupportDefinition[] Append(SupportDefinition[] source)
        {
            var result = new List<SupportDefinition>(source);
            result.Add(new SupportDefinition
            {
                Id = "lifeSteal", Name = "Life Steal", MaxRank = 5, Accent = "#fb7185", Weight = 6,
                Descriptions = new[]
                {
                    "Heal 0.2 HP every 200 kills.", "Heal 0.4 HP every 200 kills.",
                    "Heal 0.5 HP every 200 kills.", "Heal 0.7 HP every 200 kills.", "Heal 0.9 HP every 200 kills."
                }
            });
            result.Add(new SupportDefinition
            {
                Id = "scavenger", Name = "Scavenger", MaxRank = 4, Accent = "#efbd65", Weight = 6,
                Descriptions = new[]
                {
                    "+5% Scrap drop chance. Collect 200 Scraps to gain 5 shields.",
                    "+10% Scrap drop chance. Collect 200 Scraps to gain 5 shields.",
                    "+15% Scrap drop chance. Collect 200 Scraps to gain 5 shields.",
                    "+20% Scrap drop chance. Collect 200 Scraps to gain 5 shields."
                }
            });
            return result.ToArray();
        }
    }
}
