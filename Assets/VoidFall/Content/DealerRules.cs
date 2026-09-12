using System;
using System.Collections.Generic;

namespace VoidFall.Core
{
    public enum DealerOfferKind { Shield, DelayedPower, ExtraProjectile, Fragment, EquipLegendary, UpgradeLegendary, MoreHealth, RecoveryPlan }
    public enum DealerPurchaseResult { Success, InvalidOffer, Unaffordable, AlreadyBought, DuplicateFragment, SaveFailed }
    public sealed class DealerOffer
    {
        public DealerOfferKind Kind;
        public string Title, Description, Art;
        public LegendaryWeaponId Legendary;
        public int Piece = -1, TargetWeapon = -1;
        public bool Cursed => Kind == DealerOfferKind.DelayedPower;
    }
    public struct DealerTransaction { public int WalletAfter, SoundMask, RifleMask; }
    public sealed class DealerSession
    {
        public readonly DealerOffer[] Offers;
        public int PurchasedIndex { get; private set; } = -1;
        public DealerSession(DealerOffer[] offers) { Offers = offers ?? Array.Empty<DealerOffer>(); }
        public DealerPurchaseResult TryBuy(int index, int wallet, int soundMask, int rifleMask,
            Func<DealerTransaction, bool> persist, out DealerTransaction receipt)
        {
            receipt = default;
            if (PurchasedIndex >= 0) return DealerPurchaseResult.AlreadyBought;
            if (index < 0 || index >= Offers.Length || Offers[index] == null) return DealerPurchaseResult.InvalidOffer;
            if (wallet < DealerRules.Price) return DealerPurchaseResult.Unaffordable;
            var offer = Offers[index];
            var candidate = new DealerTransaction { WalletAfter = wallet - DealerRules.Price,
                SoundMask = DealerRules.SanitizeMask(soundMask), RifleMask = DealerRules.SanitizeMask(rifleMask) };
            if ((offer.Kind == DealerOfferKind.EquipLegendary || offer.Kind == DealerOfferKind.UpgradeLegendary) &&
                (offer.Legendary == LegendaryWeaponId.None ||
                (offer.Legendary == LegendaryWeaponId.SoundBlade ? candidate.SoundMask : candidate.RifleMask) != 7))
                return DealerPurchaseResult.InvalidOffer;
            if (offer.Kind == DealerOfferKind.Fragment)
            {
                if (offer.Piece < 0 || offer.Piece > 2 || offer.Legendary == LegendaryWeaponId.None) return DealerPurchaseResult.InvalidOffer;
                var mask = offer.Legendary == LegendaryWeaponId.SoundBlade ? candidate.SoundMask : candidate.RifleMask;
                if ((mask & (1 << offer.Piece)) != 0) return DealerPurchaseResult.DuplicateFragment;
                mask |= 1 << offer.Piece;
                if (offer.Legendary == LegendaryWeaponId.SoundBlade) candidate.SoundMask = mask; else candidate.RifleMask = mask;
                if (persist == null || !persist(candidate)) return DealerPurchaseResult.SaveFailed;
            }
            receipt = candidate; PurchasedIndex = index; return DealerPurchaseResult.Success;
        }
    }
    public static class DealerRules
    {
        public const int Price = 100;
        public static string Id(DealerOffer offer)
        {
            switch (offer.Kind)
            {
                case DealerOfferKind.Shield: return "G13";
                case DealerOfferKind.DelayedPower: return "C01";
                case DealerOfferKind.ExtraProjectile: return "G11";
                case DealerOfferKind.MoreHealth: return "G05";
                case DealerOfferKind.RecoveryPlan: return "G04";
                case DealerOfferKind.Fragment: return LegendaryRules.Id(offer.Legendary) + "-fragment-" + offer.Piece;
                default: return LegendaryRules.Id(offer.Legendary) + "-" + offer.Kind.ToString().ToLowerInvariant();
            }
        }
        private static readonly string[] SoundPieces = { "First pulse", "Harmonic", "Final echo" };
        private static readonly string[] RiflePieces = { "Charge chamber", "Focusing rails", "Muzzle array" };
        public static int SanitizeMask(int mask) => Math.Max(0, mask) & 7;
        public static int PieceCount(int mask) { mask = SanitizeMask(mask); return (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1); }
        public static int MissingPiece(int mask) { for (var i = 0; i < 3; i++) if ((mask & (1 << i)) == 0) return i; return -1; }
        public static double DelayedMultiplier(bool owned, double combatSeconds) => !owned ? 1 : combatSeconds < 240 ? .8 : 1.4;
        public static float Absorb(ref float shield, float damage)
        {
            var absorbed = Math.Min(Math.Max(0, shield), Math.Max(0, damage)); shield = Math.Max(0, shield - absorbed);
            return Math.Max(0, damage - absorbed);
        }
        public static DealerOffer Fragment(LegendaryWeaponId weapon, int piece)
        {
            var names = weapon == LegendaryWeaponId.SoundBlade ? SoundPieces : RiflePieces;
            return new DealerOffer { Kind = DealerOfferKind.Fragment, Legendary = weapon, Piece = piece,
                Title = names[piece], Description = LegendaryRules.Name(weapon) + " fragment. Collect all three pieces to assemble and equip it.",
                Art = LegendaryRules.ArtId(weapon) + "-" + piece };
        }
        public static DealerOffer[] CreateOffers(uint seed, int soundMask, int rifleMask,
            LegendaryWeaponId equipped, int rank, int extraTarget, string targetName,
            bool shieldActive, bool delayedOwned, bool extraOwned, bool late)
        {
            soundMask = SanitizeMask(soundMask); rifleMask = SanitizeMask(rifleMask);
            var offers = new List<DealerOffer>();
            if (equipped == LegendaryWeaponId.None && (soundMask == 7 || rifleMask == 7))
            {
                var weapon = soundMask == 7 ? LegendaryWeaponId.SoundBlade : LegendaryWeaponId.ChargedRifle;
                offers.Add(Equipment(weapon, false));
            }
            else if (soundMask != 7) offers.Add(Fragment(LegendaryWeaponId.SoundBlade, MissingPiece(soundMask)));
            else if (rifleMask != 7) offers.Add(Fragment(LegendaryWeaponId.ChargedRifle, MissingPiece(rifleMask)));
            else if (rank < 3 && equipped != LegendaryWeaponId.None) offers.Add(Equipment(equipped, true));
            else offers.Add(Equipment(equipped == LegendaryWeaponId.SoundBlade ? LegendaryWeaponId.ChargedRifle : LegendaryWeaponId.SoundBlade, false));
            var pool = new List<DealerOffer>();
            if (!shieldActive) pool.Add(new DealerOffer { Kind = DealerOfferKind.Shield, Title = "Shield now", Description = "Gain 20 shield. It absorbs hostile damage before HP and does not regenerate.", Art = "shield" });
            if (!delayedOwned && !late) pool.Add(new DealerOffer { Kind = DealerOfferKind.DelayedPower, Title = "Delayed power", Description = "Deal 20% less damage for four combat minutes, then 40% more for the rest of this run.", Art = "delay" });
            if (!extraOwned && extraTarget >= 0) pool.Add(new DealerOffer { Kind = DealerOfferKind.ExtraProjectile, Title = "Extra projectile", Description = targetName + " fires one extra projectile per attack for this run.", TargetWeapon = extraTarget, Art = "projectile" });
            for (var i = 0; i < Math.Min(2, pool.Count); i++) offers.Add(pool[(i + (int)(seed % (uint)pool.Count)) % pool.Count]);
            if (offers.Count < 3) offers.Add(new DealerOffer { Kind = DealerOfferKind.MoreHealth, Title = "More health", Description = "Gain 3 maximum HP and 3 current HP for this run.", Art = "shield" });
            if (offers.Count < 3) offers.Add(new DealerOffer { Kind = DealerOfferKind.RecoveryPlan, Title = "Recovery plan", Description = "Add 5 healing charges. Each level-up consumes one to heal 3% maximum HP, even at full health.", Art = "shield" });
            if (offers.Count == 3) { var first = offers[0]; offers[0] = offers[1]; offers[1] = first; }
            return offers.ToArray();
        }
        private static DealerOffer Equipment(LegendaryWeaponId weapon, bool upgrade) => new DealerOffer {
            Kind = upgrade ? DealerOfferKind.UpgradeLegendary : DealerOfferKind.EquipLegendary,
            Legendary = weapon, Title = LegendaryRules.Name(weapon), Art = LegendaryRules.ArtId(weapon) + "-complete",
            Description = upgrade ? "Upgrade your equipped legendary by one tier for this run." : "Equip this legendary for this run. Replaces your current manual weapon." };
    }
}
