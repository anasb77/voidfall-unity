using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed class ArenaPlateAsset : ScriptableObject
    {
        public const int CurrentSchema = 1;

        [SerializeField] private ArenaId _arena;
        [SerializeField] private Sprite _baseSprite;
        [SerializeField] private Sprite _detailSprite;
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private int _schema = CurrentSchema;

        public ArenaId Arena => _arena;
        public Sprite BaseSprite => _baseSprite;
        public Sprite DetailSprite => _detailSprite;
        public int Width => _width;
        public int Height => _height;
        public int Schema => _schema;

        public bool IsValidFor(ArenaId arena)
        {
            return _schema == CurrentSchema &&
                   _arena == arena &&
                   _baseSprite != null &&
                   _detailSprite != null &&
                   _width > 0 &&
                   _height > 0;
        }
    }
}
