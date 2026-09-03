using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const string CourtBlackBossId = "court-grandmaster-black";
        private const string CourtWhiteBossId = "court-grandmaster-white";
        private const int CourtBoardColumns = 14;
        private const int CourtBoardRows = 9;

        private readonly SpriteRenderer[] _courtBoardTiles =
            new SpriteRenderer[CourtBoardColumns * CourtBoardRows];
        private Sprite _courtBoardTileSprite;
        private bool _courtPresentationReady;
        private bool _monochromeBossEncounterActive;
        private bool _monochromeBossSpawnedForVoid;
        private int _courtBlackBossSlot = -1;
        private int _courtWhiteBossSlot = -1;
        private Vector2 _monochromeArenaCentre;
        private Vector2 _monochromeBoardTileSize;
        private Vector2 _monochromeBoardOrigin;
        private float _monochromeSpawnTimer;
        private float _monochromeSurvivalElapsed;
        private float _monochromeBossElapsed;
        private float _monochromeSharedHealth;
        private float _monochromeSharedMaxHealth;
        private CourtHazardState _monochromeHazard;
        private CourtHazardState _monochromePreviousHazard;
        private bool _monochromeHazardInitialized;
        private float _monochromeFloorDamageCooldown;
        private int _monochromeSpawnSequence;

        private bool CurrentVoidIsMonochrome =>
            _voidRoute != null && _voidRoute.CurrentVoidId == "monochrome-court";

        private static bool IsCourtEnemy(string id) =>
            !string.IsNullOrEmpty(id) && id.StartsWith("court-", System.StringComparison.Ordinal) &&
            id != CourtBlackBossId && id != CourtWhiteBossId;

        private static bool IsCourtGrandmaster(string id) =>
            id == CourtBlackBossId || id == CourtWhiteBossId;

        private static CourtFaction CourtFactionOf(EnemyState enemy) =>
            enemy.Seed < 0f ? CourtFaction.Black : CourtFaction.White;

        private static bool CourtPawnIsPromoted(EnemyState enemy) =>
            enemy.Id == "court-pawn" && enemy.StoredXp > 0.5f;

        private void SetupMonochromePresentation()
        {
            if (_courtPresentationReady) return;
            _courtBoardTileSprite = ArenaPlateFactory.SpriteFromPixels(
                new[] { new Color32(255, 255, 255, 255) },
                1,
                1,
                "Monochrome Court Opaque Board Tile");
            for (var index = 0; index < _courtBoardTiles.Length; index++)
            {
                var tile = CreateView(
                    "Monochrome Court Boss Tile_" + index,
                    _courtBoardTileSprite,
                    -80);
                tile.enabled = false;
                _courtBoardTiles[index] = tile;
            }
            _courtPresentationReady = true;
        }

        private void ResetMonochromeEncounterState()
        {
            _monochromeBossEncounterActive = false;
            _monochromeBossSpawnedForVoid = false;
            _courtBlackBossSlot = -1;
            _courtWhiteBossSlot = -1;
            _monochromeArenaCentre = Vector2.zero;
            _monochromeBoardTileSize = Vector2.zero;
            _monochromeBoardOrigin = Vector2.zero;
            _monochromeSpawnTimer = 0f;
            _monochromeSurvivalElapsed = 0f;
            _monochromeBossElapsed = 0f;
            _monochromeSharedHealth = 0f;
            _monochromeSharedMaxHealth = 0f;
            _monochromeHazard = new CourtHazardState(CourtFaction.White, CourtHazardStage.Warning);
            _monochromePreviousHazard = _monochromeHazard;
            _monochromeHazardInitialized = false;
            _monochromeFloorDamageCooldown = 0f;
            _monochromeSpawnSequence = 0;
            HideMonochromeBoard();
        }

        private void SyncMonochromeEncounterWithObjective()
        {
            if (!CurrentVoidIsMonochrome || _objectives == null || _objectives.IsComplete) return;
            if (!(_objectives.Objective is MultiPhaseObjective phases) || phases.PhaseIndex < 2) return;
            if (_monochromeBossSpawnedForVoid) return;
            BeginMonochromeBossEncounter();
        }

        private void BeginMonochromeBossEncounter()
        {
            if (_monochromeBossSpawnedForVoid) return;
            _monochromeBossSpawnedForVoid = true;
            _monochromeBossEncounterActive = true;
            SetupMonochromePresentation();
            _monochromeArenaCentre = _gameSim.Player.Position;
            _monochromeBoardTileSize = CalculateMonochromeBoardTileSize();
            _monochromeBoardOrigin = _monochromeArenaCentre - _monochromeBoardTileSize * 0.5f;
            _monochromeBossElapsed = -1.6f;
            _monochromeHazard = new CourtHazardState(CourtFaction.White, CourtHazardStage.Warning);
            _monochromePreviousHazard = _monochromeHazard;
            _monochromeHazardInitialized = false;
            _monochromeFloorDamageCooldown = 0f;
            ClearHydraBossArena();
            SpawnBoss(CourtBlackBossId, 1.0, 1.0, 0);
            SpawnBoss(CourtWhiteBossId, 1.0, 1.0, 0);
            for (var index = 0; index < _gameSim.Bosses.Length; index++)
            {
                var boss = _gameSim.Bosses[index];
                if (!boss.Active || !IsCourtGrandmaster(boss.Id)) continue;
                var black = boss.Id == CourtBlackBossId;
                boss.Position = _monochromeArenaCentre + new Vector2(black ? -240f : 240f, 135f);
                boss.TargetPosition = boss.Position;
                boss.Speed = 0f;
                _gameSim.Bosses[index] = boss;
                if (black) _courtBlackBossSlot = index;
                else _courtWhiteBossSlot = index;
            }
            _monochromeSharedMaxHealth = 0f;
            if (_courtBlackBossSlot >= 0) _monochromeSharedMaxHealth += _gameSim.Bosses[_courtBlackBossSlot].MaxHealth;
            if (_courtWhiteBossSlot >= 0) _monochromeSharedMaxHealth += _gameSim.Bosses[_courtWhiteBossSlot].MaxHealth;
            _monochromeSharedHealth = _monochromeSharedMaxHealth;
            ShowArenaToast("THE TWIN GRANDMASTERS COMMAND THE FLOOR", 2.8f, ToastKind.Danger);
        }

        private void EndMonochromeBossEncounter()
        {
            _monochromeBossEncounterActive = false;
            _courtBlackBossSlot = -1;
            _courtWhiteBossSlot = -1;
            HideMonochromeBoard();
        }

        private void BeginMonochromeBossEncounterForCapture()
        {
            _arenaId = ArenaId.MonochromeCourt;
            SelectRecipeForCurrentArena();
            PrepareMenuArenaCatalogue();
            TryInstallPreparedArenaPlate(_arenaId);
            _monochromeBossSpawnedForVoid = false;
            BeginMonochromeBossEncounter();
            var captureBlack = _visualCaptureCourtHazard != null &&
                _visualCaptureCourtHazard.IndexOf(
                    "black",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
            var captureWarning = _visualCaptureCourtHazard != null &&
                _visualCaptureCourtHazard.IndexOf(
                    "warning",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
            var pulseSeconds = MonochromeEncounterRules.HazardWarningSeconds +
                MonochromeEncounterRules.HazardBurningSeconds +
                MonochromeEncounterRules.HazardRecoverySeconds;
            _monochromeBossElapsed = (float)((captureBlack ? pulseSeconds : 0) +
                (captureWarning ? 0.25 : MonochromeEncounterRules.HazardWarningSeconds + 0.25));
            _monochromeHazardInitialized = false;
            StepMonochromeBossEncounter(0f);
            _objectives?.Clear();
            _objectives = null;
            _objectiveLine = "MONOCHROME COURT | TWIN GRANDMASTERS — ENGAGED";
            _lastObjectiveLine = null;
        }

        private void StepMonochromeSurvival(float dt)
        {
            if (CurrentVoidIsMonochrome && !_monochromeBossEncounterActive)
                _monochromeSurvivalElapsed += Mathf.Max(0f, dt);
        }

        private void UpdateMonochromeSpawns(float dt)
        {
            if (!CurrentVoidIsMonochrome || _monochromeBossEncounterActive) return;
            _monochromeSpawnTimer -= Mathf.Max(0f, dt);
            if (_monochromeSpawnTimer > 0f || ActiveEnemies() >= DirectorRules.ActiveEnemyCap(_time, 0)) return;
            _monochromeSpawnTimer = Mathf.Max(0.32f, 0.76f - _monochromeSurvivalElapsed * 0.0012f);

            var id = MonochromeRuntimeRules.NextSpawnId(_gameSim.Rng.Next());
            var faction = (_monochromeSpawnSequence++ & 1) == 0
                ? CourtFaction.Black
                : CourtFaction.White;
            var viewport = GameplayViewportHalfExtent();
            var x = MonochromeRuntimeRules.SpawnX(
                faction,
                _gameSim.Player.Position.x,
                viewport.x + 110f);
            var y = _gameSim.Player.Position.y +
                    ((float)_gameSim.Rng.Next() - 0.5f) * viewport.y * 1.6f;
            if (!SpawnEnemy(id, new Vector2(x, y), forcedRoster: EnemyRoster.One)) return;

            var spawnId = _nextEnemyId - 1;
            for (var index = 0; index < _gameSim.Enemies.Length; index++)
            {
                var enemy = _gameSim.Enemies[index];
                if (!enemy.Active || enemy.SpawnId != spawnId) continue;
                enemy.Seed = faction == CourtFaction.Black
                    ? -Mathf.Abs(enemy.Seed) - 1f
                    : Mathf.Abs(enemy.Seed) + 1f;
                enemy.AttackCooldown = 0.8f + (float)_gameSim.Rng.Next() * 1.2f;
                enemy.Spin *= 0.3f;
                _gameSim.Enemies[index] = enemy;
                break;
            }
        }

        private void UpdateMonochromeEnemy(
            ref EnemyState enemy,
            float dt,
            float distance,
            Vector2 direction)
        {
            switch (enemy.Id)
            {
                case "court-rook": UpdateCourtRook(ref enemy, dt, distance, direction); break;
                case "court-bishop": UpdateCourtBishop(ref enemy, dt, distance, direction); break;
                case "court-knight": UpdateCourtKnight(ref enemy, dt, distance, direction); break;
                case "court-queen": UpdateCourtQueen(ref enemy, dt, distance, direction); break;
                default:
                    var sideBias = CourtFactionOf(enemy) == CourtFaction.Black ? 0.12f : -0.12f;
                    var cosine = Mathf.Cos(sideBias);
                    var sine = Mathf.Sin(sideBias);
                    enemy.Velocity = new Vector2(
                        direction.x * cosine - direction.y * sine,
                        direction.x * sine + direction.y * cosine) * enemy.Speed;
                    break;
            }
        }

        private void UpdateCourtRook(ref EnemyState enemy, float dt, float distance, Vector2 direction)
        {
            var definition = MonochromeContent.FindEnemy(enemy.Id);
            if (enemy.State == 0)
            {
                enemy.Velocity = direction * enemy.Speed;
                if (enemy.AttackCooldown <= 0f && distance < 460f && enemy.Age > 0.7f)
                {
                    enemy.State = 1;
                    enemy.StateTimer = (float)(definition.TelegraphSeconds ?? 0.8);
                    enemy.DashDirection = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                        ? new Vector2(Mathf.Sign(direction.x), 0f)
                        : new Vector2(0f, Mathf.Sign(direction.y));
                }
            }
            else if (enemy.State == 1)
            {
                enemy.Velocity *= Mathf.Max(0f, 1f - dt * 12f);
                enemy.StateTimer -= dt;
                if (enemy.StateTimer <= 0f)
                {
                    enemy.State = 2;
                    enemy.StateTimer = (float)(definition.RecoverySeconds ?? 0.75);
                    enemy.Velocity = MonochromeRuntimeRules.RookChargeVelocity(enemy.DashDirection, enemy.Speed);
                }
            }
            else
            {
                enemy.Velocity = MonochromeRuntimeRules.RookChargeVelocity(enemy.DashDirection, enemy.Speed);
                enemy.StateTimer -= dt;
                if (enemy.StateTimer <= 0f)
                {
                    enemy.State = 0;
                    enemy.AttackCooldown = (float)(definition.AttackCooldown ?? 4.5);
                }
            }
        }

        private void UpdateCourtBishop(ref EnemyState enemy, float dt, float distance, Vector2 direction)
        {
            var definition = MonochromeContent.FindEnemy(enemy.Id);
            var preferred = (float)(definition.PreferredDistance ?? 500);
            if (enemy.State == 0)
            {
                enemy.Velocity = distance > preferred + 45f
                    ? direction * enemy.Speed
                    : distance < preferred - 70f ? -direction * enemy.Speed * 0.72f : Vector2.zero;
                if (enemy.AttackCooldown <= 0f)
                {
                    enemy.State = 1;
                    enemy.StateTimer = (float)(definition.TelegraphSeconds ?? 1.15);
                    enemy.AimPosition = _gameSim.Player.Position;
                }
            }
            else
            {
                enemy.Velocity *= Mathf.Max(0f, 1f - dt * 14f);
                enemy.StateTimer -= dt;
                if (enemy.StateTimer > 0f) return;
                var shotDirection = (enemy.AimPosition - enemy.Position).normalized;
                SpawnHostileShot(
                    enemy.Position + shotDirection * (enemy.Radius + 5f),
                    shotDirection,
                    enemy.Damage * 0.9f,
                    (float)(definition.ProjectileSpeed ?? 440),
                    0f);
                enemy.State = 0;
                enemy.AttackCooldown = (float)(definition.AttackCooldown ?? 4.2);
            }
        }

        private void UpdateCourtKnight(ref EnemyState enemy, float dt, float distance, Vector2 direction)
        {
            var definition = MonochromeContent.FindEnemy(enemy.Id);
            if (enemy.State == 0)
            {
                enemy.Velocity = direction * enemy.Speed;
                if (enemy.AttackCooldown <= 0f && distance < 430f)
                {
                    enemy.State = 1;
                    enemy.StateTimer = (float)(definition.TelegraphSeconds ?? 0.7);
                    enemy.AimPosition = _gameSim.Player.Position;
                    enemy.Volley++;
                }
                return;
            }
            if (enemy.State == 1)
            {
                enemy.Velocity *= Mathf.Max(0f, 1f - dt * 14f);
                enemy.StateTimer -= dt;
                if (enemy.StateTimer > 0f) return;
                var destination = enemy.AimPosition;
                var corner = MonochromeEncounterRules.KnightCorner(
                    enemy.Position.x,
                    enemy.Position.y,
                    destination.x,
                    destination.y,
                    (enemy.Volley & 1) == 0);
                enemy.AimPosition = new Vector2((float)corner.X, (float)corner.Y);
                enemy.DashDirection = destination;
                enemy.State = 2;
            }
            if (enemy.State == 3 && enemy.StateTimer > 0f)
            {
                enemy.StateTimer -= dt;
                enemy.Velocity = Vector2.zero;
                return;
            }
            var target = enemy.State == 2 ? enemy.AimPosition : enemy.DashDirection;
            var leg = target - enemy.Position;
            enemy.Velocity = leg.sqrMagnitude > 0.001f ? leg.normalized * enemy.Speed * 2.15f : Vector2.zero;
            if (leg.sqrMagnitude > 18f * 18f) return;
            if (enemy.State == 2)
            {
                enemy.State = 3;
                enemy.StateTimer = 0.16f;
                enemy.Velocity = Vector2.zero;
            }
            else
            {
                enemy.State = 0;
                enemy.AttackCooldown = (float)(definition.AttackCooldown ?? 3.1);
            }
        }

        private void UpdateCourtQueen(ref EnemyState enemy, float dt, float distance, Vector2 direction)
        {
            var definition = MonochromeContent.FindEnemy(enemy.Id);
            var preferred = (float)(definition.PreferredDistance ?? 360);
            if (enemy.State == 0)
            {
                enemy.Velocity = distance > preferred + 55f
                    ? direction * enemy.Speed
                    : distance < preferred - 65f ? -direction * enemy.Speed * 0.6f : Vector2.zero;
                if (enemy.AttackCooldown <= 0f)
                {
                    enemy.State = 1;
                    enemy.StateTimer = (float)(definition.TelegraphSeconds ?? 1f);
                }
                return;
            }
            enemy.Velocity *= Mathf.Max(0f, 1f - dt * 12f);
            enemy.StateTimer -= dt;
            if (enemy.StateTimer > 0f) return;
            var speed = (float)(definition.ProjectileSpeed ?? 360);
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Mathf.PI * 0.25f;
                var shotDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                SpawnHostileShot(enemy.Position, shotDirection, enemy.Damage * 0.62f, speed, 0f);
            }
            PromoteCourtPawns(CourtFactionOf(enemy), enemy.Position);
            enemy.State = 0;
            enemy.AttackCooldown = (float)(definition.AttackCooldown ?? 5.2);
        }

        private void PromoteCourtPawns(CourtFaction faction, Vector2 centre)
        {
            var promoted = 0;
            for (var order = 0; order < _gameSim.EnemyOrderCount && promoted < MonochromeRuntimeRules.MaxQueenPromotions; order++)
            {
                var slot = _gameSim.EnemyOrder[order];
                var pawn = _gameSim.Enemies[slot];
                if (!pawn.Active || pawn.Id != "court-pawn" || CourtPawnIsPromoted(pawn) ||
                    CourtFactionOf(pawn) != faction || (pawn.Position - centre).sqrMagnitude > 430f * 430f) continue;
                var healthGain = pawn.MaxHealth * 0.35f;
                pawn.MaxHealth += healthGain;
                pawn.Health += healthGain;
                pawn.Speed *= 1.16f;
                pawn.Damage *= 1.2f;
                pawn.Radius *= 1.12f;
                pawn.StoredXp = 1f;
                _gameSim.Enemies[slot] = pawn;
                promoted++;
            }
        }

        private void StepMonochromeBossEncounter(float dt)
        {
            if (!_monochromeBossEncounterActive) return;
            // Capture mode freezes the requested phase while the player waits
            // for asynchronous screenshot frames. Normal gameplay always advances.
            var elapsedStep = _visualCaptureCourtBoss ? 0f : Mathf.Max(0f, dt);
            _monochromeBossElapsed += elapsedStep;
            _monochromeFloorDamageCooldown = Mathf.Max(
                0f,
                _monochromeFloorDamageCooldown - elapsedStep);
            if (_monochromeBossElapsed < 0f) return;
            var phaseTwo = _monochromeSharedHealth <= _monochromeSharedMaxHealth * 0.5f;
            _monochromeHazard = MonochromeEncounterRules.HazardAt(
                _monochromeBossElapsed,
                phaseTwo);
            ApplyMonochromeFloorHazard(phaseTwo);
            if (_monochromeHazardInitialized && _monochromeHazard == _monochromePreviousHazard) return;
            _monochromePreviousHazard = _monochromeHazard;
            _monochromeHazardInitialized = true;
            AnnounceMonochromeFloorHazard();
        }

        private void ApplyMonochromeFloorHazard(bool phaseTwo)
        {
            var tileFaction = MonochromeRuntimeRules.FactionAtWorldPosition(
                _gameSim.Player.Position,
                _monochromeBoardOrigin,
                _monochromeBoardTileSize);
            if (!MonochromeRuntimeRules.ShouldApplyFloorDamage(
                    _monochromeHazard,
                    tileFaction,
                    _monochromeFloorDamageCooldown)) return;

            DamagePlayer(phaseTwo ? 20f : 16f, Vector2.zero);
            _monochromeFloorDamageCooldown = 0.65f;
        }

        private void AnnounceMonochromeFloorHazard()
        {
            if (_monochromeHazard.Stage == CourtHazardStage.Recovery)
            {
                ShowArenaToast("BOARD COOLS — REPOSITION", 0.8f, ToastKind.Info);
                return;
            }

            var controlled = _monochromeHazard.Faction == CourtFaction.White ? "WHITE" : "BLACK";
            var safe = _monochromeHazard.Faction == CourtFaction.White ? "BLACK" : "WHITE";
            ShowArenaToast(
                _monochromeHazard.Stage == CourtHazardStage.Warning
                    ? controlled + " TILES CHARGING — MOVE TO " + safe
                    : controlled + " FLOOR ERUPTS — " + safe + " IS SAFE",
                _monochromeHazard.Stage == CourtHazardStage.Warning ? 0.9f : 1.2f,
                ToastKind.Danger);
        }

        private void ApplyMonochromeSharedBossDamage(
            int index,
            float damage,
            int weaponIndex,
            bool critical)
        {
            var boss = _gameSim.Bosses[index];
            var applied = Mathf.Min(Mathf.Max(0f, damage), _monochromeSharedHealth);
            if (applied <= 0f) return;
            _monochromeSharedHealth = MonochromeRuntimeRules.ApplySharedDamage(_monochromeSharedHealth, applied);
            _damageDealt += applied;
            TrackWeaponDamage(weaponIndex, applied);
            var ratio = _monochromeSharedMaxHealth > 0f
                ? _monochromeSharedHealth / _monochromeSharedMaxHealth
                : 0f;
            for (var slot = 0; slot < _gameSim.Bosses.Length; slot++)
            {
                var twin = _gameSim.Bosses[slot];
                if (!twin.Active || !IsCourtGrandmaster(twin.Id)) continue;
                twin.Health = twin.MaxHealth * ratio;
                if (slot == index) twin.HitTimer = 0.08f;
                _gameSim.Bosses[slot] = twin;
            }
            SpawnDamageFloater(MaxEnemies + index + 1, boss.Position, applied, critical);
            BurstFx(boss.Position, critical ? SourceDotColor("white") : BossParticleColor(boss.Id),
                critical ? 7 : 4, 205f, 0.38f, 0.68f);
            _audio?.Play(ProceduralAudio.Cue.Hit, critical ? 1f : 0.9f);
            if (_monochromeSharedHealth > 0f) return;

            var black = _courtBlackBossSlot;
            var white = _courtWhiteBossSlot;
            if (black >= 0 && _gameSim.Bosses[black].Active) KillBoss(black);
            if (white >= 0 && _gameSim.Bosses[white].Active) KillBoss(white);
            EndMonochromeBossEncounter();
        }

        private void RenderMonochromePresentation()
        {
            if (!_monochromeBossEncounterActive)
            {
                HideMonochromeBoard();
                return;
            }
            var player = _gameSim.Player.Position;
            var centreColumn = Mathf.FloorToInt(
                (player.x - _monochromeBoardOrigin.x) / _monochromeBoardTileSize.x);
            var centreRow = Mathf.FloorToInt(
                (player.y - _monochromeBoardOrigin.y) / _monochromeBoardTileSize.y);
            var firstColumn = centreColumn - CourtBoardColumns / 2;
            var firstRow = centreRow - CourtBoardRows / 2;
            for (var row = 0; row < CourtBoardRows; row++)
            {
                for (var column = 0; column < CourtBoardColumns; column++)
                {
                    var index = row * CourtBoardColumns + column;
                    var tile = _courtBoardTiles[index];
                    var globalColumn = firstColumn + column;
                    var globalRow = firstRow + row;
                    tile.transform.position = _monochromeBoardOrigin + new Vector2(
                        (globalColumn + 0.5f) * _monochromeBoardTileSize.x,
                        (globalRow + 0.5f) * _monochromeBoardTileSize.y);
                    tile.transform.localScale = new Vector3(
                        _monochromeBoardTileSize.x,
                        _monochromeBoardTileSize.y,
                        1f);
                    var faction = ((globalRow + globalColumn) & 1) == 0
                        ? CourtFaction.White
                        : CourtFaction.Black;
                    var color = faction == CourtFaction.White
                        ? new Color(0.88f, 0.87f, 0.82f, 1f)
                        : new Color(0.025f, 0.028f, 0.035f, 1f);
                    if (faction == _monochromeHazard.Faction)
                    {
                        var phase = _monochromeBossElapsed * 15f +
                            globalColumn * 0.73f + globalRow * 1.17f;
                        if (_monochromeHazard.Stage == CourtHazardStage.Warning)
                        {
                            var pulse = 0.5f + Mathf.Sin(phase) * 0.5f;
                            var warning = faction == CourtFaction.White
                                ? new Color(1f, 0.72f, 0.18f, 1f)
                                : new Color(0.68f, 0.06f, 0.14f, 1f);
                            color = Color.Lerp(color, warning, 0.28f + pulse * 0.44f);
                        }
                        else if (_monochromeHazard.Stage == CourtHazardStage.Burning)
                        {
                            var flicker = 0.78f + Mathf.Sin(phase * 1.43f) * 0.12f;
                            var burning = faction == CourtFaction.White
                                ? new Color(1f, 0.24f, 0.025f, 1f)
                                : new Color(0.74f, 0.018f, 0.035f, 1f);
                            color = Color.Lerp(color, burning, flicker);
                        }
                    }
                    tile.color = color;
                    tile.enabled = true;
                }
            }
        }

        private Vector2 CalculateMonochromeBoardTileSize()
        {
            var viewport = GameplayViewportHalfExtent();
            return new Vector2(
                viewport.x * 2.4f / (CourtBoardColumns - 2),
                viewport.y * 2.4f / (CourtBoardRows - 1));
        }

        private void HideMonochromeBoard()
        {
            for (var index = 0; index < _courtBoardTiles.Length; index++)
                if (_courtBoardTiles[index] != null) Hide(_courtBoardTiles[index]);
        }
    }
}
