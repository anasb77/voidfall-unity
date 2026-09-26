using System;
using System.Collections.Generic;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    // Referenced by the arena plate: the arena's Addressables handle owns all
    // approved sprites, including animation frames and the full-size surface.
    public sealed class ApprovedMapVisualAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Id;
            public Sprite Sprite;
        }

        [SerializeField] private ArenaId _arena;
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        private Dictionary<string, Sprite> _byId;
        private HashSet<Sprite> _sprites;
        private bool _valid;

        public IReadOnlyList<Entry> Entries => _entries;
        public bool IsValidFor(ArenaId arena)
        {
            EnsureLookup();
            return _arena == arena && _valid;
        }

        public Sprite Find(string id)
        {
            EnsureLookup();
            return id != null && _byId.TryGetValue(id, out var sprite) ? sprite : null;
        }

        public bool OwnsSprite(Sprite sprite)
        {
            if (sprite == null) return false;
            EnsureLookup();
            return _sprites.Contains(sprite);
        }

        public static bool RequiredFor(ArenaId arena) => arena == ArenaId.Hydra ||
            arena == ArenaId.NullCity || arena == ArenaId.MonochromeCourt;

        public static bool TryGetOwner(string id, out ArenaId arena)
        {
            arena = ArenaId.Void;
            if (string.IsNullOrEmpty(id)) return false;
            if (id == "null-city" || id.StartsWith("city-", StringComparison.Ordinal)) arena = ArenaId.NullCity;
            else if (id.StartsWith("court-", StringComparison.Ordinal) || id.StartsWith("sentinel-", StringComparison.Ordinal)) arena = ArenaId.MonochromeCourt;
            else if (id.StartsWith("hydra-", StringComparison.Ordinal) || id.StartsWith("insect-", StringComparison.Ordinal) || id.StartsWith("guardian-", StringComparison.Ordinal)) arena = ArenaId.Hydra;
            else return false;
            return true;
        }

        private void EnsureLookup()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            _sprites = new HashSet<Sprite>();
            _valid = _entries != null && _entries.Length > 0;
            if (_entries == null) return;
            foreach (var entry in _entries)
            {
                if (entry == null || entry.Sprite == null || !TryGetOwner(entry.Id, out var owner) ||
                    owner != _arena || _byId.ContainsKey(entry.Id))
                {
                    _valid = false;
                    continue;
                }
                _byId.Add(entry.Id, entry.Sprite);
                _sprites.Add(entry.Sprite);
            }
        }

        private void OnValidate()
        {
            _byId = null;
            _sprites = null;
        }
    }
}
