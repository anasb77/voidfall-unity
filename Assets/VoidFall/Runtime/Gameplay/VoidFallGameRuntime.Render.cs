using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime.Rendering;
using VoidFall.UI;
namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {

        private bool ConfigurePreparedSpritesForStartup()
        {
            var catalog = Resources.Load<ProceduralSpriteCatalog>(
                "VoidFall/Generated/ProceduralSpriteCatalog");
            var installed = ProceduralSpriteFactory.InstallBakedCatalog(catalog);
            _spriteWarmSteps = installed
                ? null
                : ProceduralSpriteFactory.WarmAllSpritesSteps();
            return installed;
        }

        private void RenderRailTrail(int index, RailTrailState trail)
        {
            var mesh = _railTrailMeshViews[index]?.sharedMesh;
            if (mesh == null) return;

            var vertices = _railTrailVertices[index];
            var colors = _railTrailColors[index];
            var direction = trail.End - trail.Start;
            direction = SourceVisualDirection(direction);
            var normal = new Vector2(-direction.y, direction.x);
            var progress = Mathf.Clamp01(trail.Life / 2.25f);
            for (var segment = 0; segment < RailTrailSegmentCount; segment++)
            {
                var step = 0.16f + segment * 0.13f;
                var centre = Vector2.Lerp(trail.Start, trail.End, step) +
                    normal * (Mathf.Sin(step * 37f) * 5f);
                var halfLength = (16f + progress * 10f) * 0.5f;
                const float halfWidth = 1.5f;
                var along = direction * halfLength;
                var across = normal * halfWidth;
                var vertex = segment * 4;
                vertices[vertex] = centre - along - across;
                vertices[vertex + 1] = centre + along - across;
                vertices[vertex + 2] = centre + along + across;
                vertices[vertex + 3] = centre - along + across;

                var bright = step > 0.7f
                    ? new Color(0.914f, 0.835f, 1f, 1f)
                    : new Color(0.545f, 0.361f, 0.988f, 1f);
                bright.a = progress * (0.3f + step * 0.38f);
                colors[vertex] = bright;
                colors[vertex + 1] = bright;
                colors[vertex + 2] = bright;
                colors[vertex + 3] = bright;
            }
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(_railTrailTriangles[index], 0);
            mesh.RecalculateBounds();
            _railTrailViews[index].enabled = true;
        }

        private static string SourceEnemySpriteId(
            string id,
            bool elite,
            string eliteVariantBaseId)
        {
            // Browser elite variants keep their base Enemy.type so their body
            // sprite stays Mortar/Exploder/Gunner/etc.; only the original
            // charging elite uses the generic Elite sprite.
            if (!string.IsNullOrEmpty(eliteVariantBaseId)) return eliteVariantBaseId;
            return elite ? "elite" : id;
        }

        private static string SourceEnemySpriteId(EnemyState enemy)
        {
            var variantBaseId = enemy.EliteKind.HasValue
                ? EliteRules.EliteVariantDef(enemy.EliteKind.Value).BaseId
                : null;
            return SourceEnemySpriteId(enemy.Id, enemy.Elite, variantBaseId);
        }

        private static float SourceEnemySpriteWorldSize(
            string id,
            bool elite,
            string eliteVariantBaseId,
            bool rosterTwo,
            float radius)
        {
            var spriteId = SourceEnemySpriteId(id, elite, eliteVariantBaseId);
            var canvasSize = rosterTwo && !elite
                ? ProceduralSpriteFactory.RosterTwoEnemyCanvasSize(id)
                : ProceduralSpriteFactory.EnemyCanvasSize(spriteId);
            var definitionRadius = elite && string.IsNullOrEmpty(eliteVariantBaseId)
                ? (float)ContentCatalog.Elite.Radius
                : (float)(FindEnemy(id)?.Radius ?? 1);
            return canvasSize * radius / Mathf.Max(1f, definitionRadius);
        }

        private static float SourceEnemySpriteWorldSize(EnemyState enemy)
        {
            var variantBaseId = enemy.EliteKind.HasValue
                ? EliteRules.EliteVariantDef(enemy.EliteKind.Value).BaseId
                : null;
            var rosterTwo = enemy.Roster == EnemyRoster.Two && !enemy.Elite;
            return SourceEnemySpriteWorldSize(
                enemy.Id,
                enemy.Elite,
                variantBaseId,
                rosterTwo,
                enemy.Radius);
        }

        private static float SourceBossSpriteWorldSize(string id, float radius)
        {
            var definitionRadius = (float)(FindBoss(id)?.Radius ?? 1);
            return ProceduralSpriteFactory.BossCanvasSize(id) * radius /
                Mathf.Max(1f, definitionRadius);
        }

        private static float SourceBladeSpriteWorldSize(bool hollow)
        {
            return ProceduralSpriteFactory.BladeCanvasSize(hollow);
        }

        private static float SourceMeteorSpriteWorldSize(int variant, bool explosive)
        {
            return ProceduralSpriteFactory.MeteorCanvasSize(variant, explosive);
        }

        private void RenderDeathGhosts()
        {
            EnsureDeathGhostOrderEntries();
            for (var index = 0; index < _deathGhosts.Length; index++)
            {
                var ghost = _deathGhosts[index];
                var view = _deathGhostViews[index];
                if (!ghost.Active) Hide(view);
            }
            for (var order = 0; order < _deathGhostOrderCount; order++)
            {
                var index = _deathGhostOrder[order];
                if (index < 0 || index >= _deathGhosts.Length) continue;
                var ghost = _deathGhosts[index];
                var view = _deathGhostViews[index];
                if (!ghost.Active || view == null) continue;
                var progress = Mathf.Clamp01(ghost.Life / Mathf.Max(0.001f, ghost.MaxLife));
                var size = ghost.VisualSize * (1f + (1f - progress) * 0.14f);
                view.rendererPriority = order;
                view.transform.position = ghost.Position;
                view.transform.rotation = Quaternion.Euler(0, 0, ghost.Rotation);
                view.transform.localScale = Vector3.one * size;
                view.color = new Color(1f, 1f, 1f, progress * 0.5f);
                view.enabled = true;
            }
        }

        private void RenderDamageIndicators()
        {
            if (_canvas == null) return;
            var canvasRect = _canvas.transform as RectTransform;
            if (canvasRect == null) return;
            var width = canvasRect.rect.width;
            var height = canvasRect.rect.height;
            var radius = Mathf.Min(width, height) * 0.5f - 48f;
            if (radius <= 0) return;
            EnsureDamageIndicatorOrderEntries();
            for (var index = 0; index < _damageIndicators.Length; index++)
            {
                if (!_damageIndicators[index].Active) Hide(_damageIndicatorViews[index]);
            }
            for (var order = 0; order < _damageIndicatorOrderCount; order++)
            {
                var index = _damageIndicatorOrder[order];
                if (index < 0 || index >= _damageIndicators.Length) continue;
                var indicator = _damageIndicators[index];
                var view = _damageIndicatorViews[index];
                if (!indicator.Active || view == null) continue;
                var progress = Mathf.Clamp01(indicator.Life / Mathf.Max(0.001f, indicator.MaxLife));
                view.transform.SetSiblingIndex(_damageIndicatorSiblingBase + order);
                view.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Cos(indicator.Angle) * radius,
                    Mathf.Sin(indicator.Angle) * radius);
                view.rectTransform.rotation = Quaternion.Euler(0, 0, indicator.Angle * Mathf.Rad2Deg);
                view.color = new Color(0.937f, 0.267f, 0.286f, Mathf.Min(1f, progress * 1.7f) * 0.85f);
                view.enabled = true;
            }
        }

        private void RenderFloaters()
        {
            if (_canvas == null || _camera == null) return;
            var canvasRect = _canvas.transform as RectTransform;
            if (canvasRect == null) return;
            EnsureFloaterOrderEntries();
            for (var index = 0; index < _floaters.Length; index++)
            {
                if (!_floaters[index].Active) Hide(_floaterViews[index]);
            }
            for (var order = 0; order < _floaterOrderCount; order++)
            {
                var index = _floaterOrder[order];
                if (index < 0 || index >= _floaters.Length) continue;
                var floater = _floaters[index];
                var view = _floaterViews[index];
                if (!floater.Active || view == null) continue;
                view.transform.SetSiblingIndex(_floaterSiblingBase + order);
                var screen = _camera.WorldToScreenPoint(new Vector3(floater.Position.x, floater.Position.y, 0));
                if (screen.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        screen,
                        null,
                        out var local))
                {
                    view.enabled = false;
                    continue;
                }
                var progress = Mathf.Clamp01(floater.Life / Mathf.Max(0.001f, floater.MaxLife));
                view.rectTransform.anchoredPosition = local;
                view.rectTransform.localScale = Vector3.one;
                view.text = floater.Text;
                view.fontSize = floater.FontSize;
                view.color = new Color(
                    floater.Color.r,
                    floater.Color.g,
                    floater.Color.b,
                    floater.Color.a * Mathf.Min(1f, progress * 2f));
                view.enabled = true;
            }
        }

        /// <summary>
        /// Rasterizes queued procedural sprites for a fixed slice of wall time.
        /// Spreading the set over menu frames keeps it off the load path without
        /// changing what gets built or how it looks: identical calls, just later.
        /// </summary>
        private void PumpSpriteWarm()
        {
            if (_spriteWarmSteps == null) return;

            // realtimeSinceStartupAsDouble rather than deltaTime because the
            // point is to bound the work this frame, not to track sim time.
            var deadline = Time.realtimeSinceStartupAsDouble + SpriteWarmBudgetSeconds;
            do
            {
                if (!_spriteWarmSteps.MoveNext())
                {
                    FinishSpriteWarm();
                    return;
                }
            }
            while (Time.realtimeSinceStartupAsDouble < deadline);
        }

        /// <summary>
        /// Forces the remaining warm work to complete synchronously.
        /// </summary>
        private void DrainSpriteWarm()
        {
            if (_spriteWarmSteps == null) return;
            while (_spriteWarmSteps.MoveNext())
            {
            }

            FinishSpriteWarm();
        }

        private void FinishSpriteWarm()
        {
            _spriteWarmSteps = null;
            // The one upload for everything the warm produced.
            ProceduralSpriteFactory.FlushAtlas();
        }

        private void Render()
        {
            // Publish any sprite baked since the last frame so a lazily baked
            // sprite still appears.
            //
            // Suppressed while the background warm is running: it dirties the
            // atlas page every frame, and Flush re-uploads the whole 2048x2048
            // page, so leaving this unconditional would trade the load-time
            // stall for a menu-long stream of uploads. Safe because every
            // atlased family (enemy, boss, projectile, pickup, gem, meteor) is
            // gameplay-only, and FinishSpriteWarm flushes once at the end.
            if (_spriteWarmSteps == null) ProceduralSpriteFactory.FlushAtlas();
            var playerVisible = _playerHealth > 0 && !_gameOver && !_mainMenuBrowsing;
            if (!playerVisible)
            {
                Hide(_playerAuraView);
                Hide(_playerView);
                Hide(_playerRingView);
            }
            if (playerVisible && _playerAuraView != null)
            {
                _playerAuraView.transform.position = _playerPosition;
                var auraRadius = 32f + Mathf.Sin(_ambientClock * 2.2f) * 4f;
                _playerAuraView.transform.localScale = Vector3.one * (auraRadius * 2f);
                var adrenalLit = _adrenalTimer > 0 && SupportRank("adrenal") > 0;
                _playerAuraView.sprite = ProceduralSpriteFactory.PlayerAura(adrenalLit);
                _playerAuraView.color = Color.white;
                _playerAuraView.enabled = true;
            }
            if (playerVisible && _playerView != null)
            {
                _playerView.transform.position = _playerPosition;
                var blink = _playerIframes > 0 && Mathf.Sin(_ambientClock * 34f) > 0;
                _playerView.color = new Color(1f, 1f, 1f, blink ? 0.35f : 1f);
                _playerView.enabled = true;
            }
            if (playerVisible && _playerRingView != null)
            {
                _playerRingView.transform.position = _playerPosition;
                _playerRingView.transform.rotation = Quaternion.Euler(
                    0,
                    0,
                    _ambientClock * PlayerRingRotationRate() * Mathf.Rad2Deg);
                _playerRingView.transform.localScale = Vector3.one * 62f;
                var blink = _playerIframes > 0 && Mathf.Sin(_ambientClock * 34f) > 0;
                _playerRingView.color = new Color(1f, 1f, 1f, blink ? 0.35f : 1f);
                _playerRingView.enabled = true;
            }

            // The browser draws its compact enemy array forward. Pooled Unity
            // slots are not that array after swap-removal, so keep the visual
            // refresh in logical source order and mirror that order inside the
            // shared body sorting slot with rendererPriority.
            for (var i = 0; i < _enemies.Length; i++)
            {
                Hide(_enemyHarvesterFullViews[i]);
                Hide(_enemyExploderWarningViews[i]);
                if (!_enemies[i].Active) Hide(_enemyViews[i]);
            }
            for (var order = 0; order < _enemyOrderCount; order++)
            {
                var i = _enemyOrder[order];
                if (i < 0 || i >= _enemies.Length || !_enemies[i].Active || _enemyViews[i] == null) continue;
                var enemy = _enemies[i];
                _enemyViews[i].rendererPriority = order;
                SetEnemyPresentationPriority(i, order);
                var rosterTwoVisual = enemy.Roster == EnemyRoster.Two && !enemy.Elite &&
                    (enemy.Id == "chaser" || enemy.Id == "gunner" || enemy.Id == "guard" || enemy.Id == "exploder");
                var harvesterFull = enemy.Id == "harvester" &&
                    enemy.StoredXp >= PickupRules.HarvesterXpLimits(_xpNeed).Individual;
                var flash = SourceEnemyVisualFlash(
                    enemy.Id,
                    enemy.Elite,
                    enemy.State,
                    enemy.HitTimer,
                    harvesterFull,
                    enemy.Seed,
                    _ambientClock);
                var spriteId = SourceEnemySpriteId(enemy);
                _enemyViews[i].sprite = rosterTwoVisual
                    ? ProceduralSpriteFactory.RosterTwoEnemy(enemy.Id, flash)
                    : ProceduralSpriteFactory.Enemy(
                        spriteId,
                        EnemySpriteAccent(enemy),
                        flash);
                _enemyViews[i].transform.position = enemy.Position;
                _enemyViews[i].transform.rotation = Quaternion.Euler(
                    0,
                    0,
                    enemy.Rotation * Mathf.Rad2Deg);
                var enemyVisualScale = SourceEnemyIntroScale(enemy.Age);
                if (enemy.Id == "exploder" && enemy.State == 1)
                {
                    var definition = FindEnemy("exploder");
                    var telegraph = enemy.EliteKind.HasValue &&
                        enemy.EliteKind.Value == EliteVariantId.Exploder
                        ? (float)EliteRules.EliteVariantStatsFor(EliteVariantId.Exploder).TelegraphSeconds
                        : (float)(definition?.TelegraphSeconds ?? 0.9) +
                            (enemy.Roster == EnemyRoster.Two ? 0.28f : 0);
                    enemyVisualScale *= SourceExploderArmedScale(
                        enemy.StateTimer,
                        telegraph,
                        _ambientClock);
                }
                _enemyViews[i].transform.localScale = Vector3.one *
                    (SourceEnemySpriteWorldSize(enemy) * enemyVisualScale);
                if (enemy.Id == "exploder" && enemy.State == 1)
                {
                    var definition = FindEnemy("exploder");
                    var eliteExploder = enemy.EliteKind.HasValue &&
                        enemy.EliteKind.Value == EliteVariantId.Exploder;
                    var telegraph = eliteExploder
                        ? (float)EliteRules.EliteVariantStatsFor(EliteVariantId.Exploder).TelegraphSeconds
                        : (float)(definition?.TelegraphSeconds ?? 0.9) +
                            (enemy.Roster == EnemyRoster.Two ? 0.28f : 0);
                    var warning = EnsureEnemyExploderWarningView(i);
                    warning.sprite = enemy.Roster == EnemyRoster.Two
                        ? ProceduralSpriteFactory.RosterTwoEnemy("exploder", true)
                        : ProceduralSpriteFactory.Enemy("exploder", EnemySpriteAccent(enemy), true);
                    warning.transform.position = enemy.Position;
                    warning.transform.rotation = _enemyViews[i].transform.rotation;
                    warning.transform.localScale = _enemyViews[i].transform.localScale;
                    warning.color = new Color(
                        1f,
                        1f,
                        1f,
                        SourceExploderWarningAlpha(
                            enemy.StateTimer,
                            telegraph,
                            eliteExploder,
                            _ambientClock));
                    warning.enabled = true;
                }
                if (harvesterFull)
                {
                    var overlay = EnsureEnemyHarvesterFullView(i);
                    overlay.sprite = ProceduralSpriteFactory.Enemy(
                        "harvester",
                        EnemySpriteAccent(enemy),
                        true);
                    overlay.transform.position = enemy.Position;
                    overlay.transform.rotation = _enemyViews[i].transform.rotation;
                    overlay.transform.localScale = _enemyViews[i].transform.localScale;
                    overlay.color = new Color(
                        1f,
                        1f,
                        1f,
                        SourceHarvesterFullOverlayAlpha(_ambientClock, enemy.Seed));
                    overlay.enabled = true;
                }
            }
            RenderEliteTelegraphs();
            RenderEnemyTelegraphs();
            RenderEnemyStatus();
            // Player bullets are also a browser compact array. Keep their
            // shared body sorting slot in source order after pooled reuse.
            EnsureBulletOrderEntries();
            for (var order = 0; order < _bulletOrderCount; order++)
            {
                var i = _bulletOrder[order];
                if (i < 0 || i >= _bullets.Length || !_bullets[i].Active || _bulletViews[i] == null)
                {
                    if (i >= 0 && i < _bullets.Length) Hide(_bulletViews[i]);
                    if (i >= 0 && i < _bullets.Length)
                    {
                        Hide(_railAfterimageFarViews[i]);
                        Hide(_railAfterimageNearViews[i]);
                    }
                    continue;
                }
                _bulletViews[i].rendererPriority = order;
                _bulletViews[i].transform.position = _bullets[i].Position;
                var visualScale = SourceBulletVisualScale(
                    ContentCatalog.Weapons[_bullets[i].WeaponIndex].Id,
                    _bullets[i].Radius,
                    _bullets[i].Rank);
                var projectileId = ContentCatalog.Weapons[_bullets[i].WeaponIndex].Id;
                var projectileFrame = ProceduralSpriteFactory.ProjectileFrame(
                    projectileId,
                    SourceProjectileFrameIndex(_bullets[i].Velocity));
                _bulletViews[i].sprite = projectileFrame;
                _bulletViews[i].transform.rotation = Quaternion.identity;
                var projectileFrameSize = SourceProjectileSpriteWorldSize(projectileId);
                _bulletViews[i].transform.localScale = Vector3.one * (projectileFrameSize * visualScale);
                var isRailgun = _bullets[i].WeaponIndex >= 0 &&
                    _bullets[i].WeaponIndex < ContentCatalog.Weapons.Length &&
                    ContentCatalog.Weapons[_bullets[i].WeaponIndex].Id == "railgun";
                if (isRailgun)
                {
                    var direction = SourceVisualDirection(_bullets[i].Velocity);
                    var scale = Vector3.one * (projectileFrameSize * visualScale);
                    var far = EnsureRailAfterimageView(i, false);
                    far.sprite = projectileFrame;
                    far.transform.position = _bullets[i].Position - direction * 34f;
                    far.transform.rotation = Quaternion.identity;
                    far.transform.localScale = scale;
                    far.color = new Color(1f, 1f, 1f, 0.1f);
                    far.enabled = true;
                    var near = EnsureRailAfterimageView(i, true);
                    near.sprite = projectileFrame;
                    near.transform.position = _bullets[i].Position - direction * 19f;
                    near.transform.rotation = Quaternion.identity;
                    near.transform.localScale = scale;
                    near.color = new Color(1f, 1f, 1f, 0.22f);
                    near.enabled = true;
                }
                else
                {
                    Hide(_railAfterimageFarViews[i]);
                    Hide(_railAfterimageNearViews[i]);
                }
                var contrast = _bulletContrastViews[i];
                if (contrast != null)
                {
                    var highContrast = _saveData?.settings != null && _saveData.settings.highContrast;
                    contrast.sprite = projectileFrame;
                    contrast.transform.position = _bullets[i].Position;
                    contrast.transform.rotation = Quaternion.identity;
                    contrast.transform.localScale = Vector3.one *
                        (projectileFrameSize * visualScale * 1.22f);
                    contrast.enabled = highContrast;
                }
            }
            // Hostile shots use the same forward draw order as their source
            // array; fixed Unity slots must not decide overlap order.
            EnsureHostileShotOrderEntries();
            for (var order = 0; order < _hostileShotOrderCount; order++)
            {
                var i = _hostileShotOrder[order];
                if (i < 0 || i >= _hostileShots.Length || !_hostileShots[i].Active)
                {
                    if (i >= 0 && i < _hostileShots.Length && _hostileShotViews[i] != null) Hide(_hostileShotViews[i]);
                    continue;
                }
                var view = EnsureHostileShotView(i);
                view.rendererPriority = order;
                view.transform.position = _hostileShots[i].Position;
                if (!_hostileShots[i].MeteorOwned && !_hostileShots[i].Curved)
                {
                    view.sprite = ProceduralSpriteFactory.ProjectileFrame(
                        "gunner",
                        SourceProjectileFrameIndex(_hostileShots[i].Velocity));
                }
                view.transform.rotation = Quaternion.identity;
                view.transform.localScale = Vector3.one *
                    (_hostileShots[i].MeteorOwned
                        ? 18f
                        : SourceProjectileSpriteWorldSize(_hostileShots[i].Curved ? "curved" : "gunner"));
                view.enabled = true;
            }
            // Meteors are rendered from the browser's compact meteor array as
            // well, so keep body order stable when a slot is recycled.
            EnsureMeteorOrderEntries();
            for (var order = 0; order < _meteorOrderCount; order++)
            {
                var i = _meteorOrder[order];
                if (i < 0 || i >= _meteors.Length || !_meteors[i].Active)
                {
                    if (i >= 0 && i < _meteors.Length)
                    {
                        Hide(_meteorViews[i]);
                        Hide(_meteorHitViews[i]);
                        Hide(_meteorCoreViews[i]);
                        Hide(_meteorDangerArcViews[i]);
                        Hide(_meteorDangerRingViews[i]);
                        Hide(_meteorHealthArcViews[i]);
                    }
                    continue;
                }
                var meteorView = EnsureMeteorView(i);
                meteorView.rendererPriority = order;
                meteorView.sprite = ProceduralSpriteFactory.Meteor(_meteors[i].Variant, _meteors[i].Explosive);
                meteorView.transform.position = _meteors[i].Position;
                meteorView.transform.rotation = Quaternion.Euler(
                    0,
                    0,
                    _meteors[i].Rotation * Mathf.Rad2Deg);
                meteorView.transform.localScale = Vector3.one *
                    SourceMeteorSpriteWorldSize(_meteors[i].Variant, _meteors[i].Explosive);
                // The browser never tints the meteor body while armed; the
                // warning arcs and seeded core carry the fuse read instead.
                meteorView.color = Color.white;
                var hitView = EnsureMeteorHitView(i);
                hitView.sprite = meteorView.sprite;
                hitView.transform.position = _meteors[i].Position;
                hitView.transform.rotation = meteorView.transform.rotation;
                hitView.transform.localScale = meteorView.transform.localScale;
                hitView.color = new Color(1f, 1f, 1f, 0.3f);
                hitView.enabled = !_meteors[i].Explosive && _meteors[i].HitTimer > 0;
                Hide(_meteorHealthArcViews[i]);
                if (_meteorCoreViews[i] != null)
                {
                    _meteorCoreViews[i].transform.position = _meteors[i].Position;
                    _meteorCoreViews[i].transform.rotation = meteorView.transform.rotation;
                    _meteorCoreViews[i].enabled = _meteors[i].Explosive;
                }
                if (_meteors[i].FuseTimer > 0)
                {
                    var fuse = Mathf.Clamp01(1f - _meteors[i].FuseTimer / (float)MeteorRules.ExplosiveFlashSeconds);
                    var sweep = Mathf.PI * 2f * (0.35f + fuse * 0.65f);
                    var dangerArc = EnsureMeteorDangerArcView(i);
                    SetArcLine(
                        dangerArc,
                        _meteors[i].Position,
                        (float)MeteorRules.ExplosiveBlastRadius,
                        -Mathf.PI * 0.5f,
                        -Mathf.PI * 0.5f + sweep,
                        1.5f + fuse * 1.5f,
                        new Color(249f / 255f, 115f / 255f, 22f / 255f, 0.3f + fuse * 0.5f));
                    var dangerRing = EnsureMeteorDangerRingView(i);
                    SetArcLine(
                        dangerRing,
                        _meteors[i].Position,
                        (float)MeteorRules.ExplosiveBlastRadius - 6f,
                        0,
                        Mathf.PI * 2f,
                        1f,
                        new Color(253f / 255f, 230f / 255f, 138f / 255f, 0.18f + fuse * 0.2f));
                    var rate = 6f + fuse * 22f;
                    var beat = 0.5f + 0.5f * Mathf.Sin(_ambientClock * rate + _meteors[i].Seed);
                    var heat = Mathf.Min(0.95f, 0.4f + fuse * 0.5f + beat * 0.22f);
                    if (_meteorCoreViews[i] != null)
                    {
                        _meteorCoreViews[i].color = new Color(1f, 1f, 1f, heat);
                        _meteorCoreViews[i].transform.localScale = Vector3.one * (_meteors[i].VisibleRadius * (2.1f + fuse * 0.5f));
                    }
                }
                else
                {
                    Hide(_meteorDangerArcViews[i]);
                    Hide(_meteorDangerRingViews[i]);
                    if (_meteorCoreViews[i] != null)
                    {
                        var heat = 0.12f + (0.5f + 0.5f * Mathf.Sin(_ambientClock * 2.2f + _meteors[i].Seed)) * 0.1f;
                        _meteorCoreViews[i].color = new Color(1f, 1f, 1f, heat);
                        _meteorCoreViews[i].transform.localScale = Vector3.one * (_meteors[i].VisibleRadius * 1.7f);
                    }
                    if (_meteors[i].Health < _meteors[i].MaxHealth)
                    {
                        var healthRatio = Mathf.Clamp01(_meteors[i].Health / Mathf.Max(0.001f, _meteors[i].MaxHealth));
                        SetArcLine(
                            EnsureMeteorHealthArcView(i),
                            _meteors[i].Position,
                            _meteors[i].VisibleRadius + 5f,
                            -Mathf.PI * 0.5f,
                            -Mathf.PI * 0.5f + Mathf.PI * 2f * healthRatio,
                            2f,
                            new Color(0.886f, 0.51f, 0.247f, 0.45f));
                    }
                }
            }
            RenderSourceFxOrder();
            RenderImpactMarks();
            RenderBlastWaves();
            // Pickups are drawn in their compact forward order. Their slot
            // list already tracks the browser's swap-removal behavior.
            for (var order = 0; order < _pickupOrderCount; order++)
            {
                var i = _pickupOrder[order];
                if (i < 0 || i >= _pickups.Length || !_pickups[i].Active || _pickupViews[i] == null)
                {
                    if (i >= 0 && i < _pickups.Length) Hide(_pickupViews[i]);
                    continue;
                }
                _pickupViews[i].rendererPriority = order;
                var xpTier = -1;
                if (_pickups[i].Kind == PickupKind.Xp)
                {
                    xpTier = XpPickupTier(_pickups[i].Value);
                    _pickupViews[i].sprite = ProceduralSpriteFactory.Gem(xpTier);
                }
                _pickupViews[i].transform.position = _pickups[i].Position;
                var pickupKind = PickupKindName(_pickups[i].Kind);
                var pulse = SourcePickupPulseScale(
                    pickupKind,
                    _pickups[i].Age,
                    _qualityPreset.PickupPulse);
                _pickupViews[i].transform.rotation = Quaternion.Euler(
                    0,
                    0,
                    SourcePickupRotationRadians(pickupKind, _pickups[i].Age) * Mathf.Rad2Deg);
                var frameSize = pickupKind == "xp"
                    ? SourceXpPickupFrameSize(xpTier)
                    : SourceSpecialPickupFrameSize();
                _pickupViews[i].transform.localScale = Vector3.one * (frameSize * pulse);
            }
            RenderBossTelegraphs();
            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _bossOrderCount; bossOrder++)
            {
                var i = _bossOrder[bossOrder];
                var boss = _bosses[i];
                if ((!boss.Active && boss.DeathTimer <= 0) || _bossViews[i] == null) continue;
                _bossViews[i].sprite = ProceduralSpriteFactory.Boss(
                    boss.Id,
                    BossAccent(boss),
                    boss.HitTimer > 0);
                _bossViews[i].transform.position = boss.Position;
                var introProgress = boss.Active && boss.State == 4
                    ? BackOut(Mathf.Clamp01(1f - boss.StateTimer / 1.6f))
                    : 1f;
                var deathAlpha = boss.Active ? 1f : Mathf.Clamp01(boss.DeathTimer / 1.4f);
                _bossViews[i].color = new Color(1f, 1f, 1f, deathAlpha);
                _bossViews[i].transform.localScale = Vector3.one *
                    (SourceBossSpriteWorldSize(boss.Id, boss.Radius) * introProgress);
                _bossViews[i].transform.rotation = Quaternion.Euler(
                    0,
                    0,
                    SourceBossBodyRotationRadians(_ambientClock) * Mathf.Rad2Deg);
                _bossViews[i].enabled = true;
            }
            for (var i = 0; i < _arcEffects.Length; i++)
            {
                var effect = _arcEffects[i];
                if (!effect.Active || _arcViews[i] == null) continue;
                _arcViews[i].enabled = true;
                if (_arcCoreViews[i] != null) _arcCoreViews[i].enabled = true;
            }

            if (_camera != null)
            {
                var shake = CameraShakeOffset();
                _camera.transform.position = new Vector3(
                    _cameraFollowPosition.x + shake.x,
                    _cameraFollowPosition.y + shake.y,
                    -10);
            }

            RenderArena();
            RenderDeathGhosts();
            RenderDamageIndicators();
            RenderFloaters();
        }

        private static void SetRendererPriority(Renderer renderer, int priority)
        {
            if (renderer != null) renderer.rendererPriority = priority;
        }

        private Vector2 CameraShakeOffset()
        {
            if (_cameraTrauma <= 0) return Vector2.zero;
            var magnitude = CameraShakeAmplitude(_cameraTrauma);
            return new Vector2(
                ((float)_fxRng.Next() * 2f - 1f) * magnitude,
                ((float)_fxRng.Next() * 2f - 1f) * magnitude);
        }

        private static float CameraShakeAmplitude(float trauma)
        {
            var clamped = Mathf.Clamp01(trauma);
            return clamped * clamped * 14f;
        }

        /// <summary>
        /// The world is rendered straight to the backbuffer at native
        /// resolution. The previous dynamic-resolution path rendered into a
        /// downscaled RenderTexture and upscaled it through a canvas RawImage,
        /// which resampled the whole frame and was a major source of the port's
        /// softness. Quality presets now scale cosmetic budgets only, never the
        /// resolution the world is rasterized at.
        /// </summary>
        private void ApplyRenderResolution()
        {
            if (_camera == null || Screen.width <= 0 || Screen.height <= 0) return;
            UpdateGameplayCameraViewport();
            _camera.allowDynamicResolution = false;
            // Defensive: guarantee the camera targets the backbuffer even if a
            // scene-authored camera arrived with a render texture assigned.
            if (_camera.targetTexture != null) _camera.targetTexture = null;
            _renderResolutionWidth = Screen.width;
            _renderResolutionHeight = Screen.height;
            _renderResolutionDpi = Mathf.RoundToInt(Mathf.Max(0, Screen.dpi) * 10f);
        }

        private void DrawProfileModal()
        {
            var safeArea = Screen.safeArea;
            var panelWidth = MenuPanelWidth(safeArea.width, _menuPage);
            var panelHeight = MenuPanelMaxHeight(safeArea.height);
            var x = safeArea.xMin + (safeArea.width - panelWidth) * 0.5f;
            var y = safeArea.yMin + (safeArea.height - panelHeight) * 0.5f;
            var panelRect = new Rect(x, y, panelWidth, panelHeight);

            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            DrawOverlayCardBackdropBlur(panelRect, 1f);
            DrawOverlayCardShadow(panelRect, 1f);

            GUI.Box(panelRect, GUIContent.none, MenuSkin().box);

            var accentRect = MenuPanelAccentRect(x, y, panelWidth);
            GUI.color = new Color(0.133f, 0.827f, 0.933f, 0.9f);
            GUI.DrawTexture(accentRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUILayout.BeginArea(new Rect(x + 18f, y + 16f, panelWidth - 36f, panelHeight - 32f));
            DrawProfilePageHeader();
            GUILayout.Space(8f);

            _menuScroll = GUILayout.BeginScrollView(_menuScroll, false, false);
            switch (_menuPage)
            {
                case MenuPage.Workshop:
                    DrawWorkshopMenu();
                    break;
                case MenuPage.Records:
                    DrawRecordsMenu();
                    break;
                case MenuPage.Settings:
                    DrawSettingsMenu();
                    break;
                case MenuPage.Main:
                    DrawOverviewMenu();
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawScreenWarnings()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            var playing = _menuPage == MenuPage.None && !_paused && !_gameOver &&
                !_levelUpActive && !_revivePending;
            if (!playing) return;

            var eventId = _nextDirectorEvent.Id;
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            var width = Screen.width;
            var height = Screen.height;

            if (RusherWarningVisible(playing, _directorWarned, _directorActive, eventId))
            {
                var pulse = (0.4f + 0.3f * Mathf.Sin(_ambientClock * 9f)) * 0.35f;
                GUI.color = new Color(0.984f, 0.443f, 0.545f, pulse);
                var texture = RusherChevronTexture();
                var edge = _nextDirectorEvent.SpawnEdge;
                for (var index = 0; index < 5; index++)
                {
                    var t = (index + 1f) / 6f;
                    var x = 0f;
                    var y = 0f;
                    var angle = 0f;
                    if (edge == 0)
                    {
                        x = width * t;
                        y = 26f;
                        angle = 90f;
                    }
                    else if (edge == 1)
                    {
                        x = width * t;
                        y = height - 26f;
                        angle = -90f;
                    }
                    else if (edge == 2)
                    {
                        x = 26f;
                        y = height * t;
                    }
                    else
                    {
                        x = width - 26f;
                        y = height * t;
                        angle = 180f;
                    }

                    var centre = new Vector2(x, y);
                    GUI.matrix = oldMatrix;
                    GUIUtility.RotateAroundPivot(angle, centre);
                    GUI.DrawTexture(new Rect(x - 20f, y - 10f, 40f, 20f), texture, ScaleMode.StretchToFill, true);
                }
            }

            if (PressureBorderVisible(playing, _directorWarned, _directorActive, eventId))
            {
                var pulse = 0.05f + 0.04f * Mathf.Sin(_ambientClock * 7f);
                GUI.matrix = oldMatrix;
                GUI.color = new Color(0.984f, 0.443f, 0.545f, pulse);
                GUI.DrawTexture(new Rect(0f, 0f, width, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, height - 3f, width, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, 0f, 3f, height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(width - 3f, 0f, 3f, height), Texture2D.whiteTexture);
            }

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawDebugOverlay()
        {
            if (!_debugOverlay) return;

            if (_debugReadoutStyle == null)
            {
                _debugReadoutStyle = new GUIStyle(GUI.skin.label)
                {
                    font = BrowserBodyFont(),
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                };
                _debugReadoutStyle.normal.textColor = new Color(0.525f, 0.937f, 0.611f, 1f);
            }
            if (_debugButtonStyle == null)
            {
                _debugButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    font = BrowserBodyFont(),
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(6, 6, 4, 4),
                };
                _debugButtonStyle.normal.textColor = new Color(0.733f, 0.969f, 0.816f, 1f);
            }

            var safeArea = Screen.safeArea;
            var width = 252f;
            var height = 252f;
            var x = Mathf.Max(12f, safeArea.xMin + 12f);
            var y = Mathf.Max(120f, safeArea.yMin + 110f);
            var oldColor = GUI.color;
            GUI.color = new Color(0.008f, 0.024f, 0.047f, 0.86f);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(x + 8f, y + 7f, width - 16f, height - 14f));
            var fps = Mathf.RoundToInt(1000f / Mathf.Max(0.1f, _debugFrameEmaMs));
            var cycle = ArenaCycleRules.At(ArenaIdName(_arenaId), ArenaCycleElapsedSeconds());
            var lines = new[]
            {
                $"FPS {fps}  {_debugFrameEmaMs:0.0} ms",
                $"Enemies {ActiveEnemies()}/{MaxEnemies}  Bosses {ActiveBosses()}",
                $"Shots {ActiveBullets() + ActiveHostileShots()}",
                $"Particles {(_fx != null ? _fx.particleCount : 0)}",
                $"Pickups {ActivePickups()}",
                $"Quality {_qualityPreset.Detail:0}",
                $"Seed {_runSeed}",
                $"RNG {_rng.Draws}/{_fxRng.Draws}",
                $"Boss cycle {_bossSequence}",
                $"Arena {ArenaName(_arenaId)} · {cycle.CycleId}",
                $"Shift {_arenaTransitionState.Phase} in {Mathf.Max(0f, (float)(_arenaTransitionState.DueAt - _time)):0}s",
                $"Meteors {ActiveMeteors()}  Elites {ActiveEliteVariantTotal()}  R2 {ActiveRosterTwoTotal()}",
            };
            foreach (var line in lines) GUILayout.Label(line, _debugReadoutStyle, GUILayout.Height(14f));
            if (GUILayout.Button("Export run data  [F2]", _debugButtonStyle, GUILayout.Height(24f)))
                ExportTelemetrySnapshot(_gameOver ? "gameover" : "active");
            GUILayout.EndArea();
            GUI.color = oldColor;
        }

        private void DrawMuteControl()
        {
            var safeArea = Screen.safeArea;
            const float size = 44f;
            var rect = new Rect(
                safeArea.xMax - size - 14f,
                safeArea.yMax - size - 14f,
                size,
                size);
            if (_muteButtonStyle == null)
            {
                _muteButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    font = BrowserDisplayFont(),
                    fontSize = 17,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                };
            }

            var muted = _audio != null && _audio.Muted;
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.96f);
            var clicked = GUI.Button(rect, GUIContent.none, _muteButtonStyle);
            if (ControlIconTexture() != null)
            {
                DrawControlIcon(
                    new Rect(rect.x + 11f, rect.y + 11f, 22f, 22f),
                    muted ? "volume-x" : "volume-2",
                    new Color(0.8f, 0.94f, 0.98f, 1f));
            }
            else
            {
                GUI.Label(rect, muted ? "\u2715" : "\u266b", _muteButtonStyle);
            }
            if (clicked)
            {
                ToggleMute();
            }
            GUI.color = oldColor;
        }

        private void DrawEvolutionReveal()
        {
            var safeArea = Screen.safeArea;
            var elapsedSeconds = Mathf.Clamp(
                EvolutionRevealDuration() - _evolutionRevealTimer,
                0f,
                EvolutionRevealDuration());
            var alpha = EvolutionRevealOpacity(elapsedSeconds);
            var scale = EvolutionRevealScale(elapsedSeconds);
            var center = safeArea.center;
            var accent = new Color(
                _evolutionRevealAccent.r,
                _evolutionRevealAccent.g,
                _evolutionRevealAccent.b,
                alpha);
            var oldColor = GUI.color;

            if (_evolutionRevealKickerStyle == null)
            {
                _evolutionRevealKickerStyle = new GUIStyle(MenuBodyStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                };
                _evolutionRevealPreviousStyle = new GUIStyle(MenuBodyStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 13,
                };
                _evolutionRevealTitleStyle = new GUIStyle(MenuTitleStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = EvolutionRevealTitleFontSize(safeArea.width),
                    wordWrap = false,
                };
                _evolutionRevealTitleGlowStyle = new GUIStyle(_evolutionRevealTitleStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                };
                _evolutionRevealTitleGlowStyle.normal.background = null;
                _evolutionRevealTitleGlowStyle.hover.background = null;
            }
            _evolutionRevealTitleStyle.fontSize = EvolutionRevealTitleFontSize(safeArea.width);
            _evolutionRevealTitleGlowStyle.fontSize = _evolutionRevealTitleStyle.fontSize;
            _evolutionRevealKickerStyle.normal.textColor = accent;
            _evolutionRevealPreviousStyle.normal.textColor = new Color(0.58f, 0.64f, 0.72f, alpha);
            _evolutionRevealTitleStyle.normal.textColor = new Color(0.97f, 0.98f, 1f, alpha);

            var markSize = 78f * scale;
            var markRect = new Rect(center.x - markSize * 0.5f, center.y - 116f * scale,
                markSize, markSize);
            var lineWidth = Mathf.Min(safeArea.width * 0.70f, 620f) * scale;
            var lineHeight = EvolutionRevealCrossLineLength(safeArea.width) * scale;
            GUI.color = new Color(1f, 1f, 1f, alpha * 0.7f);
            var crossLineTexture = EvolutionCrossLineTexture(_evolutionRevealAccent);
            GUI.DrawTexture(
                new Rect(center.x - lineWidth * 0.5f, center.y - 2f, lineWidth, 1f),
                crossLineTexture,
                ScaleMode.StretchToFill,
                true);
            var crossLineMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(90f, center);
            GUI.DrawTexture(
                new Rect(center.x - lineHeight * 0.5f, center.y - 0.5f, lineHeight, 1f),
                crossLineTexture,
                ScaleMode.StretchToFill,
                true);
            GUI.matrix = crossLineMatrix;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            // Browser `.evolution-mark` rotates as a badge; restore the IMGUI
            // matrix before drawing the centered text below it.
            var revealMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-8f, markRect.center);
            var haloSize = 150f * scale;
            GUI.DrawTexture(
                new Rect(markRect.center.x - haloSize * 0.5f, markRect.center.y - haloSize * 0.5f, haloSize, haloSize),
                EvolutionMarkGlowTexture(_evolutionRevealAccent),
                ScaleMode.StretchToFill,
                true);
            var ringSize = 94f * scale;
            GUI.DrawTexture(
                new Rect(markRect.center.x - ringSize * 0.5f, markRect.center.y - ringSize * 0.5f, ringSize, ringSize),
                EvolutionMarkRingTexture(_evolutionRevealAccent),
                ScaleMode.StretchToFill,
                true);
            GUI.Box(markRect, GUIContent.none, EvolutionMarkStyle(_evolutionRevealAccent));
            GUI.matrix = revealMatrix;
            GUI.color = accent;
            var iconTexture = BuildChipIconTexture();
            var iconSlot = BuildChipIconSlot(_evolutionRevealWeaponId);
            var iconMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(8f, markRect.center);
            if (iconTexture != null && iconSlot >= 0)
            {
                var iconSize = 34f * scale;
                GUI.DrawTextureWithTexCoords(
                    new Rect(
                        center.x - iconSize * 0.5f,
                        markRect.center.y - iconSize * 0.5f,
                        iconSize,
                        iconSize),
                    iconTexture,
                    BuildChipIconUv(_evolutionRevealWeaponId),
                    true);
            }
            else
            {
                GUI.Label(markRect, "\u25c7", _evolutionRevealTitleStyle);
            }
            GUI.matrix = iconMatrix;

            var labelWidth = Mathf.Min(safeArea.width - 40f, 640f);
            var kickerRect = new Rect(
                center.x - labelWidth * 0.5f,
                center.y - 34f * scale,
                labelWidth,
                26f);
            var previousRect = new Rect(
                center.x - labelWidth * 0.5f,
                center.y + 4f * scale,
                labelWidth,
                28f);
            var titleRect = new Rect(
                center.x - labelWidth * 0.5f,
                center.y + 30f * scale,
                labelWidth,
                58f);
            DrawEvolutionTextBand(kickerRect, alpha);
            DrawEvolutionTextBand(previousRect, alpha);
            DrawEvolutionTextBand(titleRect, alpha);
            GUI.color = Color.white;
            var titleText = (_evolutionRevealName ?? string.Empty).ToUpperInvariant();
            var introBlur = EvolutionRevealIntroBlur(elapsedSeconds);
            if (introBlur > 0.01f)
            {
                DrawEvolutionBlurredLabel(
                    kickerRect,
                    "WEAPON EVOLVED",
                    _evolutionRevealKickerStyle,
                    introBlur);
                DrawEvolutionBlurredLabel(
                    previousRect,
                    (_evolutionRevealPreviousName ?? string.Empty).ToUpperInvariant(),
                    _evolutionRevealPreviousStyle,
                    introBlur);
                DrawEvolutionBlurredLabel(
                    titleRect,
                    titleText,
                    _evolutionRevealTitleStyle,
                    introBlur);
            }
            DrawEvolutionTitleGlow(titleRect, titleText, scale, alpha);
            GUI.Label(kickerRect, "WEAPON EVOLVED", _evolutionRevealKickerStyle);
            GUI.Label(
                previousRect,
                (_evolutionRevealPreviousName ?? string.Empty).ToUpperInvariant(),
                _evolutionRevealPreviousStyle);
            GUI.Label(titleRect, titleText, _evolutionRevealTitleStyle);
            GUI.color = oldColor;
        }

        private void DrawEvolutionBlurredLabel(
            Rect rect,
            string text,
            GUIStyle style,
            float blurRadius)
        {
            if (style == null || string.IsNullOrEmpty(text) || blurRadius <= 0f) return;
            var originalTextColor = style.normal.textColor;
            const int sampleCount = 12;
            for (var ring = 0; ring < 3; ring++)
            {
                var radius = blurRadius * (ring + 1f) / 3f;
                var sampleAlpha = originalTextColor.a *
                    (ring == 0 ? 0.018f : ring == 1 ? 0.012f : 0.006f);
                style.normal.textColor = new Color(
                    originalTextColor.r,
                    originalTextColor.g,
                    originalTextColor.b,
                    sampleAlpha);
                for (var sample = 0; sample < sampleCount; sample++)
                {
                    var angle = sample * Mathf.PI * 2f / sampleCount;
                    var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    GUI.Label(
                        new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height),
                        text,
                        style);
                }
            }
            style.normal.textColor = originalTextColor;
        }

        private void DrawEvolutionTitleGlow(Rect rect, string text, float scale, float alpha)
        {
            if (_evolutionRevealTitleGlowStyle == null || string.IsNullOrEmpty(text)) return;
            const int sampleCount = 8;
            var baseAlpha = EvolutionRevealTitleGlowAlpha(alpha);
            var radiusScale = Mathf.Max(0.01f, scale);
            for (var ring = 0; ring < 3; ring++)
            {
                var ringRadius = 26f * radiusScale * ((ring + 1f) / 3f);
                var sampleAlpha = baseAlpha * (ring == 0 ? 0.04f : ring == 1 ? 0.025f : 0.012f);
                _evolutionRevealTitleGlowStyle.normal.textColor = new Color(
                    _evolutionRevealAccent.r,
                    _evolutionRevealAccent.g,
                    _evolutionRevealAccent.b,
                    sampleAlpha);
                for (var sample = 0; sample < sampleCount; sample++)
                {
                    var angle = sample * Mathf.PI * 2f / sampleCount;
                    var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                    GUI.Label(
                        new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height),
                        text,
                        _evolutionRevealTitleGlowStyle);
                }
            }
        }

        private static void DrawEvolutionTextBand(Rect rect, float alpha)
        {
            var band = new Rect(rect.x - 24f, rect.y, rect.width + 48f, rect.height);
            GUI.color = new Color(0.012f, 0.027f, 0.071f, 0.82f * Mathf.Clamp01(alpha));
            GUI.DrawTexture(band, Texture2D.whiteTexture);
        }

        private void DrawGameOverOverlay()
        {
            var oldColor = GUI.color;
            var safeArea = Screen.safeArea;
            var width = BrowserResultCardWidth(safeArea.width);
            var height = BrowserResultCardHeight(safeArea.height);
            var x = safeArea.xMin + (safeArea.width - width) * 0.5f;
            var y = safeArea.yMin + (safeArea.height - height) * 0.5f;
            var overlayFade = CurrentOverlayFadeAlpha();
            var cardAlpha = CurrentOverlayCardAlpha();
            var cardOffset = CurrentOverlayCardOffset();
            GUI.color = new Color(0.008f, 0.020f, 0.059f, 0.59f * overlayFade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Translate(new Vector3(0f, cardOffset, 0f)) * GUI.matrix;
            DrawOverlayCardBackdropBlur(new Rect(x, y, width, height), cardAlpha);
            DrawOverlayCardShadow(new Rect(x, y, width, height), cardAlpha);
            GUI.color = WithAlpha(Color.white, cardAlpha);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, ResultCardStyle());
            GUI.color = WithAlpha(Color.white, cardAlpha);
            GUILayout.BeginArea(new Rect(x + 25f, y + 25f, width - 50f, height - 50f));
            _gameOverScroll = GUILayout.BeginScrollView(_gameOverScroll, GUILayout.ExpandHeight(true));
            GUILayout.Label("RUN ENDED", ResultKickerStyle());
            GUILayout.Space(ResultHeadingGap() + ResultKickerBottomMargin());
            DrawResultTitle();
            var hasResultBadge = false;
            if (_lastRunIsBest)
            {
                GUILayout.Space(ResultTitleBottomMargin() + ResultCardGap());
                DrawCenteredResultBadge("NEW BEST", ResultBestBadgeStyle(), true);
                hasResultBadge = true;
            }
            if (!_lastRunSaved)
            {
                GUILayout.Space(
                    (hasResultBadge ? ResultCardGap() : ResultTitleBottomMargin() + ResultCardGap()));
                DrawCenteredResultBadge("PROGRESS WAS NOT SAVED", ResultSaveWarningStyle(), false);
                hasResultBadge = true;
            }
            GUILayout.Space(
                (hasResultBadge ? ResultCardGap() : ResultTitleBottomMargin() + ResultCardGap()) +
                ResultMetricGridMargin());

            var metrics = new[]
            {
                new ResultMetric("Score", CurrentScore().ToString("N0")),
                new ResultMetric("Time", FormatRunTime(Mathf.FloorToInt(_time))),
                new ResultMetric("Kills", _kills.ToString()),
                new ResultMetric("Parts", "+" + _partsEarned.ToString()),
                new ResultMetric("Level", _level.ToString()),
                new ResultMetric("Bosses", _bossKills.ToString()),
            };
            var metricColumns = ResultMetricColumns(safeArea.width);
            for (var start = 0; start < metrics.Length; start += metricColumns)
            {
                if (start > 0) GUILayout.Space(BrowserMetricGridGap());
                GUILayout.BeginHorizontal();
                for (var column = 0; column < metricColumns; column++)
                {
                    var index = start + column;
                    if (index >= metrics.Length) break;
                    if (column > 0) GUILayout.Space(BrowserMetricGridGap());
                    DrawResultMetric(metrics[index].Label, metrics[index].Value);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(ResultMetricGridMargin() + ResultCardGap());
            GUILayout.BeginVertical(ResultDetailPanelStyle());
            GUILayout.Label("FINAL BUILD", ResultDetailHeaderStyle());
            DrawBuildChipGrid(width - 50f);
            GUILayout.EndVertical();

            GUILayout.Space(ResultCardGap());
            DrawDamageBreakdown();

            GUILayout.Space(ResultActionButtonGap());
            if (DrawResultActionButton("Play again", "rotate-ccw", true)) StartRun();
            GUILayout.Space(ResultActionButtonGap());
            if (DrawResultActionButton("Main menu", "house", false))
            {
                EnterMainMenu();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawBuildChipGrid(float availableWidth)
        {
            if (_upgradeProgress == null) return;

            var chips = new List<BuildChipRecord>();
            var weaponCount = Mathf.Min(ContentCatalog.Weapons.Length, _upgradeProgress.WeaponRanks.Length);
            for (var index = 0; index < weaponCount; index++)
            {
                var rank = _upgradeProgress.WeaponRanks[index];
                if (rank <= 0) continue;
                var isEvolved = index < _upgradeProgress.Evolved.Length && _upgradeProgress.Evolved[index];
                chips.Add(new BuildChipRecord
                {
                    Id = ContentCatalog.Weapons[index].Id,
                    Name = WeaponDisplayName(index, isEvolved),
                    Rank = rank,
                    Accent = WeaponDisplayAccent(index, isEvolved),
                    Evolved = isEvolved,
                });
            }
            var supportCount = Mathf.Min(ContentCatalog.Supports.Length, _upgradeProgress.SupportRanks.Length);
            for (var index = 0; index < supportCount; index++)
            {
                var rank = _upgradeProgress.SupportRanks[index];
                if (rank <= 0) continue;
                chips.Add(new BuildChipRecord
                {
                    Id = ContentCatalog.Supports[index].Id,
                    Name = ContentCatalog.Supports[index].Name,
                    Rank = rank,
                    Accent = ContentCatalog.Supports[index].Accent,
                });
            }
            var lateCount = Mathf.Min(ContentCatalog.LateUpgrades.Length, _upgradeProgress.LateRanks.Length);
            for (var index = 0; index < lateCount; index++)
            {
                var rank = _upgradeProgress.LateRanks[index];
                if (rank <= 0) continue;
                chips.Add(new BuildChipRecord
                {
                    Id = ContentCatalog.LateUpgrades[index].Id,
                    Name = ContentCatalog.LateUpgrades[index].Name,
                    Rank = rank,
                    Accent = ContentCatalog.LateUpgrades[index].Accent,
                });
            }

            if (chips.Count == 0) return;

            var rowWidth = 0f;
            var safeWidth = Mathf.Max(1f, availableWidth);
            for (var index = 0; index < chips.Count; index++)
            {
                var chip = chips[index];
                var chipWidth = Mathf.Min(safeWidth, BuildChipWidth(chip.Name));
                var gap = rowWidth > 0f ? 6f : 0f;
                if (rowWidth > 0f && rowWidth + gap + chipWidth > safeWidth)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.Space(6f);
                    rowWidth = 0f;
                    gap = 0f;
                }
                if (rowWidth <= 0f) GUILayout.BeginHorizontal();
                DrawBuildChip(chip.Id, chip.Name, chip.Rank, chip.Accent, chip.Evolved, chipWidth);
                rowWidth += gap + chipWidth;
            }
            if (rowWidth > 0f) GUILayout.EndHorizontal();
        }

        private void DrawDamageBreakdown()
        {
            GUILayout.BeginVertical(ResultDetailPanelStyle());
            GUILayout.Label("DAMAGE BY WEAPON", ResultDetailHeaderStyle());
            var damageRows = 0;
            for (var index = 0; index < Mathf.Min(ContentCatalog.Weapons.Length, _weaponDamage.Length); index++)
            {
                if (_upgradeProgress == null || index >= _upgradeProgress.WeaponRanks.Length ||
                    _upgradeProgress.WeaponRanks[index] <= 0) continue;

                if (damageRows > 0) GUILayout.Space(ResultDamageRowGap());
                var displayName = WeaponDisplayName(index, _upgradeProgress.Evolved[index]);
                GUILayout.BeginHorizontal();
                GUILayout.Label(displayName, ResultDamageLabelStyle(), GUILayout.ExpandWidth(true));
                GUILayout.Label(
                    RoundedDamageCounter(_weaponDamage[index]).ToString("N0"),
                    ResultDamageValueStyle(),
                    GUILayout.Width(90f));
                GUILayout.EndHorizontal();
                damageRows++;
            }
            if (damageRows == 0) GUILayout.Label("No weapon damage recorded.", ResultDamageLabelStyle());
            GUILayout.EndVertical();
        }

        private void DrawBuildChip(
            string id,
            string name,
            int rank,
            string accentHex,
            bool evolved,
            float width)
        {
            var oldColor = GUI.color;
            var accent = ParseColor(accentHex, new Color(0.4f, 0.9f, 1f, 1f));
            GUILayout.BeginHorizontal(
                ResultBuildChipStyle(accentHex, evolved),
                GUILayout.MinHeight(BuildChipMinHeight()),
                GUILayout.Width(width));
            GUI.color = accent;
            var iconSize = BuildChipIconSize();
            var iconRect = GUILayoutUtility.GetRect(
                iconSize,
                iconSize,
                GUILayout.Width(iconSize),
                GUILayout.Height(iconSize));
            var iconTexture = BuildChipIconTexture();
            if (iconTexture != null)
                GUI.DrawTextureWithTexCoords(iconRect, iconTexture, BuildChipIconUv(id), true);
            else
                GUI.Label(iconRect, BuildChipGlyph(id), MenuSectionStyle());
            GUI.color = Color.white;
            GUILayout.Space(6f);
            GUILayout.Label(name, ResultBuildChipNameStyle(), GUILayout.ExpandWidth(true));
            GUILayout.Space(6f);
            GUILayout.Label(
                rank.ToString(),
                ResultBuildChipRankStyle(accentHex),
                GUILayout.Width(BuildChipRankSize()),
                GUILayout.Height(BuildChipRankSize()));
            GUILayout.EndHorizontal();
            GUI.color = oldColor;
        }

        private void DrawCenteredResultBadge(string text, GUIStyle style, bool pulse)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var content = new GUIContent(text);
            var rect = GUILayoutUtility.GetRect(content, style);
            if (pulse && Event.current != null && Event.current.type == EventType.Repaint)
                DrawResultBestPulse(rect);
            GUI.Label(rect, content, style);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawResultTitle()
        {
            var style = ResultTitleStyle();
            var content = new GUIContent("TRY ANOTHER BUILD");
            var rect = GUILayoutUtility.GetRect(
                content,
                style,
                GUILayout.ExpandWidth(true));
            if (Event.current != null && Event.current.type == EventType.Repaint)
                DrawResultTitleGlow(rect, content.text);
            GUI.Label(rect, content, style);
        }

        private void DrawResultTitleGlow(Rect rect, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = ResultTitleGlowStyle();
            var oldTextColor = style.normal.textColor;
            const int sampleCount = 12;
            for (var ring = 0; ring < 2; ring++)
            {
                var radius = ResultTitleShadowRadius(ring);
                var sampleAlpha = ResultTitleShadowAlpha(ring) *
                    (ring == 0 ? 0.12f : 0.05f);
                style.normal.textColor = new Color(
                    34f / 255f,
                    211f / 255f,
                    238f / 255f,
                    sampleAlpha);
                for (var sample = 0; sample < sampleCount; sample++)
                {
                    var angle = sample * Mathf.PI * 2f / sampleCount;
                    var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    GUI.Label(
                        new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height),
                        text,
                        style);
                }
            }
            style.normal.textColor = oldTextColor;
        }

        private bool DrawResultActionButton(string label, string iconId, bool primary)
        {
            var style = ResultActionButtonStyle(primary);
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                style,
                GUILayout.Height(ResultActionButtonHeight()),
                GUILayout.ExpandWidth(true));
            var oldMatrix = GUI.matrix;
            if (ResultActionButtonIsActive(rect))
                GUIUtility.ScaleAroundPivot(
                    new Vector2(ResultActionActiveScale(), ResultActionActiveScale()),
                    rect.center);
            var isHovered = PrimaryActionButtonIsHovered(rect);
            if (primary) DrawPrimaryActionOuterShadow(rect, isHovered);
            var clicked = GUI.Button(rect, GUIContent.none, style);
            if (primary) DrawPrimaryActionInsetShadow(rect, isHovered);
            var oldColor = GUI.color;
            var iconSize = ResultActionIconSize(iconId);
            var labelStyle = ResultActionLabelStyle(primary);
            var labelWidth = labelStyle.CalcSize(new GUIContent(label)).x;
            var iconBoxSize = ResultActionIconBoxSize(primary, iconId);
            var groupWidth = iconBoxSize + 12f + labelWidth;
            var groupX = rect.x + Mathf.Max(0f, (rect.width - groupWidth) * 0.5f);
            var iconBoxRect = new Rect(
                groupX,
                rect.center.y - iconBoxSize * 0.5f,
                iconBoxSize,
                iconBoxSize);
            if (primary)
                GUI.Box(iconBoxRect, GUIContent.none, ResultActionPrimaryIconStyle());
            var iconRect = primary
                ? new Rect(
                    iconBoxRect.x + 10f,
                    iconBoxRect.y + 10f,
                    iconSize,
                    iconSize)
                : iconBoxRect;
            DrawControlIcon(
                iconRect,
                iconId,
                primary
                    ? new Color(0.647f, 0.953f, 0.988f, 1f)
                    : new Color(0.859f, 0.898f, 0.933f, 1f));
            GUI.color = WithAlpha(Color.white, oldColor.a);
            GUI.Label(
                new Rect(
                    groupX + iconBoxSize + 12f,
                    rect.y + 4f,
                    Mathf.Max(20f, labelWidth),
                    Mathf.Max(18f, rect.height - 8f)),
                label,
                labelStyle);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            return clicked;
        }

        private static void DrawMenuStartOuterShadow(Rect rect, bool hovered, float breathe)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            var width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var texture = MenuStartOuterShadowTexture(width, height, hovered, breathe);
            var margin = MenuStartShadowTextureMargin(hovered, breathe);
            var oldColor = GUI.color;
            GUI.color = WithAlpha(
                new Color(34f / 255f, 211f / 255f, 238f / 255f, 1f),
                oldColor.a);
            GUI.DrawTexture(
                new Rect(
                    rect.x - margin,
                    rect.y - margin,
                    texture.width,
                    texture.height),
                texture,
                ScaleMode.StretchToFill,
                true);
            GUI.color = oldColor;
        }

        private static void DrawMenuStartInsetShadow(Rect rect, bool hovered, float breathe)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            var width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var texture = MenuStartInsetShadowTexture(width, height, hovered, breathe);
            var oldColor = GUI.color;
            GUI.color = WithAlpha(
                new Color(34f / 255f, 211f / 255f, 238f / 255f, 1f),
                oldColor.a);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
        }

        private static void DrawPrimaryActionOuterShadow(Rect rect, bool hovered)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            var width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var texture = PrimaryActionOuterShadowTexture(width, height, hovered);
            var margin = PrimaryActionShadowTextureMargin(hovered);
            var oldColor = GUI.color;
            GUI.color = WithAlpha(
                new Color(34f / 255f, 211f / 255f, 238f / 255f, 1f),
                oldColor.a);
            GUI.DrawTexture(
                new Rect(
                    rect.x - margin,
                    rect.y - margin,
                    texture.width,
                    texture.height),
                texture,
                ScaleMode.StretchToFill,
                true);
            GUI.color = oldColor;
        }

        private static void DrawPrimaryActionInsetShadow(Rect rect, bool hovered)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            var width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var texture = PrimaryActionInsetShadowTexture(width, height, hovered);
            var oldColor = GUI.color;
            GUI.color = WithAlpha(
                new Color(34f / 255f, 211f / 255f, 238f / 255f, 1f),
                oldColor.a);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
        }

        private static void DrawResultBestPulse(Rect rect)
        {
            var time = Time.unscaledTime;
            var radius = ResultBestGlowRadius(time);
            var sourceAlpha = ResultBestGlowAlpha(time);
            var oldColor = GUI.color;
            for (var layer = 0; layer < ResultBestGlowLayerCount(); layer++)
            {
                DrawResultBestPulseLayer(
                    rect,
                    radius,
                    sourceAlpha * ResultBestGlowLayerAlphaFactor(layer),
                    layer);
            }
            GUI.color = oldColor;
        }

        private static void DrawResultBestPulseLayer(
            Rect rect,
            float radius,
            float alpha,
            int layer)
        {
            var spread = ResultBestGlowLayerSpread(radius, layer);
            var thickness = Mathf.Max(1f, radius / ResultBestGlowLayerCount() * 0.72f);
            var outer = new Rect(
                rect.x - spread,
                rect.y - spread,
                rect.width + spread * 2f,
                rect.height + spread * 2f);
            GUI.color = new Color(250f / 255f, 204f / 255f, 21f / 255f, alpha);
            GUI.DrawTexture(new Rect(outer.x, outer.y, outer.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outer.x, outer.yMax - thickness, outer.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outer.x, outer.y + thickness, thickness, outer.height - thickness * 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outer.xMax - thickness, outer.y + thickness, thickness, outer.height - thickness * 2f), Texture2D.whiteTexture);
        }

        private void DrawResultMetric(string label, string value)
        {
            GUILayout.BeginVertical(
                RecordMetricBoxStyle(),
                GUILayout.MinWidth(105f),
                GUILayout.Height(BrowserMetricMinHeight()),
                GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.Label(label.ToUpperInvariant(), RecordMetricLabelStyle());
            GUILayout.Space(BrowserMetricContentGap());
            GUILayout.Label(value, RecordMetricValueStyle());
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void DrawLevelUpPrompt()
        {
            var oldColor = GUI.color;
            var safeArea = Screen.safeArea;
            var width = LevelUpContentWidth(safeArea.width);
            var height = Mathf.Min(620f, safeArea.height - 32f);
            var x = safeArea.xMin + (safeArea.width - width) * 0.5f;
            var y = safeArea.yMin + (safeArea.height - height) * 0.5f;
            var smallViewport = LevelUpUsesShortLandscapeLayout(safeArea.width, safeArea.height);
            var contentInset = smallViewport ? 13f : 0f;
            var contentWidth = width - contentInset * 2f;
            GUI.color = new Color(0.008f, 0.020f, 0.059f, 0.59f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            EnsureLevelUpPromptStyles();
            GUILayout.BeginArea(new Rect(x + contentInset, y, contentWidth, height));
            if (smallViewport)
                _levelUpScroll = GUILayout.BeginScrollView(_levelUpScroll, GUILayout.ExpandHeight(true));
            GUILayout.Space(smallViewport ? 13f : 8f);
            GUILayout.Label("LEVEL UP", _levelUpKickerStyle, GUILayout.Height(18f));
            GUILayout.Space(8f);
            GUILayout.Label("CHOOSE AN UPGRADE", _levelUpTitleStyle, GUILayout.Height(48f));
            GUILayout.Space(LevelUpHeaderGap());

            if (_levelOptions != null)
            {
                var columns = LevelUpGridColumns(safeArea.width, _levelOptions.Length);
                if (columns > 1)
                {
                    var gap = UpgradeCardGridGap();
                    var cardWidth = Mathf.Max(160f, (contentWidth - gap * (columns - 1)) / columns);
                    var cardHeight = Mathf.Clamp(height - 160f, 190f, UpgradeCardMinHeight(false));
                    GUILayout.BeginHorizontal();
                    for (var index = 0; index < _levelOptions.Length; index++)
                    {
                        var option = _levelOptions[index];
                        if (DrawUpgradeCard(option, index, cardHeight, cardWidth))
                        {
                            SelectLevelOption(index);
                            GUILayout.EndHorizontal();
                            if (smallViewport) GUILayout.EndScrollView();
                            GUILayout.EndArea();
                            GUI.color = oldColor;
                            return;
                        }
                        if (index < _levelOptions.Length - 1) GUILayout.Space(gap);
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    var optionHeight = Mathf.Clamp(
                        (height - 160f) / Mathf.Max(1, _levelOptions.Length),
                        smallViewport ? UpgradeCardSmallViewportMinHeight() : UpgradeCardMinHeight(true),
                        smallViewport ? 210f : 170f);
                    for (var index = 0; index < _levelOptions.Length; index++)
                    {
                        var option = _levelOptions[index];
                        if (DrawUpgradeCard(option, index, optionHeight, -1f))
                        {
                            SelectLevelOption(index);
                            if (smallViewport) GUILayout.EndScrollView();
                            GUILayout.EndArea();
                            GUI.color = oldColor;
                            return;
                        }
                        GUILayout.Space(6f);
                    }
                }
            }

            if (_rerollsRemaining > 0)
            {
                if (DrawRerollButton())
                    RerollLevelOptions();
            }
            else
            {
                GUILayout.Label("No rerolls left.", MenuBodyStyle());
            }
            if (smallViewport) GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.color = oldColor;
        }

        private bool DrawRerollButton()
        {
            EnsureRerollStyles();
            GUILayout.Space(RerollRowMargin());
            var canReroll = _rerollsRemaining > 0;
            var oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && canReroll;
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.button,
                GUILayout.Height(RerollButtonHeight()),
                GUILayout.ExpandWidth(true));
            var clicked = GUI.Button(rect, GUIContent.none, _rerollButtonStyle);
            GUI.enabled = oldEnabled;

            var oldColor = GUI.color;
            var icon = RerollIconTexture();
            var tint = RerollActionTextColor(canReroll);
            var groupWidth = 190f;
            var groupX = rect.center.x - groupWidth * 0.5f;
            GUI.color = tint;
            if (icon != null)
            {
                GUI.DrawTexture(
                    new Rect(groupX + 4f, rect.y + (rect.height - 16f) * 0.5f, 16f, 16f),
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
            }
            GUI.color = tint;
            GUI.Label(
                new Rect(groupX + 28f, rect.y + 1f, 122f, rect.height - 2f),
                canReroll ? $"Reroll ({_rerollsRemaining})" : "Reroll used",
                _rerollButtonLabelStyle);
            var keyRect = new Rect(
                groupX + groupWidth - RerollKeycapWidth() - 4f,
                rect.y + (rect.height - 22f) * 0.5f,
                RerollKeycapWidth(),
                22f);
            GUI.color = Color.white;
            GUI.Box(keyRect, GUIContent.none, _rerollKeycapStyle);
            GUI.color = new Color(0.58f, 0.64f, 0.72f, 1f);
            GUI.Label(keyRect, "Q", _rerollKeyStyle);
            GUI.color = oldColor;
            return clicked && canReroll;
        }

        private bool DrawUpgradeCard(UpgradeOptionDefinition option, int index, float height, float width)
        {
            if (width > 0f)
                return DrawDesktopUpgradeCard(option, index, height, width);

            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.button,
                GUILayout.Height(height),
                GUILayout.ExpandWidth(true));
            var oldColor = GUI.color;
            var accent = ParseColor(option?.Accent, new Color(0.4f, 0.9f, 1f, 1f));
            var evolution = option?.Kind == UpgradeOptionKind.Evolution;
            EnsureUpgradeCardStyles();
            var smallViewport = LevelUpUsesShortLandscapeLayout(Screen.safeArea.width, Screen.safeArea.height);
            var cardPadding = UpgradeCardContentPadding(smallViewport);
            _upgradeCardMobileNameStyle.fontSize = UpgradeCardNameFontSize();
            var entranceProgress = UpgradeCardEntranceProgressForRuntime(index);
            var animationAlpha = Mathf.Clamp01(entranceProgress);
            var cardMatrix = GUI.matrix;
            ApplyUpgradeCardTransform(rect, entranceProgress);
            GUI.color = WithAlpha(Color.white, animationAlpha);
            var clicked = GUI.Button(rect, GUIContent.none, UpgradeCardStyle(accent, evolution));
            DrawUpgradeCardAccent(rect, accent, evolution, animationAlpha);

            var iconColumnWidth = 56f;
            var iconSize = smallViewport ? 36f : 56f;
            var iconFrame = new Rect(
                rect.x + cardPadding + (iconColumnWidth - iconSize) * 0.5f,
                rect.y + cardPadding,
                iconSize,
                iconSize);
            var iconId = UpgradeOptionIconId(option);
            var iconTexture = UpgradeOptionIconTexture(iconId);
            var iconRect = new Rect(
                iconFrame.center.x - 11.5f,
                iconFrame.center.y - 11.5f,
                23f,
                23f);
            var baseMatrix = GUI.matrix;
            if (evolution)
                GUIUtility.RotateAroundPivot(-8f, iconFrame.center);
            GUI.color = WithAlpha(Color.white, animationAlpha);
            GUI.Box(iconFrame, GUIContent.none, UpgradeIconStyle(accent, evolution));
            GUI.matrix = baseMatrix;
            if (evolution)
                GUIUtility.RotateAroundPivot(8f, iconFrame.center);
            GUI.color = WithAlpha(accent, animationAlpha);
            if (iconTexture != null)
                GUI.DrawTextureWithTexCoords(iconRect, iconTexture, UpgradeOptionIconUv(iconId), true);
            else
                GUI.Label(iconRect, BuildChipGlyph(iconId), MenuSectionStyle());
            GUI.matrix = baseMatrix;

            var textX = rect.x + cardPadding + iconColumnWidth + 13f;
            var textWidth = Mathf.Max(80f, rect.xMax - textX - cardPadding);
            var metaY = rect.y + cardPadding;
            var nameY = metaY + UpgradeCardMetaLineHeight() + UpgradeCardNameMarginTop();
            var descriptionY = nameY + UpgradeCardNameLineHeight() + UpgradeCardDescriptionMarginTop();
            GUI.color = WithAlpha(Color.white, animationAlpha);
            GUI.Label(
                new Rect(textX, nameY, textWidth, smallViewport ? 20f : 24f),
                option?.Name ?? string.Empty,
                _upgradeCardMobileNameStyle);
            GUI.color = WithAlpha(accent, animationAlpha);
            GUI.Label(
                new Rect(textX, metaY, textWidth, 16f),
                UpgradeOptionLabel(option),
                _upgradeCardMobileMetaStyle);
            GUI.color = WithAlpha(Color.white, animationAlpha);
            GUI.Label(
                new Rect(textX, descriptionY, textWidth, Mathf.Max(24f, rect.yMax - descriptionY - cardPadding)),
                option?.Description ?? string.Empty,
                _upgradeCardMobileDescriptionStyle);
            GUI.color = WithAlpha(new Color(0.40f, 0.46f, 0.53f, 1f), animationAlpha);
            GUI.Label(
                new Rect(rect.xMax - 11f - 23f, rect.y + 10f, 23f, 16f),
                (index + 1).ToString(),
                _upgradeCardIndexStyle);

            DrawUpgradeRankPips(option, rect, accent, false, animationAlpha, cardPadding);
            GUI.matrix = cardMatrix;
            GUI.color = oldColor;
            return clicked;
        }

        private bool DrawDesktopUpgradeCard(UpgradeOptionDefinition option, int index, float height, float width)
        {
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.button,
                GUILayout.Height(height),
                GUILayout.Width(width));
            var oldColor = GUI.color;
            var accent = ParseColor(option?.Accent, new Color(0.4f, 0.9f, 1f, 1f));
            var evolution = option?.Kind == UpgradeOptionKind.Evolution;
            var entranceProgress = UpgradeCardEntranceProgressForRuntime(index);
            var animationAlpha = Mathf.Clamp01(entranceProgress);
            var cardMatrix = GUI.matrix;
            ApplyUpgradeCardTransform(rect, entranceProgress);
            GUI.color = WithAlpha(Color.white, animationAlpha);
            var clicked = GUI.Button(rect, GUIContent.none, UpgradeCardStyle(accent, evolution));
            DrawUpgradeCardAccent(rect, accent, evolution, animationAlpha);
            EnsureUpgradeCardStyles();

            var iconFrame = new Rect(rect.center.x - 28f, rect.y + 20f, 56f, 56f);
            var iconId = UpgradeOptionIconId(option);
            var iconTexture = UpgradeOptionIconTexture(iconId);
            var baseMatrix = GUI.matrix;
            if (evolution)
            {
                GUIUtility.RotateAroundPivot(-8f, iconFrame.center);
            }
            GUI.color = WithAlpha(Color.white, animationAlpha);
            GUI.Box(iconFrame, GUIContent.none, UpgradeIconStyle(accent, evolution));
            GUI.matrix = baseMatrix;

            var iconSize = 23f;
            var iconRect = new Rect(
                iconFrame.center.x - iconSize * 0.5f,
                iconFrame.center.y - iconSize * 0.5f,
                iconSize,
                iconSize);
            if (evolution)
            {
                GUIUtility.RotateAroundPivot(8f, iconFrame.center);
            }
            GUI.color = WithAlpha(accent, animationAlpha);
            if (iconTexture != null)
                GUI.DrawTextureWithTexCoords(iconRect, iconTexture, UpgradeOptionIconUv(iconId), true);
            else
                GUI.Label(iconRect, BuildChipGlyph(iconId), MenuSectionStyle());
            GUI.matrix = baseMatrix;

            var copyWidth = Mathf.Max(96f, rect.width - 32f);
            var copyX = rect.center.x - copyWidth * 0.5f;
            var metaY = iconFrame.yMax + UpgradeCardIconMarginBottom();
            var nameY = metaY + UpgradeCardMetaLineHeight() + UpgradeCardNameMarginTop();
            var descriptionY = nameY + UpgradeCardNameLineHeight() + UpgradeCardDescriptionMarginTop();
            GUI.color = WithAlpha(accent, animationAlpha);
            GUI.Label(
                new Rect(copyX, metaY, copyWidth, 18f),
                UpgradeOptionLabel(option),
                _upgradeCardMetaStyle);
            GUI.color = WithAlpha(Color.white, animationAlpha);
            GUI.Label(
                new Rect(copyX, nameY, copyWidth, 28f),
                option?.Name ?? string.Empty,
                _upgradeCardNameStyle);
            GUI.Label(
                new Rect(
                    copyX,
                    descriptionY,
                    copyWidth,
                    Mathf.Max(24f, rect.yMax - descriptionY - 32f)),
                option?.Description ?? string.Empty,
                _upgradeCardDescriptionStyle);
            GUI.color = WithAlpha(new Color(0.40f, 0.46f, 0.53f, 1f), animationAlpha);
            GUI.Label(
                new Rect(rect.xMax - 34f, rect.y + 9f, 23f, 16f),
                (index + 1).ToString(),
                _upgradeCardIndexStyle);
            DrawUpgradeRankPips(option, rect, accent, true, animationAlpha);
            GUI.matrix = cardMatrix;
            GUI.color = oldColor;
            return clicked;
        }

        private static void DrawUpgradeCardAccent(
            Rect rect,
            Color accent,
            bool evolution,
            float opacity = 1f)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            var oldColor = GUI.color;
            GUI.color = WithAlpha(accent, opacity);
            if (evolution)
            {
                GUI.DrawTexture(
                    new Rect(rect.x + rect.width * 0.08f, rect.y, rect.width * 0.84f, 3f),
                    Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), Texture2D.whiteTexture);
            }
            else
            {
                GUI.DrawTexture(
                    new Rect(rect.x + rect.width * 0.10f, rect.y, rect.width * 0.32f, 2f),
                    Texture2D.whiteTexture);
            }
            GUI.color = oldColor;
        }

        private static void DrawUpgradeRankPips(
            UpgradeOptionDefinition option,
            Rect cardRect,
            Color accent,
            bool centered = false,
            float opacity = 1f,
            float contentPadding = 20f)
        {
            if (option == null ||
                (option.Kind != UpgradeOptionKind.Weapon && option.Kind != UpgradeOptionKind.Support) ||
                option.MaxRank <= 0)
                return;

            var pipWidth = UpgradeRankPipWidth();
            var gap = 4f;
            var totalWidth = option.MaxRank * pipWidth + Mathf.Max(0, option.MaxRank - 1) * gap;
            var x = centered
                ? cardRect.center.x - totalWidth * 0.5f
                : (cardRect.x + contentPadding + 56f + 13f + cardRect.xMax - contentPadding) * 0.5f - totalWidth * 0.5f;
            var pipHeight = UpgradeRankPipHeight();
            var y = cardRect.yMax - contentPadding - pipHeight;
            var oldColor = GUI.color;
            for (var rank = 0; rank < option.MaxRank; rank++)
            {
                var filled = rank < option.CurrentRank;
                var next = rank == option.CurrentRank;
                var pipRect = new Rect(x + rank * (pipWidth + gap), y, pipWidth, pipHeight);
                if (next)
                {
                    var borderColor = WithAlpha(accent, opacity);
                    GUI.color = borderColor;
                    GUI.DrawTexture(new Rect(pipRect.x, pipRect.y, pipRect.width, 1f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(pipRect.x, pipRect.yMax - 1f, pipRect.width, 1f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(pipRect.x, pipRect.y, 1f, pipRect.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(pipRect.xMax - 1f, pipRect.y, 1f, pipRect.height), Texture2D.whiteTexture);
                }
                else
                {
                    GUI.color = WithAlpha(
                        filled ? accent : new Color(0.392f, 0.455f, 0.545f, 1f),
                        filled ? opacity : opacity * 0.28f);
                    GUI.DrawTexture(pipRect, Texture2D.whiteTexture);
                }
            }
            GUI.color = oldColor;
        }

        private void DrawRevivePrompt()
        {
            var oldColor = GUI.color;
            var safeArea = Screen.safeArea;
            var width = ReviveCardWidth(safeArea.width);
            var height = ReviveCardHeight(false);
            var x = safeArea.xMin + (safeArea.width - width) * 0.5f;
            var y = safeArea.yMin + (safeArea.height - height) * 0.5f;
            var overlayFade = CurrentOverlayFadeAlpha();
            var cardAlpha = CurrentOverlayCardAlpha();
            var cardOffset = CurrentOverlayCardOffset();
            GUI.color = new Color(0.008f, 0.020f, 0.059f, 0.59f * overlayFade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Translate(new Vector3(0f, cardOffset, 0f)) * GUI.matrix;
            GUI.color = WithAlpha(Color.white, cardAlpha);
            EnsureReviveStyles();
            DrawOverlayCardBackdropBlur(new Rect(x, y, width, height), cardAlpha);
            DrawOverlayCardShadow(new Rect(x, y, width, height), cardAlpha);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, _reviveCardStyle);
            GUILayout.BeginArea(new Rect(x + 25f, y + 25f, width - 50f, height - 50f));
            GUILayout.Label("INTEGRITY ZERO", _reviveKickerStyle, GUILayout.Height(ReviveKickerLineHeight()));
            GUILayout.Space(ReviveKickerToTitleGap());
            DrawReviveOverlayTitle("REVIVE?");
            GUILayout.Space(ReviveTitleToActionGap());
            if (DrawReviveActionButton(
                    _revivesRemaining > 1 ? $"Revive ({_revivesRemaining} left)" : "Revive",
                    "heart",
                    _revivePrimaryButtonStyle,
                    true))
                AcceptRevive();
            GUILayout.Space(ReviveActionGap());
            if (DrawReviveActionButton("End run", "skull", _reviveSecondaryButtonStyle, false))
                DeclineRevive();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private bool DrawReviveActionButton(
            string label,
            string iconId,
            GUIStyle style,
            bool prominent)
        {
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.button,
                GUILayout.Height(ReviveActionHeight()),
                GUILayout.ExpandWidth(true));
            var oldMatrix = GUI.matrix;
            if (ReviveActionButtonIsActive(rect))
                GUIUtility.ScaleAroundPivot(
                    new Vector2(ReviveActionActiveScale(), ReviveActionActiveScale()),
                    rect.center);
            var isHovered = PrimaryActionButtonIsHovered(rect);
            if (prominent) DrawPrimaryActionOuterShadow(rect, isHovered);
            var clicked = GUI.Button(rect, GUIContent.none, style);
            if (prominent) DrawPrimaryActionInsetShadow(rect, isHovered);
            var oldColor = GUI.color;
            var inheritedAlpha = oldColor.a;
            var iconSize = prominent ? 18f : 17f;
            var iconBoxSize = ReviveActionIconBoxSize(prominent, iconSize);
            var labelWidth = _reviveButtonLabelStyle.CalcSize(new GUIContent(label)).x;
            var groupWidth = iconBoxSize + 12f + labelWidth;
            var groupX = rect.center.x - groupWidth * 0.5f;
            var iconBoxRect = new Rect(
                groupX,
                rect.center.y - iconBoxSize * 0.5f,
                iconBoxSize,
                iconBoxSize);
            if (prominent)
            {
                GUI.Box(iconBoxRect, GUIContent.none, ResultActionPrimaryIconStyle());
            }
            var iconRect = prominent
                ? new Rect(iconBoxRect.x + 10f, iconBoxRect.y + 10f, iconSize, iconSize)
                : iconBoxRect;
            DrawControlIcon(
                iconRect,
                iconId,
                prominent
                    ? new Color(0.647f, 0.953f, 0.988f, 1f)
                    : new Color(219f / 255f, 229f / 255f, 238f / 255f, 1f));
            GUI.color = WithAlpha(
                prominent ? new Color(0.875f, 0.973f, 0.992f, 1f) : new Color(0.86f, 0.898f, 0.933f, 1f),
                inheritedAlpha);
            GUI.Label(
                new Rect(groupX + iconBoxSize + 12f, rect.y + 4f, Mathf.Max(20f, labelWidth + 4f), rect.height - 8f),
                label,
                _reviveButtonLabelStyle);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            return clicked;
        }

        private void DrawPausePrompt()
        {
            var oldColor = GUI.color;
            var safeArea = Screen.safeArea;
            var width = ReviveCardWidth(safeArea.width);
            var height = ReviveCardHeight(true);
            var x = safeArea.xMin + (safeArea.width - width) * 0.5f;
            var y = safeArea.yMin + (safeArea.height - height) * 0.5f;
            var overlayFade = CurrentOverlayFadeAlpha();
            var cardAlpha = CurrentOverlayCardAlpha();
            var cardOffset = CurrentOverlayCardOffset();
            GUI.color = new Color(0.008f, 0.020f, 0.059f, 0.59f * overlayFade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Translate(new Vector3(0f, cardOffset, 0f)) * GUI.matrix;
            GUI.color = WithAlpha(Color.white, cardAlpha);
            EnsureReviveStyles();
            DrawOverlayCardBackdropBlur(new Rect(x, y, width, height), cardAlpha);
            DrawOverlayCardShadow(new Rect(x, y, width, height), cardAlpha);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, _reviveCardStyle);
            GUILayout.BeginArea(new Rect(x + 25f, y + 25f, width - 50f, height - 50f));
            GUILayout.Label("RUN PAUSED", _reviveKickerStyle, GUILayout.Height(ReviveKickerLineHeight()));
            GUILayout.Space(ReviveKickerToTitleGap());
            DrawReviveOverlayTitle("PAUSED");
            GUILayout.Space(ReviveTitleToActionGap());
            if (DrawReviveActionButton("Resume", "play", _revivePrimaryButtonStyle, true))
            {
                _paused = false;
                RestartQualitySession();
                _audio?.Play(ProceduralAudio.Cue.Pause, 1.02f);
            }
            GUILayout.Space(ReviveActionGap());
            if (DrawReviveActionButton("Restart", "rotate-ccw", _reviveSecondaryButtonStyle, false))
                StartRun();
            GUILayout.Space(ReviveActionGap());
            if (DrawReviveActionButton("Main menu", "house", _reviveSecondaryButtonStyle, false))
                EnterMainMenu();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawReviveOverlayTitle(string text)
        {
            EnsureReviveStyles();
            var style = _reviveTitleStyle;
            var content = new GUIContent(text);
            var rect = GUILayoutUtility.GetRect(
                content,
                style,
                GUILayout.Height(ReviveTitleLineHeight()),
                GUILayout.ExpandWidth(true));
            if (Event.current != null && Event.current.type == EventType.Repaint)
                DrawReviveTitleGlow(rect, content.text);
            GUI.Label(rect, content, style);
        }

        private void DrawReviveTitleGlow(Rect rect, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = ReviveTitleGlowStyle();
            var oldTextColor = style.normal.textColor;
            const int sampleCount = 12;
            for (var ring = 0; ring < 2; ring++)
            {
                var radius = ReviveTitleShadowRadius(ring);
                var sampleAlpha = ReviveTitleShadowAlpha(ring) *
                    (ring == 0 ? 0.12f : 0.05f);
                style.normal.textColor = new Color(
                    34f / 255f,
                    211f / 255f,
                    238f / 255f,
                    sampleAlpha);
                for (var sample = 0; sample < sampleCount; sample++)
                {
                    var angle = sample * Mathf.PI * 2f / sampleCount;
                    var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    GUI.Label(
                        new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height),
                        text,
                        style);
                }
            }
            style.normal.textColor = oldTextColor;
        }

        private void DrawMainMenu()
        {
            var oldColor = GUI.color;
            GUI.color = WithAlpha(Color.white, CurrentMainMenuAlpha());
            var safeArea = Screen.safeArea;
            var desktopLayout = HomeMenuColumns(safeArea.width) >= 3;
            var landscapeMobileLayout = HomeMenuUsesLandscapeLayout(safeArea.width, safeArea.height);
            var width = desktopLayout
                ? Mathf.Min(620f, Mathf.Max(1f, safeArea.width - 48f))
                : landscapeMobileLayout
                ? Mathf.Min(780f, Mathf.Max(1f, safeArea.width * 0.94f))
                : Mathf.Min(620f, Mathf.Max(1f, safeArea.width * 0.92f));
            // React's desktop home stack is 18px top padding + 104px title
            // line + 29px action margin + 60px start button + 22px status
            // margin + 58px status + 11px grid margin + 67px cards + 18px
            // bottom padding. Keep this explicit so the IMGUI substitute
            // lands on the same CSS geometry instead of relying on a guessed
            // fixed area height.
            var height = desktopLayout
                ? 387f
                : landscapeMobileLayout
                ? 269f
                : 431f;
            var x = safeArea.xMin + (safeArea.width - width) * 0.5f;
            var y = desktopLayout
                ? safeArea.yMin + (safeArea.height - height) * 0.5f
                : landscapeMobileLayout
                ? safeArea.yMin + 10f
                : safeArea.yMin + (safeArea.height - height) * 0.5f - 7.5f;

            var contentRect = !desktopLayout
                ? landscapeMobileLayout
                ? new Rect(x + 20f, y + 16f, width - 40f, height - 32f)
                : new Rect(x + 22f, y + 22f, width - 44f, height - 44f)
                : new Rect(x, y, width, height);

            GUILayout.BeginArea(contentRect);
            if (desktopLayout) GUILayout.Space(18f);
            var titleFontSize = desktopLayout
                ? 104
                : HomeMenuTitleFontSize(safeArea.width, landscapeMobileLayout);
            var titleHeight = desktopLayout
                ? 104f
                : landscapeMobileLayout
                ? 41f
                : Mathf.Max(1f, titleFontSize * 0.94f);
            DrawHomeTitle(
                titleHeight,
                desktopLayout ? 1.10f : landscapeMobileLayout ? 1f : 0.93f,
                titleFontSize);
            GUILayout.Space(desktopLayout ? 29f : landscapeMobileLayout ? 12f : 29f);
            if (DrawHomeStartAction(landscapeMobileLayout ? 48f : 60f, Mathf.Min(330f, contentRect.width))) StartRun();
            GUILayout.Space(desktopLayout ? 22f : landscapeMobileLayout ? 8f : 22f);

            var stats = _saveData?.stats ?? new LifetimeStats();
            var bestScore = _saveData?.highScores != null && _saveData.highScores.Length > 0 && _saveData.highScores[0] != null
                ? _saveData.highScores[0].score
                : 0;
            var statusHeight = landscapeMobileLayout ? 46f : 58f;
            GUILayout.BeginHorizontal(
                HomeStatusStyleForHeight(statusHeight),
                GUILayout.Height(statusHeight),
                GUILayout.ExpandWidth(true));
            DrawHomeStatusMetric("BEST SCORE", bestScore.ToString("N0"), "trophy");
            DrawHomeStatusDivider();
            DrawHomeStatusMetric("PARTS", (_saveData?.parts ?? 0).ToString("N0"), "coins");
            DrawHomeStatusDivider();
            DrawHomeStatusMetric("RUNS", stats.totalRuns.ToString(), "skull");
            GUILayout.EndHorizontal();

            GUILayout.Space(desktopLayout ? 11f : landscapeMobileLayout ? 10f : 11f);
            var homeColumns = HomeMenuColumnsForLayout(safeArea.width, safeArea.height);
            var cardHeight = landscapeMobileLayout ? 72f : 67f;
            GUILayout.BeginHorizontal();
            if (DrawHomeMenuCard("Workshop", $"{_saveData?.parts ?? 0} Parts", "wrench", cardHeight))
            {
                _menuPage = MenuPage.Workshop;
                _menuScroll = Vector2.zero;
            }
            GUILayout.Space(10f);
            if (DrawHomeMenuCard("Records", $"{stats.totalRuns} runs", "trophy", cardHeight))
            {
                _menuPage = MenuPage.Records;
                _menuScroll = Vector2.zero;
            }
            if (homeColumns >= 3)
            {
                GUILayout.Space(10f);
                if (DrawHomeMenuCard("Settings", "Audio and display", "settings", cardHeight))
                {
                    _menuPage = MenuPage.Settings;
                    _menuScroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();
            if (homeColumns < 3)
            {
                GUILayout.Space(10f);
                if (DrawHomeMenuCard("Settings", "Audio and display", "settings", cardHeight))
                {
                    _menuPage = MenuPage.Settings;
                    _menuScroll = Vector2.zero;
                }
            }
            if (desktopLayout) GUILayout.Space(18f);
            GUILayout.EndArea();
            GUI.color = oldColor;
        }

        private bool DrawHomeStartAction(float height, float width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                HomeStartButtonStyle(),
                GUILayout.Width(width),
                GUILayout.Height(height));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var oldColor = GUI.color;
            var breathe = HomeStartBreathe(Time.unscaledTime);
            var isHovered = PrimaryActionButtonIsHovered(rect);
            DrawMenuStartOuterShadow(rect, isHovered, breathe);
            GUI.color = WithAlpha(Color.white, oldColor.a);
            var clicked = GUI.Button(rect, GUIContent.none, HomeStartButtonStyle());
            DrawMenuStartInsetShadow(rect, isHovered, breathe);
            DrawControlIcon(
                new Rect(rect.x + 91f, rect.center.y - 10f, 20f, 20f),
                "play",
                new Color(0.73f, 0.96f, 1f, 1f));
            GUI.Label(
                new Rect(rect.x + 54f, rect.y + 2f, rect.width - 42f, rect.height - 4f),
                "START RUN",
                HomeStartStyle());
            GUI.color = oldColor;
            return clicked;
        }

        private void DrawHomeTitle(float height, float horizontalScale, int fontSize)
        {
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                HomeTitleStyle(),
                GUILayout.ExpandWidth(true),
                GUILayout.Height(height));
            var style = new GUIStyle(HomeTitleStyle())
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                normal = { textColor = Color.white },
            };
            const string title = "VOIDFALL";
            var totalWidth = style.CalcSize(new GUIContent(title)).x;
            var cursor = rect.center.x - totalWidth * 0.5f;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Translate(
                    new Vector3(0f, HomeTitleDriftOffset(Time.unscaledTime), 0f)) * GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(horizontalScale, 1f), rect.center);
            for (var index = 0; index < title.Length; index++)
            {
                var glyph = title[index].ToString();
                var glyphWidth = style.CalcSize(new GUIContent(glyph)).x;
                var gradient = HomeTitleShimmerProgress(Time.unscaledTime, index, title.Length);
                var color = gradient < 0.45f
                    ? Color.Lerp(new Color(0.24f, 0.88f, 0.98f, 1f), Color.white, gradient / 0.45f)
                    : gradient < 0.75f
                    ? Color.Lerp(Color.white, new Color(0.70f, 0.50f, 0.98f, 1f), (gradient - 0.45f) / 0.30f)
                    : Color.Lerp(new Color(0.70f, 0.50f, 0.98f, 1f), new Color(0.30f, 0.72f, 0.98f, 1f), (gradient - 0.75f) / 0.25f);
                GUI.color = WithAlpha(color, oldColor.a);
                GUI.Label(new Rect(cursor, rect.y, glyphWidth + 2f, rect.height), glyph, style);
                cursor += glyphWidth;
            }
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        private void DrawHomeStatusMetric(string label, string value, string iconId)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            var iconRect = GUILayoutUtility.GetRect(
                14f,
                14f,
                GUILayout.Width(14f),
                GUILayout.Height(14f));
            DrawHomeIcon(iconRect, iconId, new Color(0.647f, 0.953f, 0.988f, 1f));
            GUILayout.Label(label, HomeMetricLabelStyle(), GUILayout.Height(14f));
            GUILayout.EndHorizontal();
            GUILayout.Label(value, HomeMetricValueStyle(), GUILayout.Height(21f));
            GUILayout.EndVertical();
        }

        private static void DrawHomeStatusDivider()
        {
            var rect = GUILayoutUtility.GetRect(
                1f,
                24f,
                GUILayout.Width(1f),
                GUILayout.Height(24f));
            var oldColor = GUI.color;
            GUI.color = WithAlpha(new Color(0.58f, 0.64f, 0.72f, 0.14f), oldColor.a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private bool DrawHomeMenuCard(string title, string detail, string iconId, float height)
        {
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                HomeCardButtonStyle(),
                GUILayout.Height(height),
                GUILayout.ExpandWidth(true));
            var clicked = GUI.Button(rect, GUIContent.none, HomeCardButtonStyle());
            var oldColor = GUI.color;
            DrawHomeIcon(
                new Rect(rect.x + 12f, rect.center.y - 9f, 18f, 18f),
                iconId,
                new Color(0.49f, 0.83f, 0.99f, 1f));
            GUI.color = WithAlpha(Color.white, oldColor.a);
            GUI.Label(
                new Rect(rect.x + 40f, rect.y + 6f, rect.width - 50f, 21f),
                title.ToUpperInvariant(),
                HomeCardTitleStyle());
            GUI.color = WithAlpha(Color.white, oldColor.a);
            GUI.Label(
                new Rect(rect.x + 40f, rect.y + 28f, rect.width - 50f, 18f),
                detail,
                HomeCardDetailStyle());
            GUI.color = oldColor;
            return clicked;
        }

        private static void DrawHomeIcon(Rect rect, string iconId, Color color)
        {
            var texture = HomeIconTexture();
            var oldColor = GUI.color;
            GUI.color = WithAlpha(color, color.a * oldColor.a);
            if (texture != null && HomeIconSlot(iconId) >= 0)
                GUI.DrawTextureWithTexCoords(rect, texture, HomeIconUv(iconId), true);
            GUI.color = oldColor;
        }

        private bool DrawIconTextButton(
            string label,
            string iconId,
            float height,
            bool homeAtlas = false,
            bool prominent = false)
        {
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.button,
                GUILayout.Height(height),
                GUILayout.ExpandWidth(true));
            var clicked = GUI.Button(rect, GUIContent.none, GUI.skin.button);
            var oldColor = GUI.color;
            var iconSize = prominent ? 21f : 16f;
            var iconX = rect.x + (prominent ? 16f : 12f);
            if (homeAtlas)
            {
                DrawHomeIcon(
                    new Rect(iconX, rect.center.y - iconSize * 0.5f, iconSize, iconSize),
                    iconId,
                    new Color(0.49f, 0.83f, 0.99f, 1f));
            }
            else
            {
                DrawControlIcon(
                    new Rect(iconX, rect.center.y - iconSize * 0.5f, iconSize, iconSize),
                    iconId,
                    new Color(0.8f, 0.94f, 0.98f, 1f));
            }
            GUI.color = WithAlpha(Color.white, oldColor.a);
            GUI.Label(
                new Rect(
                    rect.x + (prominent ? 48f : 38f),
                    rect.y + 4f,
                    Mathf.Max(20f, rect.width - (prominent ? 60f : 48f)),
                    Mathf.Max(18f, rect.height - 8f)),
                label,
                prominent ? MenuSectionStyle() : MenuBodyStyle());
            GUI.color = oldColor;
            return clicked;
        }

        private static void DrawControlIcon(Rect rect, string iconId, Color color)
        {
            var texture = ControlIconTexture();
            var oldColor = GUI.color;
            GUI.color = WithAlpha(color, color.a * oldColor.a);
            if (texture != null && ControlIconSlot(iconId) >= 0)
                GUI.DrawTextureWithTexCoords(rect, texture, ControlIconUv(iconId), true);
            GUI.color = oldColor;
        }

        private void DrawProfilePageHeader()
        {
            GUILayout.BeginHorizontal(
                ProfilePageHeaderStyle(),
                GUILayout.MinHeight(62f),
                GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(GUILayout.Width(82f));
            if (DrawIconTextButton("Back", "arrow-left", 40f))
            {
                _menuPage = MenuPage.Home;
                _menuScroll = Vector2.zero;
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(ProfilePageKicker(_menuPage).ToUpperInvariant(), ProfilePageKickerStyle());
            GUILayout.Label(ProfilePageTitle(_menuPage), ProfilePageTitleStyle());
            GUILayout.EndVertical();

            if (_menuPage == MenuPage.Workshop)
                DrawProfilePartsBalance();
            else
                GUILayout.Space(1f);
            GUILayout.EndHorizontal();

            var headerRect = GUILayoutUtility.GetLastRect();
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                var oldColor = GUI.color;
                GUI.color = new Color(0.58f, 0.64f, 0.72f, 0.15f * oldColor.a);
                GUI.DrawTexture(
                    new Rect(headerRect.x, headerRect.yMax - 1f, headerRect.width, 1f),
                    Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
        }

        private void DrawProfilePartsBalance()
        {
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                ProfilePartsBalanceStyle(),
                GUILayout.MinWidth(62f),
                GUILayout.Height(32f));
            GUI.Box(rect, GUIContent.none, ProfilePartsBalanceStyle());
            var icon = WorkshopCoinsTexture();
            var oldColor = GUI.color;
            GUI.color = new Color(0.99f, 0.80f, 0.13f, oldColor.a);
            if (icon != null)
            {
                GUI.DrawTexture(
                    new Rect(rect.x + 9f, rect.center.y - 7.5f, 15f, 15f),
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
            }
            GUI.color = new Color(0.99f, 0.90f, 0.54f, oldColor.a);
            GUI.Label(
                new Rect(rect.x + 29f, rect.y + 2f, Mathf.Max(22f, rect.width - 34f), rect.height - 4f),
                (_saveData?.parts ?? 0).ToString(),
                ProfilePartsBalanceTextStyle());
            GUI.color = oldColor;
        }

        private void DrawOverviewMenu()
        {
            var stats = _saveData?.stats ?? new LifetimeStats();
            GUILayout.Label("Run summary", MenuSectionStyle());
            GUILayout.Label(
                $"Time {FormatRunTime(Mathf.FloorToInt(_time))}   Level {_level}   Kills {_kills}   Bosses {_bossKills}   Score {CurrentScore()}\n" +
                $"Integrity {_playerHealth:0}/{_playerMaxHealth:0}   Parts earned {_partsEarned}\n" +
                $"Best score {stats.bestScore}   Best time {FormatRunTime(stats.bestTime)}   Highest level {stats.highestLevel}",
                MenuBodyStyle());
            if (!string.IsNullOrEmpty(_lastTelemetryPath))
                GUILayout.Label("Telemetry saved: " + _lastTelemetryPath, MenuBodyStyle());
            if (_time > 0 && DrawIconTextButton("Export run data", "download", 32f))
                ExportTelemetrySnapshot(_gameOver ? "gameover" : "active");

            GUILayout.Space(12f);
            GUILayout.Label(_gameOver ? "Final build" : "Current loadout", MenuSectionStyle());
            if (_upgradeProgress != null)
            {
                var ownedWeapons = 0;
                for (var index = 0; index < _upgradeProgress.WeaponRanks.Length; index++)
                    if (_upgradeProgress.WeaponRanks[index] > 0) ownedWeapons++;
                GUILayout.Label(
                    $"Weapons {ownedWeapons}/{UpgradeRules.WeaponSlotLimit(_upgradeProgress)}  \u2022  Supports and late systems below",
                    MenuBodyStyle());
                var weaponCount = Mathf.Min(ContentCatalog.Weapons.Length, _upgradeProgress.WeaponRanks.Length);
                for (var index = 0; index < weaponCount; index++)
                {
                    var rank = _upgradeProgress.WeaponRanks[index];
                    if (rank <= 0) continue;
                    var weapon = ContentCatalog.Weapons[index];
                    var isEvolved = index < _upgradeProgress.Evolved.Length && _upgradeProgress.Evolved[index];
                    var evolution = isEvolved ? "  \u2014 EVOLVED" : "";
                    GUILayout.Label($"{weapon.Name}  Rank {rank}/6{evolution}", MenuBodyStyle());
                }
                var supportCount = Mathf.Min(ContentCatalog.Supports.Length, _upgradeProgress.SupportRanks.Length);
                for (var index = 0; index < supportCount; index++)
                {
                    var rank = _upgradeProgress.SupportRanks[index];
                    if (rank <= 0) continue;
                    var support = ContentCatalog.Supports[index];
                    GUILayout.Label($"{support.Name}  Rank {rank}/{support.MaxRank}", MenuBodyStyle());
                }
                var lateCount = Mathf.Min(ContentCatalog.LateUpgrades.Length, _upgradeProgress.LateRanks.Length);
                for (var index = 0; index < lateCount; index++)
                {
                    var rank = _upgradeProgress.LateRanks[index];
                    if (rank <= 0) continue;
                    var late = ContentCatalog.LateUpgrades[index];
                    GUILayout.Label($"{late.Name}  Rank {rank}/{late.MaxRank}", MenuBodyStyle());
                }
            }

            if (_gameOver)
            {
                GUILayout.Space(12f);
                DrawDamageBreakdown();
            }

            GUILayout.Space(12f);
            GUILayout.Label("Controls", MenuSectionStyle());
            GUILayout.Label("WASD / arrows / left stick / touch joystick: move\nEsc or P: pause   Tab: open this menu   M: mute\nEnter / Space: resume or restart   1 / 2 / 3: choose a level-up option   Q: reroll once\nR: restart after game over", MenuBodyStyle());

            if (_gameOver && GUILayout.Button("New run", GUILayout.Height(34))) StartRun();
        }

        private void DrawWorkshopMenu()
        {
            GUILayout.Label("Focus an upgrade to preview its next visual rank.", MenuBodyStyle());
            GUILayout.Space(15f);
            if (_saveData?.workshop == null) return;

            var menuWidth = Mathf.Min(820f, Screen.safeArea.width - 40f);
            var contentWidth = Mathf.Max(1f, menuWidth - 48f);
            var columns = WorkshopMenuColumns(Screen.safeArea.width);
            if (columns > 1)
            {
                var gap = 14f;
                var previewWidth = Mathf.Clamp((contentWidth - gap) * 0.45f, 250f, 600f);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(previewWidth));
                DrawWorkshopPreview(previewWidth);
                GUILayout.EndVertical();
                GUILayout.Space(gap);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                DrawWorkshopRows();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else
            {
                DrawWorkshopPreview(Mathf.Clamp(contentWidth, 250f, 600f));
                GUILayout.Space(12f);
                DrawWorkshopRows();
            }
        }

        private void DrawWorkshopRows()
        {
            foreach (var id in WorkshopOrder)
            {
                var rank = WorkshopRank(id);
                var maxRank = id == "protocol" ? 1 : SaveStore.WorkshopMaxRank;
                var cost = WorkshopCost(id, rank);
                var previewing = _workshopPreviewId == id;
                GUILayout.BeginHorizontal(
                    WorkshopRowStyle(previewing),
                    GUILayout.MinHeight(60f),
                    GUILayout.ExpandWidth(true));
                GUILayout.BeginVertical(
                    WorkshopIconFrameStyle(),
                    GUILayout.Width(36f),
                    GUILayout.Height(36f));
                var iconRect = GUILayoutUtility.GetRect(36f, 36f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawWorkshopIcon(id, iconRect);
                GUILayout.EndVertical();
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                var controlName = WorkshopRowControlName(id);
                GUI.SetNextControlName(controlName);
                if (GUILayout.Button(
                        $"{WorkshopName(id)}  {rank}/{maxRank}",
                        WorkshopNameStyle()))
                    _workshopPreviewId = id;
                GUILayout.Label(WorkshopDescription(id), WorkshopDetailStyle());
                DrawWorkshopRankPips(rank, maxRank);
                GUILayout.EndVertical();
                if (cost < 0)
                {
                    GUILayout.Label("Complete", MenuBodyStyle(), GUILayout.Width(72f));
                }
                else if (DrawWorkshopPurchaseButton(cost, (_saveData?.parts ?? 0) >= cost))
                {
                    TryBuyWorkshop(id);
                }
                GUILayout.EndHorizontal();
                HandleWorkshopRowInteraction(id, controlName, GUILayoutUtility.GetLastRect());
            }
        }

        private static void DrawWorkshopFocusOutline(Rect rowRect)
        {
            var thickness = WorkshopFocusOutlineThickness();
            var offset = WorkshopFocusOutlineOffset();
            var outline = new Rect(
                rowRect.x - offset,
                rowRect.y - offset,
                rowRect.width + offset * 2f,
                rowRect.height + offset * 2f);
            var color = GUI.color;
            GUI.color = new Color(0.404f, 0.91f, 0.976f, 1f);
            GUI.DrawTexture(new Rect(outline.x, outline.y, outline.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.x, outline.yMax - thickness, outline.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.x, outline.y, thickness, outline.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.xMax - thickness, outline.y, thickness, outline.height), Texture2D.whiteTexture);
            GUI.color = color;
        }

        private static bool DrawWorkshopPurchaseButton(int cost, bool canAfford)
        {
            var oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && canAfford;
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.button,
                GUILayout.Width(72f),
                GUILayout.Height(38f));
            var clicked = GUI.Button(rect, GUIContent.none, GUI.skin.button);
            var oldColor = GUI.color;
            GUI.color = GUI.enabled
                ? new Color(0.98f, 0.82f, 0.35f, 1f)
                : new Color(0.42f, 0.48f, 0.56f, 1f);
            var icon = WorkshopCoinsTexture();
            if (icon != null)
            {
                GUI.DrawTexture(
                    new Rect(rect.x + 8f, rect.y + 11f, 13f, 13f),
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
            }
            GUI.color = GUI.enabled
                ? new Color(0.81f, 0.98f, 1f, 1f)
                : new Color(0.38f, 0.43f, 0.50f, 1f);
            GUI.Label(
                new Rect(rect.x + 25f, rect.y + 1f, rect.width - 29f, rect.height - 2f),
                cost.ToString(),
                MenuBodyStyleForButton());
            GUI.color = oldColor;
            GUI.enabled = oldEnabled;
            return clicked && canAfford;
        }

        private static void DrawWorkshopIcon(string workshopId, Rect rect)
        {
            var iconId = WorkshopIconId(workshopId);
            var texture = WorkshopIconTexture(iconId);
            var oldColor = GUI.color;
            GUI.color = new Color(0.49f, 0.83f, 0.99f, 1f);
            if (texture != null)
                GUI.DrawTextureWithTexCoords(rect, texture, WorkshopIconUv(iconId), true);
            else
                GUI.Label(rect, BuildChipGlyph(iconId), GUI.skin.label);
            GUI.color = oldColor;
        }

        private void DrawWorkshopRankPips(int rank, int maxRank)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(7f));
            for (var index = 0; index < maxRank; index++)
            {
                GUILayout.Label(
                    GUIContent.none,
                    index < rank ? WorkshopPipFilledStyle() : WorkshopPipEmptyStyle(),
                    GUILayout.Width(17f),
                    GUILayout.Height(3f));
                if (index + 1 < maxRank) GUILayout.Space(4f);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawWorkshopPreview(float previewWidth)
        {
            var selectedId = WorkshopPreviewSelection();
            var selectedRank = selectedId == null ? 0 : WorkshopRank(selectedId);
            var selectedMaxRank = selectedId == "protocol" ? 1 : SaveStore.WorkshopMaxRank;
            var previewRank = Mathf.Min(selectedMaxRank, selectedRank + 1);
            var integrity = PreviewWorkshopRank("integrity", selectedId);
            var power = PreviewWorkshopRank("power", selectedId);
            var mobility = PreviewWorkshopRank("mobility", selectedId);
            var recovery = PreviewWorkshopRank("recovery", selectedId);
            var magnet = PreviewWorkshopRank("magnet", selectedId);
            var precision = PreviewWorkshopRank("precision", selectedId);
            var arsenal = PreviewWorkshopRank("arsenal", selectedId);
            var protocol = PreviewWorkshopRank("protocol", selectedId);
            var description = selectedId != null && previewRank > selectedRank
                ? $"{WorkshopName(selectedId)} rank {previewRank} preview"
                : "Current configuration";

            GUILayout.BeginVertical(WorkshopPreviewPanelStyle(), GUILayout.Width(previewWidth));
            GUILayout.BeginVertical(WorkshopPreviewHeaderStyle(), GUILayout.Height(49f));
            GUILayout.Label("FRAME PREVIEW", WorkshopPreviewKickerStyle());
            GUILayout.Label(description, WorkshopPreviewTitleStyle());
            GUILayout.EndVertical();
            var previewHeight = previewWidth * (340f / 600f);
            var previewRect = GUILayoutUtility.GetRect(
                previewWidth,
                previewHeight,
                GUILayout.Width(previewWidth),
                GUILayout.Height(previewHeight));
            var oldColor = GUI.color;
            GUI.color = Color.white;
            DrawWorkshopPreviewAnimated(
                previewRect,
                integrity,
                power,
                mobility,
                recovery,
                magnet,
                precision,
                arsenal,
                protocol);
            GUI.color = oldColor;
            DrawWorkshopPreviewRanks(selectedId);
            GUILayout.EndVertical();
        }

        private void DrawWorkshopPreviewRanks(string selectedId)
        {
            GUILayout.BeginVertical(WorkshopPreviewRankStripStyle());
            var columns = WorkshopPreviewRankColumns(WorkshopOrder.Length);
            for (var start = 0; start < WorkshopOrder.Length; start += columns)
            {
                GUILayout.BeginHorizontal();
                for (var column = 0; column < columns; column++)
                {
                    var index = start + column;
                    if (index >= WorkshopOrder.Length) break;
                    var id = WorkshopOrder[index];
                    var active = selectedId == id;
                    GUILayout.BeginHorizontal(
                        WorkshopPreviewRankStyle(active),
                        GUILayout.MinHeight(31f),
                        GUILayout.ExpandWidth(true));
                    GUILayout.FlexibleSpace();
                    var iconRect = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f), GUILayout.Height(12f));
                    DrawWorkshopIcon(id, iconRect);
                    GUILayout.Space(4f);
                    GUILayout.Label(
                        PreviewWorkshopRank(id, selectedId).ToString(),
                        WorkshopPreviewRankTextStyle(active),
                        GUILayout.Width(14f));
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    if (column + 1 < columns && index + 1 < WorkshopOrder.Length)
                        GUILayout.Space(1f);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawWorkshopPreviewAnimated(
            Rect rect,
            int integrity,
            int power,
            int mobility,
            int recovery,
            int magnet,
            int precision,
            int arsenal,
            int protocol)
        {
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            var time = reducedMotion ? 0f : Time.unscaledTime;
            var drift = Mathf.Sin(time * 1.7f) * 4f;
            var contentSize = Mathf.Min(rect.height, rect.width * 0.5f);
            var contentRect = new Rect(
                rect.center.x - contentSize * 0.5f,
                rect.center.y - contentSize * 0.5f + drift,
                contentSize,
                contentSize);
            var sourceScale = WorkshopPreviewSourceScale(contentRect);

            DrawWorkshopPreviewTexture(ProceduralSpriteFactory.WorkshopPreviewWideBackdrop(), rect, 0, 1, 1);
            DrawWorkshopMobilityTrails(contentRect, mobility, time);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.WorkshopPreviewLayer("magnet", magnet),
                contentRect,
                time * 0.45f * Mathf.Rad2Deg,
                1,
                1);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.WorkshopPreviewLayer("integrity", integrity),
                contentRect,
                -time * 0.22f * Mathf.Rad2Deg,
                1,
                1);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.WorkshopPreviewLayer("recovery", recovery),
                contentRect,
                time * 0.7f * Mathf.Rad2Deg,
                1,
                1);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.WorkshopPreviewLayer("power", power),
                contentRect,
                0,
                1,
                1);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.WorkshopPreviewLayer("precision", precision),
                contentRect,
                time * 0.12f * Mathf.Rad2Deg,
                1,
                1);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.WorkshopPreviewLayer("arsenal", arsenal),
                contentRect,
                time * 1.1f * Mathf.Rad2Deg,
                1,
                1);
            var protocolPulse = 0.55f + 0.45f * Mathf.Sin(time * 2.4f);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.WorkshopPreviewLayer("protocol", protocol),
                contentRect,
                -time * 0.3f * Mathf.Rad2Deg,
                protocol > 0 ? protocolPulse : 1,
                1);

            // Match the browser draw order: the rotating ring and source-sized
            // operative sprite sit above every upgrade layer, then the Power
            // pulse is painted over the operative core.
            var ringSize = 86f * sourceScale;
            var ringRect = new Rect(
                contentRect.center.x - ringSize * 0.5f,
                contentRect.center.y - ringSize * 0.5f,
                ringSize,
                ringSize);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.PlayerRing(),
                ringRect,
                time * 0.3f * Mathf.Rad2Deg,
                1,
                1);
            var operativeSize = 94f * sourceScale;
            var operativeRect = new Rect(
                contentRect.center.x - operativeSize * 0.5f,
                contentRect.center.y - operativeSize * 0.5f,
                operativeSize,
                operativeSize);
            DrawWorkshopPreviewTexture(
                ProceduralSpriteFactory.Operative(),
                operativeRect,
                0,
                1,
                1);
            if (power > 0)
            {
                var pulseSize = WorkshopPowerPulseSize(power);
                DrawWorkshopPreviewTintedTexture(
                    ProceduralSpriteFactory.Dot(),
                    new Rect(
                        contentRect.center.x - pulseSize * sourceScale * 0.5f,
                        contentRect.center.y - pulseSize * sourceScale * 0.5f,
                        pulseSize * sourceScale,
                        pulseSize * sourceScale),
                    new Color(1f, 0.93f, 0.84f, 0.35f + power * 0.12f));
            }
        }

        private static void DrawWorkshopMobilityTrails(Rect contentRect, int mobility, float time)
        {
            if (mobility <= 0) return;
            var sprite = ProceduralSpriteFactory.WorkshopPreviewMobilityTrail();
            var scale = contentRect.width / 300f;
            for (var index = 0; index < mobility; index++)
            {
                var offset = (index - (mobility - 1) * 0.5f) * 18f * scale;
                var length = WorkshopMobilityTrailLength(mobility, index, time) * scale;
                var start = new Vector2(
                    contentRect.center.x + offset,
                    contentRect.center.y + 27f * scale);
                var end = new Vector2(
                    contentRect.center.x + offset * 1.14f,
                    contentRect.center.y + (28f + length / scale) * scale);
                DrawWorkshopTrailTexture(sprite, start, end, 4f * scale);
            }
        }

        private static void DrawWorkshopTrailTexture(Sprite sprite, Vector2 start, Vector2 end, float width)
        {
            if (sprite == null) return;
            var delta = end - start;
            var length = delta.magnitude;
            if (length <= 0.001f) return;
            var centre = (start + end) * 0.5f;
            var rect = new Rect(centre.x - width * 0.5f, centre.y - length * 0.5f, width, length);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f, centre);
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.StretchToFill, true);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private static void DrawWorkshopPreviewTexture(
            Sprite sprite,
            Rect rect,
            float rotationDegrees,
            float alpha,
            float verticalScale)
        {
            if (sprite == null || alpha <= 0) return;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(alpha));
            if (Mathf.Abs(rotationDegrees) > 0.001f)
                GUIUtility.RotateAroundPivot(rotationDegrees, rect.center);
            if (Mathf.Abs(verticalScale - 1) > 0.001f)
                GUIUtility.ScaleAroundPivot(new Vector2(1, verticalScale), rect.center);
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private static void DrawWorkshopPreviewTintedTexture(Sprite sprite, Rect rect, Color tint)
        {
            if (sprite == null || tint.a <= 0) return;
            var oldColor = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
            GUI.color = oldColor;
        }

        private void DrawRecordsMenu()
        {
            var stats = _saveData?.stats ?? new LifetimeStats();

            var columns = RecordMenuColumns(Screen.safeArea.width);
            var metrics = new[]
            {
                new RecordMetric("Runs", stats.totalRuns.ToString()),
                new RecordMetric("Kills", stats.totalKills.ToString()),
                new RecordMetric("Best score", FormatProfileNumber(stats.bestScore)),
                new RecordMetric("Longest run", FormatRunTime(stats.bestTime)),
                new RecordMetric("Bosses", stats.totalBossKills.ToString()),
                new RecordMetric("Elites", stats.totalEliteKills.ToString()),
                new RecordMetric("Total time", FormatRunTime(stats.totalPlaySeconds)),
                new RecordMetric("Parts earned", FormatProfileNumber(stats.totalPartsEarned)),
                new RecordMetric("Best kills", stats.bestKills.ToString()),
                new RecordMetric("Best level", stats.highestLevel.ToString()),
                new RecordMetric("Damage dealt", FormatProfileNumber(stats.totalDamageDealt)),
                new RecordMetric("Damage taken", FormatProfileNumber(stats.totalDamageTaken)),
            };
            var metricRowWidth = RecordMetricGridWidth(Screen.safeArea.width);
            var metricGap = 8f;
            var metricWidth = Mathf.Max(
                1f,
                (metricRowWidth - metricGap * (columns - 1)) / columns);
            for (var index = 0; index < metrics.Length; index += columns)
            {
                if (index > 0) GUILayout.Space(BrowserMetricGridGap());
                // React's lifetime grid fills the panel before dividing each
                // narrow row into two equal columns.
                GUILayout.BeginHorizontal(
                    GUILayout.Width(RecordMetricGridWidth(Screen.safeArea.width)));
                for (var column = 0; column < columns; column++)
                {
                    var metricIndex = index + column;
                    if (column > 0)
                        GUILayout.Space(metricGap);
                    if (metricIndex < metrics.Length)
                        DrawRecordMetric(metrics[metricIndex], metricWidth);
                    else
                        GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12f);
            GUILayout.Label("High scores", MenuSectionStyle());
            if (_saveData?.highScores == null || _saveData.highScores.Length == 0)
            {
                GUILayout.Label("No runs recorded yet.", MenuBodyStyle());
            }
            else
            {
                GUILayout.BeginVertical(RecordTableWrapStyle(), GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal(RecordTableHeaderStyle());
                DrawRecordTableCell("#", 38f, true, false);
                DrawRecordTableCell("Score", 130f, true, false);
                DrawRecordTableCell("Kills", 82f, true, false);
                DrawRecordTableCell("Time", 82f, true, false);
                DrawRecordTableCell("Bosses", 82f, true, false);
                GUILayout.EndHorizontal();
                var scoreCount = Mathf.Min(8, _saveData.highScores.Length);
                for (var index = 0; index < scoreCount; index++)
                {
                    var score = _saveData.highScores[index];
                    if (score == null) continue;
                    GUILayout.BeginHorizontal(RecordTableRowStyle());
                    DrawRecordTableCell((index + 1).ToString(), 38f, false, false);
                    DrawRecordTableCell(FormatProfileNumber(score.score), 130f, false, true);
                    DrawRecordTableCell(score.kills.ToString(), 82f, false, false);
                    DrawRecordTableCell(FormatRunTime(score.time), 82f, false, false);
                    DrawRecordTableCell(score.bossKills.ToString(), 82f, false, false);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
        }

        private void DrawRecordMetric(RecordMetric metric, float width)
        {
            GUILayout.BeginVertical(
                RecordMetricBoxStyle(),
                GUILayout.Width(width),
                GUILayout.Height(BrowserMetricMinHeight()),
                GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.Label(metric.Label.ToUpperInvariant(), RecordMetricLabelStyle());
            GUILayout.Space(BrowserMetricContentGap());
            GUILayout.Label(metric.Value, RecordMetricValueStyle());
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void DrawRecordTableCell(string value, float width, bool header, bool score)
        {
            GUILayout.Label(
                header ? value.ToUpperInvariant() : value,
                header
                    ? RecordTableHeaderTextStyle()
                    : score
                    ? RecordTableScoreStyle()
                    : RecordTableCellStyle(),
                GUILayout.Width(width),
                GUILayout.ExpandHeight(true));
        }

        private void DrawSettingsMenu()
        {
            if (_saveData?.settings == null) return;
            var settings = _saveData.settings;
            GUILayout.Space(14f);
            DrawSettingSlider("Master volume", settings.masterVolume, value => settings.masterVolume = value);
            DrawSettingSlider("Effects volume", settings.effectsVolume, value => settings.effectsVolume = value);
            DrawSettingSlider("Music volume", settings.musicVolume, value => settings.musicVolume = value);
            DrawSettingSlider("Screen shake", settings.shake, value => settings.shake = value);
            DrawSettingSlider(
                "Touch control size",
                settings.touchSize,
                value => settings.touchSize = value,
                0.75f,
                1.35f,
                QuantizeTouchSize);
            DrawSettingQualityRow(settings);
            DrawSettingToggleRow(
                "Reduced motion",
                "Cuts shake, flashes, and particle volume.",
                settings.reducedMotion,
                value => settings.reducedMotion = value);
            DrawSettingToggleRow(
                "High-contrast shots",
                "Adds a white edge to player projectiles.",
                settings.highContrast,
                value => settings.highContrast = value);

            GUILayout.Space(12f);
            var muted = _audio != null && _audio.Muted;
            if (DrawIconTextButton(muted ? "Unmute audio  [M]" : "Mute audio  [M]", muted ? "volume-x" : "volume-2", 32f))
                ToggleMute();

            GUILayout.Space(12f);
            if (GUILayout.Button("Export browser-compatible save", GUILayout.Height(32)))
            {
                ExportBrowserSave();
            }
            GUILayout.Label(
                "Exports the current profile as the browser v5 object-map format.",
                MenuBodyStyle());

            GUILayout.Space(12f);
            GUILayout.Label("Import browser save", MenuSectionStyle());
            GUILayout.Label(
                "Paste a browser v5 JSON export below. Import replaces this Unity profile and saves immediately.",
                MenuBodyStyle());
            _browserSaveImportText = GUILayout.TextArea(
                _browserSaveImportText ?? string.Empty,
                1_000_000,
                GUILayout.MinHeight(128f),
                GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Import browser-compatible save", GUILayout.Height(32f)))
                ImportBrowserSave();

            GUILayout.Space(12f);
            GUILayout.Label("Local progress", MenuSectionStyle());
            GUILayout.Label(
                "This resets Parts, workshop ranks, records, discoveries, and saved preferences.",
                MenuBodyStyle());
            var resetLabel = _resetProgressArmed
                ? "Tap again to reset all local progress"
                : "Reset local progress";
            if (GUILayout.Button(resetLabel, GUILayout.Height(32)))
            {
                if (!_resetProgressArmed)
                {
                    _resetProgressArmed = true;
                    _resetProgressTimer = 5f;
                    SetMenuNotice("Tap reset again within 5 seconds to confirm.");
                }
                else
                {
                    ResetLocalProgress();
                }
            }
        }

        private void DrawSettingSlider(
            string label,
            float value,
            Action<float> setter,
            float min = 0f,
            float max = 1f,
            Func<float, float> quantizer = null)
        {
            var stacked = SettingsUsesStackedLayout(Screen.safeArea.width);
            if (stacked)
            {
                GUILayout.BeginVertical(
                    SettingsRowStyle(),
                    GUILayout.MinHeight(58f),
                    GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.BeginHorizontal(
                    SettingsRowStyle(),
                    GUILayout.MinHeight(58f),
                    GUILayout.ExpandWidth(true));
            }

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(label, SettingsLabelStyle());
            GUILayout.Label($"{Mathf.RoundToInt(value * 100)}%", SettingsDetailStyle());
            GUILayout.EndVertical();
            if (stacked) GUILayout.Space(8f);
            var sliderWidth = SettingsControlWidth(Screen.safeArea.width);
            var sliderRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.horizontalSlider,
                GUILayout.Width(sliderWidth),
                GUILayout.Height(SettingsSliderHeight()));
            var next = GUI.HorizontalSlider(
                sliderRect,
                value,
                min,
                max,
                GUIStyle.none,
                GUIStyle.none);
            DrawSettingsSliderVisual(sliderRect, next, min, max);
            if (stacked) GUILayout.EndVertical();
            else GUILayout.EndHorizontal();
            var quantized = quantizer != null ? quantizer(next) : QuantizeUnitSetting(next);
            if (Mathf.Abs(quantized - value) > 0.001f)
            {
                _settingsDirtyPrevious = CloneSettings(_saveData?.settings);
                setter(quantized);
                ApplySettings();
                _settingsDirty = true;
                _settingsDirtyTimer = 0.5f;
            }
        }

        private void DrawSettingToggleRow(
            string label,
            string detail,
            bool value,
            Action<bool> setter)
        {
            var stacked = SettingsUsesStackedLayout(Screen.safeArea.width);
            if (stacked)
            {
                GUILayout.BeginVertical(
                    SettingsRowStyle(),
                    GUILayout.MinHeight(58f),
                    GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.BeginHorizontal(
                    SettingsRowStyle(),
                    GUILayout.MinHeight(58f),
                    GUILayout.ExpandWidth(true));
            }

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(label, SettingsLabelStyle());
            GUILayout.Label(detail, SettingsDetailStyle());
            GUILayout.EndVertical();
            if (stacked) GUILayout.Space(8f);
            var toggleRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.button,
                GUILayout.Width(SettingsToggleWidth()),
                GUILayout.Height(SettingsToggleHeight()));
            var clicked = GUI.Button(toggleRect, GUIContent.none, GUIStyle.none);
            var oldColor = GUI.color;
            GUI.color = WithAlpha(Color.white, oldColor.a);
            GUI.DrawTexture(
                toggleRect,
                SettingsToggleTrackTexture(value),
                ScaleMode.StretchToFill,
                true);
            var knobX = toggleRect.x + SettingsToggleKnobInset() +
                (value ? SettingsToggleKnobOnOffset() : 0f);
            GUI.DrawTexture(
                new Rect(
                    knobX,
                    toggleRect.y + SettingsToggleKnobInset(),
                    SettingsToggleKnobSize(),
                    SettingsToggleKnobSize()),
                SettingsToggleKnobTexture(value),
                ScaleMode.StretchToFill,
                true);
            GUI.color = oldColor;
            if (stacked) GUILayout.EndVertical();
            else GUILayout.EndHorizontal();

            var rowRect = GUILayoutUtility.GetLastRect();
            var rowClicked = !clicked && SettingsToggleRowWasClicked(rowRect, Event.current);
            if (clicked || rowClicked)
            {
                var previousSettings = CloneSettings(_saveData?.settings);
                setter(!value);
                ApplyAndCommitSettings(previousSettings);
                if (rowClicked) Event.current.Use();
            }
        }

        private void DrawSettingQualityRow(SaveSettings settings)
        {
            var stacked = SettingsUsesStackedLayout(Screen.safeArea.width);
            if (stacked)
            {
                GUILayout.BeginVertical(
                    SettingsRowStyle(),
                    GUILayout.MinHeight(58f),
                    GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.BeginHorizontal(
                    SettingsRowStyle(),
                    GUILayout.MinHeight(58f),
                    GUILayout.ExpandWidth(true));
            }

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("Graphics quality", SettingsLabelStyle());
            GUILayout.Label("Auto lowers cosmetic load before gameplay accuracy.", SettingsDetailStyle());
            GUILayout.EndVertical();
            if (stacked) GUILayout.Space(8f);
            var selectWidth = SettingsControlWidth(Screen.safeArea.width);
            GUILayout.BeginVertical(GUILayout.Width(selectWidth));
            HandleSettingsQualityKeyboard(settings);
            var selectRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                SettingsSelectStyle(),
                GUILayout.Width(selectWidth),
                GUILayout.Height(SettingsSelectHeight()));
            GUI.SetNextControlName("VoidFall.SettingsQualitySelect");
            if (GUI.Button(selectRect, GUIContent.none, SettingsSelectStyle()))
                _settingsQualityMenuOpen = !_settingsQualityMenuOpen;
            GUI.Label(
                new Rect(
                    selectRect.x + 10f,
                    selectRect.y,
                    selectRect.width - 34f,
                    selectRect.height),
                SettingQualityOptionLabel(settings.quality),
                SettingsSelectValueStyle());
            GUI.Label(
                new Rect(selectRect.xMax - 24f, selectRect.y, 18f, selectRect.height),
                "v",
                SettingsSelectArrowStyle());

            if (_settingsQualityMenuOpen)
            {
                GUILayout.BeginVertical(
                    SettingsSelectPopupStyle(),
                    GUILayout.Width(selectWidth));
                foreach (var quality in SettingsQualityOptions)
                {
                    var label = SettingQualityOptionLabel(quality);
                    if (GUILayout.Button(label, SettingsSelectOptionStyle(), GUILayout.Height(SettingsSelectOptionHeight())))
                    {
                        if (settings.quality != quality)
                        {
                            var previousSettings = CloneSettings(settings);
                            settings.quality = quality;
                            ApplyAndCommitSettings(previousSettings);
                        }
                        _settingsQualityMenuOpen = false;
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();
            if (stacked) GUILayout.EndVertical();
            else GUILayout.EndHorizontal();

            var qualityRowRect = GUILayoutUtility.GetLastRect();
            var currentEvent = Event.current;
            if (SettingsQualityLabelWasClicked(qualityRowRect, selectRect, currentEvent))
            {
                GUI.FocusControl("VoidFall.SettingsQualitySelect");
                currentEvent.Use();
            }
        }

        private static void DrawSettingsSliderVisual(
            Rect rect,
            float value,
            float min,
            float max)
        {
            var track = SettingsSliderTrackTexture();
            var fill = SettingsSliderFillTexture();
            var thumb = SettingsSliderThumbTexture();
            var normalized = Mathf.InverseLerp(min, max, value);
            var trackHeight = 6f;
            var trackRect = new Rect(
                rect.x,
                rect.center.y - trackHeight * 0.5f,
                rect.width,
                trackHeight);
            var fillRect = new Rect(
                trackRect.x,
                trackRect.y,
                trackRect.width * normalized,
                trackRect.height);
            var thumbSize = 14f;
            var thumbRect = new Rect(
                Mathf.Lerp(trackRect.x, trackRect.xMax, normalized) - thumbSize * 0.5f,
                rect.center.y - thumbSize * 0.5f,
                thumbSize,
                thumbSize);
            GUI.DrawTexture(trackRect, track, ScaleMode.StretchToFill, true);
            if (fillRect.width > 0.01f)
                GUI.DrawTexture(fillRect, fill, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(thumbRect, thumb, ScaleMode.StretchToFill, true);
        }

        private void DrawHomeBackdrop()
        {
            var oldColor = GUI.color;
            var plate = _arenaPlateSprites[(int)ArenaId.Void];
            var texture = plate != null ? plate.texture : HomeBackdropTexture();
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(
                new Rect(0f, 0f, Screen.width, Screen.height),
                texture,
                new Rect(0f, 1f, 1f, -1f),
                true);

            if (!_visualCaptureNoGrid)
            {
                GUI.color = new Color(117f / 255f, 133f / 255f, 160f / 255f, 0.065f);
                var centreX = Screen.width * 0.5f;
                var centreY = Screen.height * 0.5f;
                for (var x = centreX; x <= Screen.width; x += ArenaGridSpacing)
                {
                    GUI.DrawTexture(new Rect(x, 0f, 1f, Screen.height), Texture2D.whiteTexture);
                    if (x > 0f)
                        GUI.DrawTexture(new Rect(centreX - (x - centreX), 0f, 1f, Screen.height), Texture2D.whiteTexture);
                }
                for (var y = centreY; y <= Screen.height; y += ArenaGridSpacing)
                {
                    GUI.DrawTexture(new Rect(0f, y, Screen.width, 1f), Texture2D.whiteTexture);
                    if (y > 0f)
                        GUI.DrawTexture(new Rect(0f, centreY - (y - centreY), Screen.width, 1f), Texture2D.whiteTexture);
                }
            }

            GUI.color = oldColor;
        }

        // The fullscreen approximation of the browser's `.overlay {
        // backdrop-filter: blur(4px) }` was removed with the render-target path
        // it sampled from: it could only read the world through a RenderTexture,
        // and once the world renders straight to the backbuffer there is nothing
        // to sample. Every former call site already draws an explicit fullscreen
        // dim immediately afterwards, so the overlays are unchanged on screen.
        // Reinstating a true blur needs a render pipeline with post-processing,
        // not eight offset copies of the frame.

        private void DrawOverlayCardBackdropBlur(Rect cardRect, float alpha)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint ||
                cardRect.width <= 0f || cardRect.height <= 0f)
            {
                return;
            }

            var oldColor = GUI.color;
            GUI.color = new Color(0.02f, 0.04f, 0.08f, Mathf.Clamp01(alpha) * 0.94f);
            GUI.DrawTexture(cardRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
        }

        private static void DrawOverlayCardShadow(Rect cardRect, float alpha)
        {
            var width = Mathf.Max(1, Mathf.RoundToInt(cardRect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(cardRect.height));
            var texture = OverlayCardShadowTexture(width, height);
            var margin = OverlayCardShadowTextureMargin();
            GUI.color = WithAlpha(Color.white, Mathf.Clamp01(alpha));
            GUI.DrawTexture(
                new Rect(
                    cardRect.x - margin,
                    cardRect.y - margin,
                    texture.width,
                    texture.height),
                texture,
                ScaleMode.StretchToFill,
                true);
        }

        private static float SourceProjectileSpriteWorldSize(string kind)
        {
            return ProceduralSpriteFactory.ProjectileCanvasSize(kind);
        }

        private void RenderEliteTelegraphs()
        {
            var definition = ContentCatalog.Elite;
            for (var index = 0; index < _enemies.Length; index++)
            {
                var enemy = _enemies[index];
                if (!enemy.Active || !enemy.Elite)
                {
                    Hide(_eliteMarkViews[index]);
                    Hide(_eliteChargeLaneViews[index]);
                    Hide(_eliteChargeArrowViews[index]);
                    Hide(_eliteChargeFillRenderers[index]);
                    Hide(_eliteChargeArrowFillRenderers[index]);
                    continue;
                }

                var mark = EnsureEliteMarkView(index);
                var eliteVariant = enemy.EliteKind.HasValue;
                mark.sprite = eliteVariant
                    ? ProceduralSpriteFactory.EliteMark()
                    : ProceduralSpriteFactory.EliteRing();
                mark.transform.position = enemy.Position;
                mark.transform.localScale = Vector3.one * SourceEliteRingSize(_ambientClock);
                mark.color = new Color(
                    1f,
                    1f,
                    1f,
                    SourceEliteRingAlpha(eliteVariant));
                mark.enabled = true;

                if (enemy.EliteKind.HasValue || enemy.State != 1)
                {
                    Hide(_eliteChargeLaneViews[index]);
                    Hide(_eliteChargeArrowViews[index]);
                    Hide(_eliteChargeFillRenderers[index]);
                    Hide(_eliteChargeArrowFillRenderers[index]);
                    continue;
                }

                var direction = SourceVisualDirection(enemy.DashDirection);
                var normal = new Vector2(-direction.y, direction.x);
                var progress = Mathf.Clamp01(
                    1f - enemy.StateTimer / (float)definition.ChargeTelegraphSeconds);
                var reach = (float)definition.ChargeSpeed * (float)definition.ChargeDurationSeconds;
                var color = ParseColor(definition.Color, Color.yellow);
                color.a = 0.09f + progress * 0.15f;
                // Browser source fills lane and arrow polygons. The old
                // LineRenderer outline added a stroke absent in source.
                Hide(_eliteChargeLaneViews[index]);
                Hide(_eliteChargeArrowViews[index]);
                var lane = EnsureEliteChargeFillView(index);
                SetTelegraphMesh(
                    lane,
                    _eliteChargeFillRenderers[index],
                    _eliteChargeFillBuffers[index],
                    TelegraphPoint(enemy.Position, direction, normal, enemy.Radius * 0.5f, -enemy.Radius * 0.6f),
                    TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach, -13f),
                    TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach, 13f),
                    TelegraphPoint(enemy.Position, direction, normal, enemy.Radius * 0.5f, enemy.Radius * 0.6f),
                    color,
                    false);

                var arrow = EnsureEliteChargeArrowFillView(index);
                var arrowTip = TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach + 16f, 0);
                var arrowLeft = TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach - 4f, -10f);
                var arrowRight = TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach - 4f, 10f);
                color.a = 0.28f + progress * 0.4f;
                SetTelegraphMesh(
                    arrow,
                    _eliteChargeArrowFillRenderers[index],
                    _eliteChargeArrowFillBuffers[index],
                    arrowTip,
                    arrowLeft,
                    arrowRight,
                    arrowRight,
                    color,
                    true);
            }
        }

        private void RenderEnemyTelegraphs()
        {
            for (var index = 0; index < _enemies.Length; index++)
            {
                Hide(_enemyTelegraphRingViews[index]);
                Hide(_enemyTelegraphLineViews[index]);
                Hide(_enemyTelegraphSecondaryLineViews[index]);
                Hide(_enemyTelegraphTertiaryLineViews[index]);
                Hide(_enemyHarvesterCapacityRingViews[index]);
                Hide(_enemyTelegraphSiegeDashRenderers[index]);
                Hide(_enemyTelegraphMortarFillViews[index]);
                Hide(_enemyTelegraphExploderFillViews[index]);
                for (var segment = 0; segment < ExploderTelegraphSegmentCount; segment++)
                    Hide(_enemyTelegraphExploderSegmentViews[index * ExploderTelegraphSegmentCount + segment]);
                for (var segment = 0; segment < MortarTelegraphSegmentCount; segment++)
                    Hide(_enemyTelegraphMortarSegmentViews[index * MortarTelegraphSegmentCount + segment]);
                Hide(_enemyTelegraphFillRenderers[index]);
                Hide(_enemyTelegraphArrowFillRenderers[index]);
                var enemy = _enemies[index];
                if (!enemy.Active) continue;

                if (enemy.Id == "mortar" && enemy.State == 1)
                {
                    var siege = enemy.EliteKind.HasValue && enemy.EliteKind.Value == EliteVariantId.Mortar;
                    var definition = FindEnemy("mortar");
                    var stats = siege ? EliteRules.EliteVariantStatsFor(EliteVariantId.Mortar) : default(EliteVariantStats);
                    var telegraph = siege ? (float)stats.TelegraphSeconds : (float)(definition?.TelegraphSeconds ?? 1.15);
                    var radius = siege ? (float)stats.BlastRadius : (float)(definition?.BlastRadius ?? 82);
                    var progress = Mathf.Clamp01(1f - enemy.StateTimer / Mathf.Max(0.01f, telegraph));
                    var pulse = 0.5f + 0.5f * Mathf.Sin(_ambientClock * (9f + progress * 20f));
                    var drift = siege ? (float)EliteRules.SiegeMortarDrift(enemy.StateTimer) : 0;
                    var target = enemy.DashDirection;
                    if (siege)
                    {
                        var ring = EnsureEnemyTelegraphRingView(index);
                        // Browser source keeps the uncertainty ring centred on
                        // the locked aim point while its radius drifts. Once
                        // the impact locks, it becomes a steady confirmation
                        // ring at aim + radius + 6.
                        if (drift > 0.5f)
                        {
                            Hide(ring);
                            SetDashedArcMesh(
                                EnsureEnemyTelegraphSiegeDashView(index),
                                _enemyTelegraphSiegeDashRenderers[index],
                                _enemyTelegraphSiegeDashVertices[index],
                                _enemyTelegraphSiegeDashTriangles[index],
                                _enemyTelegraphSiegeDashColors[index],
                                enemy.AimPosition,
                                radius + drift,
                                1.4f,
                                SiegeMortarDashOnLength,
                                SiegeMortarDashOffLength,
                                new Color(251f / 255f, 191f / 255f, 36f / 255f, 0.2f + progress * 0.16f));
                        }
                        else
                        {
                            Hide(_enemyTelegraphSiegeDashRenderers[index]);
                            SetArcLine(
                                ring,
                                enemy.AimPosition,
                                radius + 6f,
                                0,
                                Mathf.PI * 2f,
                                1.6f,
                                new Color(1f, 251f / 255f, 235f / 255f, 0.6f));
                        }
                    }
                    else
                    {
                        Hide(_enemyTelegraphRingViews[index]);
                    }

                    var fill = EnsureEnemyTelegraphMortarFillView(index);
                    fill.transform.position = target;
                    fill.transform.localScale = Vector3.one * (radius * 2f);
                    fill.color = new Color(
                        249f / 255f,
                        115f / 255f,
                        22f / 255f,
                        0.025f + progress * 0.045f);
                    fill.enabled = true;

                    var whitePulse = pulse > 0.6f;
                    var arcColor = whitePulse
                        ? new Color(1f, 247f / 255f, 237f / 255f, 0.42f + progress * 0.25f)
                        : new Color(251f / 255f, 146f / 255f, 60f / 255f, 0.46f + progress * 0.3f);
                    var arcWidth = 1.5f + progress * 1.1f;
                    for (var segment = 0; segment < MortarTelegraphSegmentCount; segment++)
                    {
                        var start = segment * (Mathf.PI * 2f / MortarTelegraphSegmentCount) + 0.1f;
                        SetArcLine(
                            EnsureEnemyTelegraphMortarSegmentView(index, segment),
                            target,
                            radius,
                            start,
                            start + Mathf.PI * 2f / 9f,
                            arcWidth,
                            arcColor);
                    }

                    var marker = 2.5f + progress * 2.5f;
                    var markerColor = whitePulse
                        ? new Color(1f, 247f / 255f, 237f / 255f, 0.48f + progress * 0.25f)
                        : new Color(251f / 255f, 146f / 255f, 60f / 255f, 0.5f + progress * 0.25f);
                    var markerHorizontal = EnsureEnemyTelegraphLineView(index);
                    markerHorizontal.positionCount = 2;
                    markerHorizontal.SetPosition(0, target + Vector2.left * marker);
                    markerHorizontal.SetPosition(1, target + Vector2.right * marker);
                    markerHorizontal.startColor = markerColor;
                    markerHorizontal.endColor = markerColor;
                    markerHorizontal.startWidth = 2f;
                    markerHorizontal.endWidth = 2f;
                    markerHorizontal.enabled = true;
                    var markerVertical = EnsureEnemyTelegraphSecondaryLineView(index);
                    markerVertical.positionCount = 2;
                    markerVertical.SetPosition(0, target + Vector2.down * marker);
                    markerVertical.SetPosition(1, target + Vector2.up * marker);
                    markerVertical.startColor = markerColor;
                    markerVertical.endColor = markerColor;
                    markerVertical.startWidth = 2f;
                    markerVertical.endWidth = 2f;
                    markerVertical.enabled = true;
                    continue;
                }

                if (enemy.Id == "exploder")
                {
                    var elite = enemy.EliteKind.HasValue && enemy.EliteKind.Value == EliteVariantId.Exploder;
                    var definition = FindEnemy("exploder");
                    var stats = elite ? EliteRules.EliteVariantStatsFor(EliteVariantId.Exploder) : default(EliteVariantStats);
                    var telegraph = elite
                        ? (float)stats.TelegraphSeconds
                        : (float)(definition?.TelegraphSeconds ?? 0.9) + (enemy.Roster == EnemyRoster.Two ? 0.28f : 0);
                    var armed = enemy.State == 1;
                    var progress = armed ? Mathf.Clamp01(1f - enemy.StateTimer / Mathf.Max(0.01f, telegraph)) : 0;
                    var pulseRate = armed
                        ? elite
                            ? (float)EliteRules.EliteExploderFlashRate(enemy.StateTimer, telegraph)
                            : 7f + progress * 20f
                        : 5f;
                    var pulse = 0.5f + 0.5f * Mathf.Sin(_ambientClock * pulseRate);
                    var hardFlash = armed && pulse > 0.58f - progress * 0.22f;
                    var radius = armed ? (elite ? (float)stats.BlastRadius : (float)(definition?.BlastRadius ?? 76)) : enemy.Radius + 8f;
                    var ring = EnsureEnemyTelegraphRingView(index);
                    if (elite)
                    {
                        // Browser draws the Elite Exploder floor ring before
                        // the body, separate from the armed warning arcs.
                        SetArcLine(
                            ring,
                            enemy.Position,
                            enemy.Radius + 12f + pulse * (armed ? 7f : 3f),
                            0,
                            Mathf.PI * 2f,
                            armed ? 2.5f : 1.6f,
                            new Color(251f / 255f, 146f / 255f, 60f / 255f, 0.3f + pulse * 0.4f));
                    }
                    else if (!armed)
                    {
                        SetArcLine(
                            ring,
                            enemy.Position,
                            radius,
                            0,
                            Mathf.PI * 2f,
                            1.5f,
                            new Color(245f / 255f, 158f / 255f, 11f / 255f, 0.24f + pulse * 0.18f));
                    }
                    else
                    {
                        Hide(ring);
                    }

                    var fill = EnsureEnemyTelegraphExploderFillView(index);
                    fill.transform.position = enemy.Position;
                    fill.transform.localScale = Vector3.one * (radius * 2f);
                    if (armed)
                    {
                        fill.color = hardFlash
                            ? new Color(1f, 247f / 255f, 237f / 255f, 0.075f + progress * 0.12f)
                            : new Color(239f / 255f, 68f / 255f, 68f / 255f, 0.06f + progress * 0.12f);
                    }
                    else
                    {
                        fill.color = new Color(
                            245f / 255f,
                            158f / 255f,
                            11f / 255f,
                            0.025f + pulse * 0.02f);
                    }
                    fill.enabled = true;

                    if (armed)
                    {
                        var segmentColor = hardFlash
                            ? new Color(1f, 247f / 255f, 237f / 255f, 0.74f + progress * 0.24f)
                            : new Color(251f / 255f, 146f / 255f, 60f / 255f, 0.62f + progress * 0.34f);
                        var segmentWidth = 3f + progress * 2f + pulse;
                        for (var segment = 0; segment < ExploderTelegraphSegmentCount; segment++)
                        {
                            var start = -Mathf.PI * 0.5f +
                                segment * (Mathf.PI * 2f / ExploderTelegraphSegmentCount) +
                                0.09f;
                            var length = segment == 4 ? 0.48f : 0.67f;
                            SetArcLine(
                                EnsureEnemyTelegraphExploderSegmentView(index, segment),
                                enemy.Position,
                                radius,
                                start,
                                start + length,
                                segmentWidth,
                                segmentColor);
                        }

                        var inner = EnsureEnemyTelegraphLineView(index);
                        SetArcLine(
                            inner,
                            enemy.Position,
                            radius * (0.86f - progress * 0.46f + pulse * 0.04f),
                            0,
                            Mathf.PI * 2f,
                            1.8f + progress + pulse * 0.8f,
                            hardFlash
                                ? new Color(251f / 255f, 146f / 255f, 60f / 255f, 0.64f + progress * 0.3f)
                                : new Color(254f / 255f, 202f / 255f, 202f / 255f, 0.42f + progress * 0.48f));
                    }
                    continue;
                }

                if ((enemy.Id == "dasher" || (enemy.Roster == EnemyRoster.Two && enemy.Id == "chaser")) && enemy.State == 1)
                {
                    var pincer = enemy.Roster == EnemyRoster.Two && enemy.Id == "chaser";
                    var direction = SourceVisualDirection(enemy.DashDirection);
                    var normal = new Vector2(-direction.y, direction.x);
                    var reach = pincer ? 465f * 0.34f : 570f * 0.38f;
                    var progress = Mathf.Clamp01(1f - enemy.StateTimer / (pincer ? 0.52f : 0.72f));
                    var color = pincer
                        ? new Color(251f / 255f, 113f / 255f, 133f / 255f, 0.09f + progress * 0.15f)
                        : new Color(232f / 255f, 121f / 255f, 249f / 255f, 0.09f + progress * 0.15f);
                    Hide(_enemyTelegraphLineViews[index]);
                    var lane = EnsureEnemyTelegraphFillView(index);
                    SetTelegraphMesh(
                        lane,
                        _enemyTelegraphFillRenderers[index],
                        _enemyTelegraphFillBuffers[index],
                        TelegraphPoint(enemy.Position, direction, normal, enemy.Radius * 0.5f, -enemy.Radius * 0.6f),
                        TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach, -13f),
                        TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach, 13f),
                        TelegraphPoint(enemy.Position, direction, normal, enemy.Radius * 0.5f, enemy.Radius * 0.6f),
                        color,
                        false);

                    var arrow = EnsureEnemyTelegraphArrowFillView(index);
                    var arrowTip = TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach + 16f, 0);
                    var arrowLeft = TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach - 4f, -10f);
                    var arrowRight = TelegraphPoint(enemy.Position, direction, normal, enemy.Radius + reach - 4f, 10f);
                    color.a = 0.28f + progress * 0.4f;
                    SetTelegraphMesh(
                        arrow,
                        _enemyTelegraphArrowFillRenderers[index],
                        _enemyTelegraphArrowFillBuffers[index],
                        arrowTip,
                        arrowLeft,
                        arrowRight,
                        arrowRight,
                        color,
                        true);
                    continue;
                }

                if (enemy.Id == "twinGunner" && enemy.State == 1)
                {
                    var direction = SourceVisualDirection(enemy.DashDirection);
                    var normal = new Vector2(-direction.y, direction.x);
                    var line = EnsureEnemyTelegraphLineView(index);
                    var secondary = EnsureEnemyTelegraphSecondaryLineView(index);
                    // Browser source draws two separate strokes:
                    // (12, +/-7) -> (390, +/-42). A connected three-point
                    // polyline adds a diagonal that does not exist in source.
                    line.positionCount = 2;
                    line.SetPosition(0, enemy.Position + direction * 12f - normal * 7f);
                    line.SetPosition(1, enemy.Position + direction * 390f - normal * 42f);
                    secondary.positionCount = 2;
                    secondary.SetPosition(0, enemy.Position + direction * 12f + normal * 7f);
                    secondary.SetPosition(1, enemy.Position + direction * 390f + normal * 42f);
                    var progress = Mathf.Clamp01(1f - enemy.StateTimer / 0.68f);
                    var color = new Color(251f / 255f, 113f / 255f, 133f / 255f, 0.18f + progress * 0.42f);
                    line.startColor = color;
                    line.endColor = color;
                    line.startWidth = 1.5f + progress;
                    line.endWidth = line.startWidth;
                    secondary.startColor = color;
                    secondary.endColor = color;
                    secondary.startWidth = line.startWidth;
                    secondary.endWidth = line.startWidth;
                    line.enabled = true;
                    secondary.enabled = true;
                    continue;
                }

                if (enemy.Roster == EnemyRoster.Two && enemy.Id == "gunner" && enemy.State == 1)
                {
                    var direction = SourceVisualDirection(enemy.DashDirection);
                    var angle = Mathf.Atan2(direction.y, direction.x);
                    var line = EnsureEnemyTelegraphLineView(index);
                    var secondary = EnsureEnemyTelegraphSecondaryLineView(index);
                    var tertiary = EnsureEnemyTelegraphTertiaryLineView(index);
                    line.positionCount = 2;
                    line.SetPosition(0, enemy.Position + direction * enemy.Radius);
                    line.SetPosition(1, enemy.Position + new Vector2(Mathf.Cos(angle - 0.23f), Mathf.Sin(angle - 0.23f)) * 360f);
                    secondary.positionCount = 2;
                    secondary.SetPosition(0, enemy.Position + direction * enemy.Radius);
                    secondary.SetPosition(1, enemy.Position + direction * 360f);
                    tertiary.positionCount = 2;
                    tertiary.SetPosition(0, enemy.Position + direction * enemy.Radius);
                    tertiary.SetPosition(1, enemy.Position + new Vector2(Mathf.Cos(angle + 0.2f), Mathf.Sin(angle + 0.2f)) * 360f);
                    var progress = Mathf.Clamp01(1f - enemy.StateTimer / 0.78f);
                    var color = new Color(245f / 255f, 158f / 255f, 11f / 255f, 0.16f + progress * 0.4f);
                    line.startColor = color;
                    line.endColor = color;
                    line.startWidth = 1.2f + progress;
                    line.endWidth = line.startWidth;
                    secondary.startColor = color;
                    secondary.endColor = color;
                    secondary.startWidth = line.startWidth;
                    secondary.endWidth = line.startWidth;
                    tertiary.startColor = color;
                    tertiary.endColor = color;
                    tertiary.startWidth = line.startWidth;
                    tertiary.endWidth = line.startWidth;
                    line.enabled = true;
                    secondary.enabled = true;
                    tertiary.enabled = true;
                    continue;
                }

                if (enemy.Id == "harvester")
                {
                    var limits = PickupRules.HarvesterXpLimits(_xpNeed);
                    if (enemy.StoredXp >= limits.Individual)
                    {
                        var pulse = 0.5f + 0.5f * Mathf.Sin(_ambientClock * 7f + enemy.Seed);
                        var ring = EnsureEnemyHarvesterCapacityRingView(index);
                        var ringColor = pulse > 0.58f
                            ? new Color(1f, 1f, 1f, 0.42f + pulse * 0.32f)
                            : new Color(52f / 255f, 211f / 255f, 153f / 255f, 0.42f + pulse * 0.32f);
                        SetArcLine(
                            ring,
                            enemy.Position,
                            enemy.Radius + 7f + pulse * 2f,
                            0,
                            Mathf.PI * 2f,
                            2f + pulse,
                            ringColor);
                    }
                }
            }
        }

        private void RenderEnemyStatus()
        {
            for (var index = 0; index < _enemies.Length; index++)
            {
                Hide(_enemyHealthArcViews[index]);
                Hide(_enemyShieldArcViews[index]);
                Hide(_enemyHealthBackgroundViews[index]);
                Hide(_enemyHealthFillViews[index]);

                var enemy = _enemies[index];
                if (!enemy.Active) continue;

                if (enemy.EliteKind.HasValue && enemy.Health < enemy.MaxHealth)
                {
                    var healthArcRatio = Mathf.Clamp01(enemy.Health / Mathf.Max(1f, enemy.MaxHealth));
                    SetArcLine(
                        EnsureEnemyHealthArcView(index),
                        enemy.Position,
                        enemy.Radius + 9f,
                        -Mathf.PI * 0.5f,
                        -Mathf.PI * 0.5f + Mathf.PI * 2f * healthArcRatio,
                        2.5f,
                        new Color(250f / 255f, 204f / 255f, 21f / 255f, 0.9f));
                }

                if (enemy.MaxShield > 0 && enemy.Shield > 0)
                {
                    var shieldArcRatio = Mathf.Clamp01(enemy.Shield / Mathf.Max(1f, enemy.MaxShield));
                    SetArcLine(
                        EnsureEnemyShieldArcView(index),
                        enemy.Position,
                        enemy.Radius + 5f,
                        0,
                        Mathf.PI * 2f * shieldArcRatio,
                        3f,
                        new Color(96f / 255f, 165f / 255f, 250f / 255f, 0.85f));
                }

                var heavy = enemy.Elite || enemy.Id == "brute" || enemy.Id == "bulwark" ||
                    enemy.Id == "carrier" || enemy.Id == "harvester";
                if (!heavy || enemy.Health >= enemy.MaxHealth) continue;

                var width = Mathf.Max(18f, enemy.Radius * 2.2f);
                var height = 4f;
                var barY = enemy.Position.y - enemy.Radius - 12f;
                var background = EnsureEnemyHealthBackgroundView(index);
                background.transform.position = new Vector3(enemy.Position.x, barY, 0);
                background.transform.localScale = new Vector3(width, height, 1);
                background.color = new Color(2f / 255f, 6f / 255f, 18f / 255f, 0.75f);
                background.enabled = true;

                var healthBarRatio = Mathf.Clamp01(enemy.Health / Mathf.Max(1f, enemy.MaxHealth));
                var fillWidth = Mathf.Max(0.001f, width * healthBarRatio);
                var fill = EnsureEnemyHealthFillView(index);
                fill.transform.position = new Vector3(
                    enemy.Position.x - width * 0.5f + fillWidth * 0.5f,
                    barY,
                    0);
                fill.transform.localScale = new Vector3(fillWidth, height, 1);
                fill.color = enemy.Elite
                    ? new Color(250f / 255f, 204f / 255f, 21f / 255f, 1)
                    : new Color(251f / 255f, 146f / 255f, 60f / 255f, 1);
                fill.enabled = true;
            }
        }

        private void RenderSourceFxOrder()
        {
            EnsureSourceFxOrderEntries();
            for (var index = 0; index < _sourceParticles.Length; index++)
                if (!_sourceParticles[index].Active) Hide(_sourceParticleViews[index]);
            for (var index = 0; index < _meteorShards.Length; index++)
                if (!_meteorShards[index].Active) Hide(_meteorShardViews[index]);
            for (var index = 0; index < _ringWaves.Length; index++)
                if (!_ringWaves[index].Active)
                {
                    Hide(_ringWaveViews[index]);
                    Hide(_ringWaveGlowViews[index]);
                    Hide(_ringWaveSpriteViews[index]);
                }

            for (var order = 0; order < _sourceFxOrderCount; order++)
            {
                var kind = (SourceFxKind)_sourceFxOrderKind[order];
                var slot = _sourceFxOrderSlot[order];
                switch (kind)
                {
                    case SourceFxKind.Particle:
                        RenderSourceParticleSlot(slot, order);
                        break;
                    case SourceFxKind.MeteorShard:
                        RenderMeteorShardSlot(slot, order);
                        break;
                    case SourceFxKind.RingWave:
                        RenderRingWaveSlot(slot, order);
                        break;
                }
            }
        }

        private void RenderSourceParticleSlot(int index, int order)
        {
            if (index < 0 || index >= _sourceParticles.Length || !_sourceParticles[index].Active)
            {
                if (index >= 0 && index < _sourceParticleViews.Length) Hide(_sourceParticleViews[index]);
                return;
            }
            var particle = _sourceParticles[index];
            var progress = Mathf.Clamp01(particle.Life / particle.MaxLife);
            var view = EnsureSourceParticleView(index);
            view.rendererPriority = order;
            view.transform.position = particle.Position;
            view.transform.localScale = Vector3.one *
                (24f * particle.Size * (0.4f + progress * 0.6f));
            view.color = new Color(
                particle.Color.r,
                particle.Color.g,
                particle.Color.b,
                particle.Color.a * progress);
            view.enabled = true;
        }

        private void RenderMeteorShardSlot(int index, int order)
        {
            if (index < 0 || index >= _meteorShards.Length || !_meteorShards[index].Active)
            {
                if (index >= 0 && index < _meteorShardViews.Length) Hide(_meteorShardViews[index]);
                return;
            }
            var shard = _meteorShards[index];
            var alpha = Mathf.Clamp01(shard.Life / Mathf.Max(0.001f, shard.MaxLife));
            var view = EnsureMeteorShardView(index);
            view.rendererPriority = order;
            view.transform.position = shard.Position;
            view.transform.rotation = Quaternion.Euler(0, 0, shard.Rotation * Mathf.Rad2Deg);
            view.transform.localScale = Vector3.one *
                SourceMeteorShardWorldSize(shard.Size, alpha);
            view.color = new Color(1f, 1f, 1f, alpha);
            view.enabled = true;
        }

        private void RenderMeteorShards()
        {
            for (var index = 0; index < _meteorShards.Length; index++)
                RenderMeteorShardSlot(index, 0);
        }

        private void RenderImpactMarks()
        {
            EnsureImpactMarkOrderEntries();
            for (var index = 0; index < _impactMarks.Length; index++)
            {
                var mark = _impactMarks[index];
                if (!mark.Active)
                {
                    Hide(_impactMarkViews[index]);
                    for (var segment = 0; segment < ImpactHeatSegmentCount; segment++)
                        Hide(_impactHeatViews[ImpactHeatSlot(index, segment)]);
                }
            }
            for (var order = 0; order < _impactMarkOrderCount; order++)
            {
                var index = _impactMarkOrder[order];
                if (index < 0 || index >= _impactMarks.Length || !_impactMarks[index].Active)
                    continue;
                var mark = _impactMarks[index];
                var fade = Mathf.Clamp01(1f - mark.Age / mark.Life);
                var heat = Mathf.Clamp01(1f - mark.Age / 0.8f);
                var view = EnsureImpactMarkView(index);
                view.rendererPriority = order;
                view.transform.position = mark.Position;
                view.transform.rotation = Quaternion.Euler(0, 0, mark.Rotation * Mathf.Rad2Deg);
                view.transform.localScale = Vector3.one * (mark.Radius * 2f);
                view.color = new Color(1f, 1f, 1f, fade * 0.72f);
                view.enabled = true;
                if (heat <= 0.001f)
                {
                    for (var segment = 0; segment < ImpactHeatSegmentCount; segment++)
                        Hide(_impactHeatViews[ImpactHeatSlot(index, segment)]);
                    continue;
                }
                for (var segment = 0; segment < ImpactHeatSegmentCount; segment++)
                {
                    var heatView = EnsureImpactHeatView(index, segment);
                    heatView.rendererPriority = order;
                    var start = segment * (Mathf.PI * 2f / ImpactHeatSegmentCount) + 0.13f + mark.Rotation;
                    SetArcLine(
                        heatView,
                        mark.Position,
                        mark.Radius * 0.66f,
                        start,
                        start + Mathf.PI * 2f / 9f,
                        2f,
                        new Color(251f / 255f, 146f / 255f, 60f / 255f, heat * 0.65f));
                }
            }
        }

        private void RenderBlastWaves()
        {
            EnsureBlastWaveOrderEntries();
            for (var index = 0; index < _blastWaves.Length; index++)
            {
                var wave = _blastWaves[index];
                if (!wave.Active)
                {
                    Hide(_blastWaveFillViews[index]);
                    Hide(_blastWaveRimViews[index]);
                    Hide(_blastWaveArcViews[index]);
                    continue;
                }
            }
            for (var order = 0; order < _blastWaveOrderCount; order++)
            {
                var index = _blastWaveOrder[order];
                if (index < 0 || index >= _blastWaves.Length) continue;
                var wave = _blastWaves[index];
                if (!wave.Active) continue;

                var progress = Mathf.Clamp01(wave.Age / wave.Life);
                var eased = 1f - Mathf.Pow(1f - progress, 3f);
                var radius = wave.MaxRadius * eased;
                var fade = 1f - progress;
                var fill = wave.Bomb
                    ? new Color(245f / 255f, 158f / 255f, 11f / 255f, fade * 0.1f)
                    : new Color(239f / 255f, 68f / 255f, 68f / 255f, fade * 0.17f);
                var fillView = _blastWaveFillViews[index];
                fillView.rendererPriority = order;
                fillView.transform.position = wave.Position;
                fillView.transform.localScale = Vector3.one * (radius * 2f);
                fillView.color = fill;
                fillView.enabled = true;

                var rimColor = wave.Bomb
                    ? new Color(255f / 255f, 237f / 255f, 213f / 255f, fade * 0.9f)
                    : new Color(254f / 255f, 215f / 255f, 170f / 255f, fade * 0.82f);
                SetArcLine(
                    _blastWaveRimViews[index],
                    wave.Position,
                    radius,
                    0,
                    Mathf.PI * 2f,
                    wave.Bomb ? 8f - progress * 5f : 6f - progress * 3f,
                    rimColor);
                _blastWaveRimViews[index].rendererPriority = order;

                var arcColor = wave.Bomb
                    ? new Color(251f / 255f, 146f / 255f, 60f / 255f, fade * 0.55f)
                    : new Color(239f / 255f, 68f / 255f, 68f / 255f, fade * 0.62f);
                SetArcLine(
                    _blastWaveArcViews[index],
                    wave.Position,
                    radius * (wave.Bomb ? 0.72f : 0.62f),
                    -0.18f * Mathf.PI,
                    1.34f * Mathf.PI,
                    wave.Bomb ? 3f : 2.5f,
                    arcColor);
                _blastWaveArcViews[index].rendererPriority = order;
            }
        }

        private void RenderRingWaves()
        {
            for (var index = 0; index < _ringWaves.Length; index++)
                RenderRingWaveSlot(index, 0);
        }

        private void RenderRingWaveSlot(int index, int order)
        {
            if (index < 0 || index >= _ringWaves.Length || !_ringWaves[index].Active)
            {
                if (index >= 0 && index < _ringWaveViews.Length)
                {
                    Hide(_ringWaveViews[index]);
                    Hide(_ringWaveGlowViews[index]);
                    Hide(_ringWaveSpriteViews[index]);
                }
                return;
            }

            var wave = _ringWaves[index];
            var progress = Mathf.Clamp01(wave.Age / wave.Life);
            var fade = 1f - progress;
            // Keep the legacy line-renderer shadow populated for diagnostics
            // and reflection fixtures, but deactivate its GameObject so it
            // cannot double-render the source sprite below.
            var sourceSize = wave.Size;
            var radius = sourceSize * (52f / 64f);
            var coreShadow = EnsureRingWaveView(index);
            coreShadow.rendererPriority = order;
            SetArcLine(
                coreShadow,
                wave.Position,
                radius,
                0,
                Mathf.PI * 2f,
                sourceSize * (7f / 64f),
                new Color(1f, 1f, 1f, 0.9f * fade));
            var glowShadow = EnsureRingWaveGlowView(index);
            glowShadow.rendererPriority = order;
            SetArcLine(
                glowShadow,
                wave.Position,
                radius,
                0,
                Mathf.PI * 2f,
                sourceSize * (16f / 64f),
                new Color(1f, 1f, 1f, 0.22f * fade));
            coreShadow.gameObject.SetActive(false);
            glowShadow.gameObject.SetActive(false);

            // Browser ringWave() draws the cached 128 px sprite at
            // particle.size * 2 and applies globalAlpha only once. The
            // texture already contains the source 7 px/16 px stroke pair.
            var view = EnsureRingWaveSpriteView(index);
            view.rendererPriority = order;
            view.transform.position = wave.Position;
            view.transform.localScale = Vector3.one * (wave.Size * 2f);
            view.color = new Color(1f, 1f, 1f, fade);
            view.enabled = true;
        }

        private static void SetDashedArcMesh(
            MeshFilter view,
            MeshRenderer renderer,
            List<Vector3> vertices,
            List<int> triangles,
            List<Color> colors,
            Vector2 centre,
            float radius,
            float width,
            float dashOn,
            float dashOff,
            Color color)
        {
            if (view == null || renderer == null || radius <= 0.001f || dashOn <= 0f || dashOff < 0f)
                return;

            vertices.Clear();
            triangles.Clear();
            colors.Clear();
            var circumference = Mathf.PI * 2f * radius;
            var distance = 0f;
            var drawing = true;
            while (distance < circumference - 0.0001f)
            {
                var length = drawing ? dashOn : dashOff;
                var next = Mathf.Min(circumference, distance + length);
                if (drawing && next > distance + 0.0001f)
                {
                    AddArcBand(
                        vertices,
                        triangles,
                        colors,
                        centre,
                        radius,
                        width,
                        distance / radius,
                        next / radius,
                        color);
                }

                distance = next;
                drawing = !drawing;
            }

            var mesh = view.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();
            renderer.enabled = vertices.Count > 0;
        }

        private void RenderBossTelegraphs()
        {
            for (var index = 0; index < _bosses.Length; index++)
            {
                Hide(_bossTelegraphFillRenderers[index]);
                Hide(_bossTelegraphOutlineViews[index]);
                Hide(_bossShieldFillViews[index]);
            }

            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _bossOrderCount; bossOrder++)
            {
                var index = _bossOrder[bossOrder];
                var boss = _bosses[index];
                if (!boss.Active) continue;

                var attack = boss.ActiveAttack;
                if (attack != null && boss.State == 1)
                {
                    var progress = Mathf.Clamp01(
                        1f - boss.StateTimer / Mathf.Max(0.01f, (float)attack.TelegraphSeconds));
                    var vertices = _bossTelegraphVertices[index];
                    var triangles = _bossTelegraphTriangles[index];
                    var colors = _bossTelegraphColors[index];
                    vertices.Clear();
                    triangles.Clear();
                    colors.Clear();

                    if (attack.Id == "charge")
                    {
                        var direction = SourceVisualDirection(boss.DashDirection);
                        var normal = new Vector2(-direction.y, direction.x);
                        var start = boss.Position + direction * 18f;
                        var end = boss.Position + direction * 135f;
                        var startHalfWidth = boss.Radius * 0.6f;
                        var color = new Color(0.973f, 0.443f, 0.443f, 0.12f + progress * 0.18f);
                        AddQuad(
                            vertices,
                            triangles,
                            colors,
                            start - normal * startHalfWidth,
                            end - normal * 18f,
                            end + normal * 18f,
                            start + normal * startHalfWidth,
                            color);
                        SetBossTelegraphMesh(index);

                        var outline = EnsureBossTelegraphOutlineView(index);
                        outline.positionCount = 5;
                        outline.SetPosition(0, start - normal * startHalfWidth);
                        outline.SetPosition(1, end - normal * 18f);
                        outline.SetPosition(2, end + normal * 18f);
                        outline.SetPosition(3, start + normal * startHalfWidth);
                        outline.SetPosition(4, start - normal * startHalfWidth);
                        outline.startColor = outline.endColor = new Color(1f, 0.45f, 0.45f, 0.22f + progress * 0.24f);
                        outline.startWidth = outline.endWidth = 1.2f + progress * 0.8f;
                        outline.enabled = true;
                    }
                    else if (attack.Id == "blink")
                    {
                        var radius = (float)(attack.Radius ?? 80) * (0.72f + progress * 0.28f);
                        AddFan(
                            vertices,
                            triangles,
                            colors,
                            boss.TargetPosition,
                            radius,
                            0,
                            Mathf.PI * 2f,
                            32,
                            new Color(0.376f, 0.647f, 0.98f, 0.1f + progress * 0.22f));
                        SetBossTelegraphMesh(index);
                        SetArcLine(
                            EnsureBossTelegraphOutlineView(index),
                            boss.TargetPosition,
                            radius,
                            0,
                            Mathf.PI * 2f,
                            1.4f + progress,
                            new Color(0.376f, 0.647f, 0.98f, 0.36f + progress * 0.34f));
                    }
                    else if (attack.Id == "beam")
                    {
                        BuildBossBeamMesh(index, boss, attack, 0.1f + progress * 0.12f);
                        SetBossTelegraphMesh(index);
                    }
                    else if (attack.Id == "volley")
                    {
                        var direction = SourceVisualDirection(boss.DashDirection);
                        var angle = Mathf.Atan2(direction.y, direction.x);
                        var fan = 52f * Mathf.Deg2Rad;
                        var color = new Color(0.655f, 0.545f, 0.98f, 0.09f + progress * 0.18f);
                        AddFan(vertices, triangles, colors, boss.Position, 330f, angle - fan * 0.5f, angle + fan * 0.5f, 20, color);
                        SetBossTelegraphMesh(index);

                        var outline = EnsureBossTelegraphOutlineView(index);
                        var left = boss.Position + new Vector2(Mathf.Cos(angle - fan * 0.5f), Mathf.Sin(angle - fan * 0.5f)) * 330f;
                        var right = boss.Position + new Vector2(Mathf.Cos(angle + fan * 0.5f), Mathf.Sin(angle + fan * 0.5f)) * 330f;
                        outline.positionCount = 4;
                        outline.SetPosition(0, boss.Position);
                        outline.SetPosition(1, left);
                        outline.SetPosition(2, right);
                        outline.SetPosition(3, boss.Position);
                        outline.startColor = outline.endColor = new Color(0.655f, 0.545f, 0.98f, 0.26f + progress * 0.3f);
                        outline.startWidth = outline.endWidth = 1.2f + progress * 0.8f;
                        outline.enabled = true;
                    }
                    else
                    {
                        var radius = attack.Id == "slam"
                            ? (float)(attack.Radius ?? 190) * progress
                            : boss.Radius * (1.4f + progress * 0.7f);
                        var color = attack.Id == "summon"
                            ? new Color(0.204f, 0.827f, 0.6f, 0.08f + progress * 0.16f)
                            : new Color(0.655f, 0.545f, 0.98f, 0.08f + progress * 0.18f);
                        AddFan(vertices, triangles, colors, boss.Position, radius, 0, Mathf.PI * 2f, 32, color);
                        SetBossTelegraphMesh(index);
                        SetArcLine(
                            EnsureBossTelegraphOutlineView(index),
                            boss.Position,
                            radius,
                            0,
                            Mathf.PI * 2f,
                            1.2f + progress,
                            new Color(color.r, color.g, color.b, 0.28f + progress * 0.24f));
                    }
                }
                else if (attack != null && boss.State == 2 && attack.Id == "beam")
                {
                    BuildBossBeamMesh(index, boss, attack, 0.34f);
                    SetBossTelegraphMesh(index);
                }

                // The browser boss telegraphs are translucent fills. Unity
                // previously added bright outline strokes around these shapes,
                // which changed the source visual for every attack type.
                Hide(_bossTelegraphOutlineViews[index]);

                if (IsMatriarchShielded(boss))
                {
                    var shieldFill = EnsureBossShieldFillView(index);
                    shieldFill.transform.position = boss.Position;
                    shieldFill.transform.localScale = Vector3.one * BossShieldVisualDiameter(boss.Radius);
                    shieldFill.color = BossShieldVisualColor();
                    shieldFill.enabled = true;
                }
            }
        }

        private void BuildBossBeamMesh(int index, BossState boss, BossAttackDefinition attack, float alpha)
        {
            var vertices = _bossTelegraphVertices[index];
            var triangles = _bossTelegraphTriangles[index];
            var colors = _bossTelegraphColors[index];
            vertices.Clear();
            triangles.Clear();
            colors.Clear();

            var length = (float)(attack.BeamLength ?? 680);
            var width = (float)(attack.BeamWidth ?? 48);
            var direction = new Vector2(Mathf.Cos(boss.AttackAngle), Mathf.Sin(boss.AttackAngle));
            var normal = new Vector2(-direction.y, direction.x);
            var start = 36f;
            var segmentIndex = 0;
            for (;
                start < length && segmentIndex < BossBeamMaxSegments;
                start += 68f + 34f, segmentIndex++)
            {
                var end = Mathf.Min(length, start + 68f);
                var segmentWidth = width * (0.86f + Mathf.Min(0.24f, segmentIndex * 0.035f));
                var outer = new Color(0.376f, 0.647f, 0.98f, alpha * (segmentIndex % 2 == 0 ? 1f : 0.78f));
                AddQuad(
                    vertices,
                    triangles,
                    colors,
                    boss.Position + direction * start - normal * segmentWidth * 0.5f,
                    boss.Position + direction * end - normal * segmentWidth * 0.5f,
                    boss.Position + direction * end + normal * segmentWidth * 0.5f,
                    boss.Position + direction * start + normal * segmentWidth * 0.5f,
                    outer);
                if (end - start > 14f)
                {
                    var innerWidth = segmentWidth * 0.3f;
                    var inner = new Color(0.859f, 0.918f, 0.996f, alpha * 0.34f);
                    AddQuad(
                        vertices,
                        triangles,
                        colors,
                        boss.Position + direction * (start + 7f) - normal * innerWidth * 0.5f,
                        boss.Position + direction * (end - 7f) - normal * innerWidth * 0.5f,
                        boss.Position + direction * (end - 7f) + normal * innerWidth * 0.5f,
                        boss.Position + direction * (start + 7f) + normal * innerWidth * 0.5f,
                        inner);
                }
            }
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            List<Color> colors,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color color)
        {
            var start = vertices.Count;
            vertices.Add(new Vector3(a.x, a.y, 0));
            vertices.Add(new Vector3(b.x, b.y, 0));
            vertices.Add(new Vector3(c.x, c.y, 0));
            vertices.Add(new Vector3(d.x, d.y, 0));
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private MeshFilter CreateMeshView(string name, int sortingOrder, out MeshRenderer renderer)
        {
            var objectRoot = new GameObject(name);
            objectRoot.transform.SetParent(_worldRoot, false);
            var filter = objectRoot.AddComponent<MeshFilter>();
            var mesh = new Mesh { name = name + " Mesh" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            _dynamicMeshes.Add(mesh);
            renderer = objectRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = VoidFallRenderMaterials.DefaultUnlit;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return filter;
        }

        // Browser backdrop passes are screen-space. Camera trauma moves the
        // gameplay camera, but those passes only receive their source parallax
        // response; using the player position here would make every decorative
        // layer inherit the full camera shake.
        private Vector2 RenderCameraCentre()
        {
            return _camera == null
                ? _playerPosition
                : new Vector2(_camera.transform.position.x, _camera.transform.position.y);
        }

        private static Vector2 RenderViewportHalfExtent(float orthographicSize, float aspect)
        {
            var halfHeight = Mathf.Max(1f, orthographicSize);
            var halfWidth = halfHeight * Mathf.Max(0.1f, aspect);
            return new Vector2(halfWidth, halfHeight);
        }

        private Vector2 RenderViewportHalfExtent()
        {
            return _camera == null
                ? new Vector2(WorldHalfWidth, WorldHalfHeight)
                : RenderViewportHalfExtent(_camera.orthographicSize, _camera.aspect);
        }

        private void RenderArena()
        {
            if (_backdropView == null) return;
            EnsureArenaPlateViewport();
            var cameraCentre = RenderCameraCentre();
            var viewportHalf = RenderViewportHalfExtent();
            if (_arenaVignetteView != null)
            {
                var vignetteSprite = ProceduralSpriteFactory.ArenaVignette(_arenaId);
                _arenaVignetteView.sprite = vignetteSprite;
                _arenaVignetteView.color = Color.white;
                _arenaVignetteView.transform.position = new Vector3(cameraCentre.x, cameraCentre.y, 0f);
                if (vignetteSprite != null)
                {
                    var size = vignetteSprite.bounds.size;
                    _arenaVignetteView.transform.localScale = new Vector3(
                        viewportHalf.x * 2f / Mathf.Max(0.01f, size.x),
                        viewportHalf.y * 2f / Mathf.Max(0.01f, size.y),
                        1f);
                }
                _arenaVignetteView.enabled = true;
            }
            _backdropView.sprite = _arenaPlateSprites[(int)_arenaId];
            _backdropView.color = Color.white;
            var recipe = ArenaCatalogRules.RecipeLayout(_arenaRecipeIndex);
            var skyOffset = ArenaParallaxOffsetForViewport(
                cameraCentre,
                ArenaSkyParallax,
                ArenaSkyOverscan);
            _backdropView.transform.localScale = new Vector3(
                (recipe.MirrorX ? -1f : 1f) *
                    viewportHalf.x * 2f * ArenaSkyOverscan / Mathf.Max(1, _arenaPlateBakeWidth),
                viewportHalf.y * 2f * ArenaSkyOverscan / Mathf.Max(1, _arenaPlateBakeHeight),
                1);
            _backdropView.transform.position = new Vector3(
                cameraCentre.x - skyOffset.x,
                cameraCentre.y - skyOffset.y,
                0);
            if (_arenaBakedDetailView != null)
            {
                _arenaBakedDetailView.sprite = _arenaPlateDetailSprites[(int)_arenaId];
                _arenaBakedDetailView.color = Color.white;
                _arenaBakedDetailView.transform.localScale =
                    _backdropView.transform.localScale * recipe.DetailScale;
                _arenaBakedDetailView.transform.position = _backdropView.transform.position +
                    new Vector3(
                        viewportHalf.x * recipe.DetailOffsetX,
                        viewportHalf.y * recipe.DetailOffsetY,
                        0f);
                _arenaBakedDetailView.enabled = _arenaBakedDetailView.sprite != null;
            }

            RenderArenaGrid();

            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            var cycle = ArenaCycleRules.At(ArenaIdName(_arenaId), ArenaCycleElapsedSeconds());
            var cycleVisual = ArenaCycleVisual(cycle.CycleId, (float)cycle.Progress);
            if (!_arenaMoteSeedsReady ||
                _arenaMoteSeedArena != _arenaId ||
                _arenaMoteSeedDetail != _qualityPreset.Detail ||
                _arenaMoteSeedReducedMotion != reducedMotion)
            {
                ConfigureArenaMoteSeeds(reducedMotion);
            }
            if (!_arenaFarFilamentSeedsReady || _arenaFarFilamentSeedArena != _arenaId)
                ConfigureArenaFarFilaments();
            // The browser passes sprites.dot.orange as emberSprite for every
            // non-petal arena. It is not retinted from the arena palette.
            var moteColor = ParseColor("#fb923c", Color.white);
            var petal = _arenaId == ArenaId.WhiteSakura;
            var visibleMotes = Mathf.Clamp(
                SourceRound(_arenaMoteSeedCount * Mathf.Min(1.2f, cycleVisual.Density)),
                0,
                _arenaMoteViews.Length);
            var moteSprite = petal
                ? ProceduralSpriteFactory.Petal()
                : ProceduralSpriteFactory.ArenaDot(moteColor);
            RenderArenaStars();
            for (var index = 0; index < _arenaMoteViews.Length; index++)
            {
                var view = _arenaMoteViews[index];
                if (view == null) continue;
                if (index >= visibleMotes)
                {
                    view.enabled = false;
                    continue;
                }
                var seed = _arenaMoteSeeds[index];
                var depth = _arenaMoteDepths[index];
                var parallax = _arenaMoteParallax[index];
                var rate = _arenaMoteRates[index];
                var phase = seed.w;
                var wobble = Mathf.Sin(_arenaDecorClock * rate * 0.6f + phase) *
                    (petal ? 26f : 14f) * (0.4f + cycleVisual.Current);
                var cross = Mathf.Cos(ArenaMoteAngle(_arenaId));
                var crossY = Mathf.Sin(ArenaMoteAngle(_arenaId));
                var screenWidth = viewportHalf.x * 2f;
                var screenHeight = viewportHalf.y * 2f;
                var moteX = ArenaDecorScreenCoordinate(
                    seed.x + _arenaDecorDrift.x * rate - crossY * wobble,
                    cameraCentre.x,
                    viewportHalf.x,
                    parallax);
                var moteY = ArenaDecorScreenCoordinate(
                    seed.y + _arenaDecorDrift.y * rate + cross * wobble,
                    cameraCentre.y,
                    viewportHalf.y,
                    parallax);
                if (moteX < -30f || moteX > screenWidth + 30f ||
                    moteY < -30f || moteY > screenHeight + 30f)
                {
                    view.enabled = false;
                    continue;
                }
                var edge = Mathf.Min(1f, new Vector2(
                    (moteX - viewportHalf.x) / Mathf.Max(1f, viewportHalf.x),
                    (moteY - viewportHalf.y) / Mathf.Max(1f, viewportHalf.y)).magnitude);
                if (edge < 0.42f && (index % 5) / 5f < cycleVisual.EdgeBias)
                {
                    view.enabled = false;
                    continue;
                }

                view.sprite = moteSprite;
                var position = cameraCentre + new Vector2(
                    moteX - viewportHalf.x,
                    viewportHalf.y - moteY);
                view.transform.position = position;
                var size = _arenaMoteSizes[index] * (depth == 2 ? 1.35f : 1f);
                if (petal)
                {
                    var rotation = seed.z + _arenaDecorClock * _arenaMoteSpins[index];
                    var squash = 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(rotation * 0.7f));
                    view.transform.localScale = new Vector3(
                        size / Mathf.Max(0.01f, view.sprite.bounds.size.x),
                        size * squash / Mathf.Max(0.01f, view.sprite.bounds.size.y),
                        1f);
                    view.transform.rotation = Quaternion.Euler(0, 0, rotation * Mathf.Rad2Deg);
                }
                else
                {
                    view.transform.localScale = new Vector3(
                        size / Mathf.Max(0.01f, view.sprite.bounds.size.x),
                        size / Mathf.Max(0.01f, view.sprite.bounds.size.y),
                        1f);
                    view.transform.rotation = Quaternion.identity;
                }
                var depthAlpha = depth == 2 ? 0.5f : depth == 1 ? 0.38f : 0.24f;
                var alpha = Mathf.Min(0.62f, depthAlpha * (petal ? 1f : 0.8f + cycleVisual.Current * 0.5f));
                view.color = new Color(1f, 1f, 1f, alpha);
                view.enabled = true;
            }

            RenderArenaCurrentGlow(cycleVisual, reducedMotion);

            EnsureArenaFilamentViewport();
            RenderArenaFarFilaments();
            RenderArenaNearFilaments();
            RenderArenaRocks();
            RenderArenaLandmark();
            UpdateTransitionOverlay();
        }

        private void RenderArenaGrid()
        {
            if (_arenaGridRenderer == null || _arenaGridMesh == null) return;
            if (_visualCaptureNoGrid)
            {
                _arenaGridRenderer.enabled = false;
                return;
            }

            var visible = _arenaId == ArenaId.Void;
            _arenaGridRenderer.enabled = visible;
            if (!visible) return;

            var cameraCentre = _camera == null
                ? new Vector3(_playerPosition.x, _playerPosition.y, 0)
                : _camera.transform.position;
            var worldHeight = _camera == null
                ? WorldHalfHeight * 2f
                : Mathf.Max(1f, _camera.orthographicSize * 2f);
            var worldWidth = _camera == null
                ? WorldHalfWidth * 2f
                : Mathf.Max(1f, worldHeight * Mathf.Max(0.1f, _camera.aspect));
            var halfWidth = worldWidth * 0.5f;
            var halfHeight = worldHeight * 0.5f;
            var left = cameraCentre.x - halfWidth;
            var right = cameraCentre.x + halfWidth;
            var bottom = cameraCentre.y - halfHeight;
            var top = cameraCentre.y + halfHeight;
            var firstX = Mathf.Floor(left / ArenaGridSpacing) * ArenaGridSpacing;
            var firstY = Mathf.Floor(bottom / ArenaGridSpacing) * ArenaGridSpacing;
            var verticalCount = Mathf.Clamp(
                Mathf.CeilToInt((right + ArenaGridSpacing - firstX) / ArenaGridSpacing),
                1,
                200);
            var horizontalCount = Mathf.Clamp(
                Mathf.CeilToInt((top + ArenaGridSpacing - firstY) / ArenaGridSpacing),
                1,
                200);

            if (Mathf.Abs(_arenaGridFirstX - firstX) < 0.1f &&
                Mathf.Abs(_arenaGridFirstY - firstY) < 0.1f &&
                Mathf.Abs(_arenaGridWidth - worldWidth) < 0.1f &&
                Mathf.Abs(_arenaGridHeight - worldHeight) < 0.1f &&
                _arenaGridVerticalCount == verticalCount &&
                _arenaGridHorizontalCount == horizontalCount)
                return;

            var lineCount = verticalCount + horizontalCount;
            var vertices = new Vector3[lineCount * 2];
            var colors = new Color[lineCount * 2];
            var indices = new int[lineCount * 2];
            var gridColor = new Color(117f / 255f, 133f / 255f, 160f / 255f, 0.065f);
            var vertex = 0;
            for (var line = 0; line < verticalCount; line++)
            {
                var x = firstX + line * ArenaGridSpacing;
                vertices[vertex] = new Vector3(x, bottom, 0);
                vertices[vertex + 1] = new Vector3(x, top, 0);
                colors[vertex] = gridColor;
                colors[vertex + 1] = gridColor;
                indices[vertex] = vertex;
                indices[vertex + 1] = vertex + 1;
                vertex += 2;
            }
            for (var line = 0; line < horizontalCount; line++)
            {
                var y = firstY + line * ArenaGridSpacing;
                vertices[vertex] = new Vector3(left, y, 0);
                vertices[vertex + 1] = new Vector3(right, y, 0);
                colors[vertex] = gridColor;
                colors[vertex + 1] = gridColor;
                indices[vertex] = vertex;
                indices[vertex + 1] = vertex + 1;
                vertex += 2;
            }

            _arenaGridMesh.Clear();
            _arenaGridMesh.vertices = vertices;
            _arenaGridMesh.colors = colors;
            _arenaGridMesh.SetIndices(indices, MeshTopology.Lines, 0);
            _arenaGridMesh.RecalculateBounds();
            _arenaGridFirstX = firstX;
            _arenaGridFirstY = firstY;
            _arenaGridWidth = worldWidth;
            _arenaGridHeight = worldHeight;
            _arenaGridVerticalCount = verticalCount;
            _arenaGridHorizontalCount = horizontalCount;
        }

        private void RenderArenaStars()
        {
            var cameraCentre = RenderCameraCentre();
            var sourceCount = _arenaId == ArenaId.RedNebula
                ? 26
                : _arenaId == ArenaId.Void ? 42 : 0;
            // The browser hides the star pass at quality 0; quality 1 keeps
            // the reduced 60% population and quality 2 keeps the full count.
            var qualityScale = _qualityPreset.Detail <= 0
                ? 0f
                : _qualityPreset.Detail > 1 ? 1f : 0.6f;
            var visible = Mathf.Clamp(
                Mathf.RoundToInt(sourceCount * qualityScale),
                0,
                _arenaStarViews.Length);
            var starColor = ArenaStarColor(_arenaId);
            var starSprite = ProceduralSpriteFactory.ArenaDot(starColor);
            const float starSpan = 2200f;
            var viewportHalf = RenderViewportHalfExtent();
            for (var index = 0; index < _arenaStarViews.Length; index++)
            {
                var view = _arenaStarViews[index];
                if (view == null) continue;
                if (index >= visible)
                {
                    view.enabled = false;
                    continue;
                }

                var x = ArenaWrappedScreenCoordinate(
                    index * 613.7f,
                    starSpan,
                    cameraCentre.x,
                    viewportHalf.x,
                    0.1f);
                var y = ArenaWrappedScreenCoordinate(
                    index * 419.3f,
                    starSpan,
                    cameraCentre.y,
                    viewportHalf.y,
                    0.1f);
                var twinkle = 0.4f + 0.35f * Mathf.Sin(
                    _arenaDecorClock * (0.6f + (index % 5) * 0.22f) + index);
                var size = 1.6f + (index % 3) * 0.9f;
                view.sprite = starSprite;
                view.transform.position = cameraCentre + new Vector2(
                    x - viewportHalf.x,
                    viewportHalf.y - y);
                view.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, view.sprite.bounds.size.x));
                view.color = new Color(1f, 1f, 1f, twinkle * 0.55f);
                view.enabled = true;
            }
        }

        private void RenderArenaCurrentGlow(ArenaCycleVisualState cycle, bool reducedMotion)
        {
            if (_arenaCurrentGlowView == null) return;
            // Source drawMotes gates this localized current glow on
            // `frame.quality > 0`, not on the broader particle scale.
            if (cycle.Current <= 0.6f || reducedMotion || _qualityPreset.Detail <= 0)
            {
                _arenaCurrentGlowView.enabled = false;
                return;
            }

            var travel = Mathf.Repeat(_arenaDecorClock * 0.18f, 1f);
            var viewportHalf = RenderViewportHalfExtent();
            var x = -viewportHalf.x + (0.12f + travel * 0.82f) * viewportHalf.x * 2f;
            var y = -viewportHalf.y +
                (0.24f + Mathf.Sin(travel * 4.1f) * 0.2f + 0.2f) * viewportHalf.y * 2f;
            var reach = Mathf.Min(viewportHalf.x * 2f, viewportHalf.y * 2f) * 0.16f;
            var tint = ArenaCloudTint(_arenaId);
            _arenaCurrentGlowView.transform.position = RenderCameraCentre() + new Vector2(x, y);
            _arenaCurrentGlowView.transform.localScale = Vector3.one *
                (reach * 2f / Mathf.Max(0.01f, _arenaCurrentGlowView.sprite.bounds.size.x));
            _arenaCurrentGlowView.color = new Color(tint.r, tint.g, tint.b, 0.14f * cycle.Current);
            _arenaCurrentGlowView.enabled = true;
        }

        private void RenderArenaRocks()
        {
            var cameraCentre = RenderCameraCentre();
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            if (!_arenaRockSeedsReady || _arenaRockSeedArena != _arenaId ||
                _arenaRockSeedDetail != _qualityPreset.Detail ||
                _arenaRockSeedReducedMotion != reducedMotion)
                ConfigureArenaRockSeeds();

            var visible = _arenaRockTotalCount;
            var pale = _arenaId == ArenaId.WhiteSakura;
            // Keep the browser ArenaVisualSpec palette as the authority. The
            // previous hand-tuned floats were close, but not the exact source
            // sRGB tokens and drifted on the low-contrast White Sakura layer.
            var farBody = ParseColor(pale ? "#94919a" : "#0d0709", Color.white);
            var midBody = ParseColor(pale ? "#6d6a77" : "#160b0d", Color.white);
            var farRim = ParseColor(pale ? "#f3ede4" : "#8a3a22", Color.white);
            var midRim = ParseColor(pale ? "#e8e0d6" : "#7a3520", Color.white);
            var farAlpha = pale ? 0.42f : 0.94f;
            var midAlpha = pale ? 0.46f : 0.7f;
            var lightAngle = ArenaLightAngle(_arenaId);
            var rimPulse = 1f + _arenaFlash * 0.5f;
            for (var index = 0; index < _arenaRockViews.Length; index++)
            {
                var view = _arenaRockViews[index];
                var plane = _arenaRockPlaneViews[index];
                var rim = _arenaRockRimViews[index];
                if (view == null) continue;
                if (index >= visible)
                {
                    view.enabled = false;
                    Hide(plane);
                    Hide(rim);
                    continue;
                }

                var seed = _arenaRockSeeds[index];
                var far = index < _arenaRockFarCount;
                var parallax = far
                    ? (pale ? 0.15f : 0.14f)
                    : (pale ? 0.31f : 0.3f);
                var sourceX = seed.x * ArenaDecorField;
                var sourceY = seed.y * ArenaDecorField;
                var viewportHalf = RenderViewportHalfExtent();
                var screenX = ArenaDecorScreenCoordinate(
                    sourceX,
                    cameraCentre.x,
                    viewportHalf.x,
                    parallax);
                var screenY = ArenaDecorScreenCoordinate(
                    sourceY,
                    cameraCentre.y,
                    viewportHalf.y,
                    parallax);
                view.transform.position = cameraCentre + new Vector2(
                    screenX - viewportHalf.x,
                    viewportHalf.y - screenY);
                var diameter = far
                    ? Mathf.Lerp(110f, pale ? 175f : 180f, seed.z)
                    : Mathf.Lerp(pale ? 30f : 34f, pale ? 56f : 62f, seed.z);
                view.sprite = ProceduralSpriteFactory.ArenaRock(_arenaRockShapes[index]);
                view.transform.localScale = Vector3.one * diameter;
                var rockAngle = seed.w * 360f +
                    _arenaRockSpins[index] * _arenaDecorClock * Mathf.Rad2Deg;
                view.transform.rotation = Quaternion.Euler(0, 0, rockAngle);
                var layerAlpha = far ? farAlpha : midAlpha;
                var body = far ? farBody : midBody;
                view.color = new Color(body.r, body.g, body.b, layerAlpha);
                view.enabled = true;

                var rimBase = far ? farRim : midRim;
                if (plane != null)
                {
                    var planeRadius = diameter * 0.5f;
                    plane.transform.position = view.transform.position +
                        view.transform.rotation * new Vector3(-planeRadius * 0.18f, planeRadius * 0.12f, 0);
                    plane.transform.rotation = Quaternion.Euler(
                        0, 0, rockAngle + _arenaRockTones[index] * 360f);
                    var circleWidth = Mathf.Max(0.01f, plane.sprite.bounds.size.x);
                    var circleHeight = Mathf.Max(0.01f, plane.sprite.bounds.size.y);
                    plane.transform.localScale = new Vector3(
                        planeRadius * 0.92f / circleWidth,
                        planeRadius * 0.68f / circleHeight,
                        1);
                    plane.color = new Color(rimBase.r, rimBase.g, rimBase.b, layerAlpha * 0.1f);
                    plane.enabled = true;
                }

                if (rim == null) continue;
                var radius = diameter * 0.5f;
                rim.positionCount = 16;
                for (var point = 0; point < rim.positionCount; point++)
                {
                    var t = point / (float)(rim.positionCount - 1);
                    var angle = lightAngle - 0.62f + t * 1.24f;
                    rim.SetPosition(
                        point,
                        view.transform.position + new Vector3(
                            Mathf.Cos(angle) * radius * 0.9f,
                            Mathf.Sin(angle) * radius * 0.9f,
                            0));
                }
                var rimColor = new Color(
                    rimBase.r,
                    rimBase.g,
                    rimBase.b,
                    Mathf.Min(1f, layerAlpha * 0.22f * rimPulse));
                rim.startColor = rimColor;
                rim.endColor = rimColor;
                rim.startWidth = Mathf.Max(1f, radius * 0.03f);
                rim.endWidth = rim.startWidth;
                rim.enabled = true;
            }
        }

        private void RenderArenaFarFilaments()
        {
            var cameraCentre = RenderCameraCentre();
            var layerCentre = cameraCentre - ArenaParallaxOffsetForViewport(
                cameraCentre,
                ArenaSkyParallax,
                ArenaSkyOverscan);

            var plateView = _arenaFilamentPlateViews[2];
            if (plateView != null && plateView.sprite != null)
            {
                plateView.transform.position = layerCentre;
                plateView.color = Color.white;
                plateView.enabled = _arenaFarFilamentCount > 0;
                HideArenaFilamentCompatibilityViews(4, 4);
                return;
            }

            for (var index = 0; index < 4; index++)
            {
                var slot = 4 + index;
                var outer = _arenaNearFilamentOuterViews[slot];
                var outerRenderer = _arenaNearFilamentOuterRenderers[slot];
                var inner = _arenaNearFilamentInnerViews[slot];
                var strand = _arenaNearFilamentStrandViews[slot];
                var strandRenderer = _arenaNearFilamentStrandRenderers[slot];
                if (outer == null || outerRenderer == null || inner == null || strand == null || strandRenderer == null ||
                    index >= _arenaFarFilamentCount)
                {
                    Hide(outerRenderer);
                    Hide(inner);
                    Hide(strandRenderer);
                    continue;
                }

                var points = _arenaNearFilamentPoints[slot];
                var mesh = outer.sharedMesh;
                var bandColors = _arenaNearFilamentBandColors[slot];
                if (points == null || mesh == null || bandColors == null)
                {
                    Hide(outerRenderer);
                    Hide(inner);
                    Hide(strandRenderer);
                    continue;
                }

                outer.transform.position = layerCentre;
                var baseColor = _arenaNearFilamentColors[slot];
                var peak = Mathf.Clamp01(_arenaNearFilamentAlphas[slot]);
                var filamentMaterial = _arenaNearFilamentMaterials[slot];
                var useNotchMask = filamentMaterial != null && _arenaNearFilamentNotchMasks[slot] != null;
                if (useNotchMask)
                {
                    filamentMaterial.SetFloat("_Peak", peak);
                    filamentMaterial.SetFloat("_PassCount", ArenaNearFilamentPasses);
                }
                for (var pass = 0; pass < ArenaNearFilamentPasses; pass++)
                {
                    for (var point = 0; point < points.Length; point++)
                    {
                        var half = _arenaNearFilamentPointWidths[slot][point] *
                            (1.6f - pass * 0.13f) * 0.5f;
                        var leftNotchFactor = Mathf.Clamp01(NearFilamentNotchFactor(
                            point,
                            half,
                            _arenaNearFilamentPointWidths[slot][point],
                            _arenaNearFilamentNotches[slot],
                            _arenaNearFilamentNotchHeights[slot],
                            _arenaNearFilamentPointSpacings[slot]));
                        var rightNotchFactor = Mathf.Clamp01(NearFilamentNotchFactor(
                            point,
                            -half,
                            _arenaNearFilamentPointWidths[slot][point],
                            _arenaNearFilamentNotches[slot],
                            _arenaNearFilamentNotchHeights[slot],
                            _arenaNearFilamentPointSpacings[slot]));
                        var vertex = (pass * points.Length + point) * 2;
                        bandColors[vertex] = new Color(
                            baseColor.r,
                            baseColor.g,
                            baseColor.b,
                            useNotchMask
                                ? 1f
                                : NearFilamentPassAlpha(peak, leftNotchFactor, ArenaNearFilamentPasses));
                        bandColors[vertex + 1] = new Color(
                            baseColor.r,
                            baseColor.g,
                            baseColor.b,
                            useNotchMask
                                ? 1f
                                : NearFilamentPassAlpha(peak, rightNotchFactor, ArenaNearFilamentPasses));
                    }
                }
                mesh.colors = bandColors;
                outerRenderer.enabled = true;

                var strandFrom = Mathf.Clamp(_arenaNearFilamentStrandFrom[slot], 0, points.Length - 1);
                var strandTo = Mathf.Clamp(_arenaNearFilamentStrandTo[slot], strandFrom, points.Length);
                var strandCount = strandTo - strandFrom;
                var strandMesh = strand.sharedMesh;
                var strandColors = _arenaNearFilamentStrandColors[slot];
                if (strandCount < 4 || strandMesh == null || strandColors == null)
                {
                    Hide(inner);
                    Hide(strandRenderer);
                    continue;
                }

                strand.transform.position = layerCentre;
                var strandPerPass = 1f - Mathf.Pow(
                    1f - Mathf.Clamp01(_arenaNearFilamentAlphas[slot] * 0.6f),
                    1f / ArenaNearStrandPasses);
                for (var vertex = 0; vertex < strandColors.Length; vertex++)
                {
                    strandColors[vertex] = new Color(
                        _arenaNearFilamentCoreColors[slot].r,
                        _arenaNearFilamentCoreColors[slot].g,
                        _arenaNearFilamentCoreColors[slot].b,
                        strandPerPass);
                }
                strandMesh.colors = strandColors;
                strandRenderer.enabled = true;
                Hide(inner);
            }
        }

        private void RenderArenaNearFilaments()
        {
            // Browser buildArenaLayers omits the near filament canvas at
            // quality 0; keep only the baked sky/far layer on Low.
            var activeStart = _arenaId == ArenaId.RedNebula ? 0 : _arenaId == ArenaId.WhiteSakura ? 2 : -1;
            var plateGroup = activeStart == 0 ? 0 : activeStart == 2 ? 1 : -1;
            if (_qualityPreset.Detail <= 0)
            {
                HideArenaFilamentCompatibilityViews(0, 4);
                for (var index = 0; index < 2; index++) Hide(_arenaFilamentPlateViews[index]);
                return;
            }
            var plateView = plateGroup >= 0 ? _arenaFilamentPlateViews[plateGroup] : null;
            if (plateView != null && plateView.sprite != null)
            {
                var plateCycle = ArenaCycleRules.At(ArenaIdName(_arenaId), ArenaCycleElapsedSeconds());
                var plateCycleVisual = ArenaCycleVisual(plateCycle.CycleId, (float)plateCycle.Progress);
                var plateAlphaScale = 0.4f + plateCycleVisual.Definition * 0.6f;
                var plateCameraCentre = RenderCameraCentre();
                plateView.transform.position = plateCameraCentre - ArenaParallaxOffsetForViewport(
                    plateCameraCentre,
                    ArenaNearParallax,
                    ArenaNearOverscan);
                plateView.color = new Color(1f, 1f, 1f, Mathf.Clamp01(plateAlphaScale));
                plateView.enabled = plateGroup >= 0;
                for (var index = 0; index < 2; index++)
                {
                    var other = _arenaFilamentPlateViews[index];
                    if (other != null && other != plateView) other.enabled = false;
                }
                HideArenaFilamentCompatibilityViews(0, 4);
                return;
            }

            var cycle = ArenaCycleRules.At(ArenaIdName(_arenaId), ArenaCycleElapsedSeconds());
            var cycleVisual = ArenaCycleVisual(cycle.CycleId, (float)cycle.Progress);
            var alphaScale = 0.4f + cycleVisual.Definition * 0.6f;
            var cameraCentre = RenderCameraCentre();
            var layerCentre = cameraCentre - ArenaParallaxOffsetForViewport(
                cameraCentre,
                ArenaNearParallax,
                ArenaNearOverscan);

            for (var index = 0; index < 4; index++)
            {
                var outer = _arenaNearFilamentOuterViews[index];
                var outerRenderer = _arenaNearFilamentOuterRenderers[index];
                var inner = _arenaNearFilamentInnerViews[index];
                var strand = _arenaNearFilamentStrandViews[index];
                var strandRenderer = _arenaNearFilamentStrandRenderers[index];
                if (outer == null || outerRenderer == null || inner == null || strand == null || strandRenderer == null ||
                    activeStart < 0 || index < activeStart || index >= activeStart + 2)
                {
                    Hide(outerRenderer);
                    Hide(inner);
                    Hide(strandRenderer);
                    continue;
                }

                var points = _arenaNearFilamentPoints[index];
                var mesh = outer.sharedMesh;
                var bandColors = _arenaNearFilamentBandColors[index];
                if (points == null || mesh == null || bandColors == null)
                {
                    Hide(outerRenderer);
                    Hide(inner);
                    continue;
                }

                outer.transform.position = layerCentre;
                var baseColor = _arenaNearFilamentColors[index];
                var peak = Mathf.Clamp01(_arenaNearFilamentAlphas[index] * alphaScale * 0.95f);
                var filamentMaterial = _arenaNearFilamentMaterials[index];
                var useNotchMask = filamentMaterial != null && _arenaNearFilamentNotchMasks[index] != null;
                if (useNotchMask)
                {
                    filamentMaterial.SetFloat("_Peak", peak);
                    filamentMaterial.SetFloat("_PassCount", ArenaNearFilamentPasses);
                }
                for (var pass = 0; pass < ArenaNearFilamentPasses; pass++)
                {
                    for (var point = 0; point < points.Length; point++)
                    {
                        var half = _arenaNearFilamentPointWidths[index][point] *
                            (1.6f - pass * 0.13f) * 0.5f;
                        var leftNotchFactor = Mathf.Clamp01(NearFilamentNotchFactor(
                            point,
                            half,
                            _arenaNearFilamentPointWidths[index][point],
                            _arenaNearFilamentNotches[index],
                            _arenaNearFilamentNotchHeights[index],
                            _arenaNearFilamentPointSpacings[index]));
                        var rightNotchFactor = Mathf.Clamp01(NearFilamentNotchFactor(
                            point,
                            -half,
                            _arenaNearFilamentPointWidths[index][point],
                            _arenaNearFilamentNotches[index],
                            _arenaNearFilamentNotchHeights[index],
                            _arenaNearFilamentPointSpacings[index]));
                        var vertex = (pass * points.Length + point) * 2;
                        bandColors[vertex] = new Color(
                            baseColor.r,
                            baseColor.g,
                            baseColor.b,
                            useNotchMask
                                ? 1f
                                : NearFilamentPassAlpha(peak, leftNotchFactor, ArenaNearFilamentPasses));
                        bandColors[vertex + 1] = new Color(
                            baseColor.r,
                            baseColor.g,
                            baseColor.b,
                            useNotchMask
                                ? 1f
                                : NearFilamentPassAlpha(peak, rightNotchFactor, ArenaNearFilamentPasses));
                    }
                }
                mesh.colors = bandColors;
                outerRenderer.enabled = true;

                var strandFrom = Mathf.Clamp(_arenaNearFilamentStrandFrom[index], 0, points.Length - 1);
                var strandTo = Mathf.Clamp(_arenaNearFilamentStrandTo[index], strandFrom, points.Length);
                var strandCount = strandTo - strandFrom;
                var strandMesh = strand.sharedMesh;
                var strandColors = _arenaNearFilamentStrandColors[index];
                if (strandCount < 4 || strandMesh == null || strandColors == null)
                {
                    Hide(inner);
                    Hide(strandRenderer);
                    continue;
                }

                strand.transform.position = layerCentre;
                var strandPeak = Mathf.Clamp01(_arenaNearFilamentAlphas[index] * alphaScale * 0.6f);
                var strandPerPass = 1f - Mathf.Pow(
                    1f - strandPeak,
                    1f / ArenaNearStrandPasses);
                for (var vertex = 0; vertex < strandColors.Length; vertex++)
                {
                    strandColors[vertex] = new Color(
                        _arenaNearFilamentCoreColors[index].r,
                        _arenaNearFilamentCoreColors[index].g,
                        _arenaNearFilamentCoreColors[index].b,
                        strandPerPass);
                }
                strandMesh.colors = strandColors;
                strandRenderer.enabled = true;
                Hide(inner);
            }
        }

        private void RenderArenaLandmark()
        {
            var cameraCentre = RenderCameraCentre();
            var viewportHalf = RenderViewportHalfExtent();
            var shorterAxis = Mathf.Min(viewportHalf.x, viewportHalf.y) * 2f;
            Hide(_arenaLandmarkBodyView);
            for (var index = 0; index < _arenaLandmarkViews.Length; index++)
            {
                Hide(_arenaLandmarkViews[index]);
                Hide(_arenaLandmarkRimViews[index]);
            }
            for (var index = 0; index < _arenaStellarRimViews.Length; index++) Hide(_arenaStellarRimViews[index]);
            for (var index = 0; index < _arenaRingSlabFillRenderers.Length; index++) Hide(_arenaRingSlabFillRenderers[index]);
            for (var index = 0; index < _arenaRingDebrisViews.Length; index++) Hide(_arenaRingDebrisViews[index]);
            for (var index = 0; index < _arenaOrbitViews.Length; index++) Hide(_arenaOrbitViews[index]);
            for (var index = 0; index < _arenaOrbitFractureViews.Length; index++) Hide(_arenaOrbitFractureViews[index]);

            if (_arenaId == ArenaId.RedNebula)
            {
                var radius = shorterAxis * 0.78f;
                var skyOffset = ArenaParallaxOffsetForViewport(
                    cameraCentre,
                    ArenaSkyParallax,
                    ArenaSkyOverscan);
                var centre = cameraCentre - skyOffset + ArenaScreenPoint(-0.2f, 0.68f);
                _arenaLandmarkBodyView.sprite = ProceduralSpriteFactory.ArenaStellarLimb();
                _arenaLandmarkBodyView.transform.position = centre;
                _arenaLandmarkBodyView.transform.localScale = Vector3.one * (radius * 2f);
                _arenaLandmarkBodyView.color = Color.white;
                _arenaLandmarkBodyView.enabled = true;

                var stellarPhase = ArenaOrbitPhase() * 0.72f;
                var light = Mathf.PI * 0.86f + stellarPhase;
                for (var segment = 0; segment < MaxArenaStellarRimSegments; segment++)
                {
                    var t0 = segment / (float)MaxArenaStellarRimSegments;
                    var t1 = (segment + 1) / (float)MaxArenaStellarRimSegments;
                    var a0 = light - Mathf.PI * 0.52f + t0 * Mathf.PI * 1.04f;
                    var a1 = light - Mathf.PI * 0.52f + t1 * Mathf.PI * 1.04f;
                    var rim = _arenaStellarRimViews[segment];
                    var ripple = StellarRimRipple(Mathf.Repeat(t0 + stellarPhase * 0.08f, 1f));
                    rim.positionCount = 2;
                    rim.SetPosition(0, centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius * 0.995f);
                    rim.SetPosition(1, centre + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius * 0.995f);
                    var alpha = Mathf.Max(0, 0.85f * ripple * Mathf.Pow(Mathf.Sin(t0 * Mathf.PI), 0.6f));
                    var color = new Color(226f / 255f, 98f / 255f, 47f / 255f, alpha);
                    rim.startColor = color;
                    rim.endColor = color;
                    rim.startWidth = radius * (0.012f + ripple * 0.02f);
                    rim.endWidth = rim.startWidth;
                    rim.enabled = true;
                }
                return;
            }

            if (_arenaId != ArenaId.WhiteSakura) return;

            var ringRadius = shorterAxis * 0.92f;
            var skyRingOffset = ArenaParallaxOffsetForViewport(
                cameraCentre,
                ArenaSkyParallax,
                ArenaSkyOverscan);
            var ringCentre = cameraCentre - skyRingOffset + ArenaScreenPoint(0.83f, -0.16f);
            var angleCursor = -0.35f;
            var inner = ringRadius * 0.74f;
            var ringRotation = -0.34f + ArenaRingPhase();
            const float ringLightAngle = -0.72f;
            var landmarkStream = (uint)ArenaRockNoiseSeed(_arenaId) ^ 0x4f21u;
            for (var slab = 0; slab < MaxArenaLandmarkSegments; slab++)
            {
                // Keep the same sequential makeStream consumption as the
                // browser's paintFracturedRing. Per-slab hash values produce a
                // deterministic ring, but they do not preserve the source
                // stream when an optional debris branch consumes extra values.
                var span = 0.1f + ArenaDecorStreamNext(ref landmarkStream) * 0.26f;
                var gap = 0.03f + ArenaDecorStreamNext(ref landmarkStream) * 0.12f;
                var thickness = ringRadius * (0.05f + ArenaDecorStreamNext(ref landmarkStream) * 0.09f);
                var r0 = inner + ringRadius * (ArenaDecorStreamNext(ref landmarkStream) - 0.5f) * 0.05f;
                var r1 = r0 + thickness;
                var skew = (ArenaDecorStreamNext(ref landmarkStream) - 0.5f) * 0.9f;
                var chip = 0.16f + ArenaDecorStreamNext(ref landmarkStream) * 0.3f;
                var body = _arenaLandmarkViews[slab];
                var highlight = _arenaLandmarkRimViews[slab];
                var slabVertices = _arenaRingSlabVertices[slab];
                body.positionCount = 7;
                highlight.positionCount = 7;
                for (var point = 0; point <= ArenaRingSlabSteps; point++)
                {
                    var t = point / (float)ArenaRingSlabSteps;
                    var angle = angleCursor + ringRotation + span * t;
                    var outerRadius = r1 * (1 + skew * (t - 0.5f) * 0.1f);
                    var innerRadius = r0 * (1 - skew * (t - 0.5f) * 0.08f);
                    if (t > 1 - chip)
                        innerRadius += thickness * 0.5f * ((t - (1 - chip)) / chip);
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var outerPoint = ringCentre + direction * outerRadius;
                    var innerPoint = ringCentre + direction * innerRadius;
                    slabVertices[point] = outerPoint;
                    slabVertices[ArenaRingSlabSteps + 1 + point] = innerPoint;
                    body.SetPosition(point, outerPoint);
                }
                body.startColor = new Color(0.227f, 0.216f, 0.259f, 0.9f);
                body.endColor = body.startColor;
                body.startWidth = thickness;
                body.endWidth = thickness;
                body.enabled = false;
                var slabMesh = _arenaRingSlabFillViews[slab].sharedMesh;
                slabMesh.vertices = slabVertices;
                slabMesh.RecalculateBounds();
                _arenaRingSlabFillRenderers[slab].enabled = true;
                var lit = Mathf.Cos(angleCursor + ringRotation + span * 0.5f - ringLightAngle);
                var rimStartAngle = angleCursor + ringRotation + 0.01f;
                var rimEndAngle = angleCursor + ringRotation + span - 0.01f;
                var rimRadius = r1 - thickness * 0.15f;
                for (var point = 0; point <= ArenaRingSlabSteps; point++)
                {
                    var t = point / (float)ArenaRingSlabSteps;
                    var rimAngle = Mathf.Lerp(rimStartAngle, rimEndAngle, t);
                    highlight.SetPosition(
                        point,
                        ringCentre + new Vector2(Mathf.Cos(rimAngle), Mathf.Sin(rimAngle)) * rimRadius);
                }
                var rimColor = new Color(0.95f, 0.91f, 0.84f, Mathf.Max(0, 0.16f + lit * 0.4f));
                highlight.startColor = rimColor;
                highlight.endColor = highlight.startColor;
                highlight.startWidth = Mathf.Max(1f, thickness * 0.3f);
                highlight.endWidth = highlight.startWidth;
                // The source does not paint a rim at all when this slab is on
                // the unlit side of the fractured ring.
                highlight.enabled = lit > 0;

                // Browser parity: occasional fractured chips sit just outside a
                // slab. Keep these pooled and deterministic so arena rendering
                // stays allocation-free during a run.
                var debris = _arenaRingDebrisViews[slab];
                if (ArenaDecorStreamNext(ref landmarkStream) < 0.45f)
                {
                    var debrisAngle = angleCursor + ringRotation + span * ArenaDecorStreamNext(ref landmarkStream);
                    var debrisRadius = r1 + ringRadius *
                        (0.02f + ArenaDecorStreamNext(ref landmarkStream) * 0.06f);
                    var debrisSize = ringRadius *
                        (0.012f + ArenaDecorStreamNext(ref landmarkStream) * 0.022f);
                    debris.transform.position = ringCentre + new Vector2(
                        Mathf.Cos(debrisAngle),
                        Mathf.Sin(debrisAngle)) * debrisRadius;
                    debris.transform.rotation = Quaternion.Euler(
                        0,
                        0,
                        ArenaDecorStreamNext(ref landmarkStream) * 360f);
                    debris.transform.localScale = Vector3.one * (debrisSize * 2f);
                    debris.color = new Color(0.341f, 0.322f, 0.373f, 0.55f);
                    debris.enabled = true;
                }
                angleCursor += span + gap;
            }

            var orbitalOffset = ArenaParallaxOffsetForViewport(
                cameraCentre,
                ArenaOrbitalParallax,
                ArenaOrbitalOverscan);
            var orbitalShorterAxis = shorterAxis;
            var orbitPhase = ArenaOrbitPhase();
            var orbitalState = 0x2ad9u ^ 0x33b7u;
            var runViewIndex = 0;
            for (var arc = 0; arc < 7; arc++)
            {
                var centre = cameraCentre - orbitalOffset + ArenaScreenPoint(
                    0.1f + NearStreamNext(ref orbitalState) * 0.9f,
                    0.05f + NearStreamNext(ref orbitalState) * 0.95f);
                var centrePhase = orbitPhase * (0.28f + arc * 0.035f) + arc * 1.37f;
                centre += new Vector2(Mathf.Cos(centrePhase), Mathf.Sin(centrePhase)) *
                    orbitalShorterAxis * 0.018f;
                var radius = orbitalShorterAxis * (0.22f + NearStreamNext(ref orbitalState) * 0.7f);
                var width = 1f + NearStreamNext(ref orbitalState) * 2.6f;
                var runs = 2 + Mathf.FloorToInt(NearStreamNext(ref orbitalState) * 2f);
                var arcPhase = orbitPhase * (0.78f + (arc % 3) * 0.18f);
                if ((arc & 1) != 0) arcPhase = -arcPhase;
                var angle = NearStreamNext(ref orbitalState) * Mathf.PI * 2f;
                for (var run = 0; run < runs; run++)
                {
                    var span = 0.25f + NearStreamNext(ref orbitalState) * 0.8f;
                    var view = _arenaOrbitViews[runViewIndex++];
                    view.positionCount = 7;
                    for (var point = 0; point < view.positionCount; point++)
                    {
                        var t = point / (float)(view.positionCount - 1);
                        var pointAngle = angle + span * t + arcPhase;
                        view.SetPosition(
                            point,
                            centre + new Vector2(Mathf.Cos(pointAngle), -Mathf.Sin(pointAngle)) * radius);
                    }
                    var alpha = 0.35f + NearStreamNext(ref orbitalState) * 0.55f;
                    var color = new Color(70f / 255f, 64f / 255f, 80f / 255f, 0.22f * alpha);
                    view.startColor = color;
                    view.endColor = color;
                    view.startWidth = width;
                    view.endWidth = width;
                    view.enabled = true;
                    angle += span + 0.2f + NearStreamNext(ref orbitalState) * 0.9f;
                }
            }

            for (var line = 0; line < MaxArenaOrbitFractures; line++)
            {
                var fracturePhase = orbitPhase *
                    (((line & 1) == 0 ? 1f : -1f) * (1.05f + (line % 3) * 0.14f));
                var angle = NearStreamNext(ref orbitalState) * Mathf.PI + fracturePhase;
                var start = cameraCentre - orbitalOffset + ArenaScreenPoint(
                    NearStreamNext(ref orbitalState),
                    NearStreamNext(ref orbitalState));
                var startPhase = fracturePhase * 0.32f + line * 1.11f;
                start += new Vector2(Mathf.Cos(startPhase), Mathf.Sin(startPhase)) *
                    orbitalShorterAxis * 0.014f;
                var length = orbitalShorterAxis * (0.18f + NearStreamNext(ref orbitalState) * 0.5f);
                var end = start + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * length;
                var view = _arenaOrbitFractureViews[line];
                view.positionCount = 2;
                view.SetPosition(0, start);
                view.SetPosition(1, end);
                var alpha = 0.3f + NearStreamNext(ref orbitalState) * 0.4f;
                var color = new Color(70f / 255f, 64f / 255f, 80f / 255f, 0.22f * alpha);
                view.startColor = color;
                view.endColor = color;
                view.startWidth = 0.8f + NearStreamNext(ref orbitalState) * 1.4f;
                view.endWidth = view.startWidth;
                view.enabled = true;
            }
        }
    }
}
