namespace Landsong
{
    /// <summary>
    /// 回合推进期间的科技累计与确认状态。与任务运行时状态分离。
    /// </summary>
    public sealed partial class GameSystem
    {
        private int pendingTechnologyPointsThisTurn;
        private int turnWithMissingResearchWarning = -1;
    }
}
