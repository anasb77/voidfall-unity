namespace VoidFall.Core
{
    public sealed class OperativeDefinition
    {
        public string Id;
        public string Name;
        public double MaxHealth;
        public double MoveSpeed;
        public double PickupRadius;
        public string StartingWeapon;
    }

    public sealed class WeightedValueDefinition
    {
        public string Id;
        public double Value;
    }

    public sealed class ArenaCycleDefinition
    {
        public string Id;
        public string Name;
        public double Seconds;
        public double FlashRate;
    }

    public sealed class ArenaDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string Modifier;
        public string StarTint;
        public WeightedValueDefinition[] WeightMultipliers;
        public string[] Features;
        public ArenaCycleDefinition[] Cycles;
        public double EliteCadenceMultiplier;
        public double EliteRewardMultiplier;
    }

    public sealed class WeaponStatsDefinition
    {
        public double Damage;
        public double Cooldown;
        public double Range;
        public double ProjectileSpeed;
        public int ProjectileCount;
        public double SpreadDegrees;
        public int Pierce;
        public double Knockback;
        public double ProjectileRadius;
        public double BlastRadius;
        public int OrbitCount;
        public double OrbitRadius;
        public double OrbitSpeed;
        public double HitCooldown;
        public int ChainCount;
    }

    public sealed class WeaponRankDefinition
    {
        public int Rank;
        public WeaponStatsDefinition Stats;
    }

    public sealed class WeaponDefinition
    {
        public string Id;
        public string Name;
        public string Kind;
        public string Accent;
        public string Summary;
        public WeaponRankDefinition[] Ranks;
    }

    public sealed class EnemyDefinition
    {
        public string Id;
        public string Name;
        public string Behavior;
        public double Health;
        public double Speed;
        public double ContactDamage;
        public double Radius;
        public double Xp;
        public string Color;
        public double NaturalStartSeconds;
        public double? PreferredDistance;
        public double? AttackCooldown;
        public double? TelegraphSeconds;
        public double? ProjectileSpeed;
        public double? Shield;
        public double? TriggerDistance;
        public double? BlastRadius;
        public double? RecoverySeconds;
    }

    public sealed class EliteDefinition
    {
        public string Id;
        public string Name;
        public double Health;
        public double Speed;
        public double ContactDamage;
        public double Radius;
        public double Xp;
        public string Color;
        public double FirstAtSeconds;
        public double RepeatEverySeconds;
        public double ChargeTelegraphSeconds;
        public double ChargeDurationSeconds;
        public double ChargeSpeed;
        public double ChargeDamageMultiplier;
        public double ChargeCooldownSeconds;
        public double ChargeRecoverySeconds;
    }

    public sealed class BossAttackDefinition
    {
        public string Id;
        public double TelegraphSeconds;
        public double ActiveSeconds;
        public double RecoverySeconds;
        public double CooldownSeconds;
        public double Damage;
        public double? Radius;
        public double? ShockwaveSpeed;
        public int? ProjectileCount;
        public double? ProjectileSpeed;
        public int? SummonCount;
        public double? BeamLength;
        public double? BeamWidth;
        public double? RotationSpeed;
    }

    public sealed class BossDefinition
    {
        public string Id;
        public string Name;
        public double Health;
        public double Speed;
        public double ContactDamage;
        public double Radius;
        public string Color;
        public double StartsAtSeconds;
        public double RepeatEverySeconds;
        public double PhaseTwoHealthRatio;
        public double PhaseTwoSpeedMultiplier;
        public double PhaseTwoCooldownMultiplier;
        public int RewardParts;
        public BossAttackDefinition[] Attacks;
    }

    public sealed class SpawnWeightDefinition
    {
        public string Id;
        public double Weight;
    }

    public sealed class SpawnBandDefinition
    {
        public double StartSeconds;
        public double EndSeconds;
        public SpawnWeightDefinition[] Weights;
    }

    public sealed class SupportDefinition
    {
        public string Id;
        public string Name;
        public int MaxRank;
        public string Accent;
        public double Weight;
        public string[] Descriptions;
    }

    public sealed class LateUpgradeDefinition
    {
        public string Id;
        public string Name;
        public int MaxRank;
        public string Accent;
        public string Description;
    }

    public sealed class EvolutionDefinition
    {
        public string WeaponId;
        public string SupportId;
        public string Name;
        public string Description;
        public string Accent;
    }

    public sealed class RankValueDefinition
    {
        public string Id;
        public int Rank;
    }

    public sealed class StressScenarioDefinition
    {
        public string Id;
        public string Name;
        public string Intent;
        public double TimeSeconds;
        public string Arena;
        public RankValueDefinition[] WeaponRanks;
        public string[] Evolve;
        public RankValueDefinition[] SupportRanks;
        public RankValueDefinition[] LateRanks;
        public double EnemyFill;
        public int EliteVariantRounds;
        public int RosterTwoRounds;
        public int Harvesters;
        public int Bosses;
        public bool MeteorStorm;
        public double PickupFill;
        public double HostileShotFill;
        public double WarmupSeconds;
        public double MeasureSeconds;
        public double TopUpSeconds;
    }

    public static partial class ContentCatalog
    {
    }
}
