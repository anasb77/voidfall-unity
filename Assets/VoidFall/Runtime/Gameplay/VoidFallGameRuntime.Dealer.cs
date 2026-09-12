using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private DealerSession _dealerSession;
        private bool _dealerOpen, _dealerDelayedOwned;
        private float _dealerShield, _dealerCombatSeconds, _dealerSmileUntil;
        private int _dealerExtraWeapon = -1, _dealerHealthBonus, _dealerVariation, _dealerOpenedFrame, _dealerRecoveryCharges;
        private Vector2 _dealerPosition;
        private DealerRoomView _dealerRoom;
        private float _dealerRoomClock;

        private void ResetDealerRun()
        {
            _dealerOpen = false; _dealerSession = null; _dealerShield = 0;
            _dealerDelayedOwned = false; _dealerCombatSeconds = 0; _dealerExtraWeapon = -1; _dealerHealthBonus = 0;
            _dealerRecoveryCharges = 0;
            ResetLegendaries();
        }
        private int DealerExtraTarget()
        {
            if (_upgradeProgress == null || _dealerExtraWeapon >= 0) return -1;
            var chosen = -1; var rank = int.MaxValue;
            for (var i = 0; i < Math.Min(_upgradeProgress.WeaponRanks.Length, ContentCatalog.Weapons.Length); i++)
            {
                var kind = ContentCatalog.Weapons[i].Kind;
                if (kind == "chain" || kind == "orbit" || i >= 6 || _upgradeProgress.WeaponRanks[i] <= 0) continue;
                if (_upgradeProgress.WeaponRanks[i] < rank) { chosen = i; rank = _upgradeProgress.WeaponRanks[i]; }
            }
            return chosen;
        }
        private void BeginDealerCrossing()
        {
            var seed = _runSeed ^ (uint)(_completedVoids + 1) * 0x9e3779b9u;
            seed ^= seed >> 16; seed *= 0x85ebca6bu; seed ^= seed >> 13; seed *= 0xc2b2ae35u; seed ^= seed >> 16;
            var bottom = (seed & 1) != 0;
            _dealerPosition = new Vector2(0, bottom ? -260 : 10);
            _gameSim.Player.Position = bottom ? new Vector2(-180, -140) : new Vector2(0, -210);
            _gameSim.Player.Velocity = Vector2.zero;
            _dealerVariation = (int)((seed >> 5) % 4);
            _dealerOpen = false;
            var target = DealerExtraTarget();
            var late = true;
            foreach (var id in _junctionDestinations) if (_voidRoute.Node(id).Outgoing.Count > 0) late = false;
            _dealerSession = new DealerSession(DealerRules.CreateOffers(seed,
                _saveData?.soundBladeFragments ?? 0, _saveData?.chargedRifleFragments ?? 0,
                _legendaryWeapon, _legendaryRank, target, target >= 0 ? UpgradeRules.WeaponDisplayName(ContentCatalog.Weapons[target].Id) : "",
                _dealerShield > 0, _dealerDelayedOwned, _dealerExtraWeapon >= 0, late));
            EnsureDealerCrossingVisuals();
            CancelLegendaryInput();
            RecordRunHistory("dealer_stock", "dealer", sourceId: "dealer", instanceId: _completedVoids,
                options: Array.ConvertAll(_dealerSession.Offers, DealerRules.Id), detail: JsonUtility.ToJson(DealerSnapshot()));
        }
        private bool CanOpenDealer => !_mainMenuBrowsing && !_gameOver && !_revivePending && !_paused &&
            !_routeMapOpen && !_rouletteActive && !_prizeRevealActive && !_levelUpActive &&
            _journeyStage == JourneyStage.Junction && _dealerSession != null && _levelUpTimer < 0 && _gameSim.Player.Health > 0 &&
            Vector2.Distance(_gameSim.Player.Position, _dealerPosition) <= 95;
        private void ReadDealerInput()
        {
            var keyboard = Keyboard.current; var pad = Gamepad.current;
            if (_dealerOpen)
            {
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame) CloseDealer();
                return;
            }
            if (CanOpenDealer && (keyboard != null && keyboard.eKey.wasPressedThisFrame || pad != null && pad.buttonSouth.wasPressedThisFrame)) OpenDealer();
        }
        private void OpenDealer()
        {
            if (!CanOpenDealer || _ui?.Dealer == null) return;
            _dealerOpen = true; _paused = true; CancelLegendaryInput();
            _dealerOpenedFrame = Time.frameCount;
            RecordRunHistory("dealer_opened", "dealer", sourceId: "dealer", instanceId: _completedVoids, detail: JsonUtility.ToJson(DealerSnapshot()));
            RefreshDealerView(); SyncUiScreen();
        }
        private void RefreshDealerView()
        {
            _ui.Dealer.Show(_dealerSession, _partsEarned, _saveData?.soundBladeFragments ?? 0,
                _saveData?.chargedRifleFragments ?? 0, _dealerVariation, _saveData?.settings?.reducedMotion == true,
                BuyDealerOffer, CloseDealer);
        }
        private void CloseDealer()
        {
            if (!_dealerOpen) return;
            RecordRunHistory("dealer_closed", "dealer", _dealerSession != null && _dealerSession.PurchasedIndex >= 0 ? "purchased" : "skipped",
                sourceId: "dealer", instanceId: _completedVoids, detail: JsonUtility.ToJson(DealerSnapshot()));
            _dealerOpen = false; _paused = _applicationInactive; CancelLegendaryInput();
            _ui?.Dealer?.SetVisible(false); SyncUiScreen();
        }
        private bool PersistDealerFragment(DealerTransaction transaction)
        {
            if (_saveData == null || _saveStore == null) return false;
            var next = CloneSaveData(_saveData);
            next.soundBladeFragments = transaction.SoundMask; next.chargedRifleFragments = transaction.RifleMask;
            try { _saveStore.Save(next); _saveData = next; return true; }
            catch (Exception error) { Debug.LogWarning("Dealer fragment save failed; purchase rolled back: " + error.Message); return false; }
        }
        private void BuyDealerOffer(int index)
        {
            if (!_dealerOpen || _journeyStage != JourneyStage.Junction || _dealerSession == null) return;
            if (Time.frameCount <= _dealerOpenedFrame + 1) return;
            if (index < 0 || index >= _dealerSession.Offers.Length) return;
            var offer = _dealerSession.Offers[index];
            var before = DealerSnapshot();
            if (offer.Kind == DealerOfferKind.ExtraProjectile &&
                (offer.TargetWeapon < 0 || _upgradeProgress.WeaponRanks[offer.TargetWeapon] <= 0 || _dealerExtraWeapon >= 0)) return;
            var result = _dealerSession.TryBuy(index, _partsEarned, _saveData?.soundBladeFragments ?? 0,
                _saveData?.chargedRifleFragments ?? 0, PersistDealerFragment, out var receipt);
            if (result != DealerPurchaseResult.Success)
            {
                RecordRunHistory("dealer_rejected", DealerRules.Id(offer), result.ToString(), sourceId: "dealer", instanceId: _completedVoids,
                    detail: JsonUtility.ToJson(before));
                _ui.Dealer.SetNotice(result == DealerPurchaseResult.SaveFailed ? "Could not save the fragment. No Scraps spent." :
                    result == DealerPurchaseResult.Unaffordable ? "You need 100 Scraps." : "That deal is no longer available.");
                return;
            }
            _partsEarned = receipt.WalletAfter;
            switch (offer.Kind)
            {
                case DealerOfferKind.Shield: _dealerShield = 20; break;
                case DealerOfferKind.DelayedPower: _dealerDelayedOwned = true; _dealerCombatSeconds = 0; break;
                case DealerOfferKind.ExtraProjectile: _dealerExtraWeapon = offer.TargetWeapon; break;
                case DealerOfferKind.MoreHealth: _dealerHealthBonus += 3; _gameSim.Player.Health += 3; RecalculatePlayerStats(false); break;
                case DealerOfferKind.RecoveryPlan: _dealerRecoveryCharges += 5; break;
                case DealerOfferKind.Fragment:
                    if ((offer.Legendary == LegendaryWeaponId.SoundBlade ? receipt.SoundMask : receipt.RifleMask) == 7) EquipLegendary(offer.Legendary, 1);
                    break;
                case DealerOfferKind.EquipLegendary: EquipLegendary(offer.Legendary, 1); break;
                case DealerOfferKind.UpgradeLegendary: EquipLegendary(offer.Legendary, Mathf.Min(3, _legendaryRank + 1)); break;
            }
            _dealerSmileUntil = Time.unscaledTime + 3.5f;
            _audio?.Play(ProceduralAudio.Cue.Currency, .65f);
            RecordRunHistory("dealer_purchase", DealerRules.Id(offer), sourceId: "dealer", instanceId: _completedVoids, amount: DealerRules.Price,
                hp: _gameSim.Player.Health, maxHp: _gameSim.Player.MaxHealth, progress: BuildTelemetryProgress(),
                detail: JsonUtility.ToJson(new DealerPurchaseTelemetry { before = before, after = DealerSnapshot(), offerIndex = index }));
            RefreshDealerView(); _ui.Dealer.Purchased(index); SyncUiScreen();
        }
        private void EnsureDealerCrossingVisuals()
        {
            if (_dealerRoom != null) return;
            _dealerRoom = DealerRoomView.Create(_junctionCanvas);
        }
        private void RenderDealerCrossing()
        {
            if (_journeyStage != JourneyStage.Junction || _dealerRoom == null) return;
            var reduced = _saveData?.settings?.reducedMotion == true;
            if (!reduced) _dealerRoomClock += Mathf.Min(Time.unscaledDeltaTime, .1f);
            var names = new string[_junctionDestinations.Length];
            var colors = new Color[names.Length]; var frames = new Sprite[names.Length];
            var frame = reduced || _riftPortalFrames.Length == 0 ? 0 : (int)(_dealerRoomClock * 10) % _riftPortalFrames.Length;
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = _voidRoute.Node(_junctionDestinations[i]).DisplayName;
                colors[i] = PortalDestinationColor(_junctionDestinations[i]);
                frames[i] = _riftPortalFrames.Length == 0 ? null : _riftPortalFrames[frame];
                _junctionPortals[i].enabled = false; _junctionLabels[i].gameObject.SetActive(false);
            }
            _dealerRoom.Draw(_gameSim.Player.Position, _dealerPosition, _dealerVariation,
                Time.unscaledTime < _dealerSmileUntil, reduced, CanOpenDealer && !_dealerOpen,
                _partsEarned, _voidRoute.Node(CurrentVoidId).DisplayName, names, colors, frames, _dealerRoomClock);
            _dealerRoom.PresentPlayer(_playerAuraView); _dealerRoom.PresentPlayer(_playerRingView);
            if (_playerTrailViews != null) foreach (var view in _playerTrailViews) _dealerRoom.PresentPlayer(view);
            if (_playerCosmeticViews != null) foreach (var view in _playerCosmeticViews) _dealerRoom.PresentPlayer(view);
            _dealerRoom.PresentPlayer(_playerView);
            _junctionCanvas.enabled = !_routeMapOpen;
        }
        private void DestroyDealerVisuals() { DestroyLegendaryVisuals(); }
        [Serializable] private sealed class DealerOfferTelemetry { public string id, kind, legendary, targetWeapon; public int piece; }
        [Serializable] private sealed class DealerTelemetrySnapshot
        {
            public int price = DealerRules.Price, wallet, purchasedIndex, soundMask, rifleMask, rank, faceVariation;
            public string equipped, placement;
            public DealerOfferTelemetry[] offers;
        }
        [Serializable] private sealed class DealerPurchaseTelemetry { public DealerTelemetrySnapshot before, after; public int offerIndex; }
        private DealerTelemetrySnapshot DealerSnapshot()
        {
            return new DealerTelemetrySnapshot {
                wallet = _partsEarned, purchasedIndex = _dealerSession?.PurchasedIndex ?? -1,
                soundMask = _saveData?.soundBladeFragments ?? 0, rifleMask = _saveData?.chargedRifleFragments ?? 0,
                equipped = LegendaryRules.Id(_legendaryWeapon), rank = _legendaryRank, faceVariation = _dealerVariation,
                placement = _dealerPosition.y < 0 ? "bottom" : "top",
                offers = _dealerSession == null ? Array.Empty<DealerOfferTelemetry>() : Array.ConvertAll(_dealerSession.Offers, offer => new DealerOfferTelemetry {
                    id = DealerRules.Id(offer), kind = offer.Kind.ToString(), legendary = LegendaryRules.Id(offer.Legendary), piece = offer.Piece,
                    targetWeapon = offer.TargetWeapon >= 0 && offer.TargetWeapon < ContentCatalog.Weapons.Length ? ContentCatalog.Weapons[offer.TargetWeapon].Id : null })
            };
        }
    }
}
