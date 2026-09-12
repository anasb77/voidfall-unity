namespace VoidFall.Core
{
    public enum HydraPopulationKind { Cleft, Hook, Rachis, Bloat, Graft, Bastion, Aegis, Riftkin, Reclaimer, Broodsmith }

    public static class HydraPopulationRules
    {
        public const int Count = 10;
        public const int RepairDroneLimit = 2;
        public const float RepairPerSecond = 7f;
        public const float BlastWarningSeconds = 1.1f;
        public const float BlastRadius = 95f;
        public static bool IsValid(int kind) => kind >= 0 && kind < Count;
        public static bool IsVirus(int kind) => IsValid(kind) && kind < 5;
        public static int KindForAttempt(int attempt) => (attempt % Count + Count) % Count;
        public static string Name(int kind)
        {
            switch ((HydraPopulationKind)kind)
            {
                case HydraPopulationKind.Cleft: return "Cleft";
                case HydraPopulationKind.Hook: return "Hook";
                case HydraPopulationKind.Rachis: return "Rachis";
                case HydraPopulationKind.Bloat: return "Bloat";
                case HydraPopulationKind.Graft: return "Graft";
                case HydraPopulationKind.Bastion: return "Bastion";
                case HydraPopulationKind.Aegis: return "Aegis";
                case HydraPopulationKind.Riftkin: return "Riftkin";
                case HydraPopulationKind.Reclaimer: return "Reclaimer";
                case HydraPopulationKind.Broodsmith: return "Broodsmith";
                default: return string.Empty;
            }
        }
        public static string StableId(int kind) => IsValid(kind) ? "hydra-" + Name(kind).ToLowerInvariant() : string.Empty;
        public static string BaseId(int kind)
        {
            switch ((HydraPopulationKind)kind)
            {
                case HydraPopulationKind.Hook:
                case HydraPopulationKind.Riftkin: return "dasher";
                case HydraPopulationKind.Rachis: return "gunner";
                case HydraPopulationKind.Bastion: return "brute";
                case HydraPopulationKind.Aegis: return "runner";
                case HydraPopulationKind.Reclaimer: return "harvester";
                case HydraPopulationKind.Broodsmith: return "technician";
                default: return "chaser";
            }
        }
        public static string Parents(int kind)
        {
            switch ((HydraPopulationKind)kind)
            {
                case HydraPopulationKind.Bastion: return "brute+gunner";
                case HydraPopulationKind.Aegis: return "runner+guard";
                case HydraPopulationKind.Riftkin: return "dasher+splitter";
                case HydraPopulationKind.Reclaimer: return "harvester+mortar";
                case HydraPopulationKind.Broodsmith: return "technician+carrier";
                default: return string.Empty;
            }
        }
        public static bool Splits(int kind) => kind == (int)HydraPopulationKind.Cleft || kind == (int)HydraPopulationKind.Riftkin;
        public static float IncomingDamageMultiplier(int kind, float frontalDot) =>
            kind == (int)HydraPopulationKind.Aegis && frontalDot > .25f ? .15f : 1f;
    }
}
