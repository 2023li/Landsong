namespace Landsong.ECS.Presentation
{
    /// <summary>Panel navigation only. Carries no canvas, modal, query, command or feature service access.</summary>
    public interface IGameUiNavigation
    {
        GamePanelId Panel { get; }

        bool IsPanelOpen { get; }

        void OpenPanel(GamePanelId panel);
        void ClosePanel();
        void BackPanel();
    }
}
