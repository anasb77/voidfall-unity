using System;
using System.Collections;
using System.IO;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        internal void BeginArsenalValidation(string weapon, int rank, bool evolved)
        {
            _diagnosticRunSeedOverride = 0x41525345;
            StartRunInternal(true);
            Array.Clear(_upgradeProgress.WeaponRanks, 0, _upgradeProgress.WeaponRanks.Length);
            Array.Clear(_upgradeProgress.Evolved, 0, _upgradeProgress.Evolved.Length);
            for (var i = 0; i < ContentCatalog.Weapons.Length; i++)
            {
                if (i < 6 && weapon == "all") continue;
                if (weapon != "all" && ContentCatalog.Weapons[i].Id != weapon) continue;
                _upgradeProgress.WeaponRanks[i] = Mathf.Clamp(rank, 1, 6);
                _upgradeProgress.Evolved[i] = evolved;
            }
            if (Array.TrueForAll(_upgradeProgress.WeaponRanks, value => value == 0))
                throw new ArgumentException("Use a weapon ID or all for -vfarsenal.");
            RecalculatePlayerStats(false);
            _pistolRank = ArsenalRank(0);
            UpdateHud(); SyncUiScreen();
        }

        internal IEnumerator CaptureArsenalValidation(string output)
        {
            Directory.CreateDirectory(output);
            Screen.SetResolution(1440, 900, false);
            yield return new WaitForSecondsRealtime(1);
            var captures = 0;
            foreach (var weapon in new[] { "mines", "summons", "clock", "boomerang" })
            {
                for (var variant = 0; variant < (weapon == "clock" ? 4 : 3); variant++)
                {
                    BeginArsenalValidation(weapon, variant == 0 ? 1 : variant == 3 ? 3 : 6, variant == 2);
                    enabled = false;
                    DestroyEnemiesForVoidTransition();
                    ClearMeteors(); ClearNebulaStrikes();
                    _gameSim.Player.Iframes = float.PositiveInfinity;
                    for (var i = 0; i < 10; i++)
                    {
                        var a = i * Mathf.PI * 2 / 10;
                        var radius = weapon == "clock" ? 92 : weapon == "mines" ? 100 : 180;
                        SpawnEnemy("chaser", new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                    }
                    for (var i = 0; i < _gameSim.Enemies.Length; i++)
                    {
                        var enemy = _gameSim.Enemies[i];
                        if (!enemy.Active) continue;
                        enemy.Health = enemy.MaxHealth = 1500; enemy.Age = 2; enemy.Speed = 22;
                        _gameSim.Enemies[i] = enemy;
                    }
                    // Capture travelling weapons before they return/despawn; evolved summons include an impact.
                    var captureTicks = weapon == "summons" ? (variant == 2 ? 30 : 18) : weapon == "boomerang" ? 15 : 85;
                    for (var tick = 0; tick < captureTicks; tick++)
                    {
                        const float dt = 1f / 60;
                        _time += dt; _ambientClock += dt;
                        if (weapon == "mines") _gameSim.Player.Position = new Vector2(Mathf.Sin(tick * .035f) * 110, 0);
                        UpdateEnemies(dt); RebuildEnemyGrid(); UpdateArsenalWeapons(dt);
                        AdvanceArsenalCaptureFx(dt);
                        Render(); UpdateHud(); SyncUiScreen();
                        yield return null;
                    }
                    // Keep a waiting squad visible as a separate, explicit behavioral capture.
                    yield return new WaitForEndOfFrame();
                    var name = weapon + (variant == 0 ? "-rank1" : variant == 1 ? "-rank6" : variant == 2 ? "-evolved" : "-rank3") + ".png";
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, name)); captures++;
                    yield return new WaitForSecondsRealtime(.2f);
                    if (weapon == "summons" && variant == 0)
                    {
                        DestroyEnemiesForVoidTransition(); ResetArsenalWeapons();
                        _weaponCooldowns[7] = 0;
                        for (var tick = 0; tick < 90; tick++) { _time += 1f / 60; UpdateArsenalWeapons(1f / 60); AdvanceArsenalCaptureFx(1f / 60); Render(); yield return null; }
                        yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(output, "summons-idle.png")); captures++;
                        yield return new WaitForSecondsRealtime(.2f);
                    }
                }
            }
            yield return CaptureApprovedWeaponValidation(output);
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Captured " + captures + " arsenal states plus 14 approved projectile states and a mine chain. Profile isolated.");
            Debug.Log("ARSENAL CAPTURES COMPLETE " + captures);
            Application.Quit(0);
        }

        private void AdvanceArsenalCaptureFx(float dt)
        {
            UpdateDeathGhosts(dt); UpdateSourceParticles(dt); UpdateRingWaves(dt); UpdateFloaters(dt);
        }
    }
}
