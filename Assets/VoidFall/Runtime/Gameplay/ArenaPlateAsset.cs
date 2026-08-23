using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed class ArenaPlateAsset : ScriptableObject
    {
        public const int CurrentSchema = 2;

        [SerializeField] private ArenaId _arena;
        [SerializeField] private Sprite _baseSprite;
        [SerializeField] private Sprite _detailSprite;
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        // Schema 2: the sparse detail pass (petals, edge rocks) bakes at a
        // lower tier than the sky base - 4K maps with 1440p elements - so it
        // carries its own dimensions for correct world scaling. Legacy
        // schema-1 assets report the base dimensions for both.
        [SerializeField] private int _detailWidth;
        [SerializeField] private int _detailHeight;
        [SerializeField] private int _schema = CurrentSchema;

        public ArenaId Arena => _arena;
        public Sprite BaseSprite => _baseSprite;
        public Sprite DetailSprite => _detailSprite;
        public int Width => _width;
        public int Height => _height;
        public int DetailWidth => _detailWidth > 0 ? _detailWidth : _width;
        public int DetailHeight => _detailHeight > 0 ? _detailHeight : _height;
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
