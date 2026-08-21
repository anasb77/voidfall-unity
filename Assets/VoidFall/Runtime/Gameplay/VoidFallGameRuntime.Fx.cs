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

        private void ResetImpactMarkOrder()
        {
            _impactMarkOrderCount = 0;
            for (var index = 0; index < _impactMarkOrderPosition.Length; index++)
            {
                _impactMarkOrder[index] = -1;
                _impactMarkOrderPosition[index] = -1;
            }
        }

        private void AppendImpactMarkOrder(int slot)
        {
            if (slot < 0 || slot >= _impactMarks.Length ||
                _impactMarkOrderCount >= _impactMarkOrder.Length) return;
            var position = _impactMarkOrderPosition[slot];
            if (position >= 0 && position < _impactMarkOrderCount &&
                _impactMarkOrder[position] == slot) return;
            _impactMarkOrderPosition[slot] = _impactMarkOrderCount;
            _impactMarkOrder[_impactMarkOrderCount++] = slot;
        }

        private void RemoveImpactMarkOrder(int slot)
        {
            if (slot < 0 || slot >= _impactMarks.Length) return;
            var position = _impactMarkOrderPosition[slot];
            if (position < 0 || position >= _impactMarkOrderCount ||
                _impactMarkOrder[position] != slot)
            {
                _impactMarkOrderPosition[slot] = -1;
                return;
            }

            var lastPosition = --_impactMarkOrderCount;
            // Browser impactMarks.splice(index, 1) preserves the survivors'
            // order. Shift the tail left instead of using pooled swap-removal.
            for (var index = position; index < lastPosition; index++)
            {
                var replacement = _impactMarkOrder[index + 1];
                _impactMarkOrder[index] = replacement;
                _impactMarkOrderPosition[replacement] = index;
            }
            _impactMarkOrder[lastPosition] = -1;
            _impactMarkOrderPosition[slot] = -1;
        }

        private void EnsureImpactMarkOrderEntries()
        {
            for (var index = 0; index < _impactMarks.Length; index++)
            {
                if (_impactMarks[index].Active) AppendImpactMarkOrder(index);
            }
            for (var order = _impactMarkOrderCount - 1; order >= 0; order--)
            {
                var slot = _impactMarkOrder[order];
                if (slot < 0 || slot >= _impactMarks.Length || !_impactMarks[slot].Active)
                    RemoveImpactMarkOrder(slot);
            }
        }

        private void ResetBlastWaveOrder()
        {
            _blastWaveOrderCount = 0;
            for (var index = 0; index < _blastWaveOrderPosition.Length; index++)
            {
                _blastWaveOrder[index] = -1;
                _blastWaveOrderPosition[index] = -1;
            }
        }

        private void AppendBlastWaveOrder(int slot)
        {
            if (slot < 0 || slot >= _blastWaves.Length ||
                _blastWaveOrderCount >= _blastWaveOrder.Length) return;
            var position = _blastWaveOrderPosition[slot];
            if (position >= 0 && position < _blastWaveOrderCount &&
                _blastWaveOrder[position] == slot) return;
            _blastWaveOrderPosition[slot] = _blastWaveOrderCount;
            _blastWaveOrder[_blastWaveOrderCount++] = slot;
        }

        private void RemoveBlastWaveOrder(int slot)
        {
            if (slot < 0 || slot >= _blastWaves.Length) return;
            var position = _blastWaveOrderPosition[slot];
            if (position < 0 || position >= _blastWaveOrderCount ||
                _blastWaveOrder[position] != slot)
            {
                _blastWaveOrderPosition[slot] = -1;
                return;
            }

            var lastPosition = --_blastWaveOrderCount;
            if (position != lastPosition)
            {
                var replacement = _blastWaveOrder[lastPosition];
                _blastWaveOrder[position] = replacement;
                _blastWaveOrderPosition[replacement] = position;
            }
            _blastWaveOrder[lastPosition] = -1;
            _blastWaveOrderPosition[slot] = -1;
        }

        private void EnsureBlastWaveOrderEntries()
        {
            for (var index = 0; index < _blastWaves.Length; index++)
            {
                if (_blastWaves[index].Active) AppendBlastWaveOrder(index);
            }
            for (var order = _blastWaveOrderCount - 1; order >= 0; order--)
            {
                var slot = _blastWaveOrder[order];
                if (slot < 0 || slot >= _blastWaves.Length || !_blastWaves[slot].Active)
                    RemoveBlastWaveOrder(slot);
            }
        }

        private void ResetDeathGhostOrder()
        {
            _deathGhostOrderCount = 0;
            for (var index = 0; index < _deathGhostOrderPosition.Length; index++)
            {
                _deathGhostOrder[index] = -1;
                _deathGhostOrderPosition[index] = -1;
            }
        }

        private void AppendDeathGhostOrder(int slot)
        {
            if (slot < 0 || slot >= _deathGhosts.Length ||
                _deathGhostOrderCount >= _deathGhostOrder.Length) return;
            var position = _deathGhostOrderPosition[slot];
            if (position >= 0 && position < _deathGhostOrderCount &&
                _deathGhostOrder[position] == slot) return;
            _deathGhostOrderPosition[slot] = _deathGhostOrderCount;
            _deathGhostOrder[_deathGhostOrderCount++] = slot;
        }

        private void RemoveDeathGhostOrder(int slot)
        {
            if (slot < 0 || slot >= _deathGhosts.Length) return;
            var position = _deathGhostOrderPosition[slot];
            if (position < 0 || position >= _deathGhostOrderCount ||
                _deathGhostOrder[position] != slot)
            {
                _deathGhostOrderPosition[slot] = -1;
                return;
            }

            var lastPosition = --_deathGhostOrderCount;
            if (position != lastPosition)
            {
                var replacement = _deathGhostOrder[lastPosition];
                _deathGhostOrder[position] = replacement;
                _deathGhostOrderPosition[replacement] = position;
            }
            _deathGhostOrder[lastPosition] = -1;
            _deathGhostOrderPosition[slot] = -1;
        }

        private void EnsureDeathGhostOrderEntries()
        {
            for (var index = 0; index < _deathGhosts.Length; index++)
            {
                if (_deathGhosts[index].Active) AppendDeathGhostOrder(index);
            }
            for (var order = _deathGhostOrderCount - 1; order >= 0; order--)
            {
                var slot = _deathGhostOrder[order];
                if (slot < 0 || slot >= _deathGhosts.Length || !_deathGhosts[slot].Active)
                    RemoveDeathGhostOrder(slot);
            }
        }

        private void ResetFloaterOrder()
        {
            _floaterOrderCount = 0;
            for (var index = 0; index < _floaterOrderPosition.Length; index++)
            {
                _floaterOrder[index] = -1;
                _floaterOrderPosition[index] = -1;
            }
        }

        private void AppendFloaterOrder(int slot)
        {
            if (slot < 0 || slot >= _floaters.Length ||
                _floaterOrderCount >= _floaterOrder.Length) return;
            var position = _floaterOrderPosition[slot];
            if (position >= 0 && position < _floaterOrderCount &&
                _floaterOrder[position] == slot) return;
            _floaterOrderPosition[slot] = _floaterOrderCount;
            _floaterOrder[_floaterOrderCount++] = slot;
        }

        private void RemoveFloaterOrder(int slot)
        {
            if (slot < 0 || slot >= _floaters.Length) return;
            var position = _floaterOrderPosition[slot];
            if (position < 0 || position >= _floaterOrderCount ||
                _floaterOrder[position] != slot)
            {
                _floaterOrderPosition[slot] = -1;
                return;
            }

            var lastPosition = --_floaterOrderCount;
            if (position != lastPosition)
            {
                var replacement = _floaterOrder[lastPosition];
                _floaterOrder[position] = replacement;
                _floaterOrderPosition[replacement] = position;
            }
            _floaterOrder[lastPosition] = -1;
            _floaterOrderPosition[slot] = -1;
        }

        private void EnsureFloaterOrderEntries()
        {
            for (var index = 0; index < _floaters.Length; index++)
            {
                if (_floaters[index].Active) AppendFloaterOrder(index);
            }
            for (var order = _floaterOrderCount - 1; order >= 0; order--)
            {
                var slot = _floaterOrder[order];
                if (slot < 0 || slot >= _floaters.Length || !_floaters[slot].Active)
                    RemoveFloaterOrder(slot);
            }
        }

        private static bool SourceProjectileTrailEligible(bool homing, bool trailsEnabled, double fxRoll)
        {
            return homing && trailsEnabled && fxRoll < 0.5;
        }

        private void ArcEndpointBurst(HostileTarget endpoint, float damage, int weaponIndex)
        {
            if (!endpoint.Valid) return;
            DamageArea(
                endpoint.Position,
                62f * _areaMultiplier,
                damage * 0.58f,
                endpoint.Boss ? -1 : endpoint.Identity,
                weaponIndex);
            BurstFx(endpoint.Position, SourceDotColor("yellow"), 6, 170, 0.3f, 0.65f);
        }

        private void StartRailTrail(Vector2 start, Vector2 end, float damage, int weaponIndex)
        {
            var slot = SelectRailTrailSlot(_railTrails);
            var trail = new RailTrailState
            {
                Active = true,
                Start = start,
                End = end,
                Life = 2.25f,
                DamageLife = 1.05f,
                Tick = 0,
                Damage = damage,
                WeaponIndex = weaponIndex,
                Sequence = _nextRailTrailSequence++,
                View = slot,
            };
            _railTrails[slot] = trail;
            EnsureRailTrailView(slot);
            RenderRailTrail(slot, trail);
        }

        private static int SelectRailTrailSlot(RailTrailState[] trails)
        {
            var slot = -1;
            var oldestSequence = int.MaxValue;
            for (var index = 0; index < trails.Length; index++)
            {
                if (!trails[index].Active) return index;
                if (trails[index].Sequence < oldestSequence)
                {
                    oldestSequence = trails[index].Sequence;
                    slot = index;
                }
            }
            return slot < 0 ? 0 : slot;
        }

        private void RailgunImpact(Vector2 position, int hitCount)
        {
            var major = hitCount == 1 || hitCount % 3 == 0;
            BurstFx(position, SourceDotColor("white"), major ? 5 : 3, 230, 0.18f, 0.46f);
            if (_qualityPreset.ParticleScale > 0.01f)
                BurstFx(position, SourceDotColor("violet"), major ? 4 : 2, 180, 0.22f, 0.52f);
            if (major)
            {
                SpawnRingWave(
                    position,
                    5f,
                    105f,
                    0.18f,
                    new Color(0.78f, 0.68f, 1f, 0.78f));
            }
            TriggerFreeze(0.014f);
            AddCameraShake(0.022f);
            if (hitCount >= 3 && hitCount % 3 == 0)
            {
                SpawnFloater(
                    position + Vector2.up * 18f,
                    "x" + hitCount,
                    new Color(0.87f, 0.84f, 1f, 1f),
                    9);
            }
        }

        private static bool SourceEnemyVisualFlash(
            string id,
            bool elite,
            int state,
            float hitTimer,
            bool harvesterFull,
            float seed,
            float ambientClock)
        {
            return hitTimer > 0
                || (elite && state == 1 && Mathf.Sin(ambientClock * 30f) > 0)
                || (id == "dasher" && state == 1 && Mathf.Sin(ambientClock * 36f) > 0)
                || (id == "gunner" && state == 1 && Mathf.Sin(ambientClock * 28f) > 0)
                || (id == "twinGunner" && state == 1 && Mathf.Sin(ambientClock * 28f) > 0)
                || (harvesterFull && Mathf.Sin(ambientClock * 9f + seed) > 0.35f);
        }

        private void DetonateExploderBlast(EnemyState enemy, int excludedEnemyIdentity)
        {
            var definition = FindEnemy("exploder");
            var radius = (float)(definition?.BlastRadius ?? 76) * 1.1f;
            var damage = 55f + _time * 0.25f;

            SpawnBlastWave(enemy.Position, radius, 0.42f, false);
            BurstFx(enemy.Position, SourceDotColor("orange"), 16, 300, 0.5f, 0.85f);
            BurstFx(enemy.Position, SourceDotColor("yellow"), 8, 220, 0.4f, 0.7f);
            AddCameraShake(0.22f);
            _audio?.Play(ProceduralAudio.Cue.ExploderBlast, 0.86f);

            // Keep the source's strict circle edge and copied-array behavior.
            // Recursive chain kills can remove enemies and reuse pooled slots,
            // so the snapshot also carries SpawnId identity for each target.
            var enemySnapshot = CaptureEnemyEffectSnapshot(out var enemySnapshotCount);
            try
            {
                for (var target = 0; target < enemySnapshotCount; target++)
                {
                    var snapshot = enemySnapshot[target];
                    if (EnemyIdentity(snapshot.State, snapshot.Slot) == excludedEnemyIdentity ||
                        !IsLiveEnemyEffectTarget(snapshot)) continue;
                    var other = snapshot.State;
                    var delta = other.Position - enemy.Position;
                    var reach = radius + other.Radius;
                    if (delta.sqrMagnitude >= reach * reach) continue;
                    ApplyEnemyDamage(snapshot.Slot, damage, delta, 240, false, -1);
                }
            }
            finally
            {
                ReleaseEnemyEffectSnapshot(enemySnapshot);
            }

            EnsureBossOrderEntries();
            for (var bossOrder = 0; bossOrder < _bossOrderCount; bossOrder++)
            {
                var index = _bossOrder[bossOrder];
                var boss = _bosses[index];
                if (!boss.Active || boss.State == 4) continue;
                var delta = boss.Position - enemy.Position;
                if (delta.magnitude >= radius + boss.Radius) continue;
                ApplyBossDamage(index, damage * 0.5f, -1, false);
            }
        }

        private static int SourceFloaterFontSize(float size)
        {
            return Mathf.Max(10, Mathf.RoundToInt(size * SourceFloatingTextScale));
        }

        private static int SelectDeathGhostSlot(DeathGhostState[] ghosts)
        {
            for (var index = 0; index < ghosts.Length; index++)
                if (!ghosts[index].Active) return index;
            return -1;
        }

        private void AddCameraShake(float amount)
        {
            if (_saveData?.settings == null || _saveData.settings.reducedMotion) return;
            var scale = Mathf.Clamp01(_saveData.settings.shake);
            if (scale <= 0 || amount <= 0) return;
            _cameraTrauma = Mathf.Clamp01(_cameraTrauma + amount * scale);
        }

        private void TriggerFreeze(float seconds)
        {
            // Hitstop durations are authored gameplay, not presentation. A
            // previous revision scaled these to 0.4x and capped them at 0.035s
            // to "prevent perceived stutter", which silently cut the protective
            // pause the source grants on every event. The per-hit case went from
            // 3 frozen steps (50ms) to 1 (17ms), and boss kills from 150ms to
            // 50ms, so the player lost most of the read-and-reposition beat and
            // the game played measurably harder than the browser build.
            //
            // A freeze sets the whole simulation step to dt = 0, so enemy
            // movement, contact cooldowns and i-frame drain all halt together.
            // Shortening it is a difficulty change, not a smoothing tweak. If
            // frame pacing needs work it belongs in the presentation layer,
            // which already treats _freezeTimer > 0 as FX speed zero.
            _freezeTimer = Mathf.Max(_freezeTimer, Mathf.Max(0f, seconds));
        }

        private static int SourceParticleLimit(float particleScale)
        {
            return Mathf.Max(24, Mathf.RoundToInt(MaxSourceParticles * Mathf.Max(0, particleScale)));
        }

        private void TrimSourceParticleViews(int maximum)
        {
            var active = 0;
            for (var index = 0; index < _sourceParticles.Length; index++)
                if (_sourceParticles[index].Active) active++;
            for (var order = _sourceFxOrderCount - 1; order >= 0 && active > maximum; order--)
            {
                if ((SourceFxKind)_sourceFxOrderKind[order] != SourceFxKind.Particle) continue;
                var slot = _sourceFxOrderSlot[order];
                if (slot < 0 || slot >= _sourceParticles.Length || !_sourceParticles[slot].Active) continue;
                var particle = _sourceParticles[slot];
                particle.Active = false;
                _sourceParticles[slot] = particle;
                RemoveSourceFxOrder(SourceFxKind.Particle, slot);
                Hide(_sourceParticleViews[slot]);
                active--;
            }
        }

        private static float SourceBurstParticleSize(int count, float speed, float lifetime)
        {
            if (Mathf.Approximately(speed, 24f) && Mathf.Approximately(lifetime, 0.26f)) return 0.6f;
            if (Mathf.Approximately(speed, 150f) && Mathf.Approximately(lifetime, 0.35f)) return 0.62f;
            if (Mathf.Approximately(speed, 120f) && Mathf.Approximately(lifetime, 0.32f)) return 0.58f;
            if (Mathf.Approximately(speed, 90f) && Mathf.Approximately(lifetime, 0.24f)) return 0.48f;
            if (Mathf.Approximately(speed, 320f) && Mathf.Approximately(lifetime, 0.54f)) return 0.86f;
            if (Mathf.Approximately(speed, 240f) && Mathf.Approximately(lifetime, 0.36f)) return 0.72f;
            if (Mathf.Approximately(speed, 150f) && Mathf.Approximately(lifetime, 0.2f)) return 0.52f;
            if (Mathf.Approximately(speed, 260f) && Mathf.Approximately(lifetime, 0.24f)) return 0.62f;
            if (Mathf.Approximately(speed, 340f) && Mathf.Approximately(lifetime, 0.58f)) return 0.95f;
            if (Mathf.Approximately(speed, 250f) && Mathf.Approximately(lifetime, 0.46f)) return 0.78f;
            if (Mathf.Approximately(speed, 320f) && Mathf.Approximately(lifetime, 0.5f)) return 0.9f;
            if (Mathf.Approximately(speed, 240f) && Mathf.Approximately(lifetime, 0.34f)) return 0.68f;
            if (Mathf.Approximately(speed, 150f) && Mathf.Approximately(lifetime, 0.18f)) return 0.46f;
            if (Mathf.Approximately(lifetime, 0.4f)) return 0.7f;
            if (Mathf.Approximately(speed, 190f) && Mathf.Approximately(lifetime, 0.35f)) return 0.7f;
            if (Mathf.Approximately(lifetime, 0.25f) && speed < 100f) return 0.46f;
            if (Mathf.Approximately(lifetime, 0.25f)) return 0.6f;
            if (Mathf.Approximately(speed, 130f) && Mathf.Approximately(lifetime, 0.28f)) return 0.65f;
            if (Mathf.Approximately(lifetime, 0.45f)) return 0.8f;
            if (Mathf.Approximately(speed, 260f) && Mathf.Approximately(lifetime, 0.5f)) return 0.8f;
            if (Mathf.Approximately(speed, 180f) && Mathf.Approximately(lifetime, 0.3f)) return 0.65f;
            if (Mathf.Approximately(speed, 150f) && Mathf.Approximately(lifetime, 0.3f)) return 0.65f;
            if (Mathf.Approximately(speed, 170f) && Mathf.Approximately(lifetime, 0.3f)) return 0.65f;
            if (Mathf.Approximately(speed, 220f) && Mathf.Approximately(lifetime, 0.42f)) return 0.72f;
            if (Mathf.Approximately(speed, 360f) && Mathf.Approximately(lifetime, 0.42f)) return 0.82f;
            if (Mathf.Approximately(speed, 230f) && Mathf.Approximately(lifetime, 0.18f)) return 0.46f;
            if (Mathf.Approximately(speed, 180f) && Mathf.Approximately(lifetime, 0.22f)) return 0.52f;
            if (Mathf.Approximately(speed, 110f) && Mathf.Approximately(lifetime, 0.22f)) return 0.5f;
            if (Mathf.Approximately(speed, 220f) && Mathf.Approximately(lifetime, 0.4f)) return 0.7f;
            if (Mathf.Approximately(speed, 290f) && Mathf.Approximately(lifetime, 0.4f)) return 0.7f;
            if (Mathf.Approximately(speed, 390f) && Mathf.Approximately(lifetime, 0.9f)) return 1.2f;
            if (Mathf.Approximately(speed, 380f) && Mathf.Approximately(lifetime, 0.72f)) return 1f;
            if (Mathf.Approximately(speed, 260f) && Mathf.Approximately(lifetime, 0.65f)) return 0.85f;
            if (Mathf.Approximately(speed, 300f) && Mathf.Approximately(lifetime, 0.65f)) return 0.9f;
            if (Mathf.Approximately(speed, 390f) && Mathf.Approximately(lifetime, 0.78f)) return 1f;
            if (Mathf.Approximately(speed, 150f) && Mathf.Approximately(lifetime, 0.55f)) return 0.55f;
            return DefaultBurstParticleSize;
        }

        private int FindSourceParticleSlot()
        {
            for (var index = 0; index < _sourceParticles.Length; index++)
                if (!_sourceParticles[index].Active) return index;
            return -1;
        }

        private void BurstFx(
            Vector2 position,
            Color color,
            int count,
            float speed,
            float lifetime,
            float explicitParticleSize = -1f)
        {
            if (_fx == null) return;
            var sourceColor = SourceParticleTint(color);
            var particleSize = explicitParticleSize > 0
                ? explicitParticleSize
                : SourceBurstParticleSize(count, speed, lifetime);
            count = Mathf.CeilToInt(count * _qualityPreset.ParticleScale);
            if (_saveData?.settings != null && _saveData.settings.reducedMotion)
                count = Mathf.CeilToInt(count * 0.35f);
            if (count <= 0) return;
            var emitCount = Mathf.Min(
                count,
                Mathf.Max(0, SourceParticleLimit(_qualityPreset.ParticleScale) - ActiveFxVisualCount()));
            for (var index = 0; index < count; index++)
            {
                var angle = (float)(_fxRng.Next() * Math.PI * 2);
                var velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) *
                    (speed * (0.3f + (float)_fxRng.Next() * 0.9f));
                // Browser singleParticle() still consumes the complete FX RNG
                // tuple when the cosmetic budget is full; only insertion is
                // dropped. Keep that stream behavior without emitting past
                // Unity's visual cap.
                if (index >= emitCount)
                {
                    _fxRng.Next();
                    _fxRng.Next();
                    continue;
                }
                var lifeMultiplier = 0.6f + (float)_fxRng.Next() * 0.7f;
                var sizeMultiplier = 0.6f + (float)_fxRng.Next() * 0.8f;
                var emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = velocity,
                    startColor = sourceColor,
                    startLifetime = lifetime * lifeMultiplier,
                    startSize = 24f * particleSize * sizeMultiplier,
                };
                _fx.Emit(emit, 1);
                SpawnSourceParticle(
                    position,
                    new Vector2(velocity.x, velocity.y),
                    emit.startLifetime,
                    particleSize * sizeMultiplier,
                    sourceColor);
            }
        }

        private void EmitTrailParticle(Vector2 position, Vector2 velocity, Color color, float lifetime, float size)
        {
            if (_fx == null || _qualityPreset.ParticleScale <= 0.01f) return;
            if (ActiveFxVisualCount() >= SourceParticleLimit(_qualityPreset.ParticleScale)) return;
            var sourceColor = SourceParticleTint(color);
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = sourceColor,
                startLifetime = lifetime,
                startSize = 24f * size,
            };
            _fx.Emit(emit, 1);
            SpawnSourceParticle(position, velocity, lifetime, size, sourceColor);
        }

        private static Color SourceParticleTint(Color color)
        {
            // Browser dot sprites bake their own alpha stops (0.95 centre,
            // 0.9 middle, transparent edge). Burst/trail particles apply only
            // the lifetime fade, so incoming event alpha must not multiply the
            // sprite profile a second time. Ring waves use a different path and
            // retain their authored opacity.
            return new Color(color.r, color.g, color.b, 1f);
        }

        private void ApplySourceParticleDrag(float dt)
        {
            if (_fx == null || dt <= 0) return;
            var count = _fx.GetParticles(_fxParticleScratch);
            if (count <= 0) return;
            var decay = Mathf.Exp(-MeteorShardDrag * Mathf.Clamp(dt, 0, 0.1f));
            for (var index = 0; index < count; index++)
                _fxParticleScratch[index].velocity *= decay;
            _fx.SetParticles(_fxParticleScratch, count);
        }

        private SpriteRenderer EnsureSourceParticleView(int index)
        {
            if (_sourceParticleViews[index] != null) return _sourceParticleViews[index];
            _sourceParticleViews[index] = CreateView(
                "Source Particle_" + index,
                ProceduralSpriteFactory.ParticleDot(),
                40);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) _sourceParticleViews[index].sharedMaterial = additiveMaterial;
            return _sourceParticleViews[index];
        }

        private SpriteRenderer EnsureImpactMarkView(int index)
        {
            if (_impactMarkViews[index] != null) return _impactMarkViews[index];
            _impactMarkViews[index] = CreateView("Impact Mark_" + index, ProceduralSpriteFactory.ImpactMark(), 3);
            return _impactMarkViews[index];
        }

        private static int ImpactHeatSlot(int markIndex, int segment)
        {
            return markIndex * ImpactHeatSegmentCount + segment;
        }

        private LineRenderer EnsureImpactHeatView(int markIndex, int segment)
        {
            var slot = ImpactHeatSlot(markIndex, segment);
            if (_impactHeatViews[slot] != null) return _impactHeatViews[slot];
            _impactHeatViews[slot] = CreateLineView("Impact Heat_" + markIndex + "_" + segment, 4);
            return _impactHeatViews[slot];
        }

        private LineRenderer EnsureEnemyTelegraphRingView(int index)
        {
            if (_enemyTelegraphRingViews[index] != null) return _enemyTelegraphRingViews[index];
            _enemyTelegraphRingViews[index] = CreateLineView("Enemy Telegraph Ring_" + index, 11);
            return _enemyTelegraphRingViews[index];
        }

        private LineRenderer EnsureEnemyTelegraphLineView(int index)
        {
            if (_enemyTelegraphLineViews[index] != null) return _enemyTelegraphLineViews[index];
            _enemyTelegraphLineViews[index] = CreateLineView("Enemy Telegraph Line_" + index, 12);
            return _enemyTelegraphLineViews[index];
        }

        private LineRenderer EnsureEnemyTelegraphSecondaryLineView(int index)
        {
            if (_enemyTelegraphSecondaryLineViews[index] != null)
                return _enemyTelegraphSecondaryLineViews[index];
            _enemyTelegraphSecondaryLineViews[index] = CreateLineView(
                "Enemy Telegraph Secondary Line_" + index,
                12);
            return _enemyTelegraphSecondaryLineViews[index];
        }

        private LineRenderer EnsureEnemyTelegraphTertiaryLineView(int index)
        {
            if (_enemyTelegraphTertiaryLineViews[index] != null)
                return _enemyTelegraphTertiaryLineViews[index];
            _enemyTelegraphTertiaryLineViews[index] = CreateLineView(
                "Enemy Telegraph Tertiary Line_" + index,
                12);
            return _enemyTelegraphTertiaryLineViews[index];
        }

        private SpriteRenderer EnsureEnemyTelegraphExploderFillView(int index)
        {
            if (_enemyTelegraphExploderFillViews[index] != null)
                return _enemyTelegraphExploderFillViews[index];
            _enemyTelegraphExploderFillViews[index] = CreateView(
                "Enemy Exploder Telegraph Fill_" + index,
                ProceduralSpriteFactory.Circle(),
                11);
            return _enemyTelegraphExploderFillViews[index];
        }

        private LineRenderer EnsureEnemyTelegraphExploderSegmentView(int index, int segment)
        {
            var slot = index * ExploderTelegraphSegmentCount + segment;
            if (_enemyTelegraphExploderSegmentViews[slot] != null)
                return _enemyTelegraphExploderSegmentViews[slot];
            _enemyTelegraphExploderSegmentViews[slot] = CreateLineView(
                "Enemy Exploder Telegraph Segment_" + index + "_" + segment,
                12);
            return _enemyTelegraphExploderSegmentViews[slot];
        }

        private MeshFilter EnsureEnemyTelegraphSiegeDashView(int index)
        {
            if (_enemyTelegraphSiegeDashViews[index] != null)
                return _enemyTelegraphSiegeDashViews[index];
            _enemyTelegraphSiegeDashViews[index] = CreateMeshView(
                "Enemy Siege Mortar Dashed Ring_" + index,
                11,
                out _enemyTelegraphSiegeDashRenderers[index]);
            _enemyTelegraphSiegeDashVertices[index] = new List<Vector3>(128);
            _enemyTelegraphSiegeDashTriangles[index] = new List<int>(192);
            _enemyTelegraphSiegeDashColors[index] = new List<Color>(128);
            return _enemyTelegraphSiegeDashViews[index];
        }

        private SpriteRenderer EnsureEnemyTelegraphMortarFillView(int index)
        {
            if (_enemyTelegraphMortarFillViews[index] != null)
                return _enemyTelegraphMortarFillViews[index];
            _enemyTelegraphMortarFillViews[index] = CreateView(
                "Enemy Mortar Telegraph Fill_" + index,
                ProceduralSpriteFactory.Circle(),
                11);
            return _enemyTelegraphMortarFillViews[index];
        }

        private LineRenderer EnsureEnemyTelegraphMortarSegmentView(int index, int segment)
        {
            var slot = index * MortarTelegraphSegmentCount + segment;
            if (_enemyTelegraphMortarSegmentViews[slot] != null)
                return _enemyTelegraphMortarSegmentViews[slot];
            _enemyTelegraphMortarSegmentViews[slot] = CreateLineView(
                "Enemy Mortar Telegraph Segment_" + index + "_" + segment,
                12);
            return _enemyTelegraphMortarSegmentViews[slot];
        }

        private MeshFilter EnsureEnemyTelegraphFillView(int index)
        {
            if (_enemyTelegraphFillViews[index] != null) return _enemyTelegraphFillViews[index];
            _enemyTelegraphFillViews[index] = CreateMeshView(
                "Enemy Telegraph Fill_" + index,
                11,
                out _enemyTelegraphFillRenderers[index]);
            _enemyTelegraphFillBuffers[index] = new TelegraphQuadBuffer();
            return _enemyTelegraphFillViews[index];
        }

        private MeshFilter EnsureEnemyTelegraphArrowFillView(int index)
        {
            if (_enemyTelegraphArrowFillViews[index] != null) return _enemyTelegraphArrowFillViews[index];
            _enemyTelegraphArrowFillViews[index] = CreateMeshView(
                "Enemy Telegraph Arrow Fill_" + index,
                12,
                out _enemyTelegraphArrowFillRenderers[index]);
            _enemyTelegraphArrowFillBuffers[index] = new TelegraphQuadBuffer();
            return _enemyTelegraphArrowFillViews[index];
        }

        private void SetBossTelegraphMesh(int index)
        {
            if (_bossTelegraphVertices[index].Count == 0) return;
            var view = EnsureBossTelegraphFillView(index);
            var mesh = view.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(_bossTelegraphVertices[index]);
            mesh.SetTriangles(_bossTelegraphTriangles[index], 0);
            mesh.SetColors(_bossTelegraphColors[index]);
            mesh.RecalculateBounds();
            _bossTelegraphFillRenderers[index].enabled = true;
        }

        private static Vector2 TelegraphPoint(
            Vector2 centre,
            Vector2 direction,
            Vector2 normal,
            float forward,
            float lateral)
        {
            return centre + direction * forward + normal * lateral;
        }

        private static void SetTelegraphMesh(
            MeshFilter view,
            MeshRenderer renderer,
            TelegraphQuadBuffer buffer,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color color,
            bool triangle)
        {
            if (view == null || renderer == null || buffer == null) return;
            buffer.Vertices[0] = new Vector3(a.x, a.y, 0);
            buffer.Vertices[1] = new Vector3(b.x, b.y, 0);
            buffer.Vertices[2] = new Vector3(c.x, c.y, 0);
            buffer.Vertices[3] = new Vector3(d.x, d.y, 0);
            for (var index = 0; index < buffer.Colors.Length; index++)
                buffer.Colors[index] = color;

            var mesh = view.sharedMesh;
            mesh.Clear();
            mesh.vertices = buffer.Vertices;
            mesh.colors = buffer.Colors;
            mesh.triangles = triangle ? TriangleIndices : QuadTriangles;
            mesh.RecalculateBounds();
            renderer.enabled = true;
        }

        private MeshFilter EnsureBossTelegraphFillView(int index)
        {
            if (_bossTelegraphFillViews[index] != null) return _bossTelegraphFillViews[index];
            _bossTelegraphFillViews[index] = CreateMeshView(
                "Boss Telegraph Fill_" + index,
                23,
                out _bossTelegraphFillRenderers[index]);
            return _bossTelegraphFillViews[index];
        }

        private LineRenderer EnsureBossTelegraphOutlineView(int index)
        {
            if (_bossTelegraphOutlineViews[index] != null) return _bossTelegraphOutlineViews[index];
            _bossTelegraphOutlineViews[index] = CreateLineView("Boss Telegraph Outline_" + index, 24);
            return _bossTelegraphOutlineViews[index];
        }

        private LineRenderer EnsureRingWaveView(int index)
        {
            if (_ringWaveViews[index] != null) return _ringWaveViews[index];
            _ringWaveViews[index] = CreateLineView("Ring Wave_" + index, 40);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) _ringWaveViews[index].sharedMaterial = additiveMaterial;
            return _ringWaveViews[index];
        }

        private LineRenderer EnsureRingWaveGlowView(int index)
        {
            if (_ringWaveGlowViews[index] != null) return _ringWaveGlowViews[index];
            _ringWaveGlowViews[index] = CreateLineView("Ring Wave Glow_" + index, 40);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) _ringWaveGlowViews[index].sharedMaterial = additiveMaterial;
            return _ringWaveGlowViews[index];
        }

        private SpriteRenderer EnsureRingWaveSpriteView(int index)
        {
            if (_ringWaveSpriteViews[index] != null) return _ringWaveSpriteViews[index];
            _ringWaveSpriteViews[index] = CreateView(
                "Ring Wave Sprite_" + index,
                ProceduralSpriteFactory.Ring(),
                40);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null)
                _ringWaveSpriteViews[index].sharedMaterial = additiveMaterial;
            return _ringWaveSpriteViews[index];
        }

        private void EnsureBlastWaveViews(int index)
        {
            if (_blastWaveFillViews[index] != null) return;
            _blastWaveFillViews[index] = CreateView(
                "Blast Wave Fill_" + index,
                ProceduralSpriteFactory.BlastWaveDisc(),
                41);
            _blastWaveRimViews[index] = CreateLineView("Blast Wave Rim_" + index, 42);
            _blastWaveArcViews[index] = CreateLineView("Blast Wave Arc_" + index, 42);

            var screenMaterial = ResolveBlastWaveScreenMaterial();
            if (screenMaterial != null)
            {
                _blastWaveFillViews[index].sharedMaterial = screenMaterial;
                _blastWaveRimViews[index].sharedMaterial = screenMaterial;
                _blastWaveArcViews[index].sharedMaterial = screenMaterial;
            }
        }

        private SpriteRenderer EnsureHollowBladeTrailView(bool near)
        {
            var view = near ? _hollowBladeNearView : _hollowBladeFarView;
            if (view != null) return view;
            view = CreateView(
                near ? "Hollow Blade Afterimage Near" : "Hollow Blade Afterimage Far",
                ProceduralSpriteFactory.Blade(true),
                near ? 29 : 28);
            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            if (additiveMaterial != null) view.sharedMaterial = additiveMaterial;
            view.color = new Color(0.37f, 0.9f, 0.82f, near ? 0.2f : 0.1f);
            if (near) _hollowBladeNearView = view;
            else _hollowBladeFarView = view;
            return view;
        }

        private MeshRenderer EnsureRailTrailView(int index)
        {
            if (_railTrailViews[index] != null) return _railTrailViews[index];
            var meshView = CreateMeshView("Rail Wake_" + index, 26, out var renderer);
            _railTrailMeshViews[index] = meshView;
            _railTrailVertices[index] = new Vector3[RailTrailSegmentCount * 4];
            _railTrailColors[index] = new Color[RailTrailSegmentCount * 4];
            _railTrailTriangles[index] = new int[RailTrailSegmentCount * 6];
            for (var segment = 0; segment < RailTrailSegmentCount; segment++)
            {
                var vertex = segment * 4;
                var triangle = segment * 6;
                _railTrailTriangles[index][triangle] = vertex;
                _railTrailTriangles[index][triangle + 1] = vertex + 1;
                _railTrailTriangles[index][triangle + 2] = vertex + 2;
                _railTrailTriangles[index][triangle + 3] = vertex;
                _railTrailTriangles[index][triangle + 4] = vertex + 2;
                _railTrailTriangles[index][triangle + 5] = vertex + 3;
            }

            var additiveMaterial = ResolveAdditiveSpriteMaterial();
            renderer.sharedMaterial = additiveMaterial;
            renderer.enabled = false;
            _railTrailViews[index] = renderer;
            return renderer;
        }
    }
}
