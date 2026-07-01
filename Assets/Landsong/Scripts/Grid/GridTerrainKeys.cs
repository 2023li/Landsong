namespace Landsong.GridSystem
{
    public static class GridTerrainKeys
    {
        public const string Land = "陆地";
        public const string Water = "水域";
        public const string Road = "道路";

        public static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        }
    }
}
