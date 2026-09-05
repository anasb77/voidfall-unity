using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private struct NullCityUnitState
        {
            public int Identity;
            public float BroodClock;
            public float ShotClock;
            public float Emergence;
            public float Grace;
            public int Shots;
            public int Barrel;
        }

        private struct NullCityBomb
        {
            public bool Active;
            public Vector2 Position;
            public float Remaining;
        }

        private readonly NullCityUnitState[] _nullCityUnits = new NullCityUnitState[MaxEnemies];
        private readonly Vector2[] _nullCityBirthQueue = new Vector2[64];
        private readonly Vector2[] _nullCityBlastQueue = new Vector2[64];
        private readonly NullCityBomb[] _nullCityBombs = new NullCityBomb[3];
        private readonly int[] _nullCityDamageSlots = new int[MaxEnemies];
        private readonly int[] _nullCityDamageIdentities = new int[MaxEnemies];
        private int _nullCityBirthCount;
        private int _nullCityBlastCount;
        private Vector2 _nullCityOrigin;
        private bool _nullCityBossActive;
        private bool _nullCityBossSpawned;
        private bool _nullCityCleared;
        private int _nullCityBossSlot = -1;
        private float _nullCityElapsed;
        private float _nullCityBossElapsed;
        private float _nullCitySpawnClock;
        private float _nullCityHeavyClock;
        private int _nullCityHeavySequence;
        private int _nullCityPoliceWave;
        private int _nullCityLastCyclePass = -1;
        private float _nullCityDashRemaining;
        private float _nullCityDashCooldown;
        private bool _nullCityDashRequested;
        private Vector2 _nullCityDashDirection = Vector2.right;
        private MotherloadMove _nullCityMove;
        private int _nullCityMoveSequence;
        private float _nullCityMoveClock;
        private float _nullCityWarnClock;
        private float _nullCityTractorClock;
        private float _nullCityVentClock;
        private float _nullCityCannonClock;
        private int _nullCityCannonCount;
        private int _nullCityTractorBarrel;
        private float _nullCityAim;

        private bool CurrentVoidIsNullCity => _arenaId == ArenaId.NullCity && !_mainMenuBrowsing;
        private static bool IsNullCityEnemy(string id) => NullCityContent.EnemyIndex(id) >= 0;
        private static bool IsMotherload(string id) => id == NullCityContent.MotherloadId;
        private bool NullCityLockdown => NullCityRules.CycleAt(_nullCityElapsed, _nullCityBossActive) == NullCityCycle.Lockdown;

        private Vector2 NullCityWorld(float x, float y) => _nullCityOrigin + new Vector2(x - 800f, 450f - y);
        private Vector2 NullCityCanvas(Vector2 world) => new Vector2(world.x - _nullCityOrigin.x + 800f, 450f - world.y + _nullCityOrigin.y);

        private void ResetNullCityEncounterState()
        {
            for (var i = 0; i < _nullCityUnits.Length; i++)
                if (_nullCityUnits[i].Identity != 0 && _enemyViews[i] != null) _enemyViews[i].sprite = null;
            if (_nullCityBossSlot >= 0 && _bossViews[_nullCityBossSlot] != null) _bossViews[_nullCityBossSlot].sprite = null;
            _nullCityOrigin = _gameSim.Player.Position;
            _nullCityBossActive = false;
            _nullCityBossSpawned = false;
            _nullCityCleared = false;
            _nullCityBossSlot = -1;
            _nullCityElapsed = _nullCityBossElapsed = 0f;
            _nullCitySpawnClock = 0.5f;
            _nullCityHeavyClock = 10f;
            _nullCityHeavySequence = 0;
            _nullCityPoliceWave = 0;
            _nullCityLastCyclePass = -1;
            _nullCityBirthCount = _nullCityBlastCount = 0;
            _nullCityDashRemaining = _nullCityDashCooldown = 0f;
            _nullCityDashRequested = false;
            _nullCityWarnClock = _nullCityTractorClock = _nullCityVentClock = 0f;
            _nullCityCannonClock = 0f;
            _nullCityCannonCount = _nullCityMoveSequence = _nullCityTractorBarrel = 0;
            _nullCityMoveClock = 0f;
            Array.Clear(_nullCityUnits, 0, _nullCityUnits.Length);
            Array.Clear(_nullCityBombs, 0, _nullCityBombs.Length);
            HideNullCityPresentation();
        }

        private void ReadNullCityDashInput()
        {
            if (!CurrentVoidIsNullCity || _paused || _gameOver || _revivePending || JourneyStopsCombat || _levelUpActive)
            { _nullCityDashRequested = false; return; }
            if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame))
                _nullCityDashRequested = true;
        }

        private void ApplyNullCityMovement(float dt, Vector2 input)
        {
            if (!CurrentVoidIsNullCity) return;
            _nullCityDashCooldown = Mathf.Max(0f, _nullCityDashCooldown - dt);
            if (input.sqrMagnitude > 0.01f) _nullCityDashDirection = input.normalized;
            if (_nullCityDashRequested && _nullCityDashCooldown <= 0f && _gameSim.Player.Health > 0f)
            {
                _nullCityDashRemaining = 0.15f;
                _nullCityDashCooldown = 1.7f;
                _gameSim.Player.Iframes = Mathf.Max(_gameSim.Player.Iframes, 0.25f);
                SpawnRingWave(_gameSim.Player.Position, 12f, 85f, .2f, new Color(.65f, 1f, .87f, .8f));
            }
            _nullCityDashRequested = false;
            if (_nullCityDashRemaining > 0f)
            {
                _gameSim.Player.Position += _nullCityDashDirection * 480f * Mathf.Min(dt, _nullCityDashRemaining);
                _nullCityDashRemaining = Mathf.Max(0f, _nullCityDashRemaining - dt);
            }
            ClampNullCityPlayer();
        }

        private void ClampNullCityPlayer()
        {
            if (!CurrentVoidIsNullCity) return;
            var p = NullCityCanvas(_gameSim.Player.Position);
            _gameSim.Player.Position = NullCityWorld(
                Mathf.Clamp(p.x, (float)NullCityRules.ArenaLeft, (float)NullCityRules.ArenaRight),
                Mathf.Clamp(p.y, (float)NullCityRules.ArenaTop, (float)NullCityRules.ArenaBottom));
        }

        private bool SpawnNullCityUnit(int type, Vector2 position, bool newborn = false, bool fromHangar = false)
        {
            if (type < 0 || type >= NullCityContent.Enemies.Length || _nullCityCleared) return false;
            if (!SpawnEnemy(NullCityContent.Enemies[type].Id, position, forcedRoster: EnemyRoster.One)) return false;
            var identity = _nextEnemyId - 1;
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var e = _gameSim.Enemies[i];
                if (!e.Active || e.SpawnId != identity) continue;
                e.AttackCooldown = 1.4f;
                e.Spin = 0f;
                e.Rotation = Mathf.Atan2(_gameSim.Player.Position.y - e.Position.y, _gameSim.Player.Position.x - e.Position.x);
                _gameSim.Enemies[i] = e;
                _nullCityUnits[i] = new NullCityUnitState
                {
                    Identity = identity, BroodClock = 8f,
                    Emergence = fromHangar ? 1.3f : 0f,
                    Grace = newborn ? .65f : fromHangar ? .9f : 0f,
                };
                return true;
            }
            return false;
        }

        private Vector2 NullCitySpawnEdge()
        {
            var edge = _gameSim.Rng.Int(4);
            var roll = (float)_gameSim.Rng.Next();
            return edge < 2
                ? NullCityWorld(edge == 0 ? 195f : 1405f, 245f + roll * 460f)
                : NullCityWorld(230f + roll * 1120f, edge == 2 ? 230f : 734f);
        }

        private bool HasNullCityHeavy()
        {
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var e = _gameSim.Enemies[i];
                if (e.Active && (e.Id == "null-gunship" || e.Id == "null-mech" || e.Id == "null-broodmother")) return true;
            }
            return false;
        }

        private void UpdateNullCitySpawns(float dt)
        {
            if (!CurrentVoidIsNullCity || _nullCityCleared || _riftTransitionActive) return;
            _nullCitySpawnClock -= dt;
            _nullCityHeavyClock -= dt;
            if (!_nullCityBossActive && _nullCitySpawnClock <= 0f && ActiveEnemies() < 30)
            {
                var roll = _gameSim.Rng.Next();
                var type = roll < .45 ? 3 : roll < .62 ? 0 : roll < .74 ? 2 : roll < .84 ? 1 : roll < .92 ? 4 : 8;
                SpawnNullCityUnit(type, NullCitySpawnEdge());
                _nullCitySpawnClock = Mathf.Max(.65f, 1.2f - _nullCityElapsed * .001f);
            }
            if (!_nullCityBossActive && _nullCityHeavyClock <= 0f && ActiveEnemies() < 40 && !HasNullCityHeavy())
            {
                SpawnNullCityUnit(5 + _nullCityHeavySequence++ % 3, NullCitySpawnEdge());
                _nullCityHeavyClock = 19f;
            }
        }

        private void StepNullCity(float dt)
        {
            if (!CurrentVoidIsNullCity || _nullCityCleared || dt <= 0f) return;
            _nullCityElapsed += dt;
            if (_nullCityBossActive) _nullCityBossElapsed += dt;
            var pass = _nullCityBossActive ? -2 : Mathf.FloorToInt(_nullCityElapsed / 46f);
            if (pass != _nullCityLastCyclePass) { _nullCityPoliceWave = 0; _nullCityLastCyclePass = pass; }
            var phaseElapsed = _nullCityBossActive ? _nullCityBossElapsed : _nullCityElapsed % 46f - 22f;
            if (NullCityLockdown && (_nullCityBossActive || _nullCityPoliceWave < 3) &&
                phaseElapsed > 1.5f + _nullCityPoliceWave * (_nullCityBossActive ? 14f : 5f))
            {
                var police = 0;
                for (var i = 0; i < _gameSim.Enemies.Length; i++)
                    if (_gameSim.Enemies[i].Active && NullCityContent.EnemyIndex(_gameSim.Enemies[i].Id) >= 9) police++;
                for (var type = 9; type <= 11 && police < 9 && ActiveEnemies() < 48; type++, police++)
                    SpawnNullCityUnit(type, NullCityWorld(680f + (type - 9) * 110f, 777f), fromHangar: true);
                if (_nullCityPoliceWave++ == 0) ShowArenaToast("LAW ENFORCEMENT DEPLOYED", 3f, ToastKind.Danger);
            }
            ProcessNullCityBirths();
            ProcessNullCityBlasts();
            var purge = NullCityRules.PurgeAt(_nullCityBossActive ? _nullCityBossElapsed : _nullCityElapsed, _nullCityBossActive);
            if (purge.Active)
            {
                // Motherload entered the cleared encounter before its summons. Resolve it first.
                if (_nullCityBossActive && _nullCityBossSlot >= 0)
                {
                    var b = _gameSim.Bosses[_nullCityBossSlot];
                    if (b.Active && InsideNullCityPurge(NullCityCanvas(b.Position), purge, b.Radius * .3f))
                    {
                        DamageNullCityBossEnvironment((float)NullCityRules.PurgeEnemyDamagePerSecond * dt);
                    }
                }
                var count = _nullCityCleared ? 0 : SnapshotNullCityDamageOrder();
                for (var order = 0; order < count && !_nullCityCleared; order++)
                {
                    var slot = _nullCityDamageSlots[order];
                    var e = _gameSim.Enemies[slot];
                    if (e.Active && e.SpawnId == _nullCityDamageIdentities[order] &&
                        InsideNullCityPurge(NullCityCanvas(e.Position), purge, e.Radius * .3f))
                        DamageNullCityMachine(slot, (float)NullCityRules.PurgeEnemyDamagePerSecond * dt);
                }
                var p = NullCityCanvas(_gameSim.Player.Position);
                if (InsideNullCityPurge(p, purge, AttackPlayerRadius * .3f))
                    DamagePlayer((float)NullCityRules.PurgePlayerDamage, Vector2.up);
                if (_nullCityCleared) return;
            }
            for (var i = 0; i < _nullCityBombs.Length; i++)
            {
                var bomb = _nullCityBombs[i];
                if (!bomb.Active) continue;
                bomb.Remaining -= dt;
                if (bomb.Remaining <= 0f)
                {
                    bomb.Active = false;
                    SpawnRingWave(bomb.Position, 8f, 160f, .3f, new Color(1f, .7f, .4f, .7f));
                    if ((_gameSim.Player.Position - bomb.Position).sqrMagnitude < 70f * 70f)
                        DamagePlayer(28f, _gameSim.Player.Position - bomb.Position);
                }
                _nullCityBombs[i] = bomb;
            }
            ClampNullCityPlayer();
        }

        private static bool InsideNullCityPurge(Vector2 p, NullCityPurge h, float pad) =>
            p.x > h.X - pad && p.x < h.X + h.Width + pad && p.y > h.Y - pad && p.y < h.Y + h.Height + pad;

        private void QueueNullCityBrood(Vector2 position, int count, float radius)
        {
            for (var i = 0; i < count && _nullCityBirthCount < _nullCityBirthQueue.Length; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                _nullCityBirthQueue[_nullCityBirthCount++] = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        private void ProcessNullCityBirths()
        {
            var remaining = 0;
            for (var i = 0; i < _nullCityBirthCount; i++)
            {
                var p = _nullCityBirthQueue[i];
                if (!SpawnNullCityUnit(3, p, newborn: true)) _nullCityBirthQueue[remaining++] = p;
            }
            _nullCityBirthCount = remaining;
        }

        private void OnNullCityEnemyDeath(EnemyState enemy)
        {
            if (!CurrentVoidIsNullCity || _nullCityCleared) return;
            if (enemy.Id == "null-broodmother") QueueNullCityBrood(enemy.Position, 4, 64f);
            if (enemy.Id == "null-volatile" && _nullCityBlastCount < _nullCityBlastQueue.Length)
                _nullCityBlastQueue[_nullCityBlastCount++] = enemy.Position;
        }

        private void DamageNullCityMachine(int index, float damage)
        {
            var e = _gameSim.Enemies[index];
            if (!e.Active || _nullCityUnits[index].Identity == e.SpawnId && _nullCityUnits[index].Grace > 0f) return;
            e.Health -= damage;
            e.HitTimer = .09f;
            _gameSim.Enemies[index] = e;
            if (e.Health <= 0f) KillEnemy(index);
        }

        private int SnapshotNullCityDamageOrder()
        {
            var count = 0;
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var slot = _gameSim.EnemyOrder[order];
                var enemy = _gameSim.Enemies[slot];
                if (!enemy.Active) continue;
                _nullCityDamageSlots[count] = slot;
                _nullCityDamageIdentities[count++] = enemy.SpawnId;
            }
            return count;
        }

        private void ProcessNullCityBlasts()
        {
            // New chain detonations are queued for the following tick, never recursively resolved.
            var count = _nullCityBlastCount;
            for (var blast = 0; blast < count && _nullCityBlastCount > 0 && !_nullCityCleared; blast++)
            {
                var position = _nullCityBlastQueue[0];
                _nullCityBlastCount--;
                Array.Copy(_nullCityBlastQueue, 1, _nullCityBlastQueue, 0, _nullCityBlastCount);
                SpawnRingWave(position, 10f, 300f, .4f, new Color(1f, .6f, .3f, .8f));
                BurstFx(position, SourceDotColor("orange"), 20, 180f, .4f, .7f);
                if ((_gameSim.Player.Position - position).sqrMagnitude < 124f * 124f)
                    DamagePlayer(27f, _gameSim.Player.Position - position);
                if (_nullCityBossActive && _nullCityBossSlot >= 0 &&
                    (_gameSim.Bosses[_nullCityBossSlot].Position - position).sqrMagnitude < 124f * 124f)
                    DamageNullCityBossEnvironment(150f);
                if (_nullCityCleared) return;
                var damageCount = SnapshotNullCityDamageOrder();
                for (var order = 0; order < damageCount && !_nullCityCleared; order++)
                {
                    var slot = _nullCityDamageSlots[order];
                    var e = _gameSim.Enemies[slot];
                    if (e.Active && e.SpawnId == _nullCityDamageIdentities[order] &&
                        (e.Position - position).sqrMagnitude < 124f * 124f)
                        DamageNullCityMachine(slot, 150f);
                }
            }
        }

        private void DamageNullCityBossEnvironment(float damage)
        {
            if (!_nullCityBossActive || _nullCityBossSlot < 0) return;
            var boss = _gameSim.Bosses[_nullCityBossSlot];
            if (!boss.Active || boss.State == 4) return;
            boss.Health -= damage * (_nullCityVentClock > 0f ? 2.1f : .7f);
            boss.HitTimer = .08f;
            _gameSim.Bosses[_nullCityBossSlot] = boss;
            if (boss.Health <= 0f) KillBoss(_nullCityBossSlot);
        }

        private void UpdateNullCityEnemy(ref EnemyState e, float dt, float distance, Vector2 direction)
        {
            var index = e.View;
            var type = NullCityContent.EnemyIndex(e.Id);
            var state = _nullCityUnits[index];
            if (state.Identity != e.SpawnId) state = new NullCityUnitState { Identity = e.SpawnId, BroodClock = 8f };
            state.Grace = Mathf.Max(0f, state.Grace - dt);
            if (state.Emergence > 0f)
            {
                state.Emergence = Mathf.Max(0f, state.Emergence - dt);
                e.Velocity = Vector2.up * 53f;
                e.Rotation = Mathf.PI * .5f;
                _nullCityUnits[index] = state;
                return;
            }
            if (state.Grace > 0f) { e.Velocity = Vector2.zero; _nullCityUnits[index] = state; return; }
            var police = type >= 9;
            var powered = police || !NullCityLockdown;
            if (!powered && (type == 0 || type == 1 || type == 2 || type == 5 || type == 6 || type == 8))
            { e.State = 0; state.Shots = 0; }
            if (e.State == 0) e.Facing = direction;
            e.Rotation = Mathf.Atan2(e.Facing.y, e.Facing.x);
            e.Velocity = direction * e.Speed * (powered ? 1f : .7f);
            if (type == 0)
            {
                var approach = distance > 290f ? 1f : distance < 205f ? -.85f : .05f;
                var side = new Vector2(-direction.y, direction.x) * Mathf.Sin(e.Seed) * .8f;
                e.Velocity = (direction * approach + side) * e.Speed * (powered ? 1f : .7f);
                if (powered && e.AttackCooldown <= 0f) { state.Shots = 3; state.ShotClock = 0f; e.AttackCooldown = 4.4f; }
            }
            if ((type == 2 && distance < 320f) || (type == 5 && distance < 385f) ||
                (type == 8 && distance < 300f) || (type == 7 && distance < 220f) || (type == 11 && distance < 260f))
                e.Velocity = Vector2.zero;
            if (type == 10 && e.Age % 6f < 3f && distance < 180f) e.Velocity *= .2f;
            if (type == 7)
            {
                state.BroodClock -= dt;
                if (state.BroodClock <= 0f)
                {
                    state.BroodClock = 8f;
                    if (ActiveEnemies() < 40 && _nullCityBirthCount < 8) QueueNullCityBrood(e.Position, 2, 64f);
                }
            }
            if (state.Shots > 0 && powered)
            {
                state.ShotClock -= dt;
                if (state.ShotClock <= 0f)
                {
                    var mounts = type == 5 ? 4 : 2;
                    var mount = mounts - state.Shots;
                    var offset = type == 5 ? (mount == 0 ? -32f : mount == 1 ? 32f : mount == 2 ? -14f : 14f) : mount == 0 ? -16f : 16f;
                    var origin = e.Position + e.Facing * e.Radius + new Vector2(-e.Facing.y, e.Facing.x) * (type == 0 ? 0f : offset);
                    SpawnHostileShot(origin, type == 0 ? direction : e.Facing, 12f, type == 0 ? 250f : type == 8 ? 340f : 295f, 0f);
                    state.Shots--;
                    state.ShotClock = type == 0 ? .14f : .27f;
                }
            }
            if (e.State == 2)
            {
                e.StateTimer -= dt;
                e.Velocity = e.DashDirection * 410f;
                if (e.StateTimer <= 0f) e.State = 0;
            }
            else if (e.State == 1)
            {
                e.Velocity = Vector2.zero;
                e.StateTimer -= dt;
                if (e.StateTimer <= 0f)
                {
                    e.State = 0;
                    if (type == 1 || type == 9) { e.State = 2; e.StateTimer = type == 9 ? .3f : .5f; e.DashDirection = e.Facing; }
                    else if (type == 2) SpawnHostileShot(e.Position + e.Facing * 27f, e.Facing, 12f, 465f, 0f);
                    else if (type == 4)
                    {
                        _gameSim.Enemies[index] = e;
                        ResolveEnemyDeath(index, true);
                        e = _gameSim.Enemies[index];
                    }
                    else if (type == 5 || type == 8) { state.Shots = type == 5 ? 4 : 2; state.ShotClock = 0f; }
                    else if (type == 6)
                    {
                        SpawnRingWave(e.Position, 15f, 290f, .35f, new Color(.8f, .6f, 1f, .7f));
                        if (distance < 128f) DamagePlayer(29f, direction);
                    }
                    else if (type == 11)
                    {
                        var muzzle = e.Position + e.Facing * 30f + new Vector2(-e.Facing.y, e.Facing.x) * (state.Barrel == 0 ? -11f : 11f);
                        SpawnHostileShot(muzzle, e.Facing, 12f, 250f, 0f);
                        state.Barrel = 1 - state.Barrel;
                    }
                }
            }
            else if (e.AttackCooldown <= 0f && state.Shots == 0)
            {
                var attack = (type == 4 && distance < 100f) || (type == 9 && distance < 340f) || type == 11 ||
                    powered && (type == 1 || type == 2 || type == 5 || type == 8 || type == 6 && distance < 153f);
                if (attack)
                {
                    e.State = 1;
                    e.StateTimer = type == 4 ? 1.5f : type == 6 ? 1.55f : 1.35f;
                    e.AttackCooldown = type == 5 ? 7f : type == 1 ? 6f : 5f;
                    e.Facing = direction;
                }
            }
            _nullCityUnits[index] = state;
        }

        private void ConstrainNullCityEnemy(ref EnemyState e)
        {
            if (!IsNullCityEnemy(e.Id) || _nullCityUnits[e.View].Emergence > 0f) return;
            var p = NullCityCanvas(e.Position);
            e.Position = NullCityWorld(Mathf.Clamp(p.x, 180f + e.Radius * .3f, 1420f - e.Radius * .3f),
                Mathf.Clamp(p.y, 220f + e.Radius * .3f, 746f - e.Radius * .3f));
        }

        private void BeginNullCityBossEncounter()
        {
            if (_nullCityBossSpawned) return;
            ClearHydraBossArena();
            _nullCityBirthCount = _nullCityBlastCount = 0;
            SpawnBoss(NullCityContent.MotherloadId, 1.0, 1.0, 0);
            for (var i = 0; i < _gameSim.Bosses.Length; i++)
            {
                var boss = _gameSim.Bosses[i];
                if (!boss.Active || !IsMotherload(boss.Id)) continue;
                boss.Position = NullCityWorld(1040f, 472f);
                boss.TargetPosition = boss.Position;
                _gameSim.Bosses[i] = boss;
                _nullCityBossSlot = i;
                _nullCityBossSpawned = _nullCityBossActive = true;
                _nullCityBossElapsed = 0f;
                _nullCityMoveClock = 0f;
                _nullCityMoveSequence = 0;
                _nullCityAim = Mathf.PI;
                ShowArenaToast("MOTHERLOAD — THE CITY'S SECRET WEAPON", 3f, ToastKind.Danger);
                return;
            }
            _voidBossEncounterSpawned = false;
        }

        private Vector2 MotherloadMuzzle(BossState boss, int mount)
        {
            var forward = new Vector2(Mathf.Cos(boss.AttackAngle), Mathf.Sin(boss.AttackAngle));
            var right = new Vector2(forward.y, -forward.x);
            return boss.Position + forward * (boss.Radius * (-.75f + (mount / 2) * .45f) + 33f) +
                right * ((mount % 2 == 0 ? -1f : 1f) * boss.Radius * .64f);
        }

        private void UpdateNullCityMotherload(ref BossState boss, float dt)
        {
            var delta = _gameSim.Player.Position - boss.Position;
            var distance = Mathf.Max(.01f, delta.magnitude);
            if (distance < boss.Radius + AttackPlayerRadius && boss.ContactCooldown <= 0f)
            { DamagePlayer(boss.Damage, delta / distance); boss.ContactCooldown = .85f; }
            if (_nullCityTractorClock > 0f || _nullCityMove == MotherloadMove.Tractor && _nullCityWarnClock > 0f)
                boss.AttackAngle = _nullCityAim;
            else boss.AttackAngle = Mathf.LerpAngle(boss.AttackAngle * Mathf.Rad2Deg, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, dt * .8f) * Mathf.Deg2Rad;
            if (_nullCityVentClock > 0f) { _nullCityVentClock = Mathf.Max(0f, _nullCityVentClock - dt); boss.State = 0; return; }
            if (_nullCityTractorClock > 0f)
            {
                boss.State = 2;
                _nullCityTractorClock = Mathf.Max(0f, _nullCityTractorClock - dt);
                if (_nullCityDashRemaining <= 0f && NullCityRules.IsInsideTractor(delta.x, delta.y, _nullCityAim))
                    _gameSim.Player.Position -= delta / distance * (float)NullCityRules.TractorPullSpeed * dt;
                ClampNullCityPlayer();
                _nullCityCannonClock -= dt;
                if (_nullCityCannonClock <= 0f)
                {
                    var muzzle = MotherloadMuzzle(boss, (_nullCityTractorBarrel++ & 1) == 0 ? 6 : 7);
                    SpawnHostileShot(muzzle, (_gameSim.Player.Position - muzzle).normalized, 12f * boss.DamageScale, 295f, 0f);
                    _nullCityCannonClock = .65f;
                }
                if (_nullCityTractorClock <= 0f) { _nullCityVentClock = 4f; _nullCityMove = MotherloadMove.Vent; }
                return;
            }
            if (_nullCityCannonCount > 0)
            {
                boss.State = 2;
                _nullCityCannonClock -= dt;
                if (_nullCityCannonClock <= 0f)
                {
                    var mount = 8 - _nullCityCannonCount--;
                    var angle = _nullCityAim + (mount - 3.5f) * .15f;
                    SpawnHostileShot(MotherloadMuzzle(boss, mount), new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 12f * boss.DamageScale, 320f, 0f);
                    _nullCityCannonClock = .18f;
                }
                return;
            }
            if (_nullCityWarnClock > 0f)
            {
                boss.State = 1;
                _nullCityWarnClock = Mathf.Max(0f, _nullCityWarnClock - dt);
                if (_nullCityWarnClock <= 0f)
                {
                    if (_nullCityMove == MotherloadMove.Cannons) { _nullCityCannonCount = 8; _nullCityCannonClock = 0f; }
                    else if (_nullCityMove == MotherloadMove.Tractor) { _nullCityTractorClock = 4f; _nullCityCannonClock = .6f; }
                    else if (_nullCityMove == MotherloadMove.Brood && ActiveEnemies() < 24)
                    { QueueNullCityBrood(boss.Position, 4, 150f); SpawnNullCityUnit(4, boss.Position + new Vector2(-140f, -70f), newborn: true); }
                    else if (_nullCityMove == MotherloadMove.Bombardment)
                    {
                        for (var i = 0; i < 3; i++)
                        {
                            var canvas = NullCityCanvas(_gameSim.Player.Position) + (i == 0 ? Vector2.zero : i == 1 ? new Vector2(-125f, 65f) : new Vector2(125f, -65f));
                            _nullCityBombs[i] = new NullCityBomb { Active = true, Remaining = 1.6f,
                                Position = NullCityWorld(Mathf.Clamp(canvas.x, 300f, 1280f), Mathf.Clamp(canvas.y, 250f, 710f)) };
                        }
                    }
                }
                return;
            }
            boss.State = 0;
            _nullCityMoveClock -= dt;
            var home = NullCityWorld(960f + Mathf.Sin(_nullCityBossElapsed * .13f) * 120f, 465f + Mathf.Cos(_nullCityBossElapsed * .16f) * 70f);
            boss.Position = Vector2.MoveTowards(boss.Position, home, 22f * dt);
            if (_nullCityMoveClock <= 0f)
            {
                _nullCityMove = NullCityRules.NextMotherloadMove(_nullCityMoveSequence++);
                _nullCityMoveClock = 1.6f;
                _nullCityAim = Mathf.Atan2(delta.y, delta.x);
                if (_nullCityMove == MotherloadMove.Vent) _nullCityVentClock = 5f;
                else _nullCityWarnClock = _nullCityMove == MotherloadMove.Tractor ? 1.8f : _nullCityMove == MotherloadMove.Bombardment ? 1.1f : 1.4f;
            }
        }

        private void EndNullCityBossEncounter()
        {
            _nullCityCleared = true;
            _nullCityBossActive = false;
            _nullCityBirthCount = _nullCityBlastCount = 0;
            _nullCityTractorClock = _nullCityWarnClock = 0f;
            _nullCityCannonCount = 0;
            Array.Clear(_nullCityBombs, 0, _nullCityBombs.Length);
            ClearNullCityHostiles();
            HideNullCityCombatTelegraphs();
        }

        private void ClearNullCityHostiles()
        {
            // Preserve the defeated boss and its native dissolution/relic-emergence timer.
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                _gameSim.Enemies[i] = default;
                Hide(_enemyViews[i]);
                Hide(_enemyHealthArcViews[i]); Hide(_enemyShieldArcViews[i]);
                Hide(_enemyHealthBackgroundViews[i]); Hide(_enemyHealthFillViews[i]);
                Hide(_enemyTelegraphRingViews[i]); Hide(_enemyTelegraphLineViews[i]);
                Hide(_enemyTelegraphSecondaryLineViews[i]); Hide(_enemyTelegraphTertiaryLineViews[i]);
            }
            ResetEnemyOrder();
            for (var i = 0; i < _gameSim.HostileShots.Length; i++)
            { _gameSim.HostileShots[i] = default; Hide(_hostileShotViews[i]); }
            ResetHostileShotOrder();
            ClearMeteors();
        }
    }
}
