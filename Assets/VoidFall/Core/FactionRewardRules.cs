using System;

namespace VoidFall.Core
{
    public enum CombatFaction { Player, Enemy, Destroyer }

    public static class FactionRewardRules
    {
        public const int AmbientOffspringAllowance = 24;
        public const int BossOffspringAllowance = 64;
        public static bool Hostile(CombatFaction source, CombatFaction target) => source != target;
    }

    // Counts effective health damage only. A healed point cannot increase the initial bounty.
    public struct RewardContribution
    {
        public readonly double InitialHealth;
        public double PlayerDamage { get; private set; }
        private double _remaining;
        public RewardContribution(double initialHealth) { InitialHealth = Math.Max(0, initialHealth); PlayerDamage = 0; _remaining = InitialHealth; }
        public void AddPlayerDamage(double effectiveHealthDamage)
            => RecordDamage(effectiveHealthDamage, true);
        public void RecordDamage(double effectiveHealthDamage, bool playerOwned)
        {
            var credited = Math.Min(_remaining, Math.Max(0, effectiveHealthDamage));
            // Native health is float; consume only its final sub-ulp residue at the budget boundary.
            if (credited > 0 && _remaining - credited < InitialHealth * 0.000001) credited = _remaining;
            _remaining -= credited;
            if (playerOwned) PlayerDamage += credited;
        }
        public double Fraction => InitialHealth > 0 ? Math.Min(1, PlayerDamage / InitialHealth) : 0;
    }

    /// <summary>Fixed storage; generation-keyed handles survive pooled actor reuse. A root holds
    /// one reference per live actor or deferred birth, and one finite allowance for all descendants.</summary>
    public sealed class RewardRootLedger
    {
        private struct Root { public long Handle; public int References, Children; }
        private readonly Root[] _roots;
        private long _sequence;
        public RewardRootLedger(int capacity) { _roots = new Root[Math.Max(1, capacity)]; }
        public long Create(int childAllowance)
        {
            for (var i = 0; i < _roots.Length; i++)
            {
                if (_roots[i].References != 0) continue;
                var handle = ++_sequence * _roots.Length + i;
                _roots[i] = new Root { Handle = handle, References = 1, Children = Math.Max(0, childAllowance) };
                return handle;
            }
            return 0; // Capacity exhaustion never mints an untracked reward source.
        }
        private int Slot(long handle) => handle > 0 ? (int)(handle % _roots.Length) : -1;
        public bool IsValid(long handle)
        {
            var slot = Slot(handle);
            return slot >= 0 && _roots[slot].Handle == handle && _roots[slot].References > 0;
        }
        public bool Retain(long handle) { if (!IsValid(handle)) return false; _roots[Slot(handle)].References++; return true; }
        public void Release(long handle) { if (IsValid(handle)) _roots[Slot(handle)].References--; }
        public bool ClaimChild(long handle)
        {
            if (!IsValid(handle) || _roots[Slot(handle)].Children <= 0) return false;
            _roots[Slot(handle)].Children--; return true;
        }
        public void Clear() => Array.Clear(_roots, 0, _roots.Length);
    }
}
