namespace Landsong.ECS.Presentation
{
    public sealed class IntelligenceViewState
    {
        public bool IsOpen { get; set; }

        public void Reset() => IsOpen = false;
    }
}
