using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const string HydraBossId = "hydra-prime";
        private const float HydraBossVerticalOffset = 245f;
        private const int HydraRibShotVariant = 31;
        private const float HydraBombFuseSeconds = 0.48f;
        private const float HydraBombBlastSeconds = 0.62f;

        private struct HydraBombState
        {
            public bool Active;
            public bool DamageApplied;
            public Vector2 Position;
            public float Fuse;
            public float BlastTimer;
            public float Damage;
        }

        private static readonly Vector2[] HydraEvasionSocketOffsets =
        {
            new Vector2(-360f, 170f),
            new Vector2(-180f, 220f),
            new Vector2(180f, 220f),
            new Vector2(360f, 170f),
            new Vector2(360f, -160f),
            new Vector2(-360f, -160f),
        };

        private readonly HydraBombState[] _hydraBombs =
            new HydraBombState[HydraEncounterRules.MarrowBombCount];
        private readonly double[] _hydraMarrowIntervals =
            new double[HydraEncounterRules.MarrowBombCount];
        private readonly int[] _hydraEvasionOrder =
            new int[HydraEncounterRules.EvasionSocketCount];
        private int _hydraPreviousEvasionSocket = -1;
        private readonly LineRenderer[] _hydraTentacleViews = new LineRenderer[10];
        private readonly LineRenderer[] _hydraRibCageViews = new LineRenderer[4];
        private readonly LineRenderer[] _hydraBombRingViews =
            new LineRenderer[HydraEncounterRules.MarrowBombCount];
        private readonly SpriteRenderer[] _hydraBombViews =
            new SpriteRenderer[HydraEncounterRules.MarrowBombCount];
        private readonly LineRenderer[] _hydraEvasionSocketViews =
            new LineRenderer[HydraEncounterRules.EvasionSocketCount];
        private LineRenderer _hydraSpineView;
        private Material _hydraBossMaterial;
        private Sprite _hydraBossSprite;

        private static readonly int HydraDamageProgressProperty =
            Shader.PropertyToID("_DamageProgress");
        private static readonly int HydraPixelCellsProperty =
            Shader.PropertyToID("_PixelCells");
        private static readonly int HydraToxicColorProperty =
            Shader.PropertyToID("_ToxicColor");

        private bool _hydraBossEncounterActive;
        private bool _hydraBossSpawnedForVoid;
        private int _hydraBossSlot = -1;
        private Vector2 _hydraArenaCentre;
        private Vector2 _hydraBossHome;
        private float _hydraSurvivalElapsed;

        private bool CurrentVoidIsHydra =>
            _voidRoute != null && _voidRoute.CurrentVoidId == "hydra";

        private void SetupHydraPresentation()
        {
            _hydraBossSprite = Resources.Load<Sprite>("VoidFall/Hydra/HydraPrime");
            if (_hydraBossSprite == null)
                Debug.LogError("Required authored Hydra Prime sprite was not found.");
            for (var index = 0; index < _hydraRibCageViews.Length; index++)
            {
                var line = CreateLineView("Hydra Rib Cage_" + index, -105);
                ConfigureRoundLine(line);
                line.positionCount = 24;
                _hydraRibCageViews[index] = line;
            }
            _hydraSpineView = CreateLineView("Hydra Vertebral Chain", -104);
            ConfigureRoundLine(_hydraSpineView);
            _hydraSpineView.positionCount = 15;
            for (var index = 0; index < _hydraTentacleViews.Length; index++)
            {
                var line = CreateLineView("Hydra Toxic Tentacle_" + index, 24);
                ConfigureRoundLine(line);
                line.positionCount = 12;
                _hydraTentacleViews[index] = line;
            }
            for (var index = 0; index < _hydraBombViews.Length; index++)
            {
                _hydraBombViews[index] = CreateView(
                    "Hydra Marrow Bomb_" + index,
                    ProceduralSpriteFactory.Dot(),
                    23);
                var ring = CreateLineView("Hydra Marrow Target_" + index, 22);
                ConfigureRoundLine(ring);
                _hydraBombRingViews[index] = ring;
            }
            for (var index = 0; index < _hydraEvasionSocketViews.Length; index++)
            {
                var ring = CreateLineView("Hydra Evasion Socket_" + index, 23);
                ConfigureRoundLine(ring);
                _hydraEvasionSocketViews[index] = ring;
            }

            var shader = Shader.Find("VoidFall/HydraDisintegrate");
            if (shader == null)
            {
                Debug.LogError("Required Hydra disintegration shader was not found.");
                return;
            }
            _hydraBossMaterial = new Material(shader) { name = "Hydra Prime Disintegration (Runtime)" };
            _hydraBossMaterial.SetFloat(HydraPixelCellsProperty, 64f);
            _hydraBossMaterial.SetColor(HydraToxicColorProperty, new Color(0.47f, 1f, 0.25f, 1f));
            _dynamicMaterials.Add(_hydraBossMaterial);
        }

        private void ResetHydraEncounterState()
        {
            _hydraBossEncounterActive = false;
            _hydraBossSpawnedForVoid = false;
            _hydraBossSlot = -1;
            _hydraArenaCentre = Vector2.zero;
            _hydraBossHome = Vector2.zero;
            _hydraSurvivalElapsed = 0f;
            ResetHydraAttackState();
        }

        private void SyncHydraEncounterWithObjective()
        {
            if (!CurrentVoidIsHydra || _objectives == null || _objectives.IsComplete) return;
            if (!(_objectives.Objective is MultiPhaseObjective phases) || phases.PhaseIndex < 1) return;
            if (_hydraBossSpawnedForVoid) return;

            BeginHydraBossEncounter();
        }

        private void BeginHydraBossEncounter()
        {
            if (_hydraBossSpawnedForVoid) return;

            _hydraBossSpawnedForVoid = true;
            _hydraBossEncounterActive = true;
            _hydraArenaCentre = _gameSim.Player.Position;
            _hydraBossHome = _hydraArenaCentre + Vector2.up * HydraBossVerticalOffset;
            ClearHydraBossArena();
            SpawnBoss(HydraBossId, 1.0, 1.0, 0);
            for (var index = 0; index < _gameSim.Bosses.Length; index++)
            {
                var boss = _gameSim.Bosses[index];
                if (!boss.Active || boss.Id != HydraBossId) continue;
                boss.Position = _hydraBossHome;
                boss.TargetPosition = _hydraBossHome;
                boss.Speed = 0f;
                _gameSim.Bosses[index] = boss;
                _hydraBossSlot = index;
                break;
            }
            ShowArenaToast("HYDRA PRIME AWAKENS", 2.8f, ToastKind.Danger);
        }

        private void BeginHydraBossEncounterForCapture()
        {
            _arenaId = ArenaId.Hydra;
            SelectRecipeForCurrentArena();
            PrepareArenaNeighborhood();
            TryInstallPreparedArenaPlate(_arenaId);
            _hydraBossSpawnedForVoid = false;
            BeginHydraBossEncounter();
            if (_hydraBossSlot >= 0)
            {
                var boss = _gameSim.Bosses[_hydraBossSlot];
                boss.State = 0;
                boss.StateTimer = 0f;
                boss.AttackCooldown = 0f;
                boss.Health = boss.MaxHealth * 0.72f;
                switch (_visualCaptureHydraAttack)
                {
                    case "evasion": boss.AttackIndex = 1; break;
                    case "ribs": boss.AttackIndex = 2; break;
                    case "optic": boss.AttackIndex = 3; break;
                    default: boss.AttackIndex = 0; break;
                }
                _gameSim.Bosses[_hydraBossSlot] = boss;
            }
            _objectives?.Clear();
            _objectives = null;
            _objectiveLine = "HYDRA | HYDRA PRIME — ENGAGED";
            _lastObjectiveLine = null;
        }

        private void ClearHydraBossArena()
        {
            for (var index = 0; index < _gameSim.Enemies.Length; index++)
            {
                _gameSim.Enemies[index] = default;
                Hide(_enemyViews[index]);
                Hide(_enemyHarvesterFullViews[index]);
                Hide(_enemyExploderWarningViews[index]);
                Hide(_eliteMarkViews[index]);
                Hide(_eliteChargeLaneViews[index]);
                Hide(_eliteChargeArrowViews[index]);
                Hide(_eliteChargeFillRenderers[index]);
                Hide(_eliteChargeArrowFillRenderers[index]);
                Hide(_enemyTelegraphRingViews[index]);
                Hide(_enemyTelegraphLineViews[index]);
                Hide(_enemyTelegraphSecondaryLineViews[index]);
                Hide(_enemyTelegraphTertiaryLineViews[index]);
                Hide(_enemyHarvesterCapacityRingViews[index]);
                Hide(_enemyTelegraphMortarFillViews[index]);
                Hide(_enemyTelegraphExploderFillViews[index]);
                Hide(_enemyTelegraphFillRenderers[index]);
                Hide(_enemyTelegraphArrowFillRenderers[index]);
                Hide(_enemyHealthArcViews[index]);
                Hide(_enemyShieldArcViews[index]);
                Hide(_enemyHealthBackgroundViews[index]);
                Hide(_enemyHealthFillViews[index]);
            }
            ResetEnemyOrder();
            for (var index = 0; index < _gameSim.HostileShots.Length; index++)
            {
                _gameSim.HostileShots[index] = default;
                Hide(_hostileShotViews[index]);
            }
            ResetHostileShotOrder();
            for (var index = 0; index < _gameSim.Bosses.Length; index++)
            {
                _gameSim.Bosses[index] = default;
                Hide(_bossViews[index]);
            }
            ResetBossOrder();
            ClearMeteors();
        }

        private void EndHydraBossEncounter()
        {
            _hydraBossEncounterActive = false;
            _hydraBossSlot = -1;
            ResetHydraAttackState();
        }

        private void ApplyHydraRibCageCollision()
        {
            if (!_hydraBossEncounterActive) return;
            _gameSim.Player.Position = HydraRuntimeRules.ClampPlayerToRibCage(
                _gameSim.Player.Position,
                _hydraArenaCentre,
                AttackPlayerRadius,
                true);
        }

        private void StepHydraSurvival(float dt)
        {
            if (CurrentVoidIsHydra && !_hydraBossEncounterActive)
                _hydraSurvivalElapsed += Mathf.Max(0f, dt);
        }

        private int HydraRecombinationStage =>
            Mathf.Clamp(Mathf.FloorToInt(_hydraSurvivalElapsed / 100f), 0, 2);

        private void ResetHydraAttackState()
        {
            for (var index = 0; index < _hydraBombs.Length; index++)
                _hydraBombs[index] = default;
            for (var index = 0; index < _hydraMarrowIntervals.Length; index++)
                _hydraMarrowIntervals[index] = 0;
            for (var index = 0; index < _hydraEvasionOrder.Length; index++)
                _hydraEvasionOrder[index] = index;
            _hydraPreviousEvasionSocket = -1;
            HideHydraAttackViews();
        }

        private void PrepareHydraAttack(ref BossState boss)
        {
            boss.HydraStep = -1;
            boss.HydraAttackElapsed = 0f;
            if (boss.ActiveAttack?.Id == "hydra-marrow")
            {
                var intervals = HydraEncounterRules.BuildMarrowIntervals(_gameSim.Rng);
                for (var index = 0; index < intervals.Length; index++)
                    _hydraMarrowIntervals[index] = intervals[index];
            }
            else if (boss.ActiveAttack?.Id == "hydra-evasion")
            {
                var order = HydraEncounterRules.BuildEvasionOrder(
                    _gameSim.Rng,
                    _hydraPreviousEvasionSocket);
                for (var index = 0; index < order.Length; index++)
                    _hydraEvasionOrder[index] = order[index];
                _hydraPreviousEvasionSocket = order[order.Length - 1];
            }
        }

        private bool ApplyHydraAttack(ref BossState boss, float dt)
        {
            var attack = boss.ActiveAttack;
            if (boss.Id != HydraBossId || attack == null) return false;
            var attackDamage = (float)attack.Damage * boss.DamageScale;
            boss.HydraAttackElapsed += Mathf.Max(0f, dt);
            if (attack.Id == "hydra-marrow")
            {
                var due = HydraRuntimeRules.MarrowBombsDue(
                    boss.HydraAttackElapsed,
                    _hydraMarrowIntervals);
                while (boss.HydraStep + 1 < due && boss.HydraStep + 1 < _hydraBombs.Length)
                {
                    boss.HydraStep++;
                    SpawnHydraBomb(_gameSim.Player.Position, attackDamage);
                }
                return true;
            }
            if (attack.Id == "hydra-evasion")
            {
                var step = HydraRuntimeRules.EvasionStep(
                    boss.HydraAttackElapsed,
                    (float)attack.ActiveSeconds);
                boss.HydraStep = step;
                var socket = _hydraEvasionOrder[step];
                boss.Position = _hydraArenaCentre + HydraEvasionSocketOffsets[socket];
                return true;
            }
            if (attack.Id == "hydra-ribs")
            {
                if (boss.ActionApplied) return true;
                boss.ActionApplied = true;
                var count = Mathf.Max(2, attack.ProjectileCount ?? 8);
                for (var index = 0; index < count; index++)
                {
                    var fromLeft = (index & 1) == 0;
                    var lane = index / 2;
                    var y = _hydraArenaCentre.y - 150f + lane * 100f;
                    var origin = _hydraArenaCentre + new Vector2(fromLeft ? -460f : 460f, y - _hydraArenaCentre.y);
                    var target = _gameSim.Player.Position + new Vector2(0f, (lane - 1.5f) * 18f);
                    var direction = (target - origin).normalized;
                    SpawnHostileShot(
                        origin,
                        direction,
                        attackDamage,
                        (float)(attack.ProjectileSpeed ?? 300),
                        0f,
                        false,
                        HydraRibShotVariant,
                        (float)HydraEncounterRules.RibProjectileRadius);
                }
                SpawnRingWave(_hydraArenaCentre, 80f, 620f, 0.55f, BossAccent(boss));
                _audio?.Play(ProceduralAudio.Cue.BossSlam, 0.9f);
                return true;
            }
            if (attack.Id == "hydra-optic")
            {
                boss.AttackAngle += (float)(attack.RotationSpeed ?? 0.72) * dt;
                if (_gameSim.Player.Iframes > 0 || boss.BeamHitCooldown > 0) return true;
                var delta = _gameSim.Player.Position - boss.Position;
                var direction = new Vector2(Mathf.Cos(boss.AttackAngle), Mathf.Sin(boss.AttackAngle));
                var along = Vector2.Dot(delta, direction);
                var across = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                if (HazardRules.SegmentedSweepContains(
                    along,
                    across,
                    attack.BeamLength ?? 760,
                    attack.BeamWidth ?? 38,
                    AttackPlayerRadius))
                {
                    DamagePlayer(attackDamage, delta);
                    boss.BeamHitCooldown = 0.45f;
                }
                return true;
            }
            return false;
        }

        private void SpawnHydraBomb(Vector2 position, float damage)
        {
            for (var index = 0; index < _hydraBombs.Length; index++)
            {
                if (_hydraBombs[index].Active) continue;
                _hydraBombs[index] = new HydraBombState
                {
                    Active = true,
                    Position = position,
                    Fuse = HydraBombFuseSeconds,
                    BlastTimer = HydraBombBlastSeconds,
                    Damage = damage,
                };
                return;
            }
        }

        private void StepHydraAttackState(float dt)
        {
            for (var index = 0; index < _hydraBombs.Length; index++)
            {
                var bomb = _hydraBombs[index];
                if (!bomb.Active) continue;
                if (!bomb.DamageApplied)
                {
                    bomb.Fuse -= dt;
                    if (bomb.Fuse <= 0f)
                    {
                        bomb.DamageApplied = true;
                        var delta = _gameSim.Player.Position - bomb.Position;
                        var radius = (float)(HydraContent.Boss.Attacks[0].Radius ?? 64);
                        if (delta.magnitude < radius + AttackPlayerRadius)
                            DamagePlayer(bomb.Damage, delta);
                        SpawnRingWave(
                            bomb.Position,
                            10f,
                            radius * 2.2f,
                            0.5f,
                            new Color(0.58f, 1f, 0.27f, 0.9f));
                        BurstFx(bomb.Position, SourceDotColor("lime"), 12, 230, 0.45f, 0.75f);
                        _audio?.Play(ProceduralAudio.Cue.BossSlam, 0.72f);
                    }
                }
                else
                {
                    bomb.BlastTimer -= dt;
                    if (bomb.BlastTimer <= 0f) bomb = default;
                }
                _hydraBombs[index] = bomb;
            }
        }

        private void HideHydraAttackViews()
        {
            for (var index = 0; index < _hydraBombViews.Length; index++)
            {
                Hide(_hydraBombViews[index]);
                Hide(_hydraBombRingViews[index]);
            }
            for (var index = 0; index < _hydraEvasionSocketViews.Length; index++)
                Hide(_hydraEvasionSocketViews[index]);
        }

        private void RenderHydraPresentation()
        {
            RenderHydraArenaBones();
            RenderHydraBossAttachments();
            RenderHydraBombs();
            RenderHydraEvasionSockets();
        }

        private void RenderHydraArenaBones()
        {
            // The approved v13 rib cage and vertebral chain are authored in the
            // transparent Hydra detail plate. Runtime collision stays logical;
            // these old primitive overlays must remain hidden or they flatten
            // the authored pores, joints, sockets and shadows.
            for (var index = 0; index < _hydraRibCageViews.Length; index++)
                Hide(_hydraRibCageViews[index]);
            Hide(_hydraSpineView);
        }

        private void RenderHydraBossAttachments()
        {
            var found = false;
            var bossPosition = Vector2.zero;
            var bossRadius = 0f;
            for (var index = 0; index < _gameSim.Bosses.Length; index++)
            {
                var boss = _gameSim.Bosses[index];
                if ((!boss.Active && boss.DeathTimer <= 0f) || boss.Id != HydraBossId) continue;
                found = true;
                bossPosition = boss.Position;
                bossRadius = boss.Radius;
                break;
            }
            if (!found)
            {
                for (var index = 0; index < _hydraTentacleViews.Length; index++)
                    Hide(_hydraTentacleViews[index]);
                return;
            }

            for (var index = 0; index < _hydraTentacleViews.Length; index++)
            {
                var line = _hydraTentacleViews[index];
                if (line == null) continue;
                var angle = index / (float)_hydraTentacleViews.Length * Mathf.PI * 2f + 0.22f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var normal = new Vector2(-direction.y, direction.x);
                // Start the animated strands near the authored head edge. The
                // old procedural radius placed their roots in the middle of the
                // larger production sprite, hiding most of their motion.
                var root = bossPosition + direction * bossRadius * 1.15f;
                for (var point = 0; point < line.positionCount; point++)
                {
                    var t = point / (float)(line.positionCount - 1);
                    var wave = Mathf.Sin(_ambientClock * (2.1f + index * 0.07f) + index * 1.7f + t * 5.5f);
                    line.SetPosition(
                        point,
                        root + direction * (t * (138f + index % 3 * 10f)) + normal * wave * (5f + t * 24f));
                }
                line.startWidth = 5.2f;
                line.endWidth = 1.4f;
                line.startColor = new Color(0.08f, 0.42f, 0.17f, 0.94f);
                line.endColor = new Color(0.68f, 1f, 0.22f, 0.62f);
                line.enabled = true;
            }
        }

        private void RenderHydraBombs()
        {
            for (var index = 0; index < _hydraBombs.Length; index++)
            {
                var bomb = _hydraBombs[index];
                var view = _hydraBombViews[index];
                var ring = _hydraBombRingViews[index];
                if (!bomb.Active)
                {
                    Hide(view);
                    Hide(ring);
                    continue;
                }
                var radius = (float)(HydraContent.Boss.Attacks[0].Radius ?? 64);
                if (ring != null)
                {
                    var pulse = 1f + Mathf.Sin(_ambientClock * 13f + index) * 0.05f;
                    SetArcLine(
                        ring,
                        bomb.Position,
                        radius * pulse,
                        0f,
                        Mathf.PI * 2f,
                        2.5f,
                        new Color(0.72f, 1f, 0.29f, bomb.DamageApplied ? 0.18f : 0.78f));
                }
                if (view != null)
                {
                    var fall = bomb.DamageApplied ? 0f : Mathf.Clamp01(bomb.Fuse / HydraBombFuseSeconds);
                    view.transform.position = bomb.Position + Vector2.up * (fall * 230f);
                    view.transform.localScale = Vector3.one * 24f;
                    view.color = bomb.DamageApplied
                        ? new Color(1f, 1f, 1f, 0f)
                        : new Color(0.72f, 1f, 0.25f, 1f);
                    view.enabled = !bomb.DamageApplied;
                }
            }
        }

        private void RenderHydraEvasionSockets()
        {
            var show = _hydraBossEncounterActive && _hydraBossSlot >= 0 &&
                _hydraBossSlot < _gameSim.Bosses.Length;
            var boss = show ? _gameSim.Bosses[_hydraBossSlot] : default;
            show &= boss.Active && boss.ActiveAttack?.Id == "hydra-evasion" && boss.State <= 2;
            for (var index = 0; index < _hydraEvasionSocketViews.Length; index++)
            {
                var ring = _hydraEvasionSocketViews[index];
                if (!show)
                {
                    Hide(ring);
                    continue;
                }
                SetArcLine(
                    ring,
                    _hydraArenaCentre + HydraEvasionSocketOffsets[index],
                    34f + Mathf.Sin(_ambientClock * 4f + index) * 3f,
                    0f,
                    Mathf.PI * 2f,
                    3f,
                    new Color(0.56f, 1f, 0.29f, 0.72f));
            }
        }

        private static Vector2 HydraDamageBurstPosition(BossState boss)
        {
            var progress = 1f - boss.Health / Mathf.Max(1f, boss.MaxHealth);
            Vector2 normalized;
            if (progress < 0.18f) normalized = new Vector2(0f, 0.72f);
            else if (progress < 0.38f) normalized = new Vector2(0.48f, 0.38f);
            else if (progress < 0.58f) normalized = new Vector2(-0.48f, 0.38f);
            else if (progress < 0.75f) normalized = new Vector2(0.46f, -0.38f);
            else if (progress < 0.92f) normalized = new Vector2(-0.46f, -0.38f);
            else normalized = Vector2.zero;
            return boss.Position + normalized * boss.Radius;
        }
    }
}
