namespace Landsong.ECS
{
    // Shared by Editor fixtures and the Development Player smoke. Never used by gameplay routing.
    public static class VerificationMap
    {
        public const string Id = "Map_Map01";
#if UNITY_EDITOR
        public const string SourceScene = "Assets/Landsong/GameMaps/" + Id + "/" + Id + ".unity";
        public const string EntityScene = "Assets/Landsong/GameMaps/" + Id + "/" + Id + "Data/Generated/" + Id + "_Entities.unity";
#endif
    }
}
