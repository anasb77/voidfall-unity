using System;
using System.Collections.Generic;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Wild Cards (spec section 44): run-only rule modifiers granted by the
    /// Boss Roulette's Legendary wedges. Each card is unique per run and
    /// resets with it.
    /// </summary>
    public sealed partial class VoidFallGameRuntime
    {
        private readonly HashSet<WildCardId> _activeWildCards = new HashSet<WildCardId>();

        /// <summary>
        /// Seconds the movement input has been at rest. Drives STANDSTILL's
        /// stance activation; tracked every step while the card is held.
        /// </summary>
        private double _standstillSeconds;

        public bool HasWildCard(WildCardId id)
        {
            return _activeWildCards.Contains(id);
        }

        private void ActivateWildCard(WildCardId id)
        {
            if (id == WildCardId.None || !_activeWildCards.Add(id)) return;
            switch (id)
            {
                case WildCardId.Standstill:
                    ShowArenaToast(
                        "STANDSTILL - hold your ground, deal double damage",
                        3f, ToastKind.Reward);
                    break;
                case WildCardId.Greed:
                    ShowArenaToast(
                        "GREED - double experience, magnet disabled",
                        3f, ToastKind.Reward);
                    break;
                case WildCardId.SecondLife:
                    _revivesRemaining += WildCardRules.SecondLifeBonusRevives;
                    ShowArenaToast(
                        "SECOND LIFE - an extra revive waits",
                        3f, ToastKind.Reward);
                    break;
                case WildCardId.Overclocker:
                    _overclock.HoldTier1();
                    ShowArenaToast(
                        "OVERCLOCKER - the boost never ends",
                        3f, ToastKind.Reward);
                    break;
                case WildCardId.ColossusArsenal:
                    ShowArenaToast(
                        "COLOSSUS ARSENAL - everything is bigger",
                        3f, ToastKind.Reward);
                    break;
            }
        }

        /// <summary>
        /// Grants one random implemented card the player does not already
        /// hold. Returns false when every implemented card is held (the caller
        /// then pays a Parts fallback).
        /// </summary>
        private bool TryGrantRandomWildCard(RouletteSession session)
        {
            var candidates = new List<WildCardId>();
            foreach (WildCardId id in Enum.GetValues(typeof(WildCardId)))
            {
                if (id == WildCardId.None || HasWildCard(id) ||
                    !WildCardRules.IsImplemented(id)) continue;
                candidates.Add(id);
            }
            if (candidates.Count == 0) return false;
            var index = _rouletteRng != null
                ? _rouletteRng.Int(candidates.Count)
                : 0;
            ActivateWildCard(candidates[index]);
            return true;
        }

        /// <summary>
        /// Player-dealt damage multiplier. STANDSTILL doubles everything while
        /// the stationary stance is held; this is the single choke point for
        /// every player damage source (bullets, arcs, blasts, rail wakes,
        /// meteors).
        /// </summary>
        private float PlayerDamageMultiplier()
        {
            return _activeWildCards.Contains(WildCardId.Standstill) &&
                WildCardRules.StandstillActive(_standstillSeconds)
                ? (float)WildCardRules.StandstillDamageMultiplier
                : 1f;
        }

        private float GreedXpMultiplier()
        {
            return _activeWildCards.Contains(WildCardId.Greed)
                ? WildCardRules.GreedXpMultiplier
                : 1;
        }
    }
}