using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    public static class ApprovedMapContent
    {
        public const string OriginalKnightId = "court-knight-original";
        public static readonly string[] CourtFamilies = { "pawn", "rook", "bishop", "knight", "queen", "armored-knight" };
        private static readonly string[] CourtBaseIds = { "court-pawn", "court-rook", "court-bishop", "court-knight", "court-queen", "court-armored-knight" };
        public static readonly string[] InsectIds = { "hydra-needlewasp", "hydra-hookmantis", "hydra-blisterbeetle", "hydra-sawroach", "hydra-mourningmoth" };
        public static readonly EnemyDefinition[] Enemies = Build();
        public static EnemyDefinition FindEnemy(string id) { foreach(var e in Enemies) if(e.Id==id) return e; return null; }
        public static int CourtType(string id)
        {
            if(string.IsNullOrEmpty(id)||!id.StartsWith("court-",StringComparison.Ordinal))return -1;
            for(var i=0;i<CourtBaseIds.Length;i++) if(id==CourtBaseIds[i] || id.Length>CourtBaseIds[i].Length&&id[CourtBaseIds[i].Length]=='-'&&id.StartsWith(CourtBaseIds[i],StringComparison.Ordinal)) return i;
            return -1;
        }
        public static int CourtRank(string id) => id.EndsWith("-elite",StringComparison.Ordinal)?3:id.EndsWith("-iii",StringComparison.Ordinal)?2:id.EndsWith("-ii",StringComparison.Ordinal)?1:0;
        public static string CourtId(int type,int rank) => "court-"+CourtFamilies[type]+(rank==0?"":rank==1?"-ii":rank==2?"-iii":"-elite");
        public static int InsectKind(string id) => Array.IndexOf(InsectIds,id);
        private static EnemyDefinition[] Build()
        {
            var entries=new List<EnemyDefinition>();
            var hp=new double[]{30,140,65,65,180,60}; var speed=new double[]{69,42,37,78,32,82.8}; var radius=new double[]{20,30,24,24,32,40};
            for(var type=0;type<6;type++) for(var tier=0;tier<(type==1?4:3);tier++)
            {
                var e=Enemy(CourtId(type,tier),CourtFamilies[type]+" "+new[]{"I","II","III","Elite"}[tier],
                    hp[type]*new[]{1,1.65,2.5,4}[tier],speed[type]*new[]{1,1.09,1.17,1.12}[tier],radius[type]*(1+tier*.1),"#d6d6cc",8+tier*3);
                e.AttackCooldown=new[]{4.5,4.2,3.8,4.2}[tier];e.TelegraphSeconds=type==4?2.7:new[]{1.2,1.05,.95,1.25}[tier];e.RecoverySeconds=.75;e.PreferredDistance=type==2?340:360;e.ProjectileSpeed=type==2?300:250;
                entries.Add(e);
            }
            var originalKnight=Enemy(OriginalKnightId,"Knight",55,100,17,"#f9fafb",18);
            originalKnight.AttackCooldown=4.5;originalKnight.TelegraphSeconds=1.2;originalKnight.RecoverySeconds=.75;
            entries.Add(originalKnight);
            for(var i=0;i<5;i++) entries.Add(Enemy(InsectIds[i],new[]{"Needlewasp","Hookmantis","Blisterbeetle","Sawroach","Mourningmoth"}[i],new[]{65d,85,60,110,65}[i],new[]{65d,72,60,48,55}[i],new[]{21d,23,24,26,23}[i]*.9,"#a0ac73",10));
            entries.Add(Enemy("hydra-hive","Hydra Hive",180,0,65,"#697651",0));
            entries.Add(Enemy("hydra-mantis-matriarch","Mantis Matriarch",480,52,60,"#c1bb80",22));
            entries.Add(Enemy("hydra-iron-carapace","Iron Carapace",480,35,65,"#c1bb80",22));
            return entries.ToArray();
        }
        private static EnemyDefinition Enemy(string id,string name,double hp,double speed,double radius,string color,double damage) => new EnemyDefinition
        { Id=id,Name=name,Behavior=id,Health=hp,Speed=speed,Radius=radius,ContactDamage=damage,Xp=id=="hydra-hive"?8:3,Color=color,NaturalStartSeconds=0 };
    }
}
