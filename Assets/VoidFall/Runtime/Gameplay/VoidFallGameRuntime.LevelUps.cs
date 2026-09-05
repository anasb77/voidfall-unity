using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        // Shared by combat and the safe reward phase; input-owned prompts survive both.
        private void AdvanceRunLevelUps(float realDt)
        {
            while (!_levelUpActive && _levelUpTimer < 0 && _xp >= _xpNeed)
            {
                _xp -= _xpNeed;
                _level++;
                _xpNeed = BalanceRules.XpNeededForLevel(_level);
                ApplyLevelRecovery();
                _telemetry.RecordLevel((float)_time, _level, _xpNeed, Mathf.FloorToInt(_xp));
                OpenLevelUp();
            }

            // The browser gives the level-up burst a short real-time slowdown
            // before it opens the choice screen. Gameplay continues at the
            // eased time scale during that window; only the prompt transition
            // uses real fixed time.
            if (_levelUpTimer >= 0)
            {
                _levelUpTimer -= realDt;
                if (_levelUpTimer <= 0)
                {
                    _levelUpTimer = -1;
                    _levelOptions = RollLevelOptions();
                    if (_levelOptions.Length == 0)
                    {
                        _partsEarned += 2;
                        _score += 150;
                        _gameSim.Player.Health = Mathf.Min(_gameSim.Player.MaxHealth, _gameSim.Player.Health + 12);
                        _targetTimeScale = 1;
                        if (_xp >= _xpNeed)
                        {
                            _xp -= _xpNeed;
                            _level++;
                            _xpNeed = BalanceRules.XpNeededForLevel(_level);
                            ApplyLevelRecovery();
                            _telemetry.RecordLevel((float)_time, _level, _xpNeed, Mathf.FloorToInt(_xp));
                            OpenLevelUp();
                        }
                    }
                    else
                    {
                        _levelUpPromptOpenedAt = Time.realtimeSinceStartup;
                        _levelUpScroll = Vector2.zero;
                        _levelUpActive = true;
                        _paused = true;
                        if (_ui != null && _levelOptions != null)
                        {
                            _ui.LevelUp?.ShowUpgrades(
                                BuildUpgradeCards(_levelOptions),
                                _rerollsRemaining,
                                SelectLevelOption);
                        }
                    }
                }
            }

        }
    }
}
