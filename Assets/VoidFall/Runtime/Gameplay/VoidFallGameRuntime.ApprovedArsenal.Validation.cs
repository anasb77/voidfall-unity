using System.Collections;
using System.IO;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private IEnumerator CaptureApprovedWeaponValidation(string output)
        {
            foreach (var weapon in new[] { 0, 2 })
                for (var variant = 1; variant <= 7; variant++)
                {
                    var rank = Mathf.Min(variant, 6);
                    BeginArsenalValidation(ContentCatalog.Weapons[weapon].Id, rank, variant == 7);
                    enabled = false; DestroyEnemiesForVoidTransition(); ClearMeteors(); ClearNebulaStrikes();
                    SpawnEnemy("chaser", new Vector2(380, 80));
                    var slot = _gameSim.EnemyOrder[0];
                    var enemy = _gameSim.Enemies[slot]; enemy.Age = 2; enemy.Health = enemy.MaxHealth = 10000; _gameSim.Enemies[slot] = enemy;
                    RebuildEnemyGrid();
                    FireWeapon(weapon, ContentCatalog.Weapons[weapon].Ranks[rank - 1].Stats, rank, FindNearestHostile(1000));
                    for (var tick = 0; tick < 8; tick++)
                    {
                        _time += 1f / 120; UpdateBullets(1f / 120); AdvanceArsenalCaptureFx(1f / 120);
                        Render(); UpdateHud(); SyncUiScreen(); yield return null;
                    }
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, ContentCatalog.Weapons[weapon].Id + (variant == 7 ? "-evolved" : "-rank" + rank) + ".png"));
                    yield return new WaitForSecondsRealtime(.15f);
                }
            BeginArsenalValidation("mines", 6, true);
            enabled = false; DestroyEnemiesForVoidTransition(); ClearMeteors(); ClearNebulaStrikes();
            _gameSim.Player.Position = new Vector2(0, -100);
            for (var i = 0; i < 5; i++)
                _arsenalMines[i] = new ArsenalEntity { Active = true, Age = 1, Rank = 6, Evolved = true,
                    Position = new Vector2(-200 + i * 100, 40), MineIdentity = ++_nextArsenalMineIdentity };
            SpawnEnemy("chaser", new Vector2(-200, 40));
            var first = _gameSim.EnemyOrder[0]; var target = _gameSim.Enemies[first];
            target.Age = 2; target.Health = target.MaxHealth = 10000; _gameSim.Enemies[first] = target; RebuildEnemyGrid();
            for (var tick = 0; tick < 26; tick++)
            {
                _time += 1f / 120; StepArsenalMines(1f / 120); AdvanceArsenalCaptureFx(1f / 120);
                Render(); UpdateHud(); SyncUiScreen(); yield return null;
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "mines-chain.png"));
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
