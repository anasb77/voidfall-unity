using System;
using UnityEngine;

namespace VoidFall.Runtime
{
    public sealed class EonSeaVisualAsset : ScriptableObject
    {
        [SerializeField] private Sprite[] _ground = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] _ice = Array.Empty<Sprite>();
        [SerializeField] private Sprite _slippery;
        public Sprite Ground(int variant) => _ground.Length == 4 ? _ground[Math.Abs(variant) % 4] : null;
        // Kind order: mass, brittle, wall, wave; eight authored variants each.
        public Sprite Ice(int kind, int variant) => _ice.Length == 32 ? _ice[Mathf.Clamp(kind, 0, 3) * 8 + Math.Abs(variant) % 8] : null;
        public Sprite Slippery => _slippery;
        public bool IsValid => _ground.Length == 4 && _ice.Length == 32 && _slippery != null;
    }
}
