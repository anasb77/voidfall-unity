using System;
using UnityEngine;

namespace VoidFall.Runtime
{
    public sealed class RosterProgressionVisualAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Id;
            public int Tier;
            public bool Elite;
            public Sprite Sprite;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public Sprite Find(string id, int tier, bool elite)
        {
            for (var i = 0; i < _entries.Length; i++)
                if (_entries[i].Id == id && _entries[i].Tier == tier && _entries[i].Elite == elite)
                    return _entries[i].Sprite;
            return null;
        }

        public int Count => _entries.Length;
    }
}
