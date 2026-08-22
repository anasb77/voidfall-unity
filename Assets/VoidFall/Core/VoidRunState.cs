using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    /// <summary>One owned card's rank inside a run snapshot.</summary>
    public sealed class RunCardRank
    {
        public string Id;
        public int Rank;

        public RunCardRank() { }

        public RunCardRank(string id, int rank)
        {
            Id = id;
            Rank = rank;
        }
    }

    /// <summary>
    /// Everything that survives a Void transition (spec §5, §8, §28). Plain
    /// serializable data — the host copies it into the fresh simulation when
    /// a rift opens; active enemies, bullets, and pickups are never carried
    /// across. Global run time (not the local map timer) drives difficulty.
    /// </summary>
    public sealed class VoidRunState
    {
        public uint Seed;
        public string CurrentVoidId;
        public int Depth;
        public double GlobalRunTime;
        public double LocalVoidTime;

        public int Level;
        public double Xp;

        public double CurrentHp;
        public double MaxHp;

        public int Kills;
        public int EliteKills;
        public int BossKills;
        public int Parts;

        public List<string> CompletedVoids = new List<string>();
        public List<string> RouteHistory = new List<string>();
        public List<string> Boons = new List<string>();

        public List<RunCardRank> Weapons = new List<RunCardRank>();
        public List<RunCardRank> Supports = new List<RunCardRank>();
        public List<string> Evolutions = new List<string>();

        public static VoidRunState Begin(uint seed, string startVoidId, int startDepth,
            double maxHp)
        {
            return new VoidRunState
            {
                Seed = seed,
                CurrentVoidId = startVoidId,
                Depth = startDepth,
                MaxHp = maxHp,
                CurrentHp = maxHp,
                RouteHistory = { startVoidId }
            };
        }

        /// <summary>
        /// Enters the next Void. Depth and route history advance here so the
        /// two can never disagree; difficulty consumers read GlobalRunTime +
        /// Depth, never LocalVoidTime.
        /// </summary>
        public void EnterVoid(string voidId, int depth)
        {
            CurrentVoidId = voidId;
            Depth = depth;
            LocalVoidTime = 0;
            RouteHistory.Add(voidId);
        }

        public void CompleteVoid()
        {
            CompletedVoids.Add(CurrentVoidId);
        }

        public int WeaponRank(string weaponId)
        {
            return RankOf(Weapons, weaponId);
        }

        public int SupportRank(string supportId)
        {
            return RankOf(Supports, supportId);
        }

        public bool HasEvolution(string evolutionId)
        {
            return Evolutions.Contains(evolutionId);
        }

        public void SetWeaponRank(string weaponId, int rank)
        {
            SetRank(Weapons, weaponId, rank);
        }

        public void SetSupportRank(string supportId, int rank)
        {
            SetRank(Supports, supportId, rank);
        }

        private static int RankOf(List<RunCardRank> cards, string id)
        {
            foreach (var card in cards)
                if (string.Equals(card.Id, id, StringComparison.Ordinal)) return card.Rank;
            return 0;
        }

        private static void SetRank(List<RunCardRank> cards, string id, int rank)
        {
            foreach (var card in cards)
            {
                if (!string.Equals(card.Id, id, StringComparison.Ordinal)) continue;
                card.Rank = rank;
                return;
            }
            cards.Add(new RunCardRank(id, rank));
        }
    }
}
