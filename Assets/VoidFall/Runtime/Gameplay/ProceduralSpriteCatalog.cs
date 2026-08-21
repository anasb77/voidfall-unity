using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidFall.Runtime
{
    [Serializable]
    public sealed class ProceduralSpriteCatalogEntry
    {
        [SerializeField] private string _key;
        [SerializeField] private Sprite _sprite;

        public string Key => _key;
        public Sprite Sprite => _sprite;

        public ProceduralSpriteCatalogEntry(string key, Sprite sprite)
        {
            _key = key;
            _sprite = sprite;
        }
    }

    public sealed class ProceduralSpriteCatalog : ScriptableObject
    {
        public const int CurrentSchema = 1;

        [SerializeField] private int _schema = CurrentSchema;
        [SerializeField] private List<ProceduralSpriteCatalogEntry> _entries =
            new List<ProceduralSpriteCatalogEntry>();

        public int Schema => _schema;
        public IReadOnlyList<ProceduralSpriteCatalogEntry> Entries => _entries;
        public int Count => _entries == null ? 0 : _entries.Count;

        public bool IsUsable()
        {
            if (_schema != CurrentSchema || _entries == null || _entries.Count == 0) return false;
            for (var index = 0; index < _entries.Count; index++)
            {
                var entry = _entries[index];
                if (entry == null || string.IsNullOrEmpty(entry.Key) || entry.Sprite == null) return false;
            }
            return true;
        }

        internal void ReplaceEntries(List<ProceduralSpriteCatalogEntry> entries)
        {
            _schema = CurrentSchema;
            _entries = entries ?? new List<ProceduralSpriteCatalogEntry>();
        }
    }
}
