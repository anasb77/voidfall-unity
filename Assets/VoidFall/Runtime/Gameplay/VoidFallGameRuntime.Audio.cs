using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime.Rendering;
using VoidFall.UI;
namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        // Sidecar ownership follows pickup slots; it never enters combat state or RNG.
        private readonly bool[] _musicMagnetSlots = new bool[MaxPickupSlots];
        private int _musicPendingMagnetGems;
        private bool _musicCriticalHealth;

        private void SetupAudio()
        {
            _audio = gameObject.AddComponent<ProceduralAudio>();
            if (!HasCommandLineArgument("-vfno-music"))
                _music = gameObject.AddComponent<MusicDirector>();
        }

        private void ResetMusicMagnetCollection()
        {
            Array.Clear(_musicMagnetSlots, 0, _musicMagnetSlots.Length);
            _musicPendingMagnetGems = 0;
        }

        private void CountPendingMusicGems()
        {
            _musicPendingMagnetGems = 0;
            for (var slot = 0; slot < _musicMagnetSlots.Length; slot++)
            {
                if (!_musicMagnetSlots[slot]) continue;
                var pickup = _gameSim.Pickups[slot];
                if (!pickup.Active || pickup.Kind != PickupKind.Xp || !pickup.Pull)
                    _musicMagnetSlots[slot] = false;
                else _musicPendingMagnetGems++;
            }
        }

        private bool UpdateMusicCriticalHealth(bool alive)
        {
            var fraction = _gameSim.Player.MaxHealth > 0
                ? _gameSim.Player.Health / _gameSim.Player.MaxHealth : 1f;
            _musicCriticalHealth = alive && fraction <= (_musicCriticalHealth ? .25f : .20f);
            return _musicCriticalHealth;
        }
    }
}
