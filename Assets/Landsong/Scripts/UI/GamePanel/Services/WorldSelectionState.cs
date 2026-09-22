namespace Landsong.ECS.Presentation
{
    public sealed class WorldSelectionState
    {
        public ulong SelectedEntityId { get; set; }

        public void Reset() => SelectedEntityId = 0;
    }
}
