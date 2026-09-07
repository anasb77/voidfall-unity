namespace VoidFall.Core
{
    public readonly struct DirectorProfileDefinition
    {
        public DirectorProfileId Id { get; }
        public string Name { get; }
        public string Recommendation { get; }
        public int PressureCeilingHundredths { get; }
        public double RecoverySeconds { get; }

        public DirectorProfileDefinition(
            DirectorProfileId id,
            string name,
            string recommendation,
            int pressureCeilingHundredths,
            double recoverySeconds)
        {
            Id = id;
            Name = name;
            Recommendation = recommendation;
            PressureCeilingHundredths = pressureCeilingHundredths;
            RecoverySeconds = recoverySeconds;
        }
    }

    public static class DirectorProfiles
    {
        private static readonly DirectorProfileDefinition Standard = new DirectorProfileDefinition(
            DirectorProfileId.Standard,
            "Director I",
            "Recommended for beginners.",
            300,
            6);

        private static readonly DirectorProfileDefinition Veteran = new DirectorProfileDefinition(
            DirectorProfileId.Veteran,
            "Director II",
            "Recommended for veterans.",
            500,
            4.5);

        private static readonly DirectorProfileDefinition Extreme = new DirectorProfileDefinition(
            DirectorProfileId.Extreme,
            "Director III",
            "An extreme challenge. YOU WILL NOT SURVIVE.",
            900,
            3);

        public static DirectorProfileDefinition For(DirectorProfileId id)
        {
            switch (id)
            {
                case DirectorProfileId.Veteran:
                    return Veteran;
                case DirectorProfileId.Extreme:
                    return Extreme;
                default:
                    return Standard;
            }
        }

        public static int PopulationLimit(DirectorProfileId id, int pressureHundredths)
        {
            switch (PressureTier(For(id), pressureHundredths))
            {
                case 0: return 64;
                case 1: return 128;
                default: return 192;
            }
        }

        public static int AttackLimit(DirectorProfileId id, int pressureHundredths)
        {
            var profile = For(id);
            var tier = PressureTier(profile, pressureHundredths);
            switch (profile.Id)
            {
                case DirectorProfileId.Veteran:
                    return tier == 0 ? 1 : 2;
                case DirectorProfileId.Extreme:
                    return tier == 2 ? 3 : 2;
                default:
                    return tier == 2 ? 2 : 1;
            }
        }

        private static int PressureTier(
            DirectorProfileDefinition profile,
            int pressureHundredths)
        {
            var safePressure = pressureHundredths < 0 ? 0 : pressureHundredths;
            var scaledPressure = (long)safePressure * 10;
            if (scaledPressure < (long)profile.PressureCeilingHundredths * 3)
                return 0;
            if (scaledPressure < (long)profile.PressureCeilingHundredths * 7)
                return 1;
            return 2;
        }
    }
}
