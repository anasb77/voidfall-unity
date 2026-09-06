using System.IO;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private string _visualCaptureCrascendo;

        private void PrepareCrascendoCaptureProfile()
        {
            if (string.IsNullOrEmpty(_visualCaptureCrascendo)) return;
            var path = string.IsNullOrEmpty(_visualCapturePath) ? Path.Combine(Application.temporaryCachePath, "crascendo-capture.profile.json") : Path.GetFullPath(_visualCapturePath) + ".profile.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            _saveStore = new SaveStore(path); _saveData = SaveStore.CreateDefault(); _saveData.arena = "crascendo"; _runSaved = true;
        }

        private void BeginCrascendoCapture()
        {
            if (string.IsNullOrEmpty(_visualCaptureCrascendo)) return;
            _arenaId = ArenaId.Crascendo;
            _time = _visualCaptureCrascendo == "late" || _visualCaptureCrascendo == "elites" ? 2400 : 0;
            _voidRoute = new VoidRouteRun(new[] { new VoidRouteNode("crascendo", "Crascendo", 0, 1, "GROWING ENEMIES", "Crying obsidian", "Survive", "Boss rewards") }, "crascendo");
            _gameSim.Player.Position = Vector2.zero; _cameraFollowPosition = Vector2.zero;
            ClearHydraBossArena(); BeginObjectiveForCurrentArena(); SelectRecipeForCurrentArena();
            PrepareMenuArenaCatalogue(); TryInstallPreparedArenaPlate(_arenaId);
            var ids = new[] { "chaser", "runner", "gunner", "twinGunner", "dasher", "brute", "exploder", "guard", "technician", "mortar", "splitter", "bulwark", "harvester", "carrier" };
            for (var i = 0; i < ids.Length; i++)
                SpawnEnemy(ids[i], new Vector2(-470 + i % 5 * 225, -230 + i / 5 * 205), forcedRoster: _time > 2000 ? EnemyRoster.Four : EnemyRoster.One);
            if (_visualCaptureCrascendo == "elites")
            {
                SpawnEnemy("exploder", new Vector2(-280, 160), EliteVariantId.Exploder, forcedRoster: EnemyRoster.Four);
                SpawnEnemy("mortar", new Vector2(80, 180), EliteVariantId.Mortar, forcedRoster: EnemyRoster.Four);
                SpawnEnemy("gunner", new Vector2(360, 100), EliteVariantId.Gunner, forcedRoster: EnemyRoster.Four);
            }
            if (_visualCaptureCrascendo == "boss") SpawnBoss("warden", 1, 1);
            _crascendoElapsed = _visualCaptureCrascendo == "mid" ? 150 : _visualCaptureCrascendo == "early" ? 0 : 300;
            if (_visualCaptureCrascendo == "boss")
                for (var i = 0; i < _gameSim.Bosses.Length; i++) if (_gameSim.Bosses[i].Active)
                    { _gameSim.Bosses[i].State = 0; _gameSim.Bosses[i].Position = new Vector2(240, 80); for (var hit = 0; hit < 20; hit++) ApplyBossDamage(i, .001f); }
            if (_visualCaptureCrascendo == "growth")
            {
                for (var i = 0; i < _gameSim.Enemies.Length; i++)
                    if (_gameSim.Enemies[i].Active && (i == 0 || i == 5))
                        for (var hit = 0; hit < 20; hit++) ApplyEnemyDamage(i, .001f, Vector2.zero, 0, false);
                SpawnEnemy("gunner", new Vector2(300, 150), EliteVariantId.Gunner, forcedRoster: EnemyRoster.Four);
                for (var i = 0; i < _gameSim.Enemies.Length; i++) if (_gameSim.Enemies[i].Active && _gameSim.Enemies[i].Elite)
                    for (var hit = 0; hit < 20; hit++) ApplyEnemyDamage(i, .001f, Vector2.zero, 0, false);
            }
            _gameSim.Player.Iframes = 10000; _visualCaptureFramesRemaining = 150;
        }

        private void MaintainCrascendoCapture()
        {
            if (string.IsNullOrEmpty(_visualCaptureCrascendo)) return;
            // A hidden validation player loses focus. Only this opt-in capture
            // path resumes itself; production focus/pause behavior is untouched.
            SetApplicationActive(true);
            _paused = false;
            if (_levelUpActive) SelectLevelOption(0);
            _gameSim.Player.Iframes = 10000;
        }
    }
}
