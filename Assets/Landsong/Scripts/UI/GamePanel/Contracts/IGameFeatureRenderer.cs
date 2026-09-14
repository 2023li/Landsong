namespace Landsong.ECS.Presentation
{
    /// <summary>A feature receives narrow session services and renders through its explicitly configured presenter.</summary>
    public interface IGameFeatureRenderer
    {
        void BindFeature(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_List panel);
        void Render();
    }
}
