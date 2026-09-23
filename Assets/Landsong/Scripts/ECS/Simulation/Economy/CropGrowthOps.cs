using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class CropGrowthOps
    {
        // Authoritative crop progress uses tenths so spring's 1.2 and winter's 0.5 are exact.
        public const int Scale = 10;
        public static int Threshold(int growthTurns) => math.max(1, growthTurns) * Scale;
        public static int PerSettlement(SeasonKind season) => season == SeasonKind.Spring ? 12 : season == SeasonKind.Winter ? 5 : 10;
        public static float Value(int progress) => math.max(0, progress) / (float)Scale;
        public static int RemainingSettlements(int progress, int growthTurns, SeasonKind season)
            => math.max(0, (Threshold(growthTurns) - math.max(0, progress) + PerSettlement(season) - 1) / PerSettlement(season));
    }
}
