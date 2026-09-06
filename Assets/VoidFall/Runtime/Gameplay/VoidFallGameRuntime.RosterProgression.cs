using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private RosterProgressionVisualAsset _rosterProgressionVisuals;
        private LineRenderer[] _rosterBlastWarnings;
        private readonly RosterActorState[] _rosterActorStates = new RosterActorState[MaxEnemies];

        private struct RosterActorState
        {
            public int SpawnId;
            public int DashRemaining;
            public int BlastIndex;
            public Vector2 AttackAxis;
        }

        private ref RosterActorState RosterStateFor(EnemyState enemy)
        {
            ref var state = ref _rosterActorStates[enemy.View];
            if (state.SpawnId != enemy.SpawnId) state = new RosterActorState { SpawnId = enemy.SpawnId };
            return ref state;
        }

        private static bool UsesRosterProgression(EnemyState enemy) =>
            enemy.Roster > EnemyRoster.One && EnemyRosterRules.RosterTwoEligible(enemy.Id);

        private static float EliteTierHealth(EnemyRoster tier) => tier == EnemyRoster.Four ? 4.35f : tier == EnemyRoster.Three ? 2.9f : tier == EnemyRoster.Two ? 1.45f : 1f;
        private static float EliteTierRadius(EnemyRoster tier) => 1f + ((int)tier - 1) * .05f;
        private static float EliteTierDamage(EnemyRoster tier) => tier == EnemyRoster.Four ? 1.65f : tier == EnemyRoster.Three ? 1.42f : tier == EnemyRoster.Two ? 1.12f : 1f;
        private static float EliteTierSpeed(string id, EnemyRoster tier)
        {
            var level = (int)tier - 1;
            if (id == "mortar") return (.92f + .04f * level) / .92f;
            if (id == "gunner") return (1.05f + .05f * level) / 1.05f;
            return (tier == EnemyRoster.Four ? 1.28f : tier == EnemyRoster.Three ? 1.25f : tier == EnemyRoster.Two ? 1.22f : 1.2f) / 1.2f;
        }

        private float RosterAttackCooldown(EnemyState enemy)
        {
            if (enemy.EliteKind.HasValue)
            {
                if (enemy.Id == "mortar") return enemy.Roster == EnemyRoster.Four ? 4.7f : enemy.Roster == EnemyRoster.Three ? 5.1f : 5.6f;
                if (enemy.Id == "gunner") return enemy.Roster == EnemyRoster.Four ? 3.1f : enemy.Roster == EnemyRoster.Three ? 3.4f : 3.7f;
            }
            return (float)EnemyRosterRules.RosterCooldownSeconds(FindEnemy(enemy.Id)?.AttackCooldown ?? 3, enemy.Roster);
        }

        private void UpdateProgressedEnemy(ref EnemyState enemy, float dt, float distance, Vector2 direction)
        {
            var traits = RosterProgressionTraits.Get(enemy.Id, enemy.Roster, enemy.EliteKind.HasValue);
            enemy.Facing = direction;
            enemy.Rotation = Mathf.Atan2(direction.y, direction.x);
            enemy.Velocity = direction * enemy.Speed;
            if (enemy.Id == "chaser" || enemy.Id == "guard" || enemy.Id == "splitter") return;
            if (enemy.Id == "runner")
            {
                var angle = Mathf.Atan2(direction.y, direction.x) + Mathf.Sin(enemy.Age * traits.ZigzagFrequency + enemy.Seed) * traits.ZigzagAmplitude;
                var sprint = traits.SprintEvery > 0 && enemy.Age % traits.SprintEvery < traits.SprintDuration ? traits.SprintMultiplier : 1;
                enemy.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * enemy.Speed * sprint;
                return;
            }
            if (enemy.Id == "dasher") { UpdateProgressedDasher(ref enemy, dt, distance, direction, traits); return; }
            if (enemy.Id == "technician" || enemy.Id == "harvester")
            {
                if (enemy.AttackCooldown > 0) return;
                var healer = enemy.Id == "technician";
                var radius = healer ? traits.HealRadius : traits.HarvestRadius;
                var count = (int)(healer ? traits.HealCount : traits.HarvestTargets);
                for (var order = 0; order < _gameSim.EnemyOrderCount && count > 0; order++)
                {
                    var slot = _gameSim.EnemyOrder[order];
                    var other = _gameSim.Enemies[slot];
                    if (!other.Active || other.SpawnId == enemy.SpawnId || other.Health >= other.MaxHealth ||
                        (other.Position - enemy.Position).sqrMagnitude > radius * radius) continue;
                    if (healer) { other.Health = Mathf.Min(other.MaxHealth, other.Health + traits.HealAmount); _gameSim.Enemies[slot] = other; }
                    else enemy.Health = Mathf.Min(enemy.MaxHealth, enemy.Health + traits.HarvestAmount);
                    count--;
                }
                enemy.AttackCooldown = RosterAttackCooldown(enemy);
                SpawnRingWave(enemy.Position, 4, radius, .6f, new Color(.2f, 1f, .65f, .5f));
                return;
            }
            if (enemy.Id == "carrier")
            {
                if (distance < 270) enemy.Velocity *= -.6f;
                if (enemy.AttackCooldown > 0) return;
                for (var child = 0; child < (int)traits.SpawnCount; child++)
                    SpawnProgressedChild(enemy, child, (int)traits.SpawnCount, (EnemyRoster)(int)traits.ChildTier, true);
                enemy.AttackCooldown = RosterAttackCooldown(enemy);
                return;
            }
            if (traits.ShotCount > 0) { UpdateProgressedGunner(ref enemy, dt, distance, direction, traits); return; }
            if (traits.BlastCount > 0) UpdateProgressedBlast(ref enemy, dt, distance, direction, traits);
        }

        private void UpdateProgressedDasher(ref EnemyState enemy, float dt, float distance, Vector2 direction, RosterProgressionTraits traits)
        {
            ref var rosterState = ref RosterStateFor(enemy);
            if (enemy.State != 0) enemy.Rotation = Mathf.Atan2(enemy.DashDirection.y, enemy.DashDirection.x);
            if (enemy.State == 1)
            {
                enemy.Velocity = Vector2.zero;
                enemy.StateTimer -= dt;
                if (enemy.StateTimer <= 0) { enemy.State = 2; enemy.StateTimer = traits.DashDuration; }
            }
            else if (enemy.State == 2)
            {
                enemy.Velocity = enemy.DashDirection * traits.DashSpeed;
                enemy.StateTimer -= dt;
                if (enemy.StateTimer <= 0)
                {
                    rosterState.DashRemaining--;
                    enemy.State = rosterState.DashRemaining > 0 ? 1 : 0;
                    enemy.StateTimer = traits.DashWindup;
                    enemy.DashDirection = direction;
                    enemy.AttackCooldown = RosterAttackCooldown(enemy);
                }
            }
            else if (enemy.AttackCooldown <= 0 && distance < 520)
            {
                enemy.State = 1; enemy.StateTimer = traits.DashWindup;
                enemy.DashDirection = direction; rosterState.DashRemaining = (int)traits.DashCount;
            }
        }

        private void UpdateProgressedGunner(ref EnemyState enemy, float dt, float distance, Vector2 direction, RosterProgressionTraits traits)
        {
            var definition = FindEnemy(enemy.Id);
            var preferred = (float)(definition?.PreferredDistance ?? 320);
            if (distance < preferred - 60) enemy.Velocity *= -.7f;
            else if (distance < preferred + 40) enemy.Velocity *= .15f;
            if (enemy.State == 1)
            {
                enemy.Velocity = Vector2.zero;
                enemy.StateTimer -= dt;
                if (enemy.StateTimer > 0) return;
                var curved = enemy.EliteKind.HasValue;
                var count = (int)traits.ShotCount;
                if (EnemyShotsRemainingForSim() >= count && (!curved || _gameSim.CurvedShotCount + count <= EliteRules.MaxCurvedProjectiles))
                {
                    var slots = curved ? EliteRules.CurvedVolleySlots : count;
                    var gap = EliteRules.CurvedVolleyGapSlot(enemy.Volley);
                    var angle = Mathf.Atan2(enemy.DashDirection.y, enemy.DashDirection.x);
                    var projectileScale = curved ? 1 + ((int)enemy.Roster - 1) * .025f : (float)EnemyRosterRules.ProjectileMultiplier(enemy.Roster);
                    if (curved) projectileScale = enemy.Roster == EnemyRoster.Four ? 1.07f : enemy.Roster == EnemyRoster.Three ? 1.05f : 1.02f;
                    var speed = traits.ShotSpeed > 0 ? traits.ShotSpeed : (float)(definition?.ProjectileSpeed ?? 240) * projectileScale;
                    for (var shot = 0; shot < slots; shot++)
                    {
                        if (curved && shot == gap) continue;
                        var offset = shot - (slots - 1) * .5f;
                        var shotAngle = angle + offset * traits.ShotSpread;
                        SpawnHostileShot(enemy.Position, new Vector2(Mathf.Cos(shotAngle), Mathf.Sin(shotAngle)), enemy.Damage * .62f,
                            speed, curved ? offset * traits.CurvatureAcceleration / (float)EliteRules.CurvedLateralAcceleration : 0,
                            radiusOverride: traits.ProjectileRadius);
                    }
                    enemy.Volley++;
                    _audio?.Play(ProceduralAudio.Cue.GunnerShot, .9f);
                }
                enemy.State = 0; enemy.AttackCooldown = RosterAttackCooldown(enemy);
            }
            else if (enemy.AttackCooldown <= 0 && distance < 700)
            {
                enemy.State = 1; enemy.StateTimer = traits.AimWindup > 0 ? traits.AimWindup : .65f; enemy.DashDirection = direction;
            }
        }

        private Vector2 RosterBlastPosition(EnemyState enemy, RosterProgressionTraits traits, int index)
        {
            ref var rosterState = ref RosterStateFor(enemy);
            var axis = enemy.Id == "exploder" ? rosterState.AttackAxis : new Vector2(-rosterState.AttackAxis.y, rosterState.AttackAxis.x);
            return enemy.DashDirection + axis * ((index - (traits.BlastCount - 1) * .5f) * traits.BlastSpacing);
        }

        private void UpdateProgressedBlast(ref EnemyState enemy, float dt, float distance, Vector2 direction, RosterProgressionTraits traits)
        {
            ref var rosterState = ref RosterStateFor(enemy);
            var mortar = enemy.Id == "mortar";
            if (mortar && distance < 400) enemy.Velocity *= -.65f;
            if (enemy.State == 1)
            {
                enemy.Velocity = Vector2.zero;
                enemy.StateTimer -= dt;
                if (traits.DriftRadius > 0 && enemy.StateTimer > traits.LockSeconds)
                {
                    var fraction = Mathf.Clamp01((enemy.StateTimer - traits.LockSeconds) / (traits.BlastDelay - traits.LockSeconds));
                    enemy.DashDirection = enemy.AimPosition + new Vector2(Mathf.Cos(enemy.Seed + enemy.Age * 2.6f), Mathf.Sin(enemy.Seed + enemy.Age * 3.1f)) * (traits.DriftRadius * fraction);
                }
                // The final 0.45 seconds retain the last warned position.
                if (enemy.StateTimer > 0) return;
                var stagger = enemy.Id == "exploder" && !enemy.EliteKind.HasValue && enemy.Roster == EnemyRoster.Four;
                var first = stagger ? rosterState.BlastIndex : 0;
                var last = stagger ? first + 1 : (int)traits.BlastCount;
                for (var impact = first; impact < last; impact++)
                {
                    var position = RosterBlastPosition(enemy, traits, impact);
                    if (Vector2.Distance(_gameSim.Player.Position, position) < traits.BlastRadius + PlayerRadius)
                        DamagePlayer(enemy.Damage, _gameSim.Player.Position - position);
                    SpawnBlastWave(position, traits.BlastRadius, .5f, false);
                    SpawnImpactMark(position, traits.BlastRadius, 0);
                }
                if (stagger && ++rosterState.BlastIndex < (int)traits.BlastCount) { enemy.StateTimer = .16f; return; }
                enemy.State = 0; enemy.AttackCooldown = RosterAttackCooldown(enemy);
                if (enemy.Id == "exploder")
                {
                    _gameSim.Enemies[enemy.View] = enemy;
                    ResolveEnemyDeath(enemy.View, true);
                    enemy.Active = false;
                }
                return;
            }
            var trigger = mortar ? 760 : enemy.Id == "brute" ? traits.BlastRadius + 45 : traits.ProximityRadius;
            if (enemy.AttackCooldown <= 0 && distance < trigger)
            {
                enemy.State = 1; enemy.StateTimer = traits.BlastDelay;
                enemy.AimPosition = mortar ? _gameSim.Player.Position + _gameSim.Player.Velocity * .24f : enemy.Position;
                enemy.DashDirection = enemy.AimPosition; rosterState.AttackAxis = direction; rosterState.BlastIndex = 0;
                _audio?.Play(ProceduralAudio.Cue.Warning, .8f);
            }
        }

        private void SpawnProgressedChild(EnemyState parent, int index, int count, EnemyRoster tier, bool carrier)
        {
            if (_gameSim.EnemyOrderCount >= _gameSim.Enemies.Length - 1) return;
            if (carrier)
            {
                var live = 0;
                for (var n = 0; n < _gameSim.EnemyOrderCount; n++)
                    if (_gameSim.Enemies[_gameSim.EnemyOrder[n]].SummonedByCarrierSpawnId == parent.SpawnId) live++;
                if (live >= 8) return;
            }
            var angle = parent.Seed + index * Mathf.PI * 2 / Mathf.Max(1, count);
            var identity = _nextEnemyId;
            if (!SpawnEnemy("runner", parent.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 20,
                forcedRoster: tier, carrierDrone: carrier)) return;
            if (!carrier) return;
            for (var n = _gameSim.EnemyOrderCount - 1; n >= 0; n--)
            {
                var slot = _gameSim.EnemyOrder[n];
                if (_gameSim.Enemies[slot].SpawnId != identity) continue;
                _gameSim.Enemies[slot].SummonedByCarrierSpawnId = parent.SpawnId; break;
            }
        }

        private bool DetonateProgressedExploder(EnemyState enemy, int excludedIdentity)
        {
            if (!UsesRosterProgression(enemy) || enemy.Id != "exploder") return false;
            ref var rosterState = ref RosterStateFor(enemy);
            var traits = RosterProgressionTraits.Get(enemy.Id, enemy.Roster, enemy.EliteKind.HasValue);
            enemy.DashDirection = enemy.Position;
            if (rosterState.AttackAxis.sqrMagnitude < .01f) rosterState.AttackAxis = enemy.Facing;
            var attackAxis = rosterState.AttackAxis;
            for (var n = 0; n < (int)traits.BlastCount; n++)
            {
                // A chain kill may reuse this dead actor's slot; retain the original blast axis.
                var position = enemy.Position + attackAxis * ((n - (traits.BlastCount - 1) * .5f) * traits.BlastSpacing);
                SpawnBlastWave(position, traits.BlastRadius, .42f, false);
                DamageArea(position, traits.BlastRadius, 55f + _time * .25f, excludedIdentity);
            }
            return true;
        }

        private Sprite ProgressedEnemySprite(EnemyState enemy)
        {
            if (!UsesRosterProgression(enemy)) return null;
            if (_rosterProgressionVisuals == null) _rosterProgressionVisuals = Resources.Load<RosterProgressionVisualAsset>("VoidFall/RosterProgressionVisuals");
            return _rosterProgressionVisuals == null ? null : _rosterProgressionVisuals.Find(enemy.Id, (int)enemy.Roster, enemy.EliteKind.HasValue);
        }

        private bool RenderProgressedTelegraphs(int slot, EnemyState enemy)
        {
            if (_rosterBlastWarnings == null) _rosterBlastWarnings = new LineRenderer[_gameSim.Enemies.Length * 4];
            for (var n = 0; n < 4; n++) Hide(_rosterBlastWarnings[slot * 4 + n]);
            if (!enemy.Active || !UsesRosterProgression(enemy)) return false;
            ref var rosterState = ref RosterStateFor(enemy);
            var traits = RosterProgressionTraits.Get(enemy.Id, enemy.Roster, enemy.EliteKind.HasValue);
            if (enemy.State == 1 && traits.BlastCount > 0)
            {
                for (var n = rosterState.BlastIndex; n < (int)traits.BlastCount; n++)
                {
                    var index = slot * 4 + n;
                    if (_rosterBlastWarnings[index] == null) _rosterBlastWarnings[index] = CreateLineView("Roster blast warning", 13);
                    SetArcLine(_rosterBlastWarnings[index], RosterBlastPosition(enemy, traits, n), traits.BlastRadius,
                        0, Mathf.PI * 2, 2.2f, new Color(1, .55f, .12f, .8f));
                }
            }
            if (enemy.State == 1 && (traits.ShotCount > 0 || enemy.Id == "dasher"))
            {
                var count = enemy.Id == "dasher" ? 1 : (int)traits.ShotCount;
                var angle = Mathf.Atan2(enemy.DashDirection.y, enemy.DashDirection.x);
                for (var n = 0; n < count; n++)
                {
                    var index = slot * 4 + n;
                    if (_rosterBlastWarnings[index] == null) _rosterBlastWarnings[index] = CreateLineView("Roster aim warning", 13);
                    var line = _rosterBlastWarnings[index];
                    var warningSlot = enemy.EliteKind.HasValue && n >= EliteRules.CurvedVolleyGapSlot(enemy.Volley) ? n + 1 : n;
                    var offset = enemy.EliteKind.HasValue ? warningSlot - 2f : n - (count - 1) * .5f;
                    var aim = angle + offset * traits.ShotSpread;
                    var length = enemy.Id == "dasher" ? traits.DashSpeed * traits.DashDuration : 230f;
                    line.enabled = true; line.loop = false; line.positionCount = 2;
                    line.startWidth = line.endWidth = 2;
                    line.startColor = line.endColor = new Color(1, .45f, .25f, .65f);
                    line.SetPosition(0, enemy.Position);
                    line.SetPosition(1, enemy.Position + new Vector2(Mathf.Cos(aim), Mathf.Sin(aim)) * length);
                }
            }
            return true;
        }
    }
}
