using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private struct HydraPopulationState
        {
            public int Identity, Kind, Drones;
            public float Cooldown, Warning, Dash, Ammo, HealClock, ReportClock;
            public Vector2 Target, DroneA, DroneB;
            public bool Dead, Child;
        }
        private struct HydraPopulationBirth
        {
            public int ParentIdentity, Kind, Visit;
            public ArenaId Arena;
            public Vector2 Position;
            public long RewardRoot;
        }
        private readonly HydraPopulationBirth[] _hydraPopulationBirths = new HydraPopulationBirth[MaxEnemies * 2];
        private int _hydraPopulationBirthHead, _hydraPopulationBirthCount;

        private readonly HydraPopulationState[] _hydraPopulation = new HydraPopulationState[MaxEnemies];
        private readonly LineRenderer[] _hydraPopulationWarnings = new LineRenderer[MaxEnemies];
        private readonly SpriteRenderer[] _hydraRepairDroneA = new SpriteRenderer[MaxEnemies];
        private readonly SpriteRenderer[] _hydraRepairDroneB = new SpriteRenderer[MaxEnemies];
        private int _hydraPopulationAttempt;
        private int _hydraPopulationChildKind = -1;

        private int SelectHydraPopulation(ref string id, bool elite, bool forcedChild)
        {
            if (id.StartsWith("hydra-", StringComparison.Ordinal)) return -1;
            if (_hydraPopulationChildKind >= 0 && forcedChild && !elite)
            {
                id = HydraPopulationRules.BaseId(_hydraPopulationChildKind);
                return _hydraPopulationChildKind;
            }
            if (!CurrentVoidIsHydra || _hydraBossEncounterActive || _hydraBossSpawnedForVoid || elite || forcedChild) return -1;
            var kind = HydraPopulationRules.KindForAttempt(_hydraPopulationAttempt);
            _hydraPopulationAttempt = (_hydraPopulationAttempt + 1) % HydraPopulationRules.Count;
            id = HydraPopulationRules.BaseId(kind);
            return kind;
        }

        private void ResetHydraPopulation()
        {
            ClearHydraPopulationBirths("arena_reset");
            Array.Clear(_hydraPopulation, 0, _hydraPopulation.Length);
            _hydraPopulationAttempt = 0;
            _hydraPopulationChildKind = -1;
            for (var i = 0; i < MaxEnemies; i++) HideHydraPopulationExtras(i);
        }

        private void InitHydraPopulation(int slot, ref EnemyState enemy, int kind)
        {
            if (!HydraPopulationRules.IsValid(kind)) return;
            _hydraPopulation[slot] = new HydraPopulationState
            {
                Identity = enemy.SpawnId, Kind = kind, Cooldown = 2f, Child = enemy.SplitterFragment,
                DroneA = enemy.Position, DroneB = enemy.Position,
            };
            enemy.MutationGene = MutationGene.None;
            enemy.Shield = enemy.MaxShield = 0;
            enemy.Facing = (_gameSim.Player.Position - enemy.Position).normalized;
            RecordRunHistory(HydraPopulationRules.IsVirus(kind) ? "virus" : "hybrid",
                HydraPopulationRules.StableId(kind), reason: enemy.SplitterFragment ? "offspring" : "spawned",
                sourceId: HydraPopulationRules.BaseId(kind), instanceId: enemy.SpawnId,
                hp: enemy.Health, maxHp: enemy.MaxHealth, detail: HydraPopulationRules.Parents(kind), position: enemy.Position);
        }

        private bool IsHydraPopulation(EnemyState enemy) => enemy.View >= 0 && enemy.View < MaxEnemies &&
            _hydraPopulation[enemy.View].Identity == enemy.SpawnId && enemy.SpawnId != 0;

        private float HydraPopulationIncomingDamageMultiplier(EnemyState enemy, Vector2 direction) =>
            IsHydraPopulation(enemy) ? HydraPopulationRules.IncomingDamageMultiplier(_hydraPopulation[enemy.View].Kind,
                Vector2.Dot(enemy.Facing, -direction.normalized)) : 1f;

        private void HydraPopulationAbility(EnemyState enemy, int kind, string ability, string reason, float amount = 0, Vector2? position = null)
        {
            RecordRunHistory("hydra_population_ability", HydraPopulationRules.StableId(kind), reason: reason,
                sourceId: ability, instanceId: enemy.SpawnId, amount: amount, detail: HydraPopulationRules.Parents(kind), position: position ?? enemy.Position);
        }

        private bool TryUpdateHydraPopulation(ref EnemyState enemy, float dt, float distance, Vector2 direction, ref float globalHarvesterXp)
        {
            if (!IsHydraPopulation(enemy)) return false;
            var state = _hydraPopulation[enemy.View];
            if (state.Dead) return false;
            var kind = (HydraPopulationKind)state.Kind;
            enemy.Facing = state.Dash > 0 ? enemy.DashDirection : direction;
            enemy.Rotation = Mathf.Atan2(enemy.Facing.y, enemy.Facing.x) - Mathf.PI * .5f;
            enemy.Velocity = direction * enemy.Speed;
            state.Cooldown -= dt;
            if (kind == HydraPopulationKind.Reclaimer)
            {
                var storedBefore = enemy.StoredXp;
                UpdateHarvester(ref enemy, dt, direction, ref globalHarvesterXp);
                state.Ammo += Mathf.Max(0, enemy.StoredXp - storedBefore);
            }
            if (kind == HydraPopulationKind.Graft)
            {
                var healed = Mathf.Min(enemy.MaxHealth - enemy.Health, enemy.MaxHealth * .055f * dt);
                enemy.Health += Mathf.Max(0, healed);
                state.HealClock += Mathf.Max(0, healed);
                if (state.Cooldown <= 0)
                {
                    if (state.HealClock > 0) HydraPopulationAbility(enemy, state.Kind, "regenerate", "healed", state.HealClock);
                    state.HealClock = 0; state.Cooldown = 3.8f;
                }
            }
            if (state.Dash > 0)
            {
                state.Dash = Mathf.Max(0, state.Dash - dt);
                enemy.Velocity = enemy.DashDirection * enemy.Speed * 3.2f;
            }
            if (state.Warning > 0)
            {
                state.Warning -= dt;
                enemy.Velocity = Vector2.zero;
                if (state.Warning <= 0)
                {
                    if (kind == HydraPopulationKind.Bastion || kind == HydraPopulationKind.Rachis)
                    {
                        var angle = Mathf.Atan2(state.Target.y, state.Target.x);
                        var fan = kind == HydraPopulationKind.Bastion ? 1 : 2;
                        for (var j = -fan; j <= fan; j++)
                        {
                            var shotAngle = angle + j * (fan == 1 ? .22f : .18f);
                            SpawnHostileShot(enemy.Position, new Vector2(Mathf.Cos(shotAngle), Mathf.Sin(shotAngle)),
                                enemy.Damage * .65f, fan == 1 ? 240f : 220f, 0f);
                        }
                        HydraPopulationAbility(enemy, state.Kind, "fan", "fired", fan * 2 + 1);
                    }
                    else if (kind == HydraPopulationKind.Reclaimer || kind == HydraPopulationKind.Bloat)
                    {
                        var center = kind == HydraPopulationKind.Bloat ? enemy.Position : state.Target;
                        SpawnRingWave(center, 8f, HydraPopulationRules.BlastRadius, .3f, new Color(.68f,.72f,.42f,.8f));
                        if ((_gameSim.Player.Position - center).sqrMagnitude < HydraPopulationRules.BlastRadius * HydraPopulationRules.BlastRadius)
                            DamagePlayer(enemy.Damage, _gameSim.Player.Position - center);
                        HydraPopulationAbility(enemy, state.Kind, "ground_blast", "detonated", enemy.Damage, center);
                        if (kind == HydraPopulationKind.Bloat)
                        {
                            _hydraPopulation[enemy.View] = state;
                            _gameSim.Enemies[enemy.View] = enemy;
                            ResolveEnemyDeath(enemy.View, true);
                            enemy = _gameSim.Enemies[enemy.View];
                            return true;
                        }
                    }
                    else
                    {
                        state.Dash = .55f; enemy.DashDirection = state.Target;
                        HydraPopulationAbility(enemy, state.Kind, "lunge", "started", .55f);
                    }
                }
            }
            else if (state.Cooldown <= 0 && kind != HydraPopulationKind.Graft && kind != HydraPopulationKind.Cleft)
            {
                if (kind == HydraPopulationKind.Broodsmith)
                {
                    if (state.Drones < HydraPopulationRules.RepairDroneLimit)
                    {
                        state.Drones++;
                        HydraPopulationAbility(enemy, state.Kind, "repair_drone", "released", state.Drones);
                    }
                    state.Cooldown = 3.8f;
                }
                else if ((kind != HydraPopulationKind.Reclaimer || state.Ammo >= 1) &&
                    (kind != HydraPopulationKind.Bloat || distance < 160f) && CanCommitDirectorAttack(enemy))
                {
                    state.Warning = kind == HydraPopulationKind.Reclaimer ? HydraPopulationRules.BlastWarningSeconds : .7f;
                    state.Target = kind == HydraPopulationKind.Reclaimer ? _gameSim.Player.Position : direction;
                    state.Cooldown = 3.8f;
                    if (kind == HydraPopulationKind.Reclaimer) state.Ammo -= 1;
                    HydraPopulationAbility(enemy, state.Kind, "warning", "armed", state.Warning, kind == HydraPopulationKind.Reclaimer ? state.Target : enemy.Position);
                }
            }
            if (kind == HydraPopulationKind.Broodsmith)
            {
                state.ReportClock += dt;
                for (var drone = 0; drone < state.Drones; drone++)
                {
                    var p = drone == 0 ? state.DroneA : state.DroneB;
                    var target = FindHydraRepairTarget(enemy, p);
                    var destination = target >= 0 ? _gameSim.Enemies[target].Position : enemy.Position;
                    p = Vector2.MoveTowards(p, destination, 110f * dt);
                    if (target >= 0 && (p - destination).sqrMagnitude < 45f * 45f)
                    {
                        var ally = _gameSim.Enemies[target];
                        var healed = Mathf.Min(ally.MaxHealth - ally.Health, HydraPopulationRules.RepairPerSecond * dt);
                        ally.Health += Mathf.Max(0, healed);
                        _gameSim.Enemies[target] = ally;
                        state.HealClock += Mathf.Max(0, healed);
                    }
                    if (drone == 0) state.DroneA = p; else state.DroneB = p;
                }
                if (state.ReportClock >= 3.8f)
                {
                    if (state.HealClock > 0) HydraPopulationAbility(enemy, state.Kind, "repair_drone", "healed", state.HealClock);
                    state.HealClock = 0; state.ReportClock = 0;
                }
            }
            enemy.State = state.Warning > 0 ? 1 : state.Dash > 0 ? 2 : 0;
            enemy.StateTimer = Mathf.Max(state.Warning, state.Dash);
            _hydraPopulation[enemy.View] = state;
            return true;
        }

        private int FindHydraRepairTarget(EnemyState owner, Vector2 position)
        {
            var nearest = -1; var distance = 420f * 420f;
            for (var order = 0; order < _gameSim.EnemyOrderCount; order++)
            {
                var slot = _gameSim.EnemyOrder[order]; var candidate = _gameSim.Enemies[slot];
                if (!candidate.Active || candidate.SpawnId == owner.SpawnId || candidate.Health >= candidate.MaxHealth ||
                    FactionOf(candidate) != FactionOf(owner)) continue;
                var d = (position - candidate.Position).sqrMagnitude;
                if (d >= distance) continue;
                nearest = slot; distance = d;
            }
            return nearest;
        }

        private void OnHydraPopulationDeath(EnemyState enemy)
        {
            if (!IsHydraPopulation(enemy)) return;
            var state = _hydraPopulation[enemy.View];
            if (state.Dead) return;
            state.Dead = true;
            _hydraPopulation[enemy.View] = state;
            HideHydraPopulationExtras(enemy.View);
            if (!HydraPopulationRules.Splits(state.Kind) || state.Child) return;
            var queued = 0;
            for (var i = 0; i < 2 && _hydraPopulationBirthCount < _hydraPopulationBirths.Length; i++)
            {
                var index = (_hydraPopulationBirthHead + _hydraPopulationBirthCount) % _hydraPopulationBirths.Length;
                _hydraPopulationBirths[index] = new HydraPopulationBirth
                {
                    ParentIdentity = enemy.SpawnId, Kind = state.Kind, Arena = _arenaId, Visit = _pressureStageIndex,
                    Position = enemy.Position + new Vector2(i == 0 ? -30 : 30, 0), RewardRoot = CaptureFactionBirthRoot(),
                };
                _hydraPopulationBirthCount++; queued++;
            }
            HydraPopulationAbility(enemy, state.Kind, "split", queued == 2 ? "queued" : "queue_capacity_limited", queued);
        }

        private void UpdateHydraPopulationBirths()
        {
            if (_hydraBossEncounterActive || _hydraBossSpawnedForVoid || JourneyStopsCombat)
            {
                ClearHydraPopulationBirths("encounter_ended");
                return;
            }
            while (_hydraPopulationBirthCount > 0)
            {
                var birth = _hydraPopulationBirths[_hydraPopulationBirthHead];
                if (birth.Arena != _arenaId || birth.Visit != _pressureStageIndex)
                {
                    RetireHydraPopulationBirth(birth, "arena_changed", 0);
                    continue;
                }
                // A full pool is temporary pressure. Keep the reserved child and its
                // retained reward ancestry until a real slot becomes available.
                if (FindInactive(_gameSim.Enemies) < 0) return;
                var previousChildKind = _hydraPopulationChildKind;
                _hydraPopulationChildKind = birth.Kind;
                bool spawned;
                try
                {
                    using (FactionBirthScope(birth.RewardRoot))
                        spawned = SpawnEnemy(HydraPopulationRules.BaseId(birth.Kind), birth.Position,
                            splitterFragment: true, forcedRoster: EnemyRoster.One);
                }
                finally { _hydraPopulationChildKind = previousChildKind; }
                if (!spawned) return;
                RetireHydraPopulationBirth(birth, "released", _nextEnemyId - 1);
            }
        }

        private void RetireHydraPopulationBirth(HydraPopulationBirth birth, string reason, int childIdentity)
        {
            ReleaseFactionBirthRoot(birth.RewardRoot);
            _hydraPopulationBirths[_hydraPopulationBirthHead] = default;
            _hydraPopulationBirthHead = (_hydraPopulationBirthHead + 1) % _hydraPopulationBirths.Length;
            _hydraPopulationBirthCount--;
            RecordRunHistory("hydra_population_ability", HydraPopulationRules.StableId(birth.Kind), reason: reason,
                sourceId: "split", instanceId: birth.ParentIdentity, relatedInstanceId: childIdentity,
                amount: childIdentity > 0 ? 1 : 0, detail: HydraPopulationRules.Parents(birth.Kind), position: birth.Position);
        }

        private void ClearHydraPopulationBirths(string reason)
        {
            while (_hydraPopulationBirthCount > 0)
                RetireHydraPopulationBirth(_hydraPopulationBirths[_hydraPopulationBirthHead], reason, 0);
            _hydraPopulationBirthHead = 0;
        }

        private void WarmHydraPopulationVisuals()
        {
            for (var kind = 0; kind < HydraPopulationRules.Count; kind++) ProceduralSpriteFactory.HydraPopulation(kind);
        }
        private void HideHydraPopulationExtras(int slot)
        {
            if (_hydraPopulationWarnings[slot] != null) _hydraPopulationWarnings[slot].enabled = false;
            if (_hydraRepairDroneA[slot] != null) _hydraRepairDroneA[slot].enabled = false;
            if (_hydraRepairDroneB[slot] != null) _hydraRepairDroneB[slot].enabled = false;
        }
        private readonly Sprite[] _approvedHydraLegacySprites = new Sprite[HydraPopulationRules.Count];
        private bool TryRenderHydraPopulation(int index, EnemyState enemy)
        {
            HideHydraPopulationExtras(index);
            if (!IsHydraPopulation(enemy)) return false;
            var state = _hydraPopulation[index];
            var view = _enemyViews[index];
            var sprite = _approvedHydraLegacySprites[state.Kind];
            if (sprite == null)
                sprite = _approvedHydraLegacySprites[state.Kind] = ApprovedMapSprite("hydra-legacy-" + state.Kind);
            view.sprite = sprite; view.transform.position = enemy.Position;
            view.transform.rotation = Quaternion.Euler(0,0,enemy.Rotation * Mathf.Rad2Deg);
            // Exported at the approved physical size, independently of collision radius.
            var scale = (state.Child ? .7f : 1f) * SourceEnemyIntroScale(enemy.Age);
            var reducedMotion = _saveData?.settings != null && _saveData.settings.reducedMotion;
            if (state.Kind == (int)HydraPopulationKind.Bloat)
                scale *= 1f + (state.Warning > 0 ? (1f - state.Warning / .7f) * .22f : 0f) +
                    (reducedMotion ? 0f : Mathf.Sin(enemy.Age * 2.5f) * .045f);
            else if (state.Kind == (int)HydraPopulationKind.Graft && !reducedMotion)
                scale *= 1f + Mathf.Sin(enemy.Age * 2f) * .02f;
            view.transform.localScale = Vector3.one * scale;
            view.color = enemy.HitTimer > 0 ? new Color(1,.95f,.76f,1) : Color.white;
            view.enabled = enemy.Active;
            if (state.Warning > 0)
            {
                var center = state.Kind == (int)HydraPopulationKind.Reclaimer ? state.Target : enemy.Position;
                var radius = state.Kind == (int)HydraPopulationKind.Reclaimer || state.Kind == (int)HydraPopulationKind.Bloat
                    ? HydraPopulationRules.BlastRadius : enemy.Radius * 1.8f;
                var warning = _hydraPopulationWarnings[index];
                if (warning == null) warning = _hydraPopulationWarnings[index] = CreateLineView("Hydra Specimen Warning " + index, 20);
                warning.positionCount = 49;
                for (var k = 0; k <= 48; k++)
                {
                    var a = k * Mathf.PI * 2f / 48;
                    warning.SetPosition(k, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
                warning.startWidth = warning.endWidth = 2;
                warning.startColor = warning.endColor = new Color(1,.73f,.38f,.85f);
                warning.enabled = true;
            }
            if (state.Drones > 0) RenderHydraRepairDrone(ref _hydraRepairDroneA[index], state.DroneA);
            if (state.Drones > 1) RenderHydraRepairDrone(ref _hydraRepairDroneB[index], state.DroneB);
            return true;
        }
        private void RenderHydraRepairDrone(ref SpriteRenderer view, Vector2 position)
        {
            if (view == null) view = CreateView("Hydra Repair Drone", ProceduralSpriteFactory.Circle(), 20);
            view.transform.position = position;
            view.transform.localScale = Vector3.one * (18f / view.sprite.bounds.size.x);
            view.color = new Color(.63f,.68f,.44f,1); view.enabled = true;
        }
    }
}
