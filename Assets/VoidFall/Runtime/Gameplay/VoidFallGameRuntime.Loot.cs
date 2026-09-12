using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const int LootPolicyVersion = 2;
        private const int ReservedSpecialPickupSlots = 24;
        private const int PrimaryXpPickupSlots = MaxPickups - ReservedSpecialPickupSlots;
        private const float XpMergeDistance = 180f;
        private float _lootRecoveryTimer = .25f;
        private static readonly PickupKind[] LootKinds = (PickupKind[])Enum.GetValues(typeof(PickupKind));
        private static bool IsPowerupPickup(PickupKind kind) => kind != PickupKind.Xp && kind != PickupKind.Part;

        private int FindXpPickupSlot()
        {
            var slot = FindInactive(_gameSim.Pickups, PrimaryXpPickupSlots);
            if (slot >= 0) return slot;
            // The last slot stays XP-only; currency cannot occupy the special reserve.
            return !_gameSim.Pickups[MaxPickups].Active ? MaxPickups : -1;
        }

        private int FindSpecialPickupSlot()
        {
            for (var i = PrimaryXpPickupSlots; i < MaxPickups; i++)
                if (!_gameSim.Pickups[i].Active) return i;
            var slot = FindInactive(_gameSim.Pickups, MaxPickups);
            if (slot >= 0) return slot;
            if (ConsolidatePickupPair(PickupKind.Xp, out slot) || ConsolidatePickupPair(PickupKind.Part, out slot)) return slot;
            // If even the pool of specials is full, duplicates can hold charges.
            // Collection consumes one and re-materializes the remainder.
            foreach (var kind in LootKinds)
                if (IsPowerupPickup(kind) && ConsolidatePickupPair(kind, out slot)) return slot;
            return -1;
        }

        private bool CanPlaceSpecialPickup()
        {
            if (FindInactive(_gameSim.Pickups, MaxPickups) >= 0) return true;
            foreach (var kind in LootKinds)
            {
                var count = 0; var ordinarySlot = false;
                for (var i = 0; i < _gameSim.Pickups.Length; i++)
                {
                    if (!_gameSim.Pickups[i].Active || _gameSim.Pickups[i].Kind != kind) continue;
                    count++; ordinarySlot |= i < MaxPickups;
                }
                if (count >= 2 && ordinarySlot) return true;
            }
            return false;
        }

        private bool ConsolidatePickupPair(PickupKind kind, out int freedSlot)
        {
            freedSlot = -1;
            var destination = -1; var nearest = float.PositiveInfinity;
            for (var i = 0; i < _gameSim.Pickups.Length; i++)
            {
                var pickup = _gameSim.Pickups[i];
                if (!pickup.Active || pickup.Kind != kind) continue;
                var distance = (pickup.Position - _gameSim.Player.Position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; destination = i;
            }
            if (destination < 0) return false;
            var farthest = -1f;
            for (var i = 0; i < MaxPickups; i++)
            {
                var pickup = _gameSim.Pickups[i];
                if (i == destination || !pickup.Active || pickup.Kind != kind) continue;
                var distance = (pickup.Position - _gameSim.Player.Position).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance; freedSlot = i;
            }
            if (freedSlot < 0 && destination < MaxPickups && _gameSim.Pickups[MaxPickups].Active && _gameSim.Pickups[MaxPickups].Kind == kind)
            { freedSlot = destination; destination = MaxPickups; }
            if (freedSlot < 0) return false;
            var source = _gameSim.Pickups[freedSlot];
            var target = _gameSim.Pickups[destination];
            var transferred = IsPowerupPickup(kind) ? Mathf.Max(1, Mathf.FloorToInt(source.Value)) : source.Value;
            target.Value = (IsPowerupPickup(kind) ? Mathf.Max(1, Mathf.FloorToInt(target.Value)) : target.Value) + transferred;
            target.Pull = (target.Pull || source.Pull) && !HasWildCard(WildCardId.Greed);
            _gameSim.Pickups[destination] = target;
            _musicMagnetSlots[destination] |= _musicMagnetSlots[freedSlot];
            RecordPickupHistory("drop_consolidated", destination, target, transferred, "capacity", _telemetryPickupIds[freedSlot]);
            source.Active = false;
            _gameSim.Pickups[freedSlot] = source;
            _musicMagnetSlots[freedSlot] = false;
            RemovePickupOrder(freedSlot);
            Hide(_pickupViews[freedSlot]);
            RefreshPickupView(destination);
            return true;
        }

        private Vector2 ConstrainLootPosition(Vector2 position)
        {
            if (CurrentVoidIsNullCity)
            {
                var canvas = NullCityCanvas(position);
                var margin = 32f / NullCityRules.WorldScale;
                return NullCityWorld(Mathf.Clamp(canvas.x, (float)NullCityRules.ArenaLeft + margin, (float)NullCityRules.ArenaRight - margin),
                    Mathf.Clamp(canvas.y, (float)NullCityRules.ArenaTop + margin, (float)NullCityRules.ArenaBottom - margin));
            }
            if (_arenaId == ArenaId.MonochromeCourt && _courtFieldReady)
                return MonochromeRuntimeRules.ClampToBoard(position, _monochromeBoardOrigin,
                    new Vector2(CourtBoardColumns, CourtBoardRows) * (float)MonochromeEncounterRules.TileSize, 32f);
            if (_hydraBossEncounterActive)
                return HydraRuntimeRules.ClampPlayerToRibCage(position, _hydraArenaCentre, 32f, true);
            return position;
        }

        private void RelocateLoot(int slot, Vector2 position, string reason)
        {
            position = ConstrainLootPosition(position);
            if (reason == "distance_recovery" && CurrentVoidIsNullCity && (position - _gameSim.Player.Position).sqrMagnitude < 80 * 80)
            {
                var center = NullCityWorld((float)(NullCityRules.ArenaLeft + NullCityRules.ArenaRight) * .5f,
                    (float)(NullCityRules.ArenaTop + NullCityRules.ArenaBottom) * .5f);
                position = ConstrainLootPosition(_gameSim.Player.Position + (center - _gameSim.Player.Position).normalized * 160);
            }
            var pickup = _gameSim.Pickups[slot];
            pickup.Position = position;
            pickup.Velocity = Vector2.zero;
            pickup.Age = 0;
            if (HasWildCard(WildCardId.Greed)) { pickup.Pull = false; pickup.Speed = 0; }
            _gameSim.Pickups[slot] = pickup;
            RecordPickupHistory("drop_relocated", slot, pickup, reason: reason);
            RefreshPickupView(slot);
        }

        private void UpdateLootReachability(float dt)
        {
            if (dt <= 0 || _mainMenuBrowsing || _gameOver || _gameSim.Player.Health <= 0) return;
            _lootRecoveryTimer -= dt;
            if (_lootRecoveryTimer > 0) return;
            _lootRecoveryTimer = .25f;
            var viewport = GameplayViewportHalfExtent();
            var threshold = Mathf.Max(viewport.x, viewport.y) * 2;
            var selected = -1; var priority = -1; var farthest = threshold * threshold;
            for (var i = 0; i < _gameSim.Pickups.Length; i++)
            {
                var pickup = _gameSim.Pickups[i];
                if (!pickup.Active || pickup.Pull) continue;
                var reachable = ConstrainLootPosition(pickup.Position);
                if ((reachable - pickup.Position).sqrMagnitude > .01f)
                {
                    RelocateLoot(i, reachable, "arena_boundary_recovery");
                    return;
                }
                var distance = (pickup.Position - _gameSim.Player.Position).sqrMagnitude;
                if (distance <= threshold * threshold) continue;
                var importance = IsPowerupPickup(pickup.Kind) ? 1 : 0;
                if (importance < priority || (importance == priority && distance <= farthest)) continue;
                priority = importance; farthest = distance; selected = i;
            }
            if (selected < 0) return;
            var direction = (_gameSim.Pickups[selected].Position - _gameSim.Player.Position).normalized;
            var reach = Mathf.Max(80, Mathf.Min(viewport.x, viewport.y) * .65f);
            // Returning loot still requires normal physical or magnet collection.
            RelocateLoot(selected, _gameSim.Player.Position + direction * reach, "distance_recovery");
        }

        private void RestorePickupStack(PickupState consumed, int remaining, int parentPickupId)
        {
            if (remaining <= 0) return;
            var oldSource = _telemetryRewardSource; var oldParent = _telemetryRewardParent;
            _telemetryRewardSource = "stack_remainder"; _telemetryRewardParent = parentPickupId;
            try { PlaceSpecialPickup(consumed.Position, remaining, consumed.Kind, false); }
            finally { _telemetryRewardSource = oldSource; _telemetryRewardParent = oldParent; }
        }
    }
}
