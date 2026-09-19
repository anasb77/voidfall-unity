using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private LegendaryWeaponId _legendaryWeapon;
        private int _legendaryRank;
        private LegendaryState _legendaryState = new LegendaryState();
        private bool _legendaryHeld;
        private float _legendaryAim, _legendaryBeamLife, _legendaryBeamWidth;
        private Vector2 _legendaryPreviousPlayer, _legendaryBeamStart, _legendaryBeamEnd;
        private int[] _legendaryEnemyIds, _legendaryBossIds;
        private float[] _legendaryEnemyHitAt, _legendaryBossHitAt;
        private Vector2[] _legendaryEnemyPrevious;
        private LineRenderer _legendaryWave, _legendaryBeam, _legendaryBeamCore, _legendaryChargeRing;
        private SpriteRenderer _legendaryCannon;
        private Sprite _legendaryCannonSprite;
        private readonly double[] _legendaryDamage = new double[3], _legendaryDamageFlushed = new double[3];
        private readonly float[] _legendaryAttackSeconds = new float[3];
        private readonly int[] _legendaryShots = new int[3], _legendaryOverloads = new int[3];

        private void ResetLegendaries()
        {
            _legendaryWeapon = LegendaryWeaponId.None; _legendaryRank = 0;
            _legendaryState = new LegendaryState(); _legendaryState.Cancel(); _legendaryHeld = false;
            _legendaryBeamLife = 0;
            Array.Clear(_legendaryDamage, 0, 3); Array.Clear(_legendaryDamageFlushed, 0, 3);
            Array.Clear(_legendaryAttackSeconds, 0, 3); Array.Clear(_legendaryShots, 0, 3); Array.Clear(_legendaryOverloads, 0, 3);
            if (_legendaryEnemyIds != null) Array.Clear(_legendaryEnemyIds, 0, _legendaryEnemyIds.Length);
            if (_legendaryBossIds != null) Array.Clear(_legendaryBossIds, 0, _legendaryBossIds.Length);
            ClearLegendaryHitTimes();
        }
        private void EquipLegendary(LegendaryWeaponId weapon, int rank)
        {
            var previous = LegendaryRules.Id(_legendaryWeapon); var previousRank = _legendaryRank;
            _legendaryWeapon = weapon; _legendaryRank = Mathf.Clamp(rank, 1, 3);
            _legendaryState = new LegendaryState(); _legendaryState.Cancel(); _legendaryBeamLife = 0;
            _legendaryPreviousPlayer = _gameSim.Player.Position;
            if (_legendaryEnemyIds == null)
            {
                _legendaryEnemyIds = new int[_gameSim.Enemies.Length]; _legendaryEnemyHitAt = new float[_gameSim.Enemies.Length];
                _legendaryEnemyPrevious = new Vector2[_gameSim.Enemies.Length];
                _legendaryBossIds = new int[_gameSim.Bosses.Length]; _legendaryBossHitAt = new float[_gameSim.Bosses.Length];
            }
            Array.Clear(_legendaryEnemyIds, 0, _legendaryEnemyIds.Length); Array.Clear(_legendaryBossIds, 0, _legendaryBossIds.Length);
            ClearLegendaryHitTimes();
            EnsureLegendaryVisuals();
            RefreshApprovedBuildHud();
            RecordRunHistory("legendary_equipped", LegendaryRules.Id(weapon), previous == LegendaryRules.Id(weapon) ? "upgrade" : "equip",
                sourceId: "dealer", instanceId: _completedVoids, amount: _legendaryRank,
                detail: "previous=" + previous + ";previousRank=" + previousRank, progress: BuildTelemetryProgress());
        }
        private void CancelLegendaryInput() { _legendaryState?.Cancel(); _legendaryHeld = false; _legendaryBeamLife = 0; }
        private void ClearLegendaryHitTimes()
        {
            if (_legendaryEnemyHitAt != null) for (var i = 0; i < _legendaryEnemyHitAt.Length; i++) _legendaryEnemyHitAt[i] = float.MinValue;
            if (_legendaryBossHitAt != null) for (var i = 0; i < _legendaryBossHitAt.Length; i++) _legendaryBossHitAt[i] = float.MinValue;
        }
        private void ReadLegendaryInput()
        {
            if (_legendaryWeapon == LegendaryWeaponId.None) return;
            if (_mainMenuBrowsing || _gameOver || _paused || _dealerOpen || JourneyStopsCombat || _applicationInactive || _levelUpActive || _revivePending || _gameSim.Player.Health <= 0)
            { CancelLegendaryInput(); return; }
            var mouse = Mouse.current; var keyboard = Keyboard.current; var pad = Gamepad.current;
            _legendaryHeld = mouse != null && mouse.leftButton.isPressed || keyboard != null && keyboard.fKey.isPressed || pad != null && pad.rightTrigger.isPressed;
            if (mouse != null)
            {
                var screen = mouse.position.ReadValue(); var target = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
                _legendaryAim = Mathf.Atan2(target.y - _gameSim.Player.Position.y, target.x - _gameSim.Player.Position.x);
            }
            if (pad != null)
            {
                var stick = pad.rightStick.ReadValue(); if (stick.sqrMagnitude > .04f) _legendaryAim = Mathf.Atan2(stick.y, stick.x);
            }
        }
        private void StepLegendaries(float dt)
        {
            if (_legendaryWeapon == LegendaryWeaponId.None || dt <= 0) return;
            var beforeAngle = (float)_legendaryState.Angle;
            var beforeOverloads = _legendaryState.Overloads;
            var shot = _legendaryState.Step(dt, _legendaryHeld, _gameSim.Player.Health > 0,
                _legendaryWeapon, _legendaryRank, _legendaryAim, WeaponRecoveryScale());
            if (_legendaryState.Overloads > beforeOverloads) SpawnFloater(_gameSim.Player.Position, "OVERLOAD", new Color(1,.3f,.4f), 14);
            _legendaryOverloads[(int)_legendaryWeapon] += _legendaryState.Overloads - beforeOverloads;
            if (_legendaryState.Attacking) _legendaryAttackSeconds[(int)_legendaryWeapon] += dt;
            _legendaryBeamLife = Mathf.Max(0, _legendaryBeamLife - dt);
            var player = _gameSim.Player.Position;
            if (_legendaryWeapon == LegendaryWeaponId.SoundBlade)
            {
                var stats = LegendaryRules.Stats(_legendaryWeapon, _legendaryRank);
                var reach = (float)stats.Reach * _areaMultiplier;
                var interval = (float)stats.Interval * WeaponRecoveryScale();
                var damage = (float)stats.Damage * _damageMultiplier;
                for (var i = 0; i < _gameSim.Enemies.Length; i++)
                {
                    var enemy = _gameSim.Enemies[i]; if (!enemy.Active || enemy.Health <= 0) continue;
                    var same = _legendaryEnemyIds[i] == enemy.SpawnId;
                    if (!same) _legendaryEnemyHitAt[i] = float.MinValue;
                    var previous = same ? _legendaryEnemyPrevious[i] : enemy.Position;
                    if (_legendaryState.Attacking && (!same || _time - _legendaryEnemyHitAt[i] >= interval) &&
                        (enemy.Position - player).sqrMagnitude < (reach + enemy.Radius + 60) * (reach + enemy.Radius + 60))
                    {
                        for (var sample = 0; sample <= 4; sample++)
                        {
                            var t = sample / 4f; var angle = Mathf.Lerp(beforeAngle, (float)_legendaryState.Angle, t);
                            var root = Vector2.Lerp(_legendaryPreviousPlayer, player, t); var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                            var position = Vector2.Lerp(previous, enemy.Position, t);
                            if (LegendaryRules.SegmentDistance(position.x, position.y, root.x + direction.x * 18, root.y + direction.y * 18,
                                    root.x + direction.x * reach, root.y + direction.y * reach) > enemy.Radius + 8) continue;
                            _legendaryEnemyHitAt[i] = _time;
                            using (new FactionScope(this, -1, 0, CombatFaction.Player, 0))
                                ApplyEnemyDamage(i, damage, direction, 30, false, LegendaryRules.SoundDamageIndex);
                            break;
                        }
                    }
                    _legendaryEnemyIds[i] = enemy.SpawnId; _legendaryEnemyPrevious[i] = enemy.Position;
                }
                for (var i = 0; i < _gameSim.Bosses.Length; i++)
                {
                    var boss = _gameSim.Bosses[i]; if (!boss.Active || boss.Health <= 0 || !_legendaryState.Attacking) continue;
                    if (_legendaryBossIds[i] == boss.TelemetryInstanceId && _time - _legendaryBossHitAt[i] < interval) continue;
                    for (var sample = 0; sample <= 4; sample++)
                    {
                        var t = sample / 4f; var angle = Mathf.Lerp(beforeAngle, (float)_legendaryState.Angle, t);
                        var root = Vector2.Lerp(_legendaryPreviousPlayer, player, t); var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        if (LegendaryRules.SegmentDistance(boss.Position.x, boss.Position.y, root.x + direction.x * 18, root.y + direction.y * 18,
                            root.x + direction.x * reach, root.y + direction.y * reach) > boss.Radius + 8) continue;
                        _legendaryBossIds[i] = boss.TelemetryInstanceId; _legendaryBossHitAt[i] = _time;
                        using (new FactionScope(this, -1, 0, CombatFaction.Player, 0)) ApplyBossDamage(i, damage, LegendaryRules.SoundDamageIndex);
                        break;
                    }
                }
            }
            if (shot.Fired)
            {
                _legendaryShots[(int)_legendaryWeapon]++;
                var direction = new Vector2(Mathf.Cos((float)shot.Angle), Mathf.Sin((float)shot.Angle));
                _legendaryBeamStart = player + direction * 68.8f;
                _legendaryBeamEnd = _legendaryBeamStart + direction * 1100;
                _legendaryBeamWidth = (float)shot.Width * _areaMultiplier; _legendaryBeamLife = .3f;
                using (new FactionScope(this, -1, 0, CombatFaction.Player, 0))
                {
                    for (var i = 0; i < _gameSim.Enemies.Length; i++)
                    {
                        var enemy = _gameSim.Enemies[i]; if (!enemy.Active || enemy.Health <= 0) continue;
                        if (LegendaryRules.SegmentDistance(enemy.Position.x, enemy.Position.y, _legendaryBeamStart.x, _legendaryBeamStart.y, _legendaryBeamEnd.x, _legendaryBeamEnd.y) <= enemy.Radius + _legendaryBeamWidth / 2)
                            ApplyEnemyDamage(i, (float)shot.Damage * _damageMultiplier, direction, 80, false, LegendaryRules.RifleDamageIndex);
                    }
                    for (var i = 0; i < _gameSim.Bosses.Length; i++)
                    {
                        var boss = _gameSim.Bosses[i]; if (!boss.Active || boss.Health <= 0) continue;
                        if (LegendaryRules.SegmentDistance(boss.Position.x, boss.Position.y, _legendaryBeamStart.x, _legendaryBeamStart.y, _legendaryBeamEnd.x, _legendaryBeamEnd.y) <= boss.Radius + _legendaryBeamWidth / 2)
                            ApplyBossDamage(i, (float)shot.Damage * _damageMultiplier, LegendaryRules.RifleDamageIndex);
                    }
                }
                _audio?.Play(ProceduralAudio.Cue.Fire, .9f);
            }
            _legendaryPreviousPlayer = player;
        }
        private void EnsureLegendaryVisuals()
        {
            if (_legendaryWave != null) return;
            _legendaryWave = CreateLineView("Sound Blade Waveform", 39); _legendaryWave.positionCount = 97;
            _legendaryWave.sharedMaterial = ResolveAdditiveSpriteMaterial(); _legendaryWave.startWidth = _legendaryWave.endWidth = 2.4f;
            _legendaryWave.startColor = new Color(.88f,.45f,1,.9f); _legendaryWave.endColor = new Color(1,.55f,.8f,.9f);
            _legendaryBeam = CreateLineView("Charged Rifle Beam", 38); _legendaryBeam.positionCount = 2; _legendaryBeam.sharedMaterial = ResolveAdditiveSpriteMaterial();
            _legendaryBeamCore = CreateLineView("Charged Rifle Core", 39); _legendaryBeamCore.positionCount = 2; _legendaryBeamCore.sharedMaterial = ResolveAdditiveSpriteMaterial();
            _legendaryChargeRing = CreateLineView("Legendary Charge", 36); _legendaryChargeRing.positionCount = 33;
            _legendaryChargeRing.startWidth = _legendaryChargeRing.endWidth = 2;
            var texture = Resources.Load<Texture2D>("VoidFall/Dealer/beam-complete");
            if (texture != null) _legendaryCannonSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 8);
            _legendaryCannon = CreateView("Charged Rifle", _legendaryCannonSprite, 39);
        }
        private void RenderLegendaries()
        {
            var visible = _legendaryWeapon != LegendaryWeaponId.None && !_mainMenuBrowsing && !_gameOver && _gameSim.Player.Health > 0 && !JourneyStopsCombat;
            if (_legendaryWave == null) return;
            _legendaryWave.enabled = visible && _legendaryWeapon == LegendaryWeaponId.SoundBlade;
            _legendaryCannon.enabled = visible && _legendaryWeapon == LegendaryWeaponId.ChargedRifle;
            _legendaryChargeRing.enabled = visible && _legendaryWeapon == LegendaryWeaponId.ChargedRifle && _legendaryState.Charge > 0;
            _legendaryBeam.enabled = _legendaryBeamCore.enabled = visible && _legendaryBeamLife > 0;
            if (!visible) return;
            var player = _gameSim.Player.Position; var angle = (float)_legendaryState.Angle;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); var normal = new Vector2(-direction.y, direction.x);
            if (_legendaryWeapon == LegendaryWeaponId.SoundBlade)
            {
                var reach = (float)LegendaryRules.Stats(_legendaryWeapon, _legendaryRank).Reach * _areaMultiplier;
                var bands = _music?.SpectrumBands;
                for (var i = 0; i < 97; i++)
                {
                    var t = i / 96f;
                    var audio = bands != null && bands.Length > 0 ? bands[i % bands.Length] : 0;
                    var amplitude = (4 + Mathf.Clamp01(audio * 8) * 30) * Mathf.Sin(t * Mathf.PI);
                    var displacement = Mathf.Sin(t * 62 + _ambientClock * 9) * amplitude;
                    var position = player + direction * Mathf.Lerp(18, reach, t) + normal * displacement;
                    _legendaryWave.SetPosition(i, new Vector3(position.x, position.y, 0));
                }
            }
            else
            {
                _legendaryCannon.transform.position = player + direction * 52;
                _legendaryCannon.transform.rotation = Quaternion.Euler(0,0,angle*Mathf.Rad2Deg);
                _legendaryCannon.transform.localScale = Vector3.one * .7f;
                var warning = _legendaryState.Charge >= 2.35;
                _legendaryCannon.color = warning ? new Color(1,.3f,.4f) : Color.Lerp(new Color(.7f,.58f,1), Color.white, (float)_legendaryState.Charge / 2);
                _legendaryChargeRing.startColor = _legendaryChargeRing.endColor = warning ? new Color(1,.3f,.4f,.9f) : new Color(.75f,.6f,1,.8f);
                for (var i = 0; i < 33; i++)
                {
                    var a = Mathf.PI * .5f - i / 32f * Mathf.PI * 2 * (float)(_legendaryState.Charge / 3);
                    _legendaryChargeRing.SetPosition(i, player + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 33);
                }
            }
            if (_legendaryBeamLife > 0)
            {
                var alpha = _legendaryBeamLife / .3f;
                _legendaryBeam.SetPosition(0,_legendaryBeamStart); _legendaryBeam.SetPosition(1,_legendaryBeamEnd);
                _legendaryBeamCore.SetPosition(0,_legendaryBeamStart); _legendaryBeamCore.SetPosition(1,_legendaryBeamEnd);
                _legendaryBeam.startWidth = _legendaryBeam.endWidth = _legendaryBeamWidth;
                _legendaryBeamCore.startWidth = _legendaryBeamCore.endWidth = _legendaryBeamWidth * .18f;
                _legendaryBeam.startColor = _legendaryBeam.endColor = new Color(.68f,.42f,1,alpha * .55f);
                _legendaryBeamCore.startColor = _legendaryBeamCore.endColor = new Color(1,.9f,1,alpha);
            }
        }
        private void DestroyLegendaryVisuals() { if (_legendaryCannonSprite != null) Destroy(_legendaryCannonSprite); }
        private void FlushLegendaryTelemetry()
        {
            for (var i = 1; i <= 2; i++)
            {
                var id = LegendaryRules.Id((LegendaryWeaponId)i); var delta = _legendaryDamage[i] - _legendaryDamageFlushed[i];
                if (delta > 0) RecordRunHistory("weapon_damage_window", id, sourceId: "manual_slot", amount: (float)delta);
                if (_legendaryAttackSeconds[i] > 0 || _legendaryShots[i] > 0 || _legendaryOverloads[i] > 0)
                    RecordRunHistory("legendary_activity", id, sourceId: "manual_slot", amount: _legendaryShots[i],
                        durationSeconds: _legendaryAttackSeconds[i], blockedAttempts: _legendaryOverloads[i]);
                _legendaryDamageFlushed[i] = _legendaryDamage[i]; _legendaryAttackSeconds[i] = 0; _legendaryShots[i] = 0; _legendaryOverloads[i] = 0;
            }
        }
    }
}
