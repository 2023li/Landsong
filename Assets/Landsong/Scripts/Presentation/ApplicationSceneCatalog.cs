namespace Landsong.ECS.Presentation
{
    // The build preprocessor validates these paths against the enabled scene GUIDs.
    public static class ApplicationSceneCatalog
    {
        public const string Root = "Assets/Landsong/Scenes/";
        public const string Boot = Root + "Boot.unity";
        public const string Menu = Root + "Start.unity";
        public const string Loading = Root + "LoadingTransition.unity";
        public const string Game = Root + "Game.unity";
        public static readonly string[] BuildScenes = { Boot, Menu, Loading, Game };
    }
}
