namespace Landsong.GridSystem
{
    public static class GridTerrainKeys
    {
        public const string Land = "陆地";
        public const string Water = "水域";
        public const string Obstacle = "障碍";
        public const string Road = "道路";
        public const string AdvancedRoad = "高级道路";
        public const string StoneDeposit = "石矿";

        public static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        }
    }
}
