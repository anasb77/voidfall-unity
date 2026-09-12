using System;
using UnityEngine;
namespace VoidFall.Runtime
{
    public sealed class ArenaMeshAuditCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string arena, role, path, meshType;
            public bool live;
            public int width, height, vertices, triangles;
            public Sprite sprite;
        }
        public Entry[] entries;
    }
}
