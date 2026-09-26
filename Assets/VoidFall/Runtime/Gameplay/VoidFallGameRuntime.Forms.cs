using System;
using VoidFall.Core;
using VoidFall.Persistence;
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
        /// <summary>Commits completed-void progress and unlocks as one profile change.</summary>
        private void RecordVoidClearedForForms()
        {
            if (_saveData == null || _voidRoute == null) return;
            var candidate = CloneSaveData(_saveData);
            var newlyUnlocked = StageFormProgress(candidate,
                AddCounter(candidate.stats?.totalBossKills ?? 0, _bossKills));
            if (newlyUnlocked.Length == 0 && candidate.voidsCleared.Length == (_saveData.voidsCleared?.Length ?? 0))
                return;
            if (TryCommitProfile(candidate))
                AnnounceFormUnlocks(newlyUnlocked);
            else
                RecordRunHistory("profile_commit", "form_progress", "failed");
            // Failed writes leave the profile untouched. Completed route nodes
            // retain the earned facts for the next crossing or terminal retry.
        }

        /// <summary>Stages run facts without saving or announcing partial progress.</summary>
        private string[] StageFormProgress(SaveData candidate, int guardianKills)
        {
            var cleared = new System.Collections.Generic.List<string>(candidate.voidsCleared ?? Array.Empty<string>());
            if (_voidRoute != null)
                foreach (var nodeId in _voidRoute.History)
                    if (_voidRoute.StateOf(nodeId) == RouteNodeState.Completed)
                    {
                        var arenaId = _voidRoute.Node(nodeId).ArenaId;
                        if (!string.IsNullOrEmpty(arenaId) && !cleared.Contains(arenaId)) cleared.Add(arenaId);
                    }
            candidate.voidsCleared = cleared.ToArray();
            var newlyUnlocked = PlayerForms.EvaluateUnlocks(candidate.unlockedForms, guardianKills, cleared.Count);
            if (newlyUnlocked.Length == 0) return newlyUnlocked;
            var merged = new System.Collections.Generic.List<string>(candidate.unlockedForms ?? Array.Empty<string>());
            merged.AddRange(newlyUnlocked);
            candidate.unlockedForms = merged.ToArray();
            return newlyUnlocked;
        }

        private void AnnounceFormUnlocks(string[] newlyUnlocked)
        {
            foreach (var id in newlyUnlocked)
            {
                var form = PlayerForms.Form(id);
                EnqueueToast("Form unlocked", form.Name + " — " + form.Blurb, 4f, ToastKind.Reward);
                RecordRunHistory("form_unlocked", id, "saved", sourceId: "profile");
            }
        }

        private void CycleNextFormFromUi() => CycleFormFromUi(1);

        private void CyclePrevFormFromUi() => CycleFormFromUi(-1);

        private void SelectFormFromUi(string id)
        {
            if (!_mainMenuBrowsing || _saveData == null) return;
            if (_workshopController == null)
                _workshopController = new WorkshopController(_gameBridge);
            if (!_workshopController.TrySelectForm(_saveData, id, out var notice))
            {
                if (!string.IsNullOrEmpty(notice)) SetMenuNotice(notice);
            }
            else
            {
                _audio?.Play(ProceduralAudio.Cue.Ui, 1f);
            }
            RefreshWorkshopUi();
            RefreshMenuProfileUi();
        }

        /// <summary>
        /// Menu form cycling. Locked forms are skipped, not offered; with only
        /// the default unlocked the control is inert but still clickable.
        /// Mirrors the arena selector's flow: mutate the profile, refresh the
        /// menu, play the shared UI cue. Uses the same immediate save as Workshop.
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
            SelectFormFromUi(PlayerForms.All[nextIndex].Id);
        }
    }
}
