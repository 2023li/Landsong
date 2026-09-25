#if UNITY_EDITOR
namespace Landsong.EditorTools
{
    public static class ContentAssetPaths
    {
        public const string Root = "Assets/Landsong/ECSContent";
        public const string CatalogSources = Root + "/Catalogs/Source";
        public const string DisplayCatalogs = Root + "/Catalogs/Generated/Display";
        public const string Units = Root + "/Units";
        public const string Buildings = Root + "/Buildings";
        public const string SharedShaders = "Assets/Landsong/Art/Shared/Shaders";
        public const string AnimatedUnitShader = SharedShaders + "/SoldierAnimatedLit.shadergraph";
        public const string UiPrefabs = "Assets/Landsong/UI/Prefabs";
        public const string Localization = "Assets/Landsong/Localization";
        public const string Presentation = Root + "/Presentation";
        public const string Audio = Presentation + "/Audio";
        public const string Effects = Presentation + "/Effects";
        public const string Portraits = Presentation + "/Portraits";
        public const string GeneratedLocalization = Presentation + "/Localization/Generated";
        public const string World = Root + "/World";

        public static string SourceCatalog(string domain) => CatalogSources + "/" + domain + "Catalog.asset";
        public static string DisplayCatalog(string domain) => DisplayCatalogs + "/" + domain + "DisplayCatalog.asset";
        public static string UnitPackage(string domain, string stableId) => Units + "/" + domain + "/" + stableId;
        public static string BuildingPackage(string stableId) => Buildings + "/" + stableId;
    }
}
#endif
