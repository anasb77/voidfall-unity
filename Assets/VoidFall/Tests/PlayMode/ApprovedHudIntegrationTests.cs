using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class ApprovedHudIntegrationTests
    {
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private bool _enabled;
        [UnitySetUp] public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            Call("StartRunInternal", true, false);
            yield return null;
        }
        [TearDown] public void TearDown() { _profile?.Dispose(); _runtime.enabled = _enabled; }

        [Test] public void Approved_hud_uses_bundled_font_live_hp_pressure_and_separate_slots()
        {
            Call("UpdateHud");
            var time = Get<Text>("_timeText");
            Assert.That(time.font.name, Does.Contain("ChakraPetch"));
            Assert.That(Get<Text>("_healthLabelText").text, Is.EqualTo("HP"));
            Assert.That(Get<Text>("_healthValueText").text, Does.Contain("/"));
            Assert.That(Get<Text>("_pressureText").text, Does.Contain("1.00"));
            Assert.That(Get<Image>("_approvedManualSlot").gameObject.activeSelf, Is.True);
            Assert.That(Get<Image>("_approvedLevelFrame").enabled, Is.True);
            var slots = Get<Image[]>("_approvedEmptySlots");
            Assert.That(slots[0].gameObject.activeSelf, Is.False);
            Assert.That(slots[4].gameObject.activeSelf, Is.True);
            Assert.That(Get<Text>("_approvedArsenalLabel").text, Does.Contain("1 / 4"));
        }

        [Test] public void Full_inventory_packs_into_bottom_grid_and_second_wind_displays_cooldown()
        {
            var progress = Get<UpgradeProgress>("_upgradeProgress");
            for (var i=0;i<progress.SupportRanks.Length;i++) progress.SupportRanks[i]=1;
            for (var i=0;i<progress.LateRanks.Length;i++) progress.LateRanks[i]=1;
            Call("RefreshApprovedBuildHud");
            var slots=Get<Image[]>("_supportChipBackgrounds");
            foreach(var slot in slots)
            {
                Assert.That(slot.gameObject.activeSelf, Is.True);
                Assert.That(slot.rectTransform.anchorMin, Is.EqualTo(new Vector2(1,0)));
                Assert.That(slot.rectTransform.anchoredPosition.y, Is.GreaterThan(0));
                Assert.That(slot.GetComponent<ApprovedHudSlot>().Title, Is.Not.Empty);
            }
            Set("_secondWindRemaining",168f);
            Call("UpdateApprovedHudCooldown");
            var second=Get<ApprovedHudSlot>("_approvedSecondWind");
            Assert.That(second.transform.Find("Cooldown").GetComponent<Text>().text, Is.EqualTo("2:48"));
            Call("ShowApprovedSlot",second);
            Assert.That(Get<Image>("_approvedTooltip").isActiveAndEnabled,Is.True);
            Assert.That(Get<Text>("_approvedTooltipText").text,Does.Contain("Second Wind"));
            Call("HideApprovedSlot");
        }

        [Test] public void Opening_arrivals_deliver_eight_bodies_per_second_at_one_x_pressure()
        {
            Set("_time",10f);
            Set("_spawnTimer",0f);
            for(var i=0;i<60;i++) Call("UpdateSpawns",1f/60);
            Assert.That(_runtime.ActiveEnemiesCount, Is.InRange(8,10));
            Assert.That(_runtime.PressureHundredths, Is.EqualTo(100));
        }

        [Test] public void Legacy_swarm_is_a_closed_evenly_spaced_tier_one_ring()
        {
            Set("_time",180f);
            var introduced=Get<bool[]>("_restorationIntroduced");
            for(var i=0;i<introduced.Length;i++) introduced[i]=true;
            Set("_circleBeat",true);
            Assert.That(Call("DeployRestorationCircle"), Is.EqualTo(true));
            var sim=Get<object>("_gameSim");
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(20));
            var angles=new float[20]; var n=0; var green=0;
            foreach(var enemy in (Array)Field(sim,"Enemies"))
            {
                if(!(bool)Field(enemy,"Active"))continue;
                Assert.That(Field(enemy,"Roster"), Is.EqualTo(EnemyRoster.One));
                if((string)Field(enemy,"Id")=="swarmer")green++;
                var delta=(Vector2)Field(enemy,"Position")-(Vector2)Field(Field(sim,"Player"),"Position");
                angles[n++]=Mathf.Repeat(Mathf.Atan2(delta.y,delta.x),Mathf.PI*2);
            }
            Array.Sort(angles);
            for(var i=0;i<angles.Length;i++)
                Assert.That(Mathf.Repeat(angles[(i+1)%20]-angles[i],Mathf.PI*2),Is.EqualTo(Mathf.PI*2/20).Within(.002));
            Assert.That(green,Is.EqualTo(5));
            Assert.That(Get<float>("_nextLegacySwarmAt"),Is.EqualTo(214));
        }
        private object Call(string name,params object[] args)=>RuntimeTestReflection.Invoke(_runtime,name,args);
        private static object Field(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        private T Get<T>(string name)=>(T)_runtime.GetType().GetField(name,Flags).GetValue(_runtime);
        private void Set(string name, object value)=>_runtime.GetType().GetField(name,Flags).SetValue(_runtime,value);
    }
}
