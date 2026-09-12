using System;
namespace VoidFall.Core
{
    public enum LegendaryWeaponId { None, SoundBlade, ChargedRifle }
    public struct LegendaryStats { public double Damage, Reach, Interval, MinimumDamage, Width, MinimumWidth, Recovery; }
    public struct LegendaryShot { public bool Fired; public double Angle, Damage, Width; }
    public static class LegendaryRules
    {
        // Attribution IDs are outside the automatic-arsenal slot array.
        public const int SoundDamageIndex = -2, RifleDamageIndex = -3;
        public static string Id(LegendaryWeaponId id) => id == LegendaryWeaponId.SoundBlade ? "sound-blade" : id == LegendaryWeaponId.ChargedRifle ? "charged-rifle" : string.Empty;
        public static string Name(LegendaryWeaponId id) => id == LegendaryWeaponId.SoundBlade ? "Sound Blade" : id == LegendaryWeaponId.ChargedRifle ? "Charged Rifle" : string.Empty;
        public static string ArtId(LegendaryWeaponId id) => id == LegendaryWeaponId.SoundBlade ? "sound" : "beam";
        public static LegendaryStats Stats(LegendaryWeaponId id, int rank)
        {
            rank = Math.Max(1, Math.Min(3, rank));
            if (id == LegendaryWeaponId.SoundBlade) return new LegendaryStats { Damage = rank == 1 ? 38 : rank == 2 ? 50 : 64,
                Reach = rank == 1 ? 230 : rank == 2 ? 255 : 280, Interval = rank == 1 ? .18 : rank == 2 ? .16 : .14 };
            return new LegendaryStats { Damage = rank == 1 ? 310 : rank == 2 ? 405 : 515, MinimumDamage = rank == 1 ? 65 : rank == 2 ? 85 : 110,
                Reach = 1100, Width = rank == 1 ? 64 : rank == 2 ? 74 : 84, MinimumWidth = rank == 1 ? 12 : rank == 2 ? 14 : 16,
                Recovery = rank == 1 ? .7 : rank == 2 ? .6 : .5 };
        }
        public static double SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
        {
            var dx = bx - ax; var dy = by - ay; var denominator = dx * dx + dy * dy;
            var t = denominator <= 0 ? 0 : Math.Max(0, Math.Min(1, ((px - ax) * dx + (py - ay) * dy) / denominator));
            return Math.Sqrt(Math.Pow(px - ax - dx * t, 2) + Math.Pow(py - ay - dy * t, 2));
        }
    }
    public sealed class LegendaryState
    {
        public double Angle, Charge, Recovery;
        public bool Attacking { get; private set; }
        public int Overloads { get; private set; }
        private bool _held, _blocked;
        public void Cancel() { Charge = 0; _held = false; _blocked = true; Attacking = false; }
        public LegendaryShot Step(double dt, bool held, bool enabled, LegendaryWeaponId weapon, int rank, double aim, double recoveryScale = 1)
        {
            if (!enabled || weapon == LegendaryWeaponId.None) { Cancel(); return default; }
            dt = Math.Max(0, Math.Min(.1, dt)); Recovery = Math.Max(0, Recovery - dt);
            if (!held) _blocked = false;
            Attacking = held && !_blocked;
            var shot = default(LegendaryShot);
            if (weapon == LegendaryWeaponId.SoundBlade)
            {
                var delta = Math.Atan2(Math.Sin(aim - Angle), Math.Cos(aim - Angle));
                Angle += Math.Max(-6 * dt, Math.Min(6 * dt, delta));
            }
            else
            {
                Angle = (Angle + 1.05 * dt) % (Math.PI * 2);
                if (Attacking && Recovery <= 0)
                {
                    Charge += dt;
                    if (Charge >= 3) { Overloads++; Charge = 0; _blocked = true; Attacking = false; Recovery = LegendaryRules.Stats(weapon, rank).Recovery * recoveryScale; }
                }
                else if (!held && _held && Charge > 0)
                {
                    if (Charge >= .12)
                    {
                        var stats = LegendaryRules.Stats(weapon, rank); var power = Math.Min(1, Charge / 2);
                        shot = new LegendaryShot { Fired = true, Angle = Angle, Damage = stats.MinimumDamage + (stats.Damage - stats.MinimumDamage) * power,
                            Width = stats.MinimumWidth + (stats.Width - stats.MinimumWidth) * power };
                        Recovery = stats.Recovery * recoveryScale;
                    }
                    Charge = 0;
                }
            }
            _held = held; return shot;
        }
    }
}
