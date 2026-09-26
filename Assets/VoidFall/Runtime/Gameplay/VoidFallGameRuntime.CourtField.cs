using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private const int CourtSentinelState = 90;
        private const int CourtRookCount = 10;
        private sealed class CourtRook
        {
            public Vector2 Position;
            public bool White, Fallen;
            public float Health;
            public int SpawnId, PendingChildren, RequestedChildren, ReleasedChildren;
            public bool ReleaseBlocked;
            public SpriteRenderer Ruin, Eye;
        }
        private readonly LineRenderer[,] _courtBossAimLines = new LineRenderer[2,5];
        private readonly Sprite[] _courtStandingSprites = new Sprite[2];
        private Sprite _courtSlitPupilSprite;
        private readonly Sprite[] _courtFallenSprites = new Sprite[2];
        private readonly CourtRook[] _courtRooks = new CourtRook[CourtRookCount];
        private readonly int[] _courtArmingOrder = new int[CourtBoardColumns * CourtBoardRows];
        private MaterialPropertyBlock _courtTileProperties;
        private bool _courtFieldReady, _courtBoundaryContact;
        private int _courtFloorCycle = -1, _courtWarnedCount, _courtArmedCount;
        private bool _courtFloorBurst;

        private static bool IsCourtSentinel(EnemyState enemy) => enemy.Id == "court-rook" && enemy.State == CourtSentinelState;

        private void ResetCourtField()
        {
            HideCourtFieldDetails();
            ResetApprovedCourt();
            _courtFieldReady = false;
            _courtBoundaryContact = false;
            _courtFloorCycle = -1;
            _courtWarnedCount = _courtArmedCount = 0;
            System.Array.Clear(_courtArmingOrder, 0, _courtArmingOrder.Length);
            // Renderer ownership remains with the runtime world root; reuse on the next visit.
            for (var i = 0; i < _courtRooks.Length; i++)
                if (_courtRooks[i] != null)
                {
                    var rook = _courtRooks[i];
                    if (rook.PendingChildren > 0) RecordRunHistory("court_rook_release_cancelled", "court-sentinel", instanceId: rook.SpawnId,
                        amount: rook.PendingChildren, reason: "arena_reset", position: rook.Position);
                    rook.SpawnId = 0; rook.Health = 0; rook.Fallen = false; rook.PendingChildren = 0;
                    rook.RequestedChildren = rook.ReleasedChildren = 0; rook.ReleaseBlocked = false;
                }
        }

        private Vector2 CourtCellCentre(int x, int y) => _monochromeBoardOrigin +
            new Vector2((x + .5f) * _monochromeBoardTileSize.x, (y + .5f) * _monochromeBoardTileSize.y);

        private void EnsureCourtField()
        {
            if (_courtFieldReady) return;
            SetupMonochromePresentation();
            _monochromeBoardTileSize = CalculateMonochromeBoardTileSize();
            _monochromeBoardOrigin = _gameSim.Player.Position - new Vector2(CourtBoardColumns, CourtBoardRows) * ((float)MonochromeEncounterRules.TileSize * .5f);
            _courtFieldReady = true;
            var placed = 0;
            for (var attempt = 0; attempt < 600 && placed < CourtRookCount; attempt++)
            {
                var position = _monochromeBoardOrigin + new Vector2(
                    320f + (float)_gameSim.Rng.Next() * (CourtBoardColumns*(float)MonochromeEncounterRules.TileSize-640f),
                    320f + (float)_gameSim.Rng.Next() * (CourtBoardRows*(float)MonochromeEncounterRules.TileSize-640f));
                if ((position - _gameSim.Player.Position).sqrMagnitude < 440f * 440f) continue;
                var spaced = true;
                for (var i = 0; i < placed; i++)
                    if ((position - _courtRooks[i].Position).sqrMagnitude < 630f * 630f) { spaced = false; break; }
                if (!spaced) continue;
                var rook = _courtRooks[placed] ?? (_courtRooks[placed] = new CourtRook());
                rook.Position = position;
                rook.White = (placed & 1) != 0;
                rook.Fallen = placed % 4 == 3;
                rook.Health = rook.Fallen ? 0f : MonochromeEncounterRules.RookHealth(_gameSim.Rng.Next());
                rook.SpawnId = 0;
                if (!rook.Fallen) SpawnCourtSentinel(rook);
                RecordRunHistory("court_rook_layout", rook.Fallen ? "court-fallen-rook" : "court-sentinel", instanceId: rook.SpawnId,
                    hp: rook.Health, maxHp: rook.Health, position: position, detail: "slot=" + placed + ";minimumSpacing=630");
                placed++;
            }
            RecordRunHistory("court_board_created", "monochrome-court", amount: placed, position: _monochromeBoardOrigin,
                detail: "columns=56;rows=56;tileSize=129.6;fixedOrigin=true;cameraHeight=908;sliderPercent=60;sentinelTerritory=4x4;alternatingColor=true;crowdPush=true;courtRevision=2;bossFloor=wholeColor");
        }

        private void SpawnCourtSentinel(CourtRook rook)
        {
            if (!SpawnEnemy("court-rook", rook.Position, forcedRoster: EnemyRoster.One))
            {
                RecordRunHistory("court_rook_spawn_rejected", "court-sentinel", reason: "enemy_pool_full", position: rook.Position);
                return;
            }
            rook.SpawnId = _nextEnemyId - 1;
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                var enemy = _gameSim.Enemies[i];
                if (!enemy.Active || enemy.SpawnId != rook.SpawnId) continue;
                enemy.State = CourtSentinelState;
                enemy.Health = enemy.MaxHealth = rook.Health;
                enemy.Speed = 0f; enemy.Velocity = enemy.Knockback = Vector2.zero;
                enemy.Radius = 115f; enemy.Spin = enemy.Rotation = 0f;
                enemy.Seed = rook.White ? Mathf.Abs(enemy.Seed) + 1f : -Mathf.Abs(enemy.Seed) - 1f;
                _gameSim.Enemies[i] = enemy;
                RecordRunHistory("court_rook_spawn", "court-sentinel", instanceId: enemy.SpawnId, hp: enemy.Health,
                    maxHp: enemy.MaxHealth, position: enemy.Position);
                return;
            }
        }

        private void ClearCourtBossArena()
        {
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
            {
                if (IsCourtSentinel(_gameSim.Enemies[i])) continue;
                if (_gameSim.Enemies[i].Active) RecordEnemyRemoval(i, "court_boss_cleanup");
                _gameSim.Enemies[i] = default;
                Hide(_enemyViews[i]);
            }
            ResetEnemyOrder();
            for (var i = 0; i < _gameSim.Enemies.Length; i++)
                if (_gameSim.Enemies[i].Active) _gameSim.AppendEnemyOrder(i);
            ClearHostileShots();
            for (var i = 0; i < _gameSim.Bosses.Length; i++)
            {
                _gameSim.Bosses[i] = default;
                Hide(_bossViews[i]);
            }
            ResetBossOrder();
        }

        private void OnCourtSentinelKilled(EnemyState enemy)
        {
            if (!IsCourtSentinel(enemy)) return;
            foreach (var rook in _courtRooks)
            {
                if (rook == null || rook.Fallen || rook.SpawnId != enemy.SpawnId) continue;
                rook.Position = enemy.Position;
                rook.Fallen = true; rook.Health = 0f;
                ShowArenaToast("Sacificed the RoooK !", 2.4f, ToastKind.Info);
                rook.PendingChildren = rook.RequestedChildren = _gameSim.Rng.Next() < .5 ? 5 : 6;
                rook.ReleasedChildren = 0;
                RecordRunHistory("court_rook_sacrificed", "court-sentinel", instanceId: enemy.SpawnId,
                    amount: rook.RequestedChildren, position: rook.Position, detail: "queued=true;roster=1;enemy=chaser");
                return;
            }
        }

        private void DrainCourtSacrifices()
        {
            foreach (var rook in _courtRooks)
            {
                if (rook == null || rook.PendingChildren == 0) continue;
                var spawned = 0;
                while (rook.PendingChildren > 0)
                {
                    var angle = rook.ReleasedChildren * Mathf.PI * 2f / rook.RequestedChildren;
                    if (!SpawnEnemy("chaser", rook.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 145f,
                        forcedRoster: EnemyRoster.One))
                    {
                        if (!rook.ReleaseBlocked) RecordRunHistory("court_rook_release_pending", "court-sentinel", instanceId: rook.SpawnId,
                            amount: rook.PendingChildren, reason: "spawn_rejected", position: rook.Position);
                        rook.ReleaseBlocked = true;
                        break;
                    }
                    RecordRunHistory("court_rook_child_spawn", "chaser", sourceId: "court-sentinel", instanceId: _nextEnemyId - 1,
                        relatedInstanceId: rook.SpawnId, position: rook.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 145f,
                        detail: "roster=1");
                    rook.PendingChildren--; rook.ReleasedChildren++; spawned++;
                    rook.ReleaseBlocked = false;
                }
                if (spawned > 0) RecordRunHistory("court_rook_release", "chaser", sourceId: "court-sentinel", relatedInstanceId: rook.SpawnId,
                    amount: spawned, position: rook.Position, detail: "roster=1;remaining=" + rook.PendingChildren);
            }
        }

        private void ClampCourtPlayer()
        {
            if (_arenaId != ArenaId.MonochromeCourt || !_courtFieldReady) return;
            var player = _gameSim.Player;
            var size = new Vector2(CourtBoardColumns, CourtBoardRows) * (float)MonochromeEncounterRules.TileSize;
            var clamped = MonochromeRuntimeRules.ClampToBoard(player.Position, _monochromeBoardOrigin, size, PlayerRadius);
            var touching = clamped != player.Position;
            if (touching && !_courtBoundaryContact) RecordRunHistory("court_boundary_contact", "court-board",
                position: clamped, amount: Vector2.Distance(clamped, player.Position));
            _courtBoundaryContact = touching;
            if (clamped.x != player.Position.x) player.Velocity.x = 0f;
            if (clamped.y != player.Position.y) player.Velocity.y = 0f;
            player.Position = clamped;
            _gameSim.Player = player;
        }

        private Vector2 CourtSpawnPosition(Vector2 desired)
        {
            const float margin = 60f;
            var size = new Vector2(CourtBoardColumns, CourtBoardRows) * (float)MonochromeEncounterRules.TileSize;
            var position = MonochromeRuntimeRules.ClampToBoard(desired, _monochromeBoardOrigin, size, margin);
            // A camera clipped against an outer edge must not create an unreachable spawn or place it on Zack.
            if ((position - _gameSim.Player.Position).sqrMagnitude < 300f * 300f)
                position.x = position.x < _monochromeBoardOrigin.x + size.x * .5f
                    ? _monochromeBoardOrigin.x + size.x - margin : _monochromeBoardOrigin.x + margin;
            return position;
        }

        private void UpdateCourtGrandmasterAttack(ref BossState boss, BossDefinition definition, float dt)
        {
            if (_visualCaptureCourtBoss || !boss.Active || definition.Attacks == null || definition.Attacks.Length == 0) return;
            dt = Mathf.Max(0f, dt);
            if (boss.State == 0)
            {
                boss.AttackCooldown -= dt;
                if (boss.AttackCooldown > 0f) return;
                boss.ActiveAttack = definition.Attacks[boss.AttackIndex % definition.Attacks.Length];
                boss.AttackIndex++;
                boss.TargetPosition = _gameSim.Player.Position;
                boss.DashDirection = SourceVisualDirection(boss.TargetPosition - boss.Position);
                boss.AttackAngle = Mathf.Atan2(boss.DashDirection.y, boss.DashDirection.x);
                boss.State = 1; boss.StateTimer = (float)boss.ActiveAttack.TelegraphSeconds;
                boss.ActionApplied = false;
                var phaseTwo = _monochromeSharedHealth <= _monochromeSharedMaxHealth * .5f;
                boss.AttackCooldown = (float)(boss.ActiveAttack.CooldownSeconds *
                    (phaseTwo ? definition.PhaseTwoCooldownMultiplier : 1));
                RecordRunHistory("court_boss_attack_warning", boss.ActiveAttack.Id, sourceId: boss.Id,
                    instanceId: boss.TelemetryInstanceId, durationSeconds: boss.StateTimer,
                    position: boss.TargetPosition, detail: "aimSnapshotted=true");
                return;
            }
            if (boss.State == 1)
            {
                boss.StateTimer -= dt;
                if (boss.StateTimer > 0f) return;
                boss.State = 2; boss.StateTimer = Mathf.Max(.01f, (float)boss.ActiveAttack.ActiveSeconds);
                FireCourtGrandmasterVolley(ref boss);
                return;
            }
            if (boss.State == 2)
            {
                FireCourtGrandmasterVolley(ref boss);
                boss.StateTimer -= dt;
                if (boss.StateTimer > 0f) return;
                boss.State = 3; boss.StateTimer = (float)boss.ActiveAttack.RecoverySeconds;
                return;
            }
            if (boss.State == 3)
            {
                boss.StateTimer -= dt;
                if (boss.StateTimer <= 0f) boss.State = 0;
            }
        }

        private void FireCourtGrandmasterVolley(ref BossState boss)
        {
            if (boss.ActionApplied || boss.ActiveAttack == null) return;
            boss.ActionApplied = true;
            var black = boss.ActiveAttack.Id == "court-black-volley";
            var count = black ? 3 : 5;
            var before = _gameSim.HostileShotOrder.Count;
            for (var shot = 0; shot < count; shot++)
            {
                var angle = boss.AttackAngle + (shot - (count - 1) * .5f) * (black ? .14f : .19f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                SpawnHostileShot(boss.Position + direction * (boss.Radius + 8f), direction,
                    (float)boss.ActiveAttack.Damage * boss.DamageScale, black ? 340f : 380f, 0f);
            }
            RecordRunHistory("court_boss_attack_fired", boss.ActiveAttack.Id, sourceId: boss.Id,
                instanceId: boss.TelemetryInstanceId, amount: _gameSim.HostileShotOrder.Count - before,
                position: boss.Position, detail: "requested=" + count + ";aimX=" + boss.TargetPosition.x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ";aimY=" + boss.TargetPosition.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private int CourtCellIndex(Vector2 position)
        {
            var cell = position - _monochromeBoardOrigin;
            var x = Mathf.FloorToInt(cell.x / (float)MonochromeEncounterRules.TileSize);
            var y = Mathf.FloorToInt(cell.y / (float)MonochromeEncounterRules.TileSize);
            return x < 0 || y < 0 || x >= CourtBoardColumns || y >= CourtBoardRows ? -1 : y * CourtBoardColumns + x;
        }

        private void AddCourtScope(Vector2 position)
        {
            var centre = CourtCellIndex(position);
            if (centre < 0) return;
            var cx = centre % CourtBoardColumns; var cy = centre / CourtBoardColumns;
            for (var dy = -3; dy <= 3; dy++)
            for (var dx = -3; dx <= 3; dx++)
            {
                var x = cx + dx; var y = cy + dy;
                if (x < 0 || y < 0 || x >= CourtBoardColumns || y >= CourtBoardRows || !MonochromeEncounterRules.InLocalScope(dx, dy)) continue;
                if (((x + y) & 1) != (_monochromeHazard.Faction == CourtFaction.White ? 0 : 1)) continue;
                _courtArmingOrder[y * CourtBoardColumns + x] = -1;
            }
        }

        private void StepCourtLocalFloor()
        {
            if (!_monochromeBossEncounterActive) return;
            var cycle = Mathf.FloorToInt(_monochromeBossElapsed / 5f);
            var age = _monochromeBossElapsed - cycle * 5f;
            if (cycle != _courtFloorCycle)
            {
                _courtFloorCycle = cycle; _courtFloorBurst = false; _courtWarnedCount = _courtArmedCount = 0;
                System.Array.Clear(_courtArmingOrder, 0, _courtArmingOrder.Length);
                for(var y=0;y<CourtBoardRows;y++)for(var x=0;x<CourtBoardColumns;x++)
                    if(((x+y)&1)==(_monochromeHazard.Faction==CourtFaction.White?0:1))
                        _courtArmingOrder[y*CourtBoardColumns+x]=++_courtWarnedCount;
                RecordRunHistory("court_floor_scope", "court-floor", instanceId: cycle + 1, amount: _courtWarnedCount,
                    position: _monochromeBoardOrigin, detail: "scope=whole_board;bossOnly=true;faction=" + _monochromeHazard.Faction);
            }
            _courtArmedCount = _courtWarnedCount;
            if (_courtFloorBurst || age < (float)MonochromeEncounterRules.HazardWarningSeconds) return;
            _courtFloorBurst = true;
            var hit = CourtPositionWasWarned(_gameSim.Player.Position);
            if(hit)DamagePlayer(22f,Vector2.zero);
            for(var y=0;y<CourtBoardRows;y++)for(var x=0;x<CourtBoardColumns;x++)
                if(_courtArmingOrder[y*CourtBoardColumns+x]>0)CourtCellBurstFx(x,y);
            RecordRunHistory("court_floor_burst", "court-floor", instanceId: cycle+1,amount:_courtWarnedCount,
                position:_monochromeBoardOrigin,detail:"bossOnly=true;playerHit="+hit);
        }

        private bool CourtPositionWasWarned(Vector2 position)
        {
            var index = CourtCellIndex(position);
            return index >= 0 && _courtArmingOrder[index] > 0 && _courtArmingOrder[index] <= _courtArmedCount;
        }

        private bool CourtCellIsArmed(int x, int y) => _monochromeBossEncounterActive && _courtFloorCycle >= 0 &&
            _monochromeHazard.Stage != CourtHazardStage.Recovery && _courtArmingOrder[y * CourtBoardColumns + x] > 0 &&
            _courtArmingOrder[y * CourtBoardColumns + x] <= _courtArmedCount;

        private void HideCourtFieldDetails()
        {
            foreach (var line in _courtBossAimLines) if (line != null) line.enabled = false;
            foreach (var rook in _courtRooks)
                if (rook != null) { Hide(rook.Ruin); Hide(rook.Eye); }
        }

        private void RenderCourtFieldDetails()
        {
            SyncCourtRookPositions();
            RenderCourtGrandmasterAim();
            RenderApprovedTerritories();
            foreach (var rook in _courtRooks)
            {
                if (rook == null || (!rook.Fallen && rook.SpawnId == 0)) continue;
                if (rook.Eye == null)
                {
                    rook.Eye = CreateView("Court rook tracking slit", CourtSlitPupilSprite(), 30);
                    rook.Ruin = CreateView("Court fallen rook", ProceduralSpriteFactory.Enemy(rook.White ? "court-rook-white" : "court-rook-black", Color.white, false), 8);
                }
                if (!rook.Fallen)
                {
                    Hide(rook.Ruin);
                    rook.Eye.sprite = CourtSlitPupilSprite();
                    var aim = (_gameSim.Player.Position - rook.Position).normalized;
                    rook.Eye.transform.position = rook.Position + new Vector2(aim.x * 12f, -4f + aim.y * 5f);
                    rook.Eye.transform.localScale = Vector3.one * (14f / Mathf.Max(.01f, rook.Eye.sprite.bounds.size.x));
                    rook.Eye.transform.rotation = Quaternion.identity;
                    rook.Eye.color = rook.White ? new Color(244f/255f,244f/255f,244f/255f) : new Color(8f/255f,8f/255f,8f/255f);
                    rook.Eye.enabled = true;
                    continue;
                }
                Hide(rook.Eye);
                rook.Ruin.sprite = CourtFallenRookSprite(rook.White);
                rook.Ruin.transform.position = rook.Position;
                rook.Ruin.transform.rotation = Quaternion.Euler(0f, 0f, 5f);
                rook.Ruin.transform.localScale = Vector3.one * (340f / Mathf.Max(.01f, rook.Ruin.sprite.bounds.size.x));
                rook.Ruin.color = Color.white; rook.Ruin.enabled = true;
            }
        }
        private void RenderCourtGrandmasterAim()
        {
            foreach (var line in _courtBossAimLines) if (line != null) line.enabled = false;
            if (!_monochromeBossEncounterActive) return;
            foreach (var boss in _gameSim.Bosses)
            {
                if (!boss.Active || !IsCourtGrandmaster(boss.Id) || boss.State != 1 || boss.ActiveAttack == null) continue;
                var black = boss.Id == CourtBlackBossId; var row = black ? 0 : 1; var count = black ? 3 : 5;
                var progress = Mathf.Clamp01(1f-boss.StateTimer/Mathf.Max(.01f,(float)boss.ActiveAttack.TelegraphSeconds));
                for (var i = 0; i < count; i++)
                {
                    var line = _courtBossAimLines[row,i];
                    if (line == null) _courtBossAimLines[row,i] = line = CreateLineView("Court Grandmaster aimed volley " + row + " " + i, 7);
                    var angle = boss.AttackAngle + (i-(count-1)*.5f)*(black?.14f:.19f);
                    var direction = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    line.positionCount = 2;
                    line.SetPosition(0,boss.Position+direction*(boss.Radius+8f));
                    line.SetPosition(1,boss.Position+direction*(float)(boss.ActiveAttack.BeamLength ?? 900));
                    line.startWidth = line.endWidth = 2f + progress * 2f;
                    line.startColor = line.endColor = new Color(1f,.72f,.4f,.18f+progress*.38f);
                    line.enabled = true;
                }
            }
        }

        private bool TryRenderCourtSentinel(int index, EnemyState enemy)
        {
            if (!IsCourtSentinel(enemy)) return false;
            var view = _enemyViews[index];
            view.sprite = CourtStandingRookSprite(CourtFactionOf(enemy) == CourtFaction.White);
            view.transform.position = enemy.Position;
            view.transform.rotation = Quaternion.identity;
            // Explicit canvas width: the ordinary rook sprite's source frame size must not magnify this prop.
            view.transform.localScale = Vector3.one * (280f / Mathf.Max(.01f, view.sprite.bounds.size.x));
            view.color = enemy.HitTimer > 0f ? new Color(1f,.86f,.78f,1f) : Color.white;
            view.enabled = true;
            return true;
        }

        private static bool InsideCourtPolygon(Vector2 point, Vector2[] polygon)
        {
            var inside = false;
            for (var i = 0; i < polygon.Length; i++)
            {
                var a=polygon[i]; var b=polygon[(i+1)%polygon.Length];
                if ((a.y>point.y)!=(b.y>point.y) && point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return inside;
        }

        private static bool OnCourtLine(Vector2 point, Vector2 a, Vector2 b, float halfWidth)
        {
            var delta=b-a;
            var t=Mathf.Clamp01(Vector2.Dot(point-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
            return (point-a-delta*t).sqrMagnitude <= halfWidth*halfWidth;
        }

        private Sprite CourtStandingRookSprite(bool white)
        {
            var index=white?1:0;
            if (_courtStandingSprites[index]!=null) return _courtStandingSprites[index];
            // Exact standing silhouette and angular socket from the approved adaptations.js study.
            var body=new[] { new Vector2(-110,110),new Vector2(110,110),new Vector2(110,82),new Vector2(76,68),new Vector2(63,-40),new Vector2(104,-61),new Vector2(104,-114),new Vector2(64,-114),new Vector2(64,-87),new Vector2(22,-87),new Vector2(22,-120),new Vector2(-22,-120),new Vector2(-22,-87),new Vector2(-64,-87),new Vector2(-64,-114),new Vector2(-104,-114),new Vector2(-104,-61),new Vector2(-63,-40),new Vector2(-76,68),new Vector2(-110,82) };
            var socket=new[] { new Vector2(-44,-7),new Vector2(-20,-17),new Vector2(0,-11),new Vector2(20,-17),new Vector2(44,-7),new Vector2(27,18),new Vector2(0,27),new Vector2(-27,18) };
            const int size=280;
            var pixels=new Color32[size*size];
            Color32 fill=white?new Color32(244,244,244,255):new Color32(8,8,8,255);
            Color32 ink=white?new Color32(8,8,8,255):new Color32(248,248,248,255);
            for(var y=0;y<size;y++)
            for(var x=0;x<size;x++)
            {
                // Canvas coordinates are downward; Unity texture rows are upward.
                var point=new Vector2(x-size*.5f,size*.5f-y);
                var edge=false;
                for(var i=0;i<body.Length;i++)
                    if(OnCourtLine(point,body[i],body[(i+1)%body.Length],2.5f)) {edge=true;break;}
                if(InsideCourtPolygon(point,body)||edge) pixels[y*size+x]=edge?ink:fill;
                if(InsideCourtPolygon(point,socket)||
                   OnCourtLine(point,new Vector2(-72,69),new Vector2(72,69),1.5f)||
                   OnCourtLine(point,new Vector2(-64,-39),new Vector2(64,-39),1.5f)||
                   OnCourtLine(point,new Vector2(-49,-29),new Vector2(-6,-20),3f)||
                   OnCourtLine(point,new Vector2(49,-29),new Vector2(6,-20),3f)||
                   OnCourtLine(point,new Vector2(-31,17),new Vector2(-40,37),1f)||
                   OnCourtLine(point,new Vector2(31,17),new Vector2(40,37),1f)) pixels[y*size+x]=ink;
            }
            _courtStandingSprites[index]=ArenaPlateFactory.SpriteFromPixels(pixels,size,size,"Court standing sentinel "+(white?"white":"black"));
            return _courtStandingSprites[index];
        }

        private Sprite CourtSlitPupilSprite()
        {
            if(_courtSlitPupilSprite!=null)return _courtSlitPupilSprite;
            const int width=14,height=44;
            var pixels=new Color32[width*height];
            var diamond=new[] {new Vector2(0,-19),new Vector2(5,0),new Vector2(0,20),new Vector2(-5,0)};
            for(var y=0;y<height;y++)
            for(var x=0;x<width;x++)
                if(InsideCourtPolygon(new Vector2(x-width*.5f,height*.5f-y),diamond)) pixels[y*width+x]=new Color32(255,255,255,255);
            _courtSlitPupilSprite=ArenaPlateFactory.SpriteFromPixels(pixels,width,height,"Court tracking diamond slit pupil");
            return _courtSlitPupilSprite;
        }

        private Sprite CourtFallenRookSprite(bool white)
        {
            var index = white ? 1 : 0;
            if (_courtFallenSprites[index] != null) return _courtFallenSprites[index];
            // Approved prototype silhouette: separate foot, jagged shaft, blunt crown, debris and X eye.
            var polygons = new[]
            {
                new[] { new Vector2(-150,-62), new Vector2(-119,-62), new Vector2(-119,-42), new Vector2(-62,-37), new Vector2(-78,-12), new Vector2(-57,9), new Vector2(-77,36), new Vector2(-119,42), new Vector2(-119,62), new Vector2(-150,62) },
                new[] { new Vector2(-48,-36), new Vector2(-63,-10), new Vector2(-44,10), new Vector2(-61,36), new Vector2(83,43), new Vector2(97,66), new Vector2(145,66), new Vector2(145,35), new Vector2(123,35), new Vector2(123,12), new Vector2(153,12), new Vector2(153,-15), new Vector2(123,-15), new Vector2(123,-37), new Vector2(145,-37), new Vector2(145,-66), new Vector2(97,-66), new Vector2(83,-43) },
                new[] { new Vector2(-75,61), new Vector2(-56,51), new Vector2(-44,64), new Vector2(-61,73) },
                new[] { new Vector2(-26,-60), new Vector2(-9,-63), new Vector2(-5,-49), new Vector2(-22,-47) },
            };
            const int width = 340, height = 180;
            var pixels = new Color32[width * height];
            Color32 fill = white ? new Color32(201,201,201,255) : new Color32(21,21,21,255);
            Color32 ink = white ? new Color32(24,24,24,255) : new Color32(197,197,197,255);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var point = new Vector2(x - width * .5f, y - height * .5f);
                var inside = false; var edge = false;
                foreach (var polygon in polygons)
                {
                    var contained = false;
                    for (var i = 0; i < polygon.Length; i++)
                    {
                        var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
                        if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) contained = !contained;
                        var ab = b-a; var t = Mathf.Clamp01(Vector2.Dot(point-a,ab)/ab.sqrMagnitude);
                        if ((point-a-ab*t).sqrMagnitude <= 4f) edge = true;
                    }
                    inside |= contained;
                }
                if (inside || edge) pixels[y*width+x] = edge ? ink : fill;
                var eye = new Vector2((point.x-14f)/29f,point.y/20f);
                if (eye.sqrMagnitude <= 1f) pixels[y*width+x] = ink;
                if (Mathf.Abs(point.x-14f) < 14f && Mathf.Abs(point.y) < 13f &&
                    (Mathf.Abs(point.y-(point.x-14f)) < 3f || Mathf.Abs(point.y+(point.x-14f)) < 3f)) pixels[y*width+x] = fill;
                if (Mathf.Abs(point.x-81f) < 1.5f && Mathf.Abs(point.y) < 41f) pixels[y*width+x] = ink;
            }
            _courtFallenSprites[index] = ArenaPlateFactory.SpriteFromPixels(pixels,width,height,"Court fallen rook " + (white ? "white" : "black"));
            return _courtFallenSprites[index];
        }

        private void DestroyCourtFieldSprites()
        {
            foreach (var sprite in _courtFallenSprites) DestroyCourtSprite(sprite);
            foreach (var sprite in _courtStandingSprites) DestroyCourtSprite(sprite);
            DestroyCourtSprite(_courtSlitPupilSprite);
        }

        private void DestroyCourtSprite(Sprite sprite)
        {
            if(sprite==null)return;
            var texture=sprite.texture;
            Destroy(sprite);
            if(texture!=null)Destroy(texture);
        }

    }
}
