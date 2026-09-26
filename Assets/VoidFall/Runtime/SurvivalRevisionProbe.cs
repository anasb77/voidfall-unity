using System;
using System.Collections;
using System.IO;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        // Uses the existing opt-in restoration probe and its isolated profile.
        private IEnumerator CaptureSurvivalRevision(string output)
        {
            _upgradeProgress.SupportRanks[Array.FindIndex(ExtendedCatalog.AllSupports(), x => x.Id == "lifeSteal")] = 5;
            _upgradeProgress.SupportRanks[Array.FindIndex(ExtendedCatalog.AllSupports(), x => x.Id == "scavenger")] = 4;
            for (var i = 0; i < 12; i++)
                SpawnSpecialPickup(_gameSim.Player.Position + new Vector2(-170 + i % 4 * 105, -90 + i / 4 * 90), 1, PickupKind.Part);
            foreach (var maximum in new[] { 100f, 125f, 175f })
            {
                _gameSim.Player.MaxHealth = maximum; _gameSim.Player.Health = maximum * .7f;
                _dealerShield = 10; _shieldCapacity = 20;
                Render(); RefreshApprovedBuildHud(); UpdateHud(); SyncUiScreen();
                yield return CaptureRestorationFrame(output, "hp-" + maximum + ".png");
            }
            GrantPlayerShield(50, "future_capacity_probe");
            Render(); UpdateHud();
            yield return CaptureRestorationFrame(output, "shield-50.png");
            _levelOptions = new[]
            {
                new UpgradeOptionDefinition { Id = "lifeSteal", TargetId = "lifeSteal", Name = "Life Steal", Kind = UpgradeOptionKind.Support,
                    Description = "Heal 0.9 HP every 200 kills.", CurrentRank = 4, NextRank = 5, MaxRank = 5, Accent = "#fb7185" },
                new UpgradeOptionDefinition { Id = "scavenger", TargetId = "scavenger", Name = "Scavenger", Kind = UpgradeOptionKind.Support,
                    Description = "+20% Scrap drop chance. Collect 200 Scraps to gain 5 shields.", CurrentRank = 3, NextRank = 4, MaxRank = 4, Accent = "#efbd65" }
            };
            _levelUpActive = true; _paused = true;
            _ui.LevelUp.ShowUpgrades(BuildUpgradeCards(_levelOptions), 0, SelectLevelOption);
            SyncUiScreen();
            yield return CaptureRestorationFrame(output, "survival-cards.png");
            FinishRunExport("diagnostic_complete");
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Main-player survival HUD, three Scraps, capacity growth and card captures complete.");
            Application.Quit(0);
        }
    }
}
