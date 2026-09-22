using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    /// <summary>Input ownership and the event system bound to this UI session.</summary>
    public sealed class GameUiInputContext
    {
        public GameUiInputPolicy Policy { get; internal set; }
        public EventSystem Events { get; internal set; }
        public UI_GamePanel_PausePop PauseMenu { get; internal set; }
        public bool TextFocused => UiInputState.TextFocused;
    }
}
