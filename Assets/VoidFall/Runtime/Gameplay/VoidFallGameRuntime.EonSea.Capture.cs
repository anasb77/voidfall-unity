using System.IO;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private string _visualCaptureEonSea;

        private void PrepareEonSeaCaptureProfile()
        {
            if (string.IsNullOrEmpty(_visualCaptureEonSea)) return;
            var path = string.IsNullOrEmpty(_visualCapturePath) ? Path.Combine(Application.temporaryCachePath, "eon-capture.profile.json") : Path.GetFullPath(_visualCapturePath) + ".profile.json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            _saveStore = new SaveStore(path); _saveData = SaveStore.CreateDefault(); _saveData.arena = "eon-sea"; _runSaved = true;
        }

        private void BeginEonSeaCapture()
        {
            if (string.IsNullOrEmpty(_visualCaptureEonSea)) return;
            _arenaId = ArenaId.EonSea;
            _time = _visualCaptureEonSea == "late" || _visualCaptureEonSea == "elites" ? 2400 : 0;
            _voidRoute = new VoidRouteRun(new[] { new VoidRouteNode("eon-sea", "Eon Sea", 0, 1, "GLACIER", "Frozen sea", "Survive", "Boss rewards") }, "eon-sea");
            _gameSim.Player.Position = Vector2.zero; _cameraFollowPosition = Vector2.zero;
            ClearHydraBossArena(); BeginObjectiveForCurrentArena(); SelectRecipeForCurrentArena();
            PrepareMenuArenaCatalogue(); TryInstallPreparedArenaPlate(_arenaId); EnsureEonSeaTerrain();
            var ids = new[] { "chaser", "runner", "gunner", "twinGunner", "dasher", "brute", "exploder", "guard", "technician", "mortar", "splitter", "bulwark", "harvester", "carrier" };
            for (var i = 0; i < ids.Length; i++)
                SpawnEnemy(ids[i], new Vector2(-470 + i % 5 * 225, -230 + i / 5 * 205), forcedRoster: _time > 2000 ? EnemyRoster.Four : EnemyRoster.One);
            if (_visualCaptureEonSea == "elites")
            {
                SpawnEnemy("exploder", new Vector2(-280, 160), EliteVariantId.Exploder, forcedRoster: EnemyRoster.Four);
                SpawnEnemy("mortar", new Vector2(80, 180), EliteVariantId.Mortar, forcedRoster: EnemyRoster.Four);
                SpawnEnemy("gunner", new Vector2(360, 100), EliteVariantId.Gunner, forcedRoster: EnemyRoster.Four);
            }
            if (_visualCaptureEonSea == "frost" && _eonSeaTerrain.Ice.Count > 0)
            {
                var ice = _eonSeaTerrain.Ice[0]; ice.X = 80; ice.Y = 40; ice.Melt = .987f;
            }
            if (_visualCaptureEonSea == "boss") SpawnBoss("warden", 1, 1);
            _gameSim.Player.Iframes = 10000; _visualCaptureFramesRemaining = 150;
        }

        private void MaintainEonSeaCapture()
        {
            if (string.IsNullOrEmpty(_visualCaptureEonSea)) return;
            // A hidden validation player loses focus. Only this opt-in capture
            // path resumes itself; production focus/pause behavior is untouched.
            SetApplicationActive(true);
            _paused = false;
            if (_levelUpActive) SelectLevelOption(0);
            _gameSim.Player.Iframes = 10000;
            if (_visualCaptureEonSea == "frost" && _eonSeaTerrain != null) _eonSeaPlayerFreeze.Refresh(1, _eonSeaTerrain.Time);
        }
    }
}
