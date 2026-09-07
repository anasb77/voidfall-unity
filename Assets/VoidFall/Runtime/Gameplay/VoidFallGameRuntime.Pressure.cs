using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private struct PressureBossMember
        {
            public int Slot;
            public int Identity;
            public double Maximum;
            public double Minimum;
        }

        private PressureBossMember[] _pressureBossMembers;
        private int _pressureBossCount;
        private int _pressureStageIndex;
        private bool _pressureEncounterCaptured;
        private bool _pressureCourtShared;
        private FrozenRunScore _frozenRunScore;
        private bool _hasFrozenRunScore;
        private Text _pressureText;
        private int _lastPressureHudValue = -1;

        public FrozenRunScore TerminalRunScore => _frozenRunScore;

        private void ResetPressureForRun()
        {
            _selectedDirectorProfile = DirectorProfiles.For((DirectorProfileId)(_saveData?.directorId ?? 0)).Id;
            _runDirectorProfile = _saveData?.directorOnboardingSeen == true
                ? _selectedDirectorProfile : DirectorProfileId.Standard;
            _runPressure.Reset(DirectorProfiles.For(_runDirectorProfile).PressureCeilingHundredths, 6);
            _hasFrozenRunScore = false;
            _frozenRunScore = default;
            _directorResultNeedsAcknowledgement = false;
            _lastPressureHudValue = -1;
            BeginPressureArena();
        }

        private void BeginPressureArena()
        {
            _pressureStageIndex = _completedVoids;
            _pressureBossCount = 0;
            _pressureEncounterCaptured = false;
            _pressureCourtShared = false;
        }

        private void StepRunPressure()
        {
            if (_runPressure.IsFrozen || _mainMenuBrowsing || _gameOver || _paused || _revivePending || _rouletteActive ||
                _prizeRevealActive || _levelUpActive || _routeMapOpen || _riftTransitionActive || JourneyStopsCombat) return;
            var phases = _objectives?.Objective as MultiPhaseObjective;
            if (phases == null) return;
            var survival = phases.PhaseIndex > 0 || phases.IsComplete ? 1.0 : phases.CurrentPhase.Progress01;
            if (_pressureStageIndex == 0 && phases.PhaseIndex == 0)
                survival = Math.Max(0, (survival * 300.0 - RunOpeningSeconds) / (300.0 - RunOpeningSeconds));
            var boss = 0.0;
            if (phases.PhaseIndex > 0 || phases.IsComplete)
            {
                CapturePressureBossEncounter();
                boss = ObservePressureBossHealth();
            }
            if (phases.IsComplete) boss = 1;
            _runPressure.ObserveStage(_pressureStageIndex, survival, boss);
        }

        private void CapturePressureBossEncounter()
        {
            if (_pressureEncounterCaptured || _gameSim == null) return;
            if (_pressureBossMembers == null || _pressureBossMembers.Length != _gameSim.Bosses.Length)
                _pressureBossMembers = new PressureBossMember[_gameSim.Bosses.Length];
            for (var slot = 0; slot < _gameSim.Bosses.Length; slot++)
            {
                var boss = _gameSim.Bosses[slot];
                if (!boss.Active || boss.MaxHealth <= 0 || float.IsNaN(boss.MaxHealth) || float.IsInfinity(boss.MaxHealth)) continue;
                if (IsCourtGrandmaster(boss.Id) && _monochromeSharedMaxHealth > 0)
                {
                    if (_pressureCourtShared) continue;
                    _pressureCourtShared = true;
                    _pressureBossMembers[_pressureBossCount++] = new PressureBossMember
                    { Slot = -1, Maximum = _monochromeSharedMaxHealth, Minimum = _monochromeSharedMaxHealth };
                    continue;
                }
                _pressureBossMembers[_pressureBossCount++] = new PressureBossMember
                { Slot = slot, Identity = GameSim.BossIdentity(boss, slot), Maximum = boss.MaxHealth, Minimum = boss.MaxHealth };
            }
            _pressureEncounterCaptured = _pressureBossCount > 0;
        }

        private double ObservePressureBossHealth()
        {
            double total = 0, remaining = 0;
            for (var index = 0; index < _pressureBossCount; index++)
            {
                var member = _pressureBossMembers[index];
                double health;
                if (member.Slot < 0) health = _monochromeSharedHealth;
                else
                {
                    var boss = _gameSim.Bosses[member.Slot];
                    health = boss.Active && GameSim.BossIdentity(boss, member.Slot) == member.Identity ? boss.Health : 0;
                }
                if (!double.IsNaN(health) && !double.IsInfinity(health))
                    member.Minimum = Math.Min(member.Minimum, Math.Max(0, health));
                _pressureBossMembers[index] = member;
                total += member.Maximum;
                remaining += member.Minimum;
            }
            return total > 0 ? Math.Max(0, Math.Min(1, 1 - remaining / total)) : 0;
        }

        private long CurrentEarnedBaseScore()
        {
            var progress = _runPressure.CreditedProgressSeconds;
            if (double.IsNaN(progress) || double.IsInfinity(progress)) progress = 0;
            var earned = (double)_score;
            if (double.IsNaN(earned) || double.IsInfinity(earned)) earned = 0;
            var total = Math.Max(0, earned) + 5 * Math.Max(0, progress) + 35.0 * Math.Max(0, _level - 1);
            return total >= long.MaxValue ? long.MaxValue : (long)Math.Floor(total + 0.5);
        }

        private void FreezePressureAndScore()
        {
            if (_hasFrozenRunScore) return;
            _runPressure.Freeze();
            _frozenRunScore = new FrozenRunScore(CurrentEarnedBaseScore(), PressureHundredths);
            _hasFrozenRunScore = true;
            _directorResultNeedsAcknowledgement = true;
        }

        private void ApplyDirectorResultSummary(ref GameOverSummary summary)
        {
            summary.HasDirectorScore = _hasFrozenRunScore;
            summary.DirectorName = DirectorProfiles.For(_runDirectorProfile).Name;
            summary.BaseScore = _frozenRunScore.BaseScore;
            summary.PressureHundredths = _frozenRunScore.PressureHundredths;
            summary.MultiplierHundredths = _frozenRunScore.MultiplierHundredths;
            summary.FinalScore = _frozenRunScore.FinalScore;
            summary.ScoringVersion = RunScoreRules.Version;
        }

        private void UpdatePressureHud()
        {
            if (_timeText == null) return;
            if (_pressureText == null)
            {
                _pressureText = CreateText(_timeText.transform.parent, Vector2.zero, new Vector2(.5f, 1), 10,
                    new Color(.663f, .733f, .812f));
                _pressureText.name = "Run Pressure";
                _pressureText.alignment = TextAnchor.UpperCenter;
                _pressureText.resizeTextForBestFit = false;
                var rect = _pressureText.rectTransform;
                rect.SetParent(_timeText.rectTransform, false);
                rect.anchorMin = new Vector2(.5f, 1);
                rect.anchorMax = new Vector2(.5f, 1);
                rect.pivot = new Vector2(.5f, 1);
                rect.sizeDelta = new Vector2(126, 14);
                rect.anchoredPosition = new Vector2(0, -32);
            }
            if (_lastPressureHudValue == PressureHundredths) return;
            _lastPressureHudValue = PressureHundredths;
            _pressureText.text = "PRESSURE " + (PressureHundredths / 100.0).ToString("F2", CultureInfo.InvariantCulture) + "×";
        }
    }
}
