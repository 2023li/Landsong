namespace Landsong.GridSystem
{
    public sealed class GridCell
    {
        public GridCell(GridPosition position, bool isBuildable = true)
        {
            Position = position;
            IsBuildable = isBuildable;
        }

        public GridPosition Position { get; }
        public bool IsBuildable { get; private set; }
        public string OccupantId { get; private set; }
        public bool IsOccupied => !string.IsNullOrEmpty(OccupantId);

        public void SetBuildable(bool isBuildable)
        {
            IsBuildable = isBuildable;
        }

        internal void Occupy(string occupantId)
        {
            OccupantId = occupantId;
        }

        internal void ClearOccupant()
        {
            OccupantId = null;
        }
    }
}
