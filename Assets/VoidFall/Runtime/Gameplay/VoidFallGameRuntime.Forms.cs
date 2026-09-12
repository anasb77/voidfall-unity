using System;
using VoidFall.Core;
using VoidFall.UI;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Player form state (spec §05): menu selection, unlock triggers and the
    /// run-start application helpers. The form changes starting strengths and
    /// the silhouette, never the protagonist; unlock gates are the spec's
    /// proposals [P] and persist inside the profile alongside — never inside —
    /// the shared Workshop ranks.
    /// </summary>
    public partial class VoidFallGameRuntime
    {
        /// <summary>
        /// Records the completed Void's arena once per clear and evaluates the
        /// form unlock gates. Called from the journey's void-complete edge.
        /// </summary>
        private void RecordVoidClearedForForms()
        {
            if (_saveData == null || _voidRoute == null) return;
            var arenaId = _voidRoute.CurrentArenaId;
            if (string.IsNullOrEmpty(arenaId)) return;
            if (Array.IndexOf(_saveData.voidsCleared ?? Array.Empty<string>(), arenaId) < 0)
            {
                var grown = new string[(_saveData.voidsCleared?.Length ?? 0) + 1];
                if (_saveData.voidsCleared != null) Array.Copy(_saveData.voidsCleared, grown, _saveData.voidsCleared.Length);
                grown[grown.Length - 1] = arenaId;
                _saveData.voidsCleared = grown;
            }
            EvaluateFormUnlocks();
        }

        /// <summary>
        /// Applies the spec's unlock gates against lifetime progress: the
        /// Dasher after the first guardian defeat, the Brute after three
        /// distinct Void clears across runs. Newly opened forms announce
        /// themselves once and persist with the profile immediately, so a
        /// crash after the gate cannot take the unlock back.
        /// </summary>
        private void EvaluateFormUnlocks()
        {
            if (_saveData == null) return;
            var guardianKills = (_saveData.stats?.totalBossKills ?? 0) + _bossKills;
            var newlyUnlocked = PlayerForms.EvaluateUnlocks(
                _saveData.unlockedForms,
                guardianKills,
                _saveData.voidsCleared?.Length ?? 0);
            if (newlyUnlocked.Length == 0) return;

            var merged = new string[(_saveData.unlockedForms?.Length ?? 0) + newlyUnlocked.Length];
            if (_saveData.unlockedForms != null) Array.Copy(_saveData.unlockedForms, merged, _saveData.unlockedForms.Length);
            for (var index = 0; index < newlyUnlocked.Length; index++)
            {
                merged[merged.Length - newlyUnlocked.Length + index] = newlyUnlocked[index];
                var form = PlayerForms.Form(newlyUnlocked[index]);
                EnqueueToast("Form unlocked", form.Name + " — " + form.Blurb, 4f, ToastKind.Reward);
            }
            _saveData.unlockedForms = merged;
            _gameBridge?.TryPersistProfile();
        }

        private void CycleNextFormFromUi() => CycleFormFromUi(1);

        private void CyclePrevFormFromUi() => CycleFormFromUi(-1);

        /// <summary>
        /// Menu form cycling. Locked forms are skipped, not offered; with only
        /// the default unlocked the control is inert but still clickable.
        /// Mirrors the arena selector's flow: mutate the profile, refresh the
        /// menu, play the shared UI cue. Persisted with the next terminal save.
        /// </summary>
        private void CycleFormFromUi(int delta)
        {
            if (_saveData == null) return;
            var unlocked = _saveData.unlockedForms ?? new[] { PlayerForms.DefaultId };
            var currentIndex = PlayerForms.IndexOf(_saveData.form);
            var nextIndex = currentIndex;
            for (var step = 0; step < PlayerForms.All.Length; step++)
            {
                nextIndex = (nextIndex + delta + PlayerForms.All.Length) % PlayerForms.All.Length;
                if (Array.IndexOf(unlocked, PlayerForms.All[nextIndex].Id) >= 0) break;
            }
            if (nextIndex == currentIndex) return;
            _saveData.form = PlayerForms.All[nextIndex].Id;
            RefreshMenuProfileUi();
            _audio?.Play(ProceduralAudio.Cue.Ui, 1f);
        }
    }
}
