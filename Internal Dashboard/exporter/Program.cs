using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoidFall.Core;

var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
options.Converters.Add(new JsonStringEnumConverter());
var data = new Dictionary<string, object>();
// Execute the game catalogs, including their static initialization and extensions.
data["Weapons"] = ContentCatalog.Weapons;
data["Supports"] = ExtendedCatalog.AllSupports();
data["Evolutions"] = ContentCatalog.Evolutions;
data["LateUpgrades"] = ContentCatalog.LateUpgrades;
var definitions = new Dictionary<string, object>();
foreach (var type in typeof(ContentCatalog).Assembly.GetTypes().Where(t => t.IsClass && t.Namespace == "VoidFall.Core"))
{
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        var elem = field.FieldType.IsArray ? field.FieldType.GetElementType() : field.FieldType;
        if (elem.Name.EndsWith("Definition") || elem.Name == "EliteVariantDefinition")
            definitions[type.Name + "." + field.Name] = field.GetValue(null);
    }
}
data["Definitions"] = definitions;
string Description(string method, params object[] args) => (string)typeof(UpgradeRules).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
data["WeaponCards"] = ContentCatalog.Weapons.Select(w => new { w.Id, w.Name, w.Accent, w.Summary, Ranks = w.Ranks.Select(r => new { r.Rank, r.Stats, Description = UpgradeRules.AddProjectileDefenseText(w.Id, Description("WeaponUpgradeDescription", w, r.Rank)) }) });
data["SupportCards"] = ExtendedCatalog.AllSupports().Select(s => new { s.Id, s.Name, s.Accent, s.MaxRank, Ranks = Enumerable.Range(1, s.MaxRank).Select(r => new { Rank = r, Description = Description("SupportUpgradeDescription", s, r-1, r) }) });
data["LateCards"] = ContentCatalog.LateUpgrades.Select(l => new { l.Id, l.Name, l.Accent, l.MaxRank, Ranks = Enumerable.Range(1, l.MaxRank).Select(r => new { Rank = r, Description = Description("LateUpgradeDescription", l, r-1, r) }) });
data["WildCards"] = Enum.GetValues<WildCardId>().Where(WildCardRules.IsImplemented).Select(w => new { Id = w.ToString(), Name = WildCardRules.DisplayName(w), Description = WildCardRules.Description(w) });
data["RouletteScraps"] = new[] {RouletteRules.PartsReward(RouletteTier.Mediocre),RouletteRules.PartsReward(RouletteTier.Premium),RouletteRules.BonusPartsReward};
data["Legendaries"] = new[] { LegendaryWeaponId.SoundBlade, LegendaryWeaponId.ChargedRifle }.Select(w => new { Id = LegendaryRules.Id(w), Name = LegendaryRules.Name(w), Art = LegendaryRules.ArtId(w), Ranks = Enumerable.Range(1,3).Select(r => new { Rank=r, Stats=LegendaryRules.Stats(w,r) }), Fragments=Enumerable.Range(0,3).Select(p => DealerRules.Fragment(w,p)) });
data["HydraPopulations"] = Enumerable.Range(0, HydraPopulationRules.Count).Select(i => new { Kind=i, Id=HydraPopulationRules.StableId(i), Name=HydraPopulationRules.Name(i), BaseId=HydraPopulationRules.BaseId(i), Parents=HydraPopulationRules.Parents(i), Virus=HydraPopulationRules.IsVirus(i) });
data["TierMultipliers"] = Enumerable.Range(1,4).Select(i => new { Tier=i, Health=EnemyRosterRules.HealthMultiplier((EnemyRoster)i), Speed=EnemyRosterRules.SpeedMultiplier((EnemyRoster)i), Radius=EnemyRosterRules.RadiusMultiplier((EnemyRoster)i), Damage=EnemyRosterRules.DamageMultiplier((EnemyRoster)i), Cooldown=EnemyRosterRules.CooldownMultiplier((EnemyRoster)i), Projectile=EnemyRosterRules.ProjectileMultiplier((EnemyRoster)i) });
data["SharedTierFamilies"] = ContentCatalog.Enemies.Where(e=>EnemyRosterRules.RosterTwoEligible(e.Id)).Select(e=>e.Id);
data["RosterTraits"] = ContentCatalog.Enemies.Where(e=>EnemyRosterRules.RosterTwoEligible(e.Id)).Select(e=>new {e.Id,Tiers=Enumerable.Range(1,4).Select(r=>new {Tier=r,Traits=RosterProgressionTraits.Get(e.Id,(EnemyRoster)r,false)})});
data["Forms"] = PlayerForms.All.Select(f => new { f.Id, f.Name, f.Blurb, f.UnlockHint, MaxHealth=PlayerForms.BaseMaxHealth(f.Id), MoveSpeedMultiplier=PlayerForms.MoveSpeedMultiplier(f.Id), StartingWeapon=PlayerForms.StartingWeapon(f.Id) });
data["EliteVariants"] = EliteRules.EliteVariantOrder.Select(i => new { Definition=EliteRules.EliteVariantDef(i), Stats=EliteRules.EliteVariantStatsFor(i) });
data["PreparedArenas"] = ContentOrder.PreparedArenas.Select(i=>new {Id=ArenaCatalogRules.StableId(i), Enum=i.ToString()});
data["Constants"] = typeof(ContentCatalog).Assembly.GetTypes().Where(t=>t.Namespace=="VoidFall.Core").SelectMany(t=>t.GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.IsLiteral).Select(f=>new { Key=t.Name+"."+f.Name, Value=f.GetRawConstantValue() })).ToDictionary(x=>x.Key,x=>x.Value);
File.WriteAllText(args[0], JsonSerializer.Serialize(data, options));
Console.WriteLine($"Exported {ContentCatalog.Weapons.Length} weapons, {ExtendedCatalog.SupportCount} supports, {ContentCatalog.Evolutions.Length} evolutions.");
