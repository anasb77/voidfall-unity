using System;
using System.Collections.Generic;
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
        private DealerPortraitView _dealerPortrait;
        private Text _dealerPrompt;
        private RawImage _dealerPlayerImage;
        private Mesh _dealerFloorMesh;
        private readonly LineRenderer[] _dealerRings = new LineRenderer[5];
        private float _dealerRoomClock;
        private readonly Dictionary<SpriteRenderer, int> _dealerPlayerOrders = new Dictionary<SpriteRenderer, int>();

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
            _dealerPosition = new Vector2(0, bottom ? -205 : 105);
            _gameSim.Player.Position = bottom ? new Vector2(-185, 0) : new Vector2(0, -175);
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
            RaiseDealerPlayerPresentation();
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
            if (_dealerPortrait != null) return;
            var floor = _junctionRoot.transform.Find("Crossing Floor"); if (floor != null) floor.gameObject.SetActive(false);
            foreach (var rim in _junctionRims) if (rim != null) rim.gameObject.SetActive(false);
            var night = new GameObject("Crossing Night").AddComponent<SpriteRenderer>(); night.transform.SetParent(_junctionRoot.transform, false);
            night.sprite = _junctionFloorSprite; night.color = new Color(.02f, .027f, .047f); night.sortingOrder = 1000;
            night.transform.localScale = new Vector3(8000, 8000, 1);
            var boundary = new[] { new Vector2(-520,-40), new Vector2(-420,160), new Vector2(-200,230), new Vector2(190,230),
                new Vector2(430,160), new Vector2(530,-40), new Vector2(390,-200), new Vector2(140,-280), new Vector2(-210,-270), new Vector2(-400,-190) };
            var vertices = new Vector3[boundary.Length + 1]; var colors = new Color[vertices.Length]; var indices = new int[boundary.Length * 3];
            colors[0] = new Color(.04f,.075f,.10f);
            for (var i = 0; i < boundary.Length; i++)
            {
                vertices[i + 1] = boundary[i]; colors[i + 1] = new Color(.027f,.052f,.075f);
                indices[i * 3] = 0; indices[i * 3 + 1] = i + 1; indices[i * 3 + 2] = (i + 1) % boundary.Length + 1;
            }
            _dealerFloorMesh = new Mesh { name = "Crossing Platform" }; _dealerFloorMesh.vertices = vertices; _dealerFloorMesh.colors = colors; _dealerFloorMesh.triangles = indices; _dealerFloorMesh.RecalculateBounds();
            var platform = new GameObject("Crossing Platform"); platform.transform.SetParent(_junctionRoot.transform, false);
            platform.AddComponent<MeshFilter>().sharedMesh = _dealerFloorMesh;
            var renderer = platform.AddComponent<MeshRenderer>(); renderer.sharedMaterial = VoidFall.Runtime.Rendering.VoidFallRenderMaterials.DefaultUnlit; renderer.sortingOrder = 1002;
            var edge = CreateLineView("Crossing Platform Edge", 1003); edge.transform.SetParent(_junctionRoot.transform, false);
            edge.positionCount = boundary.Length + 1; edge.startWidth = edge.endWidth = 1.2f;
            edge.startColor = edge.endColor = new Color(.4f,.67f,.75f,.18f); edge.enabled = true;
            for (var i = 0; i <= boundary.Length; i++) edge.SetPosition(i, boundary[i % boundary.Length]);
            for (var i = 0; i < _dealerRings.Length; i++)
            {
                var line = CreateLineView("Crossing Orbit " + i, 1001); line.transform.SetParent(_junctionRoot.transform, false);
                line.positionCount = 49; line.startWidth = line.endWidth = 6 + i * 4;
                line.startColor = line.endColor = new Color(.35f,.55f,.68f,.035f); _dealerRings[i] = line;
            }
            _dealerPortrait = DealerPortraitView.Create(_junctionCanvas.transform, "Hovering Dealer", 8.3f, new Vector2(500, 340));
            _dealerPortrait.GetComponent<RectTransform>().pivot = new Vector2(.5f, 0);
            _dealerPrompt = UIBuilder.CreateText(_junctionCanvas.transform, "Dealer Interaction", "E  Browse", 13, UITheme.TextBody, TextAnchor.MiddleCenter, true);
            _dealerPrompt.rectTransform.sizeDelta = new Vector2(220, 40);
            _dealerPlayerImage = UIBuilder.CreateRect(_junctionCanvas.transform, "Player Above Dealer").gameObject.AddComponent<RawImage>();
            _dealerPlayerImage.raycastTarget = false;
            foreach (var portal in _junctionPortals) if (portal != null) portal.sortingOrder = 1010;
        }
        private void RaiseDealerPlayerPresentation()
        {
            void Raise(SpriteRenderer view)
            {
                if (view == null || _dealerPlayerOrders.ContainsKey(view)) return;
                _dealerPlayerOrders[view] = view.sortingOrder; view.sortingOrder += 1000;
            }
            Raise(_playerView); Raise(_playerAuraView); Raise(_playerRingView);
            if (_playerCosmeticViews != null) foreach (var view in _playerCosmeticViews) Raise(view);
            if (_playerTrailViews != null) foreach (var view in _playerTrailViews) Raise(view);
        }
        private void RestoreDealerPlayerPresentation()
        {
            foreach (var entry in _dealerPlayerOrders) if (entry.Key != null) entry.Key.sortingOrder = entry.Value;
            _dealerPlayerOrders.Clear();
        }
        private void RenderDealerCrossing()
        {
            if (_journeyStage != JourneyStage.Junction || _dealerPortrait == null) return;
            var root = (RectTransform)_junctionCanvas.transform;
            var point = _camera.WorldToScreenPoint(_dealerPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, point, null, out var local);
            var portraitRect = _dealerPortrait.GetComponent<RectTransform>();
            // Keep the upper encounter clear of the timer while retaining its interaction position.
            portraitRect.anchoredPosition = new Vector2(local.x, Mathf.Min(local.y, root.rect.height * .5f - portraitRect.rect.height - 90));
            var reduced = _saveData?.settings?.reducedMotion == true;
            _dealerPortrait.SetPose((_gameSim.Player.Position.x - _dealerPosition.x) / 300, Time.unscaledTime < _dealerSmileUntil, reduced, _dealerVariation);
            _dealerPrompt.gameObject.SetActive(CanOpenDealer && !_dealerOpen);
            _dealerPrompt.text = Gamepad.current != null && Mouse.current == null ? "A  Browse" : "E  Browse";
            _dealerPrompt.rectTransform.anchoredPosition = local + Vector2.down * 35;
            _junctionCanvas.enabled = !_routeMapOpen && !_dealerOpen;
            if (!reduced) _dealerRoomClock += Mathf.Min(Time.unscaledDeltaTime, .1f);
            for (var ring = 0; ring < _dealerRings.Length; ring++)
            {
                var line = _dealerRings[ring]; line.enabled = true;
                for (var i = 0; i < 49; i++)
                {
                    var angle = (reduced ? 0 : _dealerRoomClock * .055f) + ring * 1.7f + i / 48f * 2.15f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * (320 + ring * 88), 70 + Mathf.Sin(angle) * (320 + ring * 88), 0));
                }
            }
            if (_playerView?.sprite != null)
            {
                var sprite = _playerView.sprite; var texture = sprite.texture; var rect = sprite.textureRect;
                _dealerPlayerImage.texture = texture; _dealerPlayerImage.uvRect = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
                point = _camera.WorldToScreenPoint(_gameSim.Player.Position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, point, null, out local);
                _dealerPlayerImage.rectTransform.anchoredPosition = local;
                var edge = _camera.WorldToScreenPoint(_gameSim.Player.Position + Vector2.right * _playerView.bounds.extents.x);
                var size = Mathf.Abs(edge.x - point.x) * 2 * root.rect.width / Mathf.Max(1, Screen.width);
                _dealerPlayerImage.rectTransform.sizeDelta = Vector2.one * size;
                _dealerPlayerImage.enabled = true;
            }
        }
        private void DestroyDealerVisuals()
        {
            if (_dealerFloorMesh != null) Destroy(_dealerFloorMesh);
            DestroyLegendaryVisuals();
        }
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
