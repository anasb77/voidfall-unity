namespace VoidFall.Core
{

public enum GamePhase
{
    Menu,
    Playing,
    LevelUp,
    Paused,
    Revive,
    GameOver,
}

public enum ArenaId
{
    Void,
    RedNebula,
    WhiteSakura,
}

public enum WeaponId
{
    Pistol,
    Scattergun,
    Railgun,
    Blades,
    Arc,
    Seeker,
}

// Order matches ENEMY_ORDER in src/game/content.ts.
public enum EnemyId
{
    Chaser,
    Runner,
    Gunner,
    TwinGunner,
    Dasher,
    Brute,
    Exploder,
    Guard,
    Technician,
    Mortar,
    Splitter,
    Bulwark,
    Harvester,
    Carrier,
}

public enum BossId
{
    Herald,
    Warden,
    Matriarch,
    Reaver,
}

public enum PickupId
{
    Xp,
    Part,
    Magnet,
    Repair,
    Bomb,
    Overdrive,
}

public enum SupportId
{
    Calibration,
    Cycling,
    Plating,
    Mobility,
    Collector,
    Optics,
    Overload,
    Adrenal,
    Amplifier,
    Regenerator,
}

public enum LateUpgradeId
{
    Output,
    Cooling,
    Frame,
}

public static class ContentOrder
{
    public static readonly ArenaId[] Arenas =
    {
        ArenaId.Void,
        ArenaId.RedNebula,
        ArenaId.WhiteSakura,
    };

    public static readonly WeaponId[] Weapons =
    {
        WeaponId.Pistol,
        WeaponId.Scattergun,
        WeaponId.Railgun,
        WeaponId.Blades,
        WeaponId.Arc,
        WeaponId.Seeker,
    };

    public static readonly EnemyId[] Enemies =
    {
        EnemyId.Chaser,
        EnemyId.Runner,
        EnemyId.Gunner,
        EnemyId.TwinGunner,
        EnemyId.Dasher,
        EnemyId.Brute,
        EnemyId.Exploder,
        EnemyId.Guard,
        EnemyId.Technician,
        EnemyId.Mortar,
        EnemyId.Splitter,
        EnemyId.Bulwark,
        EnemyId.Harvester,
        EnemyId.Carrier,
    };

    public static readonly BossId[] Bosses =
    {
        BossId.Herald,
        BossId.Warden,
        BossId.Matriarch,
        BossId.Reaver,
    };
}
}
