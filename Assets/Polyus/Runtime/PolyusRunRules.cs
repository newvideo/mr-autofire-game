using System;

namespace Polyus
{
    // Deterministic rules shared by runtime and executable tests (no Unity dependency).
    public static class PolyusRunRules
    {
        public const int RoomCount = 3;
        public static int EnemyCount(int room) => room == 0 ? 4 : room == 1 ? 5 : room == 2 ? 1 : 0;
        public static int ArmorBonus(int rarity) => rarity == 0 ? 20 : rarity == 1 ? 40 : 70;
        public static int WeaponBonus(int rarity) => rarity == 0 ? 3 : rarity == 1 ? 6 : 10;
        public static int ReviveHealth(int maximum) => Math.Max(1, maximum / 2);
        public static bool CanRevive(bool defeated, bool used, bool declined) => defeated && !used && !declined;
        public static double SegmentDistance(double ax, double ay, double bx, double by, double px, double py)
        {
            double dx = bx - ax, dy = by - ay;
            double denominator = dx * dx + dy * dy;
            double t = denominator == 0 ? 0 : Math.Max(0, Math.Min(1, ((px - ax) * dx + (py - ay) * dy) / denominator));
            double x = ax + dx * t - px, y = ay + dy * t - py;
            return Math.Sqrt(x * x + y * y);
        }
    }
}
