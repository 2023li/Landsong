namespace Landsong.ECS.Presentation
{
    public static class GameUiText
    {
        internal static string PhaseName(Phase p) => p == Phase.Day ? "白天建造" : p == Phase.Night ? "夜晚" : p == Phase.Deployment ? "入夜准备阶段" : p == Phase.Retreat ? "夜晚收尾阶段" : p == Phase.Celebration ? "战斗胜利" : p == Phase.Returning ? "士兵归营" : p == Phase.Report ? "今晚战报" : p == Phase.GameOver || p == Phase.Ended ? "王朝终局" : "结算";
        internal static string ResultName(ResultCode r) => r == ResultCode.PreparationFailed ? "准备失败，当前进度已保留；请检查 Console 后重试" : r == ResultCode.ConfirmationRequired ? "请确认结算后的待清空内容" : r == ResultCode.WrongPhase ? "当前阶段不能执行此操作" : r == ResultCode.InsufficientResources ? "资源不足" : r == ResultCode.InsufficientPopulation ? "空闲人口或工人不足" : r == ResultCode.NoCapacity ? "容量不足" : r == ResultCode.InvalidPlacement ? "占地、地形或通行条件不符" : r == ResultCode.MissingResearch ? "需要前置科技" : r == ResultCode.QuestOverflow ? "任务超出可承接数量" : "当前条件不满足（" + r + "）";
        public static string Direction(int value) => IntelOps.Direction(value);
    }
}
