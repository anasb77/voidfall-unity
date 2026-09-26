using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private struct SpikyGrowth
        {
            public int Identity, Contacts;
            public float Delta, Displacement, ReportAt;
        }
        private readonly SpikyGrowth[] _spikyGrowth = new SpikyGrowth[MaxEnemies];
        private struct SpikyBurst { public Vector2 Position; public float Damage, Delay; public int Identity; }
        private readonly SpikyBurst[] _spikyBursts = new SpikyBurst[MaxEnemies];
        private int _spikyBurstHead, _spikyBurstCount;
        private float _secondWindRemaining, _arrivalGrace, _rosterIntroductionReadyAt;
        private bool _circleBeat;
        private float _nextLegacySwarmAt;
        private int _legacySwarmSequence;
        private int _sharedDirectorVisitIndex = -1;
        private string _sharedDirectorVisitKey;
        private readonly int[] _legacyRushIdentities = new int[MaxEnemies];
        private readonly string[] _restorationChoiceCandidates = new string[17];
        private readonly bool[] _restorationIntroduced = new bool[17];
        private readonly float[] _restorationIntroducedAt = new float[17];
        private static readonly string[] RestorationRoster = { "chaser", "runner", "swarmer", "gunner", "dasher", "shuriken", "brute", "exploder", "spiky", "guard", "technician", "twinGunner", "splitter", "mortar", "bulwark", "harvester", "carrier" };
        private static readonly string[] BasicFallbacks = { "runner", "swarmer", "chaser" };
        private static readonly string[] PursuitFallbacks = { "runner", "gunner", "chaser" };
        private static readonly string[] FlankFallbacks = { "gunner", "runner", "dasher", "chaser" };
        private static readonly string[] HuntFallbacks = { "guard", "runner", "chaser" };
        private static readonly string[] BreakthroughFallbacks = { "guard", "runner", "chaser" };

        private bool IsSharedDirectorArena()
        {
            return _arenaId == ArenaId.Void || _arenaId == ArenaId.RedNebula ||
                _arenaId == ArenaId.WhiteSakura || _arenaId == ArenaId.EonSea ||
                _arenaId == ArenaId.Crascendo;
        }

        private void TrackSharedDirectorVisit()
        {
            if (!IsSharedDirectorArena()) return;
            var key = CurrentVoidId ?? ArenaIdName(_arenaId);
            if (string.Equals(_sharedDirectorVisitKey, key, StringComparison.Ordinal)) return;
            _sharedDirectorVisitKey = key;
            _sharedDirectorVisitIndex = Mathf.Max(0, _sharedDirectorVisitIndex + 1);
        }

        private double SharedRevealSeconds(string id)
        {
            return LegacyRestorationRules.SharedRevealSeconds(id, _sharedDirectorVisitIndex);
        }

        private string ChooseEligibleRestorationFamily(string requested, string[] fallbacks)
        {
            var selected = requested;
            if (!RestorationTypeIntroduced(selected) || !AmbientTypeAllowed(selected))
            {
                selected = null;
                for (var i = 0; i < fallbacks.Length; i++)
                {
                    var candidate = fallbacks[i];
                    if (RestorationTypeIntroduced(candidate) && AmbientTypeAllowed(candidate))
                    { selected = candidate; break; }
                }
                if (selected == null) selected = "chaser";
                if (!string.Equals(selected, requested, StringComparison.Ordinal))
                    RecordRunHistory("spawn_substituted", selected, "family_limit", sourceId: requested,
                        detail: "sharedStage=" + _sharedDirectorVisitIndex);
            }
            return selected;
        }

        private void ResetLegacyRestoration()
        {
            _secondWindRemaining = _arrivalGrace = _rosterIntroductionReadyAt = 0;
            _spikyBurstHead = _spikyBurstCount = 0;
            Array.Clear(_spikyGrowth, 0, _spikyGrowth.Length);
            _circleBeat = false;
            _nextLegacySwarmAt = 30f;
            _legacySwarmSequence = 0;
            _sharedDirectorVisitIndex = -1;
            _sharedDirectorVisitKey = null;
            Array.Clear(_legacyRushIdentities, 0, _legacyRushIdentities.Length);
            Array.Clear(_restorationIntroduced, 0, _restorationIntroduced.Length);
            Array.Clear(_restorationIntroducedAt, 0, _restorationIntroducedAt.Length);
        }
        private void StepLegacyRestoration(float dt)
        {
            _arrivalGrace = Mathf.Max(0, _arrivalGrace - dt);
            _secondWindRemaining = Mathf.Max(0, _secondWindRemaining - dt);
            TrySecondWind();
            // Snapshot the queue so children never detonate in their parent's simulation tick.
            var initial = _spikyBurstCount;
            var detonated = 0;
            for (var i = 0; i < initial; i++)
            {
                var burst = _spikyBursts[_spikyBurstHead];
                _spikyBurstHead = (_spikyBurstHead + 1) % _spikyBursts.Length;
                _spikyBurstCount--;
                burst.Delay -= dt;
                if (burst.Delay > 0 || detonated >= 8) { EnqueueSpikyBurst(burst); continue; }
                detonated++;
                using (new FactionScope(this, -1, 0, CombatFaction.Player, 0))
                    DamageArea(burst.Position, 85, burst.Damage, -1);
                SpawnRingWave(burst.Position, 8, 85, .24f, new Color(.75f, .52f, 1f, .8f));
                BurstFx(burst.Position, SourceDotColor("violet"), 8, 160, .3f, .7f);
                RecordRunHistory("spiky_chain_burst", "spiky", instanceId: burst.Identity, amount: burst.Damage, detail: "radius=85;playerSafe=true");
            }
        }
        private void TrySecondWind()
        {
            if (SupportRank("secondWind") <= 0 || !LegacyRestorationRules.CanHeal(_gameSim.Player.Health, _gameSim.Player.MaxHealth, _level, _secondWindRemaining)) return;
            var before = _gameSim.Player.Health;
            _gameSim.Player.Health = Mathf.Min(_gameSim.Player.MaxHealth, before + _level);
            _secondWindRemaining = (float)LegacyRestorationRules.SecondWindCooldown;
            RecordRunHistory("second_wind", "secondWind", amount: _gameSim.Player.Health - before, hp: _gameSim.Player.Health,
                maxHp: _gameSim.Player.MaxHealth, durationSeconds: _secondWindRemaining, detail: "level=" + _level);
            SpawnFloater(_gameSim.Player.Position + Vector2.up * 25, "SECOND WIND +" + Mathf.CeilToInt(_gameSim.Player.Health - before), new Color(.5f, 1, .7f), 14);
        }
        private void EnqueueSpikyBurst(SpikyBurst burst)
        {
            if (_spikyBurstCount >= _spikyBursts.Length)
            { RecordRunHistory("spiky_chain_rejected", "spiky", "queue_full", instanceId: burst.Identity); return; }
            _spikyBursts[(_spikyBurstHead + _spikyBurstCount) % _spikyBursts.Length] = burst;
            _spikyBurstCount++;
        }
        private void QueueSpikyDeath(EnemyState enemy)
        {
            if (enemy.Id != "spiky") return;
            FlushSpikyGrowth(enemy.View);
            if (JourneyStopsCombat) return;
            EnqueueSpikyBurst(new SpikyBurst { Position = enemy.Position, Identity = enemy.SpawnId, Delay = .075f, Damage = enemy.MaxHealth * 1.05f });
        }
        private bool UpdateLegacyEnemy(ref EnemyState enemy, Vector2 direction)
        {
            if (!LegacyRestorationRules.IsNewEnemy(enemy.Id)) return false;
            if (enemy.Id == "shuriken")
            {
                var lateral = new Vector2(-direction.y, direction.x);
                enemy.Velocity = (direction + lateral * Mathf.Sin(enemy.Age * 2.1f + enemy.Seed) * .85f).normalized * enemy.Speed;
                enemy.Rotation = enemy.Age * LegacyRestorationRules.ShurikenSpinRadians;
            }
            else
            {
                enemy.Velocity = direction * enemy.Speed;
                if (enemy.Id == "spiky")
                {
                    var phase = Mathf.Repeat(enemy.Age + (enemy.SpawnId % 17) * .117f, LegacyRestorationRules.SpikyPhaseSeconds * 2);
                    var scale = phase < LegacyRestorationRules.SpikyPhaseSeconds ? Mathf.Lerp(LegacyRestorationRules.SpikyExpandedScale, 1, Mathf.SmoothStep(0, 1, phase / .12f))
                        : Mathf.Lerp(1, LegacyRestorationRules.SpikyExpandedScale, Mathf.SmoothStep(0, 1, (phase - LegacyRestorationRules.SpikyPhaseSeconds) / .12f));
                    var radius = LegacyRestorationRules.SpikyBaseRadius * scale;
                    ref var growth = ref _spikyGrowth[enemy.View];
                    if (growth.Identity != enemy.SpawnId)
                        growth = new SpikyGrowth { Identity = enemy.SpawnId, ReportAt = _time };
                    growth.Delta = Mathf.Max(0, radius - enemy.Radius);
                    enemy.Radius = radius;
                }
            }
            return true;
        }
        // Expansion sweeps nearby bodies outward. Shrinking never pulls them back.
        // Runs once after movement, before ordinary separation; fixed storage and no combat RNG.
        private void ApplySpikyGrowthPushes()
        {
            var gridReady = false;
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var slot = _gameSim.EnemyOrder[order];
                var source = _gameSim.Enemies[slot];
                if (!source.Active || source.Id != "spiky") continue;
                ref var growth = ref _spikyGrowth[slot];
                if (growth.Identity != source.SpawnId) continue;
                var expansion = growth.Delta; growth.Delta = 0;
                if (expansion > 0)
                {
                    if (!gridReady) { RebuildEnemyGrid(); gridReady = true; }
                    var count = _gameSim.QueryEnemyNeighborhood(source.Position.x, source.Position.y, 2, _gameSim.EnemyGridSeparationCandidates);
                    for (var candidate = 0; candidate < count; candidate++)
                    {
                        var otherSlot = _gameSim.EnemyGridSeparationCandidates[candidate];
                        ref var other = ref _gameSim.Enemies[otherSlot];
                        if (otherSlot == slot || !other.Active || other.MatriarchBodyguard || IsCourtSentinel(other) ||
                            (_gameSim.EnemyAnchoredQuery != null && _gameSim.EnemyAnchoredQuery(other))) continue;
                        var delta = other.Position - source.Position;
                        var distance = delta.magnitude;
                        var reach = source.Radius + other.Radius;
                        if (distance >= reach) continue;
                        var axis = unchecked(source.SpawnId * 73856093 ^ other.SpawnId * 19349663) & 3;
                        var direction = distance > .001f ? delta / distance :
                            axis == 0 ? Vector2.right : axis == 1 ? Vector2.up : axis == 2 ? Vector2.left : Vector2.down;
                        var displacement = Mathf.Min(reach - distance, expansion * 1.5f);
                        var oldPosition = other.Position;
                        other.Position += direction * displacement;
                        ConstrainNullCityEnemy(ref other); ConstrainCourtEnemy(ref other);
                        ResolveEonSeaEnemyMovement(otherSlot, ref other, oldPosition);
                        var moved = (other.Position - oldPosition).magnitude;
                        if (moved <= .001f) continue;
                        // Preserve stronger weapon knockback; cap only the added growth shove.
                        var impulse = direction * Mathf.Min(120, displacement * 8);
                        other.Knockback = Vector2.ClampMagnitude(other.Knockback + impulse, Mathf.Max(220, other.Knockback.magnitude));
                        growth.Contacts++; growth.Displacement += moved;
                    }
                }
                if (_time >= growth.ReportAt) FlushSpikyGrowth(slot);
            }
        }

        private void FlushSpikyGrowth(int slot)
        {
            ref var growth = ref _spikyGrowth[slot];
            var source = _gameSim.Enemies[slot];
            if (growth.Identity != source.SpawnId || growth.Contacts <= 0) return;
            RecordRunHistory("spiky_growth_push", "spiky", instanceId: source.SpawnId,
                amount: growth.Displacement, position: source.Position,
                detail: "contactSteps=" + growth.Contacts + ";outwardOnly=true;maxAddedSpeed=220");
            growth.Contacts = 0; growth.Displacement = 0; growth.ReportAt = _time + 1;
        }

        private string EligibleDirectorType(string id) => RestorationTypeIntroduced(id) ? id : "chaser";
        private bool TryIntroduceRestorationEnemy()
        {
            if (_time < _rosterIntroductionReadyAt || _arrivalGrace > 0 || LocalDirectorSurvivalSeconds < 15 || _pressureReliefTimer > 0 ||
                _encounter.Phase != CombatEncounterPhase.Flow || _majorIncident.Kind != MajorIncidentKind.None || DirectorSurvivalSecondsRemaining < 35) return false;
            // Safety can delay an introduction past another family's unlock. Preserve the
            // authored reveal order rather than letting catalogue indices jump the queue.
            var local = LocalDirectorSurvivalSeconds;
            var nextFamily = -1;
            var earliest = double.MaxValue;
            for (var candidate = 1; candidate < RestorationRoster.Length; candidate++)
            {
                var reveal = SharedRevealSeconds(RestorationRoster[candidate]);
                if (_restorationIntroduced[candidate] || local < reveal || reveal >= earliest) continue;
                nextFamily = candidate; earliest = reveal;
            }
            for (var i = 1; i < RestorationRoster.Length; i++)
            {
                if (i != nextFamily) continue;
                var admitted = 0;
                var edge = i % 4;
                for (var member = 0; member < 3; member++)
                    if (SpawnEnemy(RestorationRoster[i], SustainedSpawnPosition(edge), forcedRoster: EnemyRoster.One)) admitted++;
                if (admitted == 0) return false;
                _restorationIntroduced[i] = true;
                _restorationIntroducedAt[i] = _time;
                _rosterIntroductionReadyAt = _time + 12;
                _nextEncounterTime = Mathf.Max(_nextEncounterTime, _time + 12);
                _spawnTimer = .75f;
                RecordRunHistory("roster_introduction", RestorationRoster[i], amount: admitted,
                    detail: "tier=1;grace=60;sharedStage=" + _sharedDirectorVisitIndex +
                        ";local=" + local.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
            return false;
        }
        private bool RestorationTypeIntroduced(string id)
        {
            var index = Array.IndexOf(RestorationRoster, id);
            return index <= 0 || (_restorationIntroduced[index] && _time >= _restorationIntroducedAt[index] + 12);
        }
        private float RestorationFamilyAge(string id)
        {
            var index = Array.IndexOf(RestorationRoster, id);
            return index <= 0 ? float.PositiveInfinity : _restorationIntroduced[index] ? _time - _restorationIntroducedAt[index] : 0;
        }
        private string ChooseRestorationAmbient()
        {
            // One combat draw, weighted toward familiar fodder; unlocks do not increase total arrival budget.
            var roll = _gameSim.Rng.Next();
            var local = LocalDirectorSurvivalSeconds;
            var basicShare = _sharedDirectorVisitIndex <= 0
                ? local < 270f
                    ? .88 - .18 * Mathf.Clamp01((local - 90f) / 90f)
                    : .70 - .10 * Mathf.Clamp01((local - 270f) / 90f)
                : .60 - .10 * Mathf.Clamp01(local / (float)VoidProgressionRules.SurvivalSeconds);
            string requested;
            if (roll < basicShare)
            {
                var basicRoll = roll / Math.Max(.001, basicShare);
                requested = basicRoll < .52 ? "chaser" : basicRoll < .78 ? "runner" : "swarmer";
                return ChooseEligibleRestorationFamily(requested, BasicFallbacks);
            }

            var specialists = _restorationChoiceCandidates;
            var count = 0;
            for (var i = 3; i < RestorationRoster.Length; i++)
            {
                var id = RestorationRoster[i];
                if (RestorationTypeIntroduced(id)) specialists[count++] = id;
            }
            if (count == 0) return "chaser";
            var selected = Math.Min(count - 1, (int)((roll - basicShare) / Math.Max(.001, 1d - basicShare) * count));
            requested = specialists[selected];
            if (AmbientTypeAllowed(requested)) return requested;
            for (var offset = 1; offset < count; offset++)
            {
                var candidate = specialists[(selected + offset) % count];
                if (!AmbientTypeAllowed(candidate)) continue;
                RecordRunHistory("spawn_substituted", candidate, "family_limit", sourceId: requested,
                    detail: "sharedStage=" + _sharedDirectorVisitIndex);
                return candidate;
            }
            return ChooseEligibleRestorationFamily(requested, BasicFallbacks);
        }

        private string ChooseSustainedBeatEnemy(CombatEncounterKind kind, int index)
        {
            string requested;
            string[] fallbacks;
            switch (kind)
            {
                case CombatEncounterKind.Pursuit:
                    requested = index < 3 ? "runner" : index < 5 ? "guard" : index < 7 ? "gunner" : "chaser";
                    fallbacks = PursuitFallbacks;
                    break;
                case CombatEncounterKind.Flank:
                    requested = index < 4 ? "runner" : index < 6 ? "twinGunner" : index == 6 ? "guard" : "dasher";
                    fallbacks = FlankFallbacks;
                    break;
                case CombatEncounterKind.Hunt:
                    requested = index < 2 ? "gunner" : index == 2 ? "technician" : "chaser";
                    fallbacks = HuntFallbacks;
                    break;
                default:
                    requested = index < 3 ? "brute" : index < 5 ? "guard" : "runner";
                    fallbacks = BreakthroughFallbacks;
                    break;
            }
            return ChooseEligibleRestorationFamily(requested, fallbacks);
        }
        private void TryDeployLegacySwarm()
        {
            if (_time < _nextLegacySwarmAt || _arrivalGrace > 0 || _pressureReliefTimer > 0 ||
                _majorIncident.Kind != MajorIncidentKind.None || DirectorSurvivalSecondsRemaining <= 30 ||
                !RestorationTypeIntroduced("runner")) return;
            // Keep the 30 + N*34 clock after a deferral without issuing a backlog of rings.
            do { _nextLegacySwarmAt += LegacyRestorationRules.SwarmIntervalSeconds; } while (_nextLegacySwarmAt <= _time);
            _legacySwarmSequence++;
            var id = _time >= 70 && RestorationTypeIntroduced("dasher") && _gameSim.Rng.Next() < .5 ? "dasher" : "runner";
            var count = Mathf.Min(26, 10 + Mathf.FloorToInt(_time / 18));
            var center = _gameSim.Player.Position;
            var radius = GameplayViewportHalfExtent().magnitude + 50f;
            var admitted = 0;
            for (var i = 0; i < count; i++)
            {
                var angle = _legacySwarmSequence * 1.3f + i * Mathf.PI * 2 / count;
                var slot = FindInactive(_gameSim.Enemies);
                if (slot < 0 || !SpawnEnemy(id, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    forcedRoster: EnemyRoster.One)) continue;
                _legacyRushIdentities[slot] = _gameSim.Enemies[slot].SpawnId;
                admitted++;
            }
            if (admitted > 0) ShowArenaToast("SWARM INCOMING", 2f, ToastKind.Danger);
            RecordRunHistory("director_circle_deployed", "legacy-rush-circle", instanceId: _legacySwarmSequence,
                amount: admitted, detail: "fullRing=true;interval=34;tier=1;composition=" + id + ";requested=" + count + ";nextAt=" + _nextLegacySwarmAt);
        }

        // Identity-keyed signature-ring members use the V1 dash, independently of ordinary attack slots.
        // The ring is capped at 26; every dash has a visible windup and a committed, dodgeable trajectory.
        private void UpdateLegacyRush(ref EnemyState enemy, float dt, float distance, Vector2 direction)
        {
            if (enemy.State == 0)
            {
                enemy.Velocity = direction * enemy.Speed;
                if (distance < 250 && enemy.Age > .5f)
                { enemy.State = 1; enemy.StateTimer = .45f; enemy.DashDirection = direction; }
            }
            else if (enemy.State == 1)
            {
                enemy.Velocity *= Mathf.Max(0, 1 - 10 * dt);
                enemy.DashDirection = direction;
                enemy.StateTimer -= dt;
                if (enemy.StateTimer <= 0)
                {
                    enemy.State = 2; enemy.StateTimer = .38f;
                    _gameSim.EnemyAudioCueHook?.Invoke(ProceduralAudio.Cue.Dash, .94f);
                }
            }
            else if (enemy.State == 2)
            {
                enemy.Velocity = enemy.DashDirection * 570;
                enemy.StateTimer -= dt;
                if (_gameSim.EnemyParticleScaleHook() > .01f && _gameSim.EnemyFxRollHook() < .6)
                    _gameSim.EnemyBurstFxHook?.Invoke(enemy.Position, SourceDotColor("pink"), 1, 20, .25f, .6f);
                if (enemy.StateTimer <= 0) { enemy.State = 3; enemy.StateTimer = .7f; }
            }
            else
            {
                enemy.Velocity = direction * enemy.Speed * .35f;
                enemy.StateTimer -= dt;
                if (enemy.StateTimer <= 0) enemy.State = 0;
            }
        }

        private bool DeployRestorationCircle()
        {
            if (!_circleBeat) return false;
            _circleBeat = false;
            var count = Mathf.Min(26, 10 + Mathf.FloorToInt(_time / 18));
            var center = _gameSim.Player.Position;
            var half = GameplayViewportHalfExtent();
            var baseAngle = _encounterSequence * 1.3f;
            var admitted = 0;
            for (var i = 0; i < count; i++)
            {
                var angle = baseAngle + i / (float)count * Mathf.PI * 2;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var edgeDistance = half.magnitude + 50f;
                var id = i % 4 == 0 && RestorationTypeIntroduced("swarmer") ? "swarmer" : "runner";
                var slot = FindInactive(_gameSim.Enemies);
                if (slot < 0 || !SpawnEnemy(id, center + direction * edgeDistance, forcedRoster: EnemyRoster.One)) continue;
                _encounterMembers[slot] = new EncounterMember { SpawnId = _gameSim.Enemies[slot].SpawnId, Owner = _encounterOwner, Movement = EncounterMovement.Natural };
                admitted++;
            }
            _encounter.CommitDeployment(admitted);
            RecordRunHistory("director_circle_deployed", "mixed-green-circle", instanceId: _encounterOwner, amount: admitted, detail: "fullRing=true;tier=1;composition=runner,swarmer");
            return true;
        }
    }
}
