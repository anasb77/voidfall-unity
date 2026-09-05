using System.IO;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private string _visualCaptureNullCity;

        private void PrepareNullCityCaptureProfile()
        {
            if (string.IsNullOrEmpty(_visualCaptureNullCity)) return;
            var path = string.IsNullOrEmpty(_visualCapturePath)
                ? Path.Combine(Application.temporaryCachePath, "null-city-capture.profile.json")
                : Path.GetFullPath(_visualCapturePath) + ".profile.json";
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            _saveStore = new SaveStore(path);
            _saveData = SaveStore.CreateDefault();
            _saveData.arena = "null-city";
            _runSaved = true;
        }

        private void BeginNullCityCapture()
        {
            if (string.IsNullOrEmpty(_visualCaptureNullCity)) return;
            _arenaId = ArenaId.NullCity;
            _voidRoute = null;
            _gameSim.Player.Position = Vector2.zero;
            _cameraFollowPosition = Vector2.zero;
            ClearHydraBossArena();
            BeginObjectiveForCurrentArena();
            SelectRecipeForCurrentArena();
            PrepareMenuArenaCatalogue();
            TryInstallPreparedArenaPlate(_arenaId);
            _gameSim.Player.Iframes = 10000f;
            if (_visualCaptureNullCity == "motherload" || _visualCaptureNullCity == "tractor")
            {
                BeginNullCityBossEncounter();
                if (_visualCaptureNullCity == "tractor" && _nullCityBossSlot >= 0)
                {
                    var boss = _gameSim.Bosses[_nullCityBossSlot];
                    boss.State = 0;
                    boss.AttackAngle = Mathf.PI;
                    _gameSim.Bosses[_nullCityBossSlot] = boss;
                    _nullCityMove = MotherloadMove.Tractor;
                    _nullCityTractorClock = 4f;
                    _nullCityAim = Mathf.PI;
                    _nullCityWarnClock = 0f;
                }
            }
            else
            {
                for (var type = 0; type < 9; type++)
                    SpawnNullCityUnit(type, NullCityWorld(375f + (type % 3) * 335f, 300f + (type / 3) * 155f));
                _nullCityElapsed = _visualCaptureNullCity == "lockdown" ? 25f : 6f;
            }
            _visualCaptureFramesRemaining = 180;
        }

        private void MaintainNullCityCapture()
        {
            if (string.IsNullOrEmpty(_visualCaptureNullCity)) return;
            // Pin only diagnostic poses, not production clocks. Actors and props still animate.
            _gameSim.Player.Iframes = 10000f;
            if (_visualCaptureNullCity == "lockdown") _nullCityElapsed = 25f;
            if (_visualCaptureNullCity == "surveillance") _nullCityElapsed = 6f;
            if (_visualCaptureNullCity == "tractor" && _nullCityBossActive)
            {
                _nullCityMove = MotherloadMove.Tractor;
                _nullCityTractorClock = 2f;
                _nullCityWarnClock = _nullCityVentClock = 0f;
            }
        }
    }
}
