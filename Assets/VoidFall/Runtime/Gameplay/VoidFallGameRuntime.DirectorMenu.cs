using System;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private bool _directorSelectionOpen;
        private bool _directorSelectionStartsRun;
        private bool _directorResultNeedsAcknowledgement;
        public bool DirectorResultNeedsAcknowledgement => _directorResultNeedsAcknowledgement;

        private bool TryOpenDirectorSelection()
        {
            if (_directorSelectionOpen) return true;
            if (_saveData == null || _saveData.directorOnboardingSeen || !HasCompletedDirectorRun()) return false;
            OpenDirectorSelection(true);
            return _directorSelectionOpen;
        }

        private bool HasCompletedDirectorRun() => (_saveData?.stats?.totalRuns ?? 0) > 0 ||
            (_saveData?.recentRuns?.Length ?? 0) > 0 || (_saveData?.highScores?.Length ?? 0) > 0;

        private void OpenDirectorSelectionFromHome()
        {
            if (!_mainMenuBrowsing || _saveData?.directorOnboardingSeen != true) return;
            OpenDirectorSelection(false);
        }

        private void OpenDirectorSelection(bool startsRun)
        {
            if (_ui?.DirectorSelection == null) return;
            _directorSelectionOpen = true;
            _directorSelectionStartsRun = startsRun;
            _ui.SetScreen(UIScreen.DirectorSelection);
            _ui.DirectorSelection.Show(DirectorProfiles.For((DirectorProfileId)(_saveData?.directorId ?? 0)).Id,
                CommitDirectorSelection, CancelDirectorSelection);
        }

        private void CancelDirectorSelection()
        {
            _directorSelectionOpen = false;
            _directorSelectionStartsRun = false;
            SyncUiScreen();
        }

        private void CommitDirectorSelection(DirectorProfileId id)
        {
            if (!_directorSelectionOpen || _saveData == null || _saveStore == null) return;
            var previous = CloneSaveData(_saveData);
            var chosen = DirectorProfiles.For(id).Id;
            _saveData.directorId = (int)chosen;
            _saveData.directorOnboardingSeen = true;
            try { _saveStore.Save(_saveData); }
            catch (Exception)
            {
                _saveData = previous;
                _ui?.DirectorSelection?.SetNotice("Choice was not saved. Select again to retry, or go back.");
                return;
            }
            _selectedDirectorProfile = chosen;
            var start = _directorSelectionStartsRun;
            _directorSelectionOpen = false;
            _directorSelectionStartsRun = false;
            RefreshMenuProfileUi();
            if (start) StartRunInternal(true);
            else SyncUiScreen();
        }

        private void AcknowledgeDirectorResult()
        {
            if (!_gameOver) return;
            if (!_runSaved) SaveRun();
            if (!_runSaved)
            {
                ShowDirectorResult();
                return;
            }
            _directorResultNeedsAcknowledgement = false;
            ReturnToMenuAfterResult();
        }

        private void ShowDirectorResult()
        {
            if (_ui?.GameOver == null) return;
            var summary = GameOverSummaryBuilder.Build(_runVictory, (int)Math.Min(int.MaxValue, _frozenRunScore.BaseScore),
                _time, _kills, _eliteKills, _bossKills, _level, _partsEarned, _lastRunIsBest, _lastRunSaved,
                _upgradeProgress?.WeaponRanks, _weaponDamage, _damageDealt, BuildRecapChips());
            ApplyDirectorResultSummary(ref summary);
            _ui.GameOver.Show(summary);
        }
    }
}
