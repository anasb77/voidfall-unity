using System.Linq;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class ApprovedMapRulesTests
    {
        [Test]
        public void Approved_camera_percentages_map_to_resolution_independent_world_heights()
        {
            Assert.That(ApprovedMapRules.CameraHeight("null-city"),Is.EqualTo(620+480*.5));
            Assert.That(ApprovedMapRules.CameraHeight("hydra"),Is.EqualTo(620+480*.6));
            Assert.That(ApprovedMapRules.CameraHeight("monochrome-court"),Is.EqualTo(908));
        }
        [Test]
        public void Territory_is_exactly_sixteen_fixed_cells_and_does_not_expand_at_corners()
        {
            var cells=0;for(var y=-5;y<=5;y++)for(var x=-5;x<=5;x++)if(ApprovedMapRules.InTerritory(x,y,0,0))cells++;
            Assert.That(cells,Is.EqualTo(16));Assert.That(ApprovedMapRules.InTerritory(2,0,0,0),Is.False);
        }
        [Test]
        public void Court_has_twenty_forms_and_horses_match_same_rank_pawn_speed()
        {
            Assert.That(MonochromeContent.Enemies.Length,Is.EqualTo(20));
            Assert.That(MonochromeContent.Enemies.Select(e=>e.Id).Distinct().Count(),Is.EqualTo(20));
            for(var rank=0;rank<3;rank++)
            {
                var pawn=ApprovedMapContent.FindEnemy(ApprovedMapContent.CourtId(0,rank));
                var horse=ApprovedMapContent.FindEnemy(ApprovedMapContent.CourtId(5,rank));
                Assert.That(horse.Speed,Is.EqualTo(pawn.Speed*1.2).Within(.00001));
                Assert.That(horse.Radius,Is.EqualTo(pawn.Radius*2).Within(.00001));
            }
        }
        [Test]
        public void Sentinel_color_alternates_without_changing_mid_warning_and_each_color_has_eight_cells()
        {
            for(var slot=0;slot<10;slot++)for(var cycle=0;cycle<3;cycle++)
            {
                var time=14f+cycle*14f-slot*2.31f%14f;
                var white=ApprovedMapRules.SentinelWhite(time+.1f,slot);
                Assert.That(ApprovedMapRules.SentinelWhite(time+3.44f,slot),Is.EqualTo(white));
                Assert.That(ApprovedMapRules.SentinelWhite(time+14.1f,slot),Is.Not.EqualTo(white));
                var count=0;for(var y=-2;y<2;y++)for(var x=-2;x<2;x++)if(ApprovedMapRules.CellMatches(x,y,white))count++;
                Assert.That(count,Is.EqualTo(8));
            }
            Assert.That(ApprovedMapRules.SentinelWhite(0,0),Is.False);
        }
        [Test]
        public void City_sampling_has_five_new_pursuers_and_preserves_ordinary_majority()
        {
            var simple=0;var additions=new System.Collections.Generic.HashSet<int>();
            for(var i=0;i<10000;i++){var type=ApprovedMapRules.CityAmbient((i+.5)/10000d);if(type>=17||type==3)simple++;if(type>=12)additions.Add(type);}
            Assert.That(additions.Count,Is.EqualTo(10));Assert.That(simple,Is.InRange(7100,7150));
            Assert.That(NullCityContent.Enemies.Length,Is.EqualTo(22));
        }
    }
}
