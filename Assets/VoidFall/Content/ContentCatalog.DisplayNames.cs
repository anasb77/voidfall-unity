namespace VoidFall.Core
{
    public static partial class ContentCatalog
    {
        static ContentCatalog()
        {
            // Keep the historical serialized identity; apply owner-approved terminology
            // after generated source initialization, without editing the generated file.
            foreach (var enemy in Enemies)
                if (enemy.Id == "chaser") enemy.Name = "Regular";
            Weapons = ArsenalContent.AppendWeapons(Weapons);
            Evolutions = ArsenalContent.AppendEvolutions(Evolutions);
            foreach (var weapon in Weapons)
                weapon.Summary = UpgradeRules.AddProjectileDefenseText(weapon.Id, weapon.Summary);
        }
    }
}
