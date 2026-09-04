using System;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.UI;

namespace VoidFall.Runtime.Rendering
{
    /// <summary>
    /// The Workshop's live frame preview. Rebuilds the legacy browser build's
    /// animated canvas with uGUI Images that share the same baked sprites and
    /// placement math as the in-game player cosmetics, so the preview always
    /// shows exactly the frame you play with.
    ///
    /// </summary>
    public sealed class PlayerFramePreview : MonoBehaviour
    {
        private readonly Image[] _cosmeticImages = new Image[(int)PlayerCosmeticKind.Count];
        private readonly Image[] _trailImages = new Image[PlayerCosmetics.MobilityTrailMaxCount];
        private Image _ringImage;
        private RectTransform _actor;
        private Func<string, int> _rankOf;
        private Func<bool> _reducedMotion;
        private float _clock;

        /// <summary>Scale applied to the whole actor so the widest cosmetic fits the stage.</summary>
        private const float ActorScale = 0.78f;

        /// <summary>
        /// Wires the preview to the game's workshop state. Ranks are queried
        /// live so purchases and the focused-upgrade +1 preview take effect the
        /// next frame.
        /// </summary>
        public void Bind(Func<string, int> rankOf, Func<bool> reducedMotion)
        {
            _rankOf = rankOf;
            _reducedMotion = reducedMotion;
            Rebuild();
        }

        private void Rebuild()
        {
            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                var child = transform.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            var stage = (RectTransform)transform;
            var glow = CreateImage(stage, "Glow");
            glow.sprite = UISprites.Glow(256);
            glow.color = UITheme.WithAlpha(UITheme.Cyan, 0.12f);
            SetCentered(glow.rectTransform, stage, new Vector2(240f, 240f));

            _actor = CreateRect(stage, "Actor");
            SetCentered(_actor, stage, new Vector2(360f, 360f));
            _actor.localScale = Vector3.one * ActorScale;

            _ringImage = CreateCenteredImage(_actor, "Ring", ProceduralSpriteFactory.PlayerRing(), new Vector2(86f, 86f));
            CreateCenteredImage(_actor, "Body", ProceduralSpriteFactory.Operative(), new Vector2(94f, 94f));

            for (var kind = PlayerCosmeticKind.Magnet; kind < PlayerCosmeticKind.Count; kind++)
            {
                _cosmeticImages[(int)kind] = CreateImage(_actor, "Cosmetic." + kind);
            }

            for (var index = 0; index < _trailImages.Length; index++)
            {
                _trailImages[index] = CreateCenteredImage(_actor, "Trail." + index, ProceduralSpriteFactory.PlayerMobilityTrail(), new Vector2(10f, 96f));
            }
        }

        private void Update()
        {
            if (_rankOf == null || _actor == null) return;

            var reduced = _reducedMotion != null && _reducedMotion();
            if (!reduced) _clock += Time.unscaledDeltaTime;

            // The legacy preview bobs the Operative gently on idle.
            var time = reduced ? 0f : _clock;
            _actor.anchoredPosition = new Vector2(0f, Mathf.Sin(time * 1.7f) * 4f);

            if (_ringImage != null)
                Rotate(_ringImage.rectTransform, -PlayerCosmetics.RingRotation * time);

            ApplyTrails(time);
            ApplyCosmetics(time);
        }

        private void ApplyTrails(float time)
        {
            var rank = Rank(PlayerCosmeticKind.Mobility);
            for (var index = 0; index < _trailImages.Length; index++)
            {
                var image = _trailImages[index];
                if (image == null) continue;
                var active = rank > 0 && index < rank;
                image.enabled = active;
                if (!active) continue;
                var length = PlayerCosmetics.MobilityTrailLength(rank, index, time);
                image.rectTransform.sizeDelta = new Vector2(PlayerCosmetics.MobilityTrailSpriteWidth, length);
                image.rectTransform.anchoredPosition = new Vector2(
                    PlayerCosmetics.MobilityTrailOffset(rank, index),
                    -(PlayerCosmetics.MobilityTrailTopOffset + length * 0.5f));
            }
        }

        private void ApplyCosmetics(float time)
        {
            for (var kind = PlayerCosmeticKind.Magnet; kind < PlayerCosmeticKind.Count; kind++)
            {
                var image = _cosmeticImages[(int)kind];
                if (image == null) continue;
                var rank = Rank(kind);
                if (rank <= 0)
                {
                    image.enabled = false;
                    continue;
                }
                var sprite = PlayerCosmetics.SpriteFor(kind, rank);
                if (sprite == null)
                {
                    image.enabled = false;
                    continue;
                }
                image.enabled = true;
                if (image.sprite != sprite)
                {
                    image.sprite = sprite;
                    image.rectTransform.sizeDelta = sprite.rect.size;
                }
                Rotate(image.rectTransform, PlayerCosmetics.WorldRotationRadians(kind, time));
            }
        }

        private int Rank(PlayerCosmeticKind kind)
        {
            string id;
            switch (kind)
            {
                case PlayerCosmeticKind.Mobility: id = "mobility"; break;
                case PlayerCosmeticKind.Magnet: id = "magnet"; break;
                case PlayerCosmeticKind.Integrity: id = "integrity"; break;
                case PlayerCosmeticKind.Recovery: id = "recovery"; break;
                case PlayerCosmeticKind.Power: id = "power"; break;
                case PlayerCosmeticKind.Precision: id = "precision"; break;
                case PlayerCosmeticKind.Arsenal: id = "arsenal"; break;
                case PlayerCosmeticKind.Protocol: id = "protocol"; break;
                default: return 0;
            }
            return _rankOf != null ? Mathf.Max(0, _rankOf(id)) : 0;
        }

        private static void Rotate(RectTransform rect, float radians)
        {
            rect.localEulerAngles = new Vector3(0f, 0f, radians * Mathf.Rad2Deg);
        }

        private static RectTransform CreateRect(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreateImage(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateCenteredImage(RectTransform parent, string name, Sprite sprite, Vector2 size)
        {
            var image = CreateImage(parent, name);
            image.sprite = sprite;
            SetCentered(image.rectTransform, parent, size);
            return image;
        }

        private static void SetCentered(RectTransform rect, RectTransform parent, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}