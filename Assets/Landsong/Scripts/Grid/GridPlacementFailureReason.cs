namespace Landsong.GridSystem
{
    public enum GridPlacementFailureReason
    {
        None = 0,
        InvalidSize = 1,
        InvalidOccupantId = 2,
        OutOfBounds = 3,
        NotBuildable = 4,
        Occupied = 5,
        TerrainMismatch = 6,
        TransactionFailed = 7
    }
}
