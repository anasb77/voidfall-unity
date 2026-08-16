namespace VoidFall.Core
{

public readonly struct OperativeRules
{
    public const int MaxHealth = 100;
    public const double MoveSpeed = 235;
    public const double PickupRadius = 95;
    public const WeaponId StartingWeapon = WeaponId.Pistol;
}

public static class ParityCatalog
{
    public const int ArenaCount = 3;
    public const int WeaponCount = 6;
    public const int EnemyCount = 14;
    public const int BossCount = 4;
    public const int SupportCount = 10;
    public const int LateUpgradeCount = 3;
    public const int EvolutionCount = 6;
}
}
