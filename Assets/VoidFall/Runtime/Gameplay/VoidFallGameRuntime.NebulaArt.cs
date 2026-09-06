using UnityEngine;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private readonly Sprite[] _nebulaRockSprites = new Sprite[2];

        private Sprite NebulaRockSprite(bool explosive)
        {
            var slot = explosive ? 1 : 0;
            if (_nebulaRockSprites[slot] != null) return _nebulaRockSprites[slot];
            var texture = Resources.Load<Texture2D>("VoidFall/NebulaMeteors");
            if (texture == null) return ProceduralSpriteFactory.Meteor(0, explosive);
            var width = texture.width / 2;
            _nebulaRockSprites[slot] = Sprite.Create(texture, new Rect(slot * width, 0, width, texture.height),
                new Vector2(.5f, .5f), width, 0, SpriteMeshType.FullRect);
            _nebulaRockSprites[slot].name = explosive ? "Nebula Volatile Rock" : "Nebula Ordinary Rock";
            return _nebulaRockSprites[slot];
        }

        private void DestroyNebulaArt()
        {
            foreach (var sprite in _nebulaRockSprites) if (sprite != null) Destroy(sprite);
        }
    }
}
