using System;
using NUnit.Framework;
using UnityEngine;

namespace VoidFall.Tests.PlayMode
{
    public sealed partial class ArsenalIntegrationTests
    {
        private Array Summons => (Array)Get("_arsenalSummons");
        private Array Mines => (Array)Get("_arsenalMines");
        private void PlaceMine(int slot, Vector2 position, float age = 1)
        {
            var mine = Activator.CreateInstance(Mines.GetType().GetElementType());
            FieldSet(mine, "Active", true); FieldSet(mine, "Rank", 1); FieldSet(mine, "Age", age);
            FieldSet(mine, "Position", position); FieldSet(mine, "MineIdentity", slot + 1);
            Mines.SetValue(mine, slot);
        }

        [TestCase(false)] [TestCase(true)]
        public void Mine_chain_waits_point_fourteen_seconds_and_propagates_once_in_either_slot_order(bool reverse)
        {
            Equip(6); Enemy(0, Vector2.zero);
            var source = reverse ? 2 : 0; var last = reverse ? 0 : 2;
            PlaceMine(source, Vector2.zero); PlaceMine(1, new Vector2(80, 0)); PlaceMine(last, new Vector2(160, 0));
            Call("StepArsenalMines", .01f);
            Assert.That((bool)Field(Mines.GetValue(source), "Active"), Is.False);
            Assert.That((bool)Field(Mines.GetValue(1), "Active"), Is.True);
            Assert.That((int)Field(Mines.GetValue(1), "ChainParentIdentity"), Is.EqualTo(source + 1));
            Call("StepArsenalMines", .13f);
            Assert.That((bool)Field(Mines.GetValue(1), "Active"), Is.True);
            Call("StepArsenalMines", .01f);
            Assert.That((bool)Field(Mines.GetValue(1), "Active"), Is.False);
            Assert.That((bool)Field(Mines.GetValue(last), "Active"), Is.True);
            Call("StepArsenalMines", .14f);
            Assert.That(Active("_arsenalMines"), Is.Zero);
            var damage = ((double[])Get("_weaponDamage"))[6];
            Call("StepArsenalMines", 1f);
            Assert.That(((double[])Get("_weaponDamage"))[6], Is.EqualTo(damage));
        }

        [Test]
        public void Chain_ignores_unarmed_and_out_of_radius_mines_and_travel_cancels_pending_work()
        {
            Equip(6); Enemy(0, Vector2.zero);
            PlaceMine(0, Vector2.zero); PlaceMine(1, new Vector2(80, 0), .1f); PlaceMine(2, new Vector2(100, 0));
            PlaceMine(3, new Vector2(0, 80));
            Call("StepArsenalMines", .01f);
            Assert.That((float)Field(Mines.GetValue(1), "ChainDueAge"), Is.Zero);
            Assert.That((float)Field(Mines.GetValue(2), "ChainDueAge"), Is.Zero);
            Assert.That((float)Field(Mines.GetValue(3), "ChainDueAge"), Is.GreaterThan(0));
            var damage = ((double[])Get("_weaponDamage"))[6];
            Call("ClearTransitionProjectiles"); Call("StepArsenalMines", 1f);
            Assert.That(Active("_arsenalMines"), Is.Zero);
            Assert.That(((double[])Get("_weaponDamage"))[6], Is.EqualTo(damage));
        }

        [Test]
        public void Summons_distribute_and_retain_targets_when_player_changes_position()
        {
            Equip(7); Enemy(0, new Vector2(250, 0)); Enemy(1, new Vector2(250, 80)); Step(.01f);
            var a = Field(Summons.GetValue(0), "Target"); var b = Field(Summons.GetValue(1), "Target");
            Assert.That((bool)Field(a, "Valid") && (bool)Field(b, "Valid"), Is.True);
            Assert.That((int)Field(a, "Identity"), Is.Not.EqualTo((int)Field(b, "Identity")));
            PlayerPosition = new Vector2(-250, 0);
            Call("StepArsenalSummons", .01f);
            Assert.That((int)Field(Field(Summons.GetValue(0), "Target"), "Identity"), Is.EqualTo((int)Field(a, "Identity")));
            Assert.That((int)Field(Field(Summons.GetValue(1), "Target"), "Identity"), Is.EqualTo((int)Field(b, "Identity")));
        }

        [Test]
        public void Summon_acquisition_uses_unit_position_and_does_not_inherit_a_recycled_target()
        {
            Equip(7); Step(.01f);
            var unit = Summons.GetValue(0); FieldSet(unit, "Position", new Vector2(200, 0)); Summons.SetValue(unit, 0);
            Enemy(0, new Vector2(500, 0), 500);
            Call("StepArsenalSummons", .01f);
            Assert.That((int)Field(Field(Summons.GetValue(0), "Target"), "Identity"), Is.EqualTo(500));
            var enemy = Enemies.GetValue(0); FieldSet(enemy, "SpawnId", 501); FieldSet(enemy, "Position", new Vector2(1000, 0)); Enemies.SetValue(enemy, 0);
            Call("StepArsenalSummons", .01f);
            Assert.That((bool)Field(Field(Summons.GetValue(0), "Target"), "Valid"), Is.False);
            Assert.That(Health(0), Is.EqualTo(1000));
        }

        [Test]
        public void Summons_return_without_damage_when_player_leaves_the_leash()
        {
            Equip(7); Enemy(0, new Vector2(250, 0)); Step(.01f);
            PlayerPosition = new Vector2(-800, 0);
            Call("StepArsenalSummons", .1f);
            Assert.That((bool)Field(Summons.GetValue(0), "Returning"), Is.True);
            Assert.That((bool)Field(Field(Summons.GetValue(0), "Target"), "Valid"), Is.False);
            Assert.That(Health(0), Is.EqualTo(1000));
            Assert.That(((Vector2)Field(Summons.GetValue(0), "Position")).x, Is.LessThan(0));
        }
    }
}
