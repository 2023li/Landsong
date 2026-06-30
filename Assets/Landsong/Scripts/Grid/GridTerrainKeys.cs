namespace Landsong.GridSystem
{
    public static class GridTerrainKeys
    {
        public const string Land = "land";
        public const string Water = "water";

        public static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        }
    }
}
