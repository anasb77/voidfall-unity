using UnityEngine;

namespace VoidFall.Runtime.Rendering
{
    /// <summary>
    /// The eight workshop cosmetic tracks that decorate the Operative. One
    /// shared model drives BOTH the in-game player and the Workshop frame
    /// preview, so preview and gameplay always show the same frame.
    /// </summary>
    public enum PlayerCosmeticKind
    {
        Mobility,
        Magnet,
        Integrity,
        Recovery,
        Power,
        Precision,
        Arsenal,
        Protocol,
        Count
    }

    /// <summary>All eight workshop ranks read from the save profile.</summary>
    public struct PlayerCosmeticRanks
    {
        public int Mobility;
        public int Magnet;
        public int Integrity;
        public int Recovery;
        public int Power;
        public int Precision;
        public int Arsenal;
        public int Protocol;

        public PlayerCosmeticRanks(int mobility, int magnet, int integrity, int recovery,
            int power, int precision, int arsenal, int protocol)
        {
            Mobility = mobility;
            Magnet = magnet;
            Integrity = integrity;
            Recovery = recovery;
            Power = power;
            Precision = precision;
            Arsenal = arsenal;
            Protocol = protocol;
        }

        public int this[PlayerCosmeticKind kind]
        {
            get
            {
                switch (kind)
                {
                    case PlayerCosmeticKind.Mobility: return Mobility;
                    case PlayerCosmeticKind.Magnet: return Magnet;
                    case PlayerCosmeticKind.Integrity: return Integrity;
                    case PlayerCosmeticKind.Recovery: return Recovery;
                    case PlayerCosmeticKind.Power: return Power;
                    case PlayerCosmeticKind.Precision: return Precision;
                    case PlayerCosmeticKind.Arsenal: return Arsenal;
                    case PlayerCosmeticKind.Protocol: return Protocol;
                    default: return 0;
                }
            }
            set
            {
                switch (kind)
                {
                    case PlayerCosmeticKind.Mobility: Mobility = value; break;
                    case PlayerCosmeticKind.Magnet: Magnet = value; break;
                    case PlayerCosmeticKind.Integrity: Integrity = value; break;
                    case PlayerCosmeticKind.Recovery: Recovery = value; break;
                    case PlayerCosmeticKind.Power: Power = value; break;
                    case PlayerCosmeticKind.Precision: Precision = value; break;
                    case PlayerCosmeticKind.Arsenal: Arsenal = value; break;
                    case PlayerCosmeticKind.Protocol: Protocol = value; break;
                }
            }
        }
    }

    /// <summary>
    /// Shared placement constants and rotation curve for the eight workshop
    /// cosmetics. Every value is derived from the legacy browser build's frame
    /// preview draw function so the two renderers reproduce it exactly.
    ///
    /// The preview's canvas math is in design pixels (y-down). Unity world units
    /// and uGUI pixels are y-up, so vertical offsets are negated and rotation is
    /// sign-flipped when converting; the sprite factory bakes each cosmetic at
    /// 1 design pixel == 1 sprite pixel == 1 world unit.
    /// </summary>
    public static class PlayerCosmetics
    {
        /// <summary>The preview draws the Operative at 94px; in-game it is 74 units.</summary>
        public const float InGameScale = 74f / 94f;

        // ---- Mobility ----------------------------------------------------
        public const float MobilityTrailSpacing = 18f;
        public const float MobilityTrailTopOffset = 27f;
        public const float MobilityTrailBaseLength = 35f;
        public const float MobilityTrailLengthPerRank = 9f;
        public const float MobilityTrailLengthPulse = 5f;
        public const float MobilityTrailWidth = 4f;
        public const int MobilityTrailMaxCount = 3;
        /// <summary>Baked streak sprite host size (width is the visible 4px line).</summary>
        public const float MobilityTrailSpriteWidth = 10f;
        public const float MobilityTrailSpriteLength = 96f;

        // ---- Magnet ------------------------------------------------------
        public const float MagnetBaseRadius = 76f;
        public const float MagnetRadiusPerRank = 9f;
        public const float MagnetRotation = 0.45f;

        // ---- Integrity ---------------------------------------------------
        public const float IntegrityRadius = 52f;
        public const float IntegrityArcSpan = 0.42f;
        public const float IntegrityArcBaseWidth = 3f;
        public const float IntegrityArcWidthPerRank = 0.6f;
        public const float IntegrityRotation = -0.22f;

        // ---- Recovery ----------------------------------------------------
        public const float RecoveryRadius = 42f;
        public const float RecoveryBlockSize = 8f;
        public const float RecoveryRotation = 0.7f;

        // ---- Power -------------------------------------------------------
        public const float PowerFinHostOffset = 31f;
        public const float PowerFinHostOffsetPerRank = 7f;
        public const float PowerFinScaleOffset = 32f;
        public const float PowerFinScalePerRank = 8f;
        public const float PowerFinVerticalSpacing = 13f;
        public const float PowerFinRectWidth = 12f;
        public const float PowerFinRectHeight = 10f;
        public const float PowerFinLineWidth = 3f;

        // ---- Precision ---------------------------------------------------
        public const float PrecisionRadius = 58f;
        public const float PrecisionTipRadius = 66f;
        public const float PrecisionTickWidth = 2.5f;
        public const float PrecisionRotation = 0.12f;

        // ---- Arsenal -----------------------------------------------------
        public const float ArsenalRadius = 58f;
        public const float ArsenalRotation = 1.1f;

        // ---- Protocol ----------------------------------------------------
        public const float ProtocolBaseRadius = 70f;
        public const float ProtocolRadiusPerRank = 4f;
        public const float ProtocolTickWidth = 2.5f;
        public const float ProtocolRotation = -0.3f;

        // ---- Ring --------------------------------------------------------
        public const float RingRotation = 1.35f;

        /// <summary>
        /// Legacy canvas rotation is applied in y-down space, so a positive
        /// angle spins clockwise on screen. Unity's y-up space needs the sign
        /// flipped to reproduce the same visual direction.
        /// </summary>
        public static float WorldRotationRadians(PlayerCosmeticKind kind, float time)
        {
            var canvasRadians = 0f;
            switch (kind)
            {
                case PlayerCosmeticKind.Mobility: canvasRadians = 0f; break;
                case PlayerCosmeticKind.Magnet: canvasRadians = MagnetRotation; break;
                case PlayerCosmeticKind.Integrity: canvasRadians = IntegrityRotation; break;
                case PlayerCosmeticKind.Recovery: canvasRadians = RecoveryRotation; break;
                case PlayerCosmeticKind.Precision: canvasRadians = PrecisionRotation; break;
                case PlayerCosmeticKind.Arsenal: canvasRadians = ArsenalRotation; break;
                case PlayerCosmeticKind.Protocol: canvasRadians = ProtocolRotation; break;
            }
            return -canvasRadians * time;
        }

        /// <summary>Preview trail length formula for one streak.</summary>
        public static float MobilityTrailLength(int rank, int index, float time)
        {
            return MobilityTrailBaseLength + rank * MobilityTrailLengthPerRank +
                Mathf.Sin(time * 8f + index) * MobilityTrailLengthPulse;
        }

        /// <summary>Horizontal pitch for one trail streak, centered on the player.</summary>
        public static float MobilityTrailOffset(int rank, int index)
        {
            return (index - (rank - 1) / 2f) * MobilityTrailSpacing;
        }

        /// <summary>Resolves the baked sprite for a track and rank (0 hides it).</summary>
        public static Sprite SpriteFor(PlayerCosmeticKind kind, int rank)
        {
            if (rank <= 0) return null;
            switch (kind)
            {
                case PlayerCosmeticKind.Mobility: return ProceduralSpriteFactory.PlayerMobilityTrail();
                case PlayerCosmeticKind.Magnet: return ProceduralSpriteFactory.PlayerMagnet(rank);
                case PlayerCosmeticKind.Integrity: return ProceduralSpriteFactory.PlayerIntegrity(rank);
                case PlayerCosmeticKind.Recovery: return ProceduralSpriteFactory.PlayerRecovery(rank);
                case PlayerCosmeticKind.Power: return ProceduralSpriteFactory.PlayerPower(rank);
                case PlayerCosmeticKind.Precision: return ProceduralSpriteFactory.PlayerPrecision(rank);
                case PlayerCosmeticKind.Arsenal: return ProceduralSpriteFactory.PlayerArsenal(rank);
                case PlayerCosmeticKind.Protocol: return ProceduralSpriteFactory.PlayerProtocol();
                default: return null;
            }
        }

        /// <summary>Design-pixel size of the baked sprite for a track and rank.</summary>
        public static Vector2 DesignSize(PlayerCosmeticKind kind, int rank)
        {
            var sprite = SpriteFor(kind, rank);
            return sprite != null ? sprite.rect.size : Vector2.zero;
        }
    }
}