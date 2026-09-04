using UnityEngine;
using VoidFall.Runtime.Rendering;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        /// <summary>
        /// Workshop frame cosmetics rendered on the in-game Operative. Sprites
        /// and placement come from the same <see cref="PlayerCosmetics"/> model
        /// as the Workshop frame preview, so "play whatever you see in the
        /// preview" is guaranteed by construction: one rank, two renderers.
        /// </summary>
        private SpriteRenderer[] _playerCosmeticViews;
        private SpriteRenderer[] _playerTrailViews;

        private int _workshopRecovery;
        private int _workshopPrecision;
        private int _workshopArsenal;
        private int _workshopProtocol;

        /// <summary>Ranks below the Operative body so base layers stay visible.</summary>
        private const int CosmeticSortingOrder = 29;
        private const int TrailSortingOrder = 28;

        private void SetupPlayerCosmetics()
        {
            if (_playerCosmeticViews != null) return;

            _playerCosmeticViews = new SpriteRenderer[(int)PlayerCosmeticKind.Count];
            for (var kind = PlayerCosmeticKind.Magnet; kind < PlayerCosmeticKind.Count; kind++)
            {
                _playerCosmeticViews[(int)kind] = CreateView(
                    "Cosmetic." + kind, null, CosmeticSortingOrder);
            }

            _playerTrailViews = new SpriteRenderer[PlayerCosmetics.MobilityTrailMaxCount];
            for (var index = 0; index < _playerTrailViews.Length; index++)
            {
                _playerTrailViews[index] = CreateView(
                    "Cosmetic.MobilityTrail." + index, null, TrailSortingOrder);
            }

            RefreshWorkshopCosmeticRanks();
        }

        /// <summary>Re-reads every workshop rank from the save profile.</summary>
        private void RefreshWorkshopCosmeticRanks()
        {
            _workshopIntegrity = WorkshopRank("integrity");
            _workshopPower = WorkshopRank("power");
            _workshopMobility = WorkshopRank("mobility");
            _workshopMagnet = WorkshopRank("magnet");
            _workshopRecovery = WorkshopRank("recovery");
            _workshopPrecision = WorkshopRank("precision");
            _workshopArsenal = WorkshopRank("arsenal");
            _workshopProtocol = WorkshopRank("protocol");
        }

        private int WorkshopCosmeticRank(PlayerCosmeticKind kind)
        {
            switch (kind)
            {
                case PlayerCosmeticKind.Integrity: return _workshopIntegrity;
                case PlayerCosmeticKind.Power: return _workshopPower;
                case PlayerCosmeticKind.Mobility: return _workshopMobility;
                case PlayerCosmeticKind.Magnet: return _workshopMagnet;
                case PlayerCosmeticKind.Recovery: return _workshopRecovery;
                case PlayerCosmeticKind.Precision: return _workshopPrecision;
                case PlayerCosmeticKind.Arsenal: return _workshopArsenal;
                case PlayerCosmeticKind.Protocol: return _workshopProtocol;
                default: return 0;
            }
        }

        /// <summary>Drives all cosmetic views for the current frame.</summary>
        private void UpdatePlayerCosmetics(bool playerVisible)
        {
            if (_playerCosmeticViews == null) return;

            var reduced = _saveData?.settings?.reducedMotion == true;
            var time = reduced ? 0f : _ambientClock;
            var blink = _gameSim.Player.Iframes > 0 && Mathf.Sin(_ambientClock * 34f) > 0;
            var alpha = blink ? 0.35f : 1f;
            var scale = PlayerCosmetics.InGameScale;

            for (var kind = PlayerCosmeticKind.Magnet; kind < PlayerCosmeticKind.Count; kind++)
            {
                var view = _playerCosmeticViews[(int)kind];
                if (view == null) continue;
                var sprite = PlayerCosmetics.SpriteFor(kind, WorkshopCosmeticRank(kind));
                var active = playerVisible && sprite != null;
                view.enabled = active;
                if (!active) continue;
                if (view.sprite != sprite) view.sprite = sprite;
                view.transform.position = _gameSim.Player.Position;
                view.transform.rotation = Quaternion.Euler(
                    0f,
                    0f,
                    PlayerCosmetics.WorldRotationRadians(kind, time) * Mathf.Rad2Deg);
                view.transform.localScale = Vector3.one * scale;
                view.color = new Color(1f, 1f, 1f, alpha);
            }

            var mobilityRank = WorkshopCosmeticRank(PlayerCosmeticKind.Mobility);
            for (var index = 0; index < _playerTrailViews.Length; index++)
            {
                var view = _playerTrailViews[index];
                if (view == null) continue;
                var active = playerVisible && mobilityRank > 0 && index < mobilityRank;
                view.enabled = active;
                if (!active) continue;
                var length = PlayerCosmetics.MobilityTrailLength(mobilityRank, index, time);
                view.transform.position = _gameSim.Player.Position + new Vector2(
                    PlayerCosmetics.MobilityTrailOffset(mobilityRank, index) * scale,
                    -(PlayerCosmetics.MobilityTrailTopOffset + length * 0.5f) * scale);
                view.transform.rotation = Quaternion.identity;
                view.transform.localScale = new Vector3(
                    (PlayerCosmetics.MobilityTrailWidth / PlayerCosmetics.MobilityTrailSpriteWidth) * scale,
                    (length / PlayerCosmetics.MobilityTrailSpriteLength) * scale,
                    1f);
                view.color = new Color(1f, 1f, 1f, alpha * 0.9f);
            }
        }
    }
}