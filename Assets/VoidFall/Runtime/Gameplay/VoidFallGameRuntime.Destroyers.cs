using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private struct DestroyerActor
        {
            public int Identity, AttackSerial, RaidMember;
            public Vector2 Origin, Aim;
            public bool SweepThisStep, Withdrawing, HasAttacked;
        }
        private readonly DestroyerActor[] _destroyers = new DestroyerActor[MaxEnemies];
        private readonly int[] _destroyerHitIdentities = new int[MaxEnemies * DestroyerContent.RaidCount];
        private readonly int[] _destroyerHitSerials = new int[MaxEnemies * DestroyerContent.RaidCount];
        private readonly int[] _destroyerPlayerHitSerials = new int[DestroyerContent.RaidCount];
        private readonly LineRenderer[] _destroyerWarnings = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _destroyerHealth = new LineRenderer[MaxEnemies];
        private readonly Sprite[,] _destroyerSprites = new Sprite[5, 8];
        private bool _destroyerSpritesLoaded, _destroyerRaidActive, _destroyerWithdrawing;
        private Vector2 _destroyerRaidCenter;
        private int _destroyerAttackSequence;
        private float _destroyerRaidElapsed;
        private bool _destroyerRaidResolved;
        private Material _destroyerLineMaterial;
        private static readonly string[] DestroyerPoseNames = { "idle0", "idle1", "idle2", "idle3", "windup", "attack", "recover", "hit" };
        private static int DestroyerType(string id)
        {
            for (var i = 0; i < DestroyerContent.Enemies.Length; i++) if (DestroyerContent.Enemies[i].Id == id) return i;
            return -1;
        }
        private void StartDestroyerRaid(Vector2 center)
        {
            EndDestroyerRaid();
            _destroyerRaidActive = true; _destroyerRaidCenter = center; _destroyerWithdrawing = false;
            _destroyerRaidElapsed = 0; _destroyerRaidResolved = false;
            var healthMultiplier = DestroyerContent.RaidHealthMultiplier(_encounterInitialized ? DirectorChallengeSeconds : _time);
            var admitted = 0;
            for (var member = 0; member < DestroyerContent.RaidCount; member++)
            {
                var type = DestroyerContent.RaidTypeAt(member);
                var angle = (member - 3.5f) * .25f;
                var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * DestroyerContent.RaidEntryDistance(type);
                // A raid is an independent finite source, even when initiated during another callback.
                using (new FactionScope(this, -1, 0, CombatFaction.Destroyer, 0))
                    if (!SpawnEnemy(DestroyerContent.Enemies[type].Id, position, healthMultiplier: healthMultiplier, forcedRoster: EnemyRoster.One))
                    {
                        RecordRunHistory("destroyer_raid_deployed", reason: "admission_failed", instanceId: _incidentSequence,
                            amount: admitted, detail: "requested=8;cancelled=true");
                        EndDestroyerRaid(); return;
                    }
                var slot = _gameSim.EnemyOrder[_gameSim.EnemyOrderCount - 1];
                _destroyers[slot] = new DestroyerActor { Identity = _gameSim.Enemies[slot].SpawnId, RaidMember = member };
                admitted++;
            }
            RecordRunHistory("destroyer_raid_deployed", reason: "admitted", instanceId: _incidentSequence,
                amount: admitted, detail: "requested=8;maw=2;razor=2;husk=1;grasp=1;spite=2");
        }
        private void StepDestroyerRaid(float dt, bool withdrawing)
        {
            if (!_destroyerRaidActive || dt <= 0) return;
            _destroyerRaidElapsed += dt;
            _destroyerWithdrawing = withdrawing;
            if (withdrawing) CancelDestroyerShots();
            if (!_destroyerRaidResolved)
            {
                var alive = ActiveDestroyerRaidBodies();
                if (alive == 0 || withdrawing) RecordDestroyerRaidResolved(alive == 0 ? "defeated" : "release", alive);
            }
        }
        private void EndDestroyerRaid()
        {
            if (_destroyerRaidActive && !_destroyerRaidResolved && _gameSim != null)
                RecordDestroyerRaidResolved("cancelled", ActiveDestroyerRaidBodies());
            _destroyerRaidActive = false; _destroyerWithdrawing = false;
            if (_gameSim == null) return;
            CancelDestroyerShots();
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var enemy = _gameSim.Enemies[i];
                if (DestroyerType(enemy.Id) < 0) continue;
                HideDestroyerPresentation(i);
                if (!enemy.Active) continue;
                enemy.Active = false; enemy.Velocity = Vector2.zero;
                _gameSim.Enemies[i] = enemy; RemoveEnemyOrder(i); Hide(_enemyViews[i]);
                _rewardRoots.Release(_factionActors[i].Root); _factionActors[i] = default;
            }
            Array.Clear(_destroyers, 0, _destroyers.Length);
        }
        private int ActiveDestroyerRaidBodies()
        {
            var count = 0;
            for (var n = 0; n < _gameSim.EnemyOrderCount; n++)
            {
                var enemy = _gameSim.Enemies[_gameSim.EnemyOrder[n]];
                if (enemy.Active && DestroyerType(enemy.Id) >= 0) count++;
            }
            return count;
        }
        private void RecordDestroyerRaidResolved(string reason, int survivors)
        {
            _destroyerRaidResolved = true;
            RecordRunHistory("destroyer_raid_resolved", MajorIncidentKind.DestroyerRaid.ToString(), reason,
                instanceId: _incidentSequence, amount: survivors, durationSeconds: _destroyerRaidElapsed);
        }
        private void CancelDestroyerShots()
        {
            for (var i = 0; i < _gameSim.HostileShots.Length; i++)
            {
                if (_gameSim.HostileShotSources[i].Faction != CombatFaction.Destroyer) continue;
                _gameSim.RetireHostileShot(i);
                Hide(_hostileShotViews[i]);
            }
        }
        private bool TryUpdateDestroyer(ref EnemyState enemy, float dt, float distance, Vector2 direction)
        {
            var type = DestroyerType(enemy.Id); if (type < 0) return false;
            if (dt <= 0) return true;
            ref var actor = ref _destroyers[enemy.View];
            if (actor.Identity != enemy.SpawnId) actor = new DestroyerActor { Identity = enemy.SpawnId };
            actor.SweepThisStep = false;
            if (_destroyerWithdrawing)
            {
                actor.Withdrawing = true; enemy.State = 0;
                var away = enemy.Position - _destroyerRaidCenter;
                enemy.Velocity = (away.sqrMagnitude > 1 ? away.normalized : Vector2.right) * 220;
                HideDestroyerPresentation(enemy.View); return true;
            }
            var definition = DestroyerContent.Enemies[type];
            if (enemy.State == 0)
            {
                enemy.Facing = direction;
                enemy.Velocity = direction * enemy.Speed;
                if (type == 4 && distance < 350) enemy.Velocity *= -.5f;
                // Maw establishes its full warning before the simultaneous ranged/fast arrivals
                // compete for attention. The attack telegraph itself is never shortened.
                var arrivalSeconds = type == 0 ? .2f : .4f;
                if (distance <= (float)definition.PreferredDistance && enemy.AttackCooldown <= 0 && enemy.Age > arrivalSeconds &&
                    DestroyerAttackAttentionAvailable())
                {
                    enemy.State = 1; enemy.StateTimer = (float)definition.TelegraphSeconds;
                    actor.Origin = enemy.Position; actor.Aim = direction;
                    actor.AttackSerial = ++_destroyerAttackSequence;
                    enemy.DashDirection = direction; enemy.Velocity = Vector2.zero;
                    enemy.Knockback = Vector2.zero;
                }
            }
            else if (enemy.State == 1)
            {
                enemy.Knockback = Vector2.zero;
                enemy.Velocity = Vector2.zero; enemy.StateTimer -= dt;
                if (enemy.StateTimer > 0) return true;
                enemy.State = 2; enemy.StateTimer = type == 0 ? .47f : type == 1 ? .45f : .2f;
                RecordRunHistory("destroyer_attack", enemy.Id, actor.HasAttacked ? "repeat" : "first",
                    instanceId: enemy.SpawnId, relatedInstanceId: _incidentSequence, amount: actor.AttackSerial,
                    hp: enemy.Health, maxHp: enemy.MaxHealth, durationSeconds: enemy.Age);
                actor.HasAttacked = true;
                if (type == 2 || type == 3)
                    FactionBlast(actor.Origin, type == 2 ? 145 : 165, enemy.Damage, actor.Aim, type == 2 ? Mathf.Cos(1.05f) : -1);
                if (type == 4)
                {
                    var angle = Mathf.Atan2(actor.Aim.y, actor.Aim.x);
                    for (var bolt = -1; bolt <= 1; bolt++)
                    {
                        var shotAngle = angle + bolt * .16f;
                        SpawnHostileShot(enemy.Position, new Vector2(Mathf.Cos(shotAngle), Mathf.Sin(shotAngle)), enemy.Damage, 215, 0);
                    }
                }
            }
            else if (enemy.State == 2)
            {
                enemy.Knockback = Vector2.zero;
                var activeStep = Mathf.Min(dt, Mathf.Max(0, enemy.StateTimer));
                enemy.Velocity = type < 2 ? actor.Aim * ((type == 0 ? 620 : 690) * activeStep / Mathf.Max(.00001f, dt)) : Vector2.zero;
                actor.SweepThisStep = type < 2 && activeStep > 0;
                enemy.StateTimer -= dt;
                if (enemy.StateTimer <= 0) { enemy.State = 3; enemy.StateTimer = (float)definition.RecoverySeconds; }
            }
            else
            {
                enemy.Velocity = Vector2.zero; enemy.StateTimer -= dt;
                if (enemy.StateTimer <= 0) enemy.State = 0;
            }
            return true;
        }
        private bool DestroyerAttackAttentionAvailable()
        {
            var active = 0;
            var limit = DirectorProfiles.AttackLimit(_runDirectorProfile, PressureHundredths);
            for (var n = 0; n < _gameSim.EnemyOrderCount; n++)
            {
                var other = _gameSim.Enemies[_gameSim.EnemyOrder[n]];
                if (!other.Active || (other.State != 1 && other.State != 2)) continue;
                if (DestroyerType(other.Id) >= 0 || IsDemandingEnemy(other.Id) || other.EliteKind.HasValue)
                    if (++active >= limit) return false;
            }
            return true;
        }
        private void ResolveDestroyerSweep(EnemyState enemy, Vector2 previousPosition)
        {
            var type = DestroyerType(enemy.Id); if (type < 0) return;
            var actor = _destroyers[enemy.View]; if (!actor.SweepThisStep) return;
            if (_destroyerPlayerHitSerials[actor.RaidMember] != actor.AttackSerial &&
                SegmentBodyFraction(previousPosition, enemy.Position, _gameSim.Player.Position, enemy.Radius + AttackPlayerRadius) <= 1)
            {
                _destroyerPlayerHitSerials[actor.RaidMember] = actor.AttackSerial;
                DamagePlayer(enemy.Damage, actor.Aim);
            }
            // At most four dash actors; duplicate roles retain independent hit histories.
            for (var slot = 0; slot < _gameSim.Enemies.Length; slot++)
            {
                var target = _gameSim.Enemies[slot]; var hitSlot = actor.RaidMember * MaxEnemies + slot;
                if (!target.Active || !FactionRewardRules.Hostile(CombatFaction.Destroyer, FactionOf(target)) ||
                    (_destroyerHitIdentities[hitSlot] == target.SpawnId && _destroyerHitSerials[hitSlot] == actor.AttackSerial) ||
                    SegmentBodyFraction(previousPosition, enemy.Position, target.Position, enemy.Radius + target.Radius) > 1) continue;
                _destroyerHitIdentities[hitSlot] = target.SpawnId; _destroyerHitSerials[hitSlot] = actor.AttackSerial;
                ApplyEnemyDamage(slot, enemy.Damage, actor.Aim, 0, false, -1);
            }
        }
        private LineRenderer DestroyerLine(ref LineRenderer line, string name, float width)
        {
            if (line != null) return line;
            if (_destroyerLineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _destroyerLineMaterial = new Material(shader);
            }
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true;
            line.sharedMaterial = _destroyerLineMaterial; line.widthMultiplier = width;
            line.startColor = line.endColor = Color.white; line.sortingOrder = 19;
            return line;
        }
        private bool TryRenderDestroyer(int slot, EnemyState enemy)
        {
            var type = DestroyerType(enemy.Id);
            if (type < 0) { HideDestroyerPresentation(slot); return false; }
            if (!_destroyerSpritesLoaded)
            {
                for (var t = 0; t < 5; t++)
                    for (var p = 0; p < 8; p++)
                        _destroyerSprites[t, p] = Resources.Load<Sprite>("VoidFall/Destroyers/" + DestroyerContent.Enemies[t].Id.Substring(10) + "/" + DestroyerPoseNames[p]);
                _destroyerSpritesLoaded = true;
            }
            var pose = enemy.HitTimer > 0 ? 7 : enemy.State == 1 ? 4 : enemy.State == 2 ? 5 : enemy.State == 3 ? 6 : (int)(enemy.Age * 7) % 4;
            var view = EnsureEnemyView(slot); view.sprite = _destroyerSprites[type, pose]; view.color = Color.white;
            view.transform.position = enemy.Position;
            var aim = enemy.State == 0 ? enemy.Facing : _destroyers[slot].Aim;
            view.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            view.transform.localScale = Vector3.one * (enemy.Radius / 64f); view.enabled = enemy.Active;
            var health = DestroyerLine(ref _destroyerHealth[slot], "Destroyer health", 3);
            health.positionCount = 2; var healthStart = enemy.Position + new Vector2(-enemy.Radius, enemy.Radius + 14);
            health.SetPosition(0, healthStart); health.SetPosition(1, healthStart + Vector2.right * (enemy.Radius * 2 * Mathf.Clamp01(enemy.Health / enemy.MaxHealth)));
            health.enabled = enemy.Active && !_destroyerWithdrawing;
            if (enemy.State != 1 || _destroyerWithdrawing) { if (_destroyerWarnings[slot] != null) _destroyerWarnings[slot].enabled = false; return true; }
            var warning = DestroyerLine(ref _destroyerWarnings[slot], "Destroyer attack warning", 2.5f);
            warning.enabled = true; var actor = _destroyers[slot]; var side = new Vector2(-actor.Aim.y, actor.Aim.x);
            if (type < 2)
            {
                var end = actor.Origin + actor.Aim * (type == 0 ? 620 * .47f : 690 * .45f);
                warning.loop = true; warning.positionCount = 4;
                warning.SetPosition(0, actor.Origin + side * enemy.Radius); warning.SetPosition(1, end + side * enemy.Radius);
                warning.SetPosition(2, end - side * enemy.Radius); warning.SetPosition(3, actor.Origin - side * enemy.Radius);
            }
            else if (type == 4)
            {
                warning.loop = false; warning.positionCount = 6; var angle = Mathf.Atan2(actor.Aim.y, actor.Aim.x);
                for (var bolt = 0; bolt < 3; bolt++)
                { warning.SetPosition(bolt * 2, actor.Origin); warning.SetPosition(bolt * 2 + 1, actor.Origin + new Vector2(Mathf.Cos(angle + (bolt - 1) * .16f), Mathf.Sin(angle + (bolt - 1) * .16f)) * 510); }
            }
            else
            {
                warning.loop = true; warning.positionCount = type == 2 ? 34 : 64;
                var angle = Mathf.Atan2(actor.Aim.y, actor.Aim.x);
                if (type == 2) warning.SetPosition(0, actor.Origin);
                for (var n = type == 2 ? 1 : 0; n < warning.positionCount; n++)
                {
                    var a = type == 2 ? angle - 1.05f + (n - 1) / 32f * 2.1f : n / 64f * Mathf.PI * 2;
                    warning.SetPosition(n, actor.Origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (type == 2 ? 145 : 165));
                }
            }
            return true;
        }
        private void HideDestroyerPresentation(int slot)
        {
            if (_destroyerWarnings[slot] != null) _destroyerWarnings[slot].enabled = false;
            if (_destroyerHealth[slot] != null) _destroyerHealth[slot].enabled = false;
        }
        private void DestroyDestroyerPresentation()
        {
            for (var i = 0; i < MaxEnemies; i++)
            {
                if (_destroyerWarnings[i] != null) Destroy(_destroyerWarnings[i].gameObject);
                if (_destroyerHealth[i] != null) Destroy(_destroyerHealth[i].gameObject);
            }
            if (_destroyerLineMaterial != null) Destroy(_destroyerLineMaterial);
            // Resource sprites are imported shared assets; never destroy them.
        }
    }
}
