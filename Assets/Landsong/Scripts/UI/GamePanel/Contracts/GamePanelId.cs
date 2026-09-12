using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    // Serialized values are stable; presentation labels never identify navigation destinations.
    public enum GamePanelId
    {
        [LabelText("未配置")] None = 0,
        [LabelText("建筑")] Building = 1,
        [LabelText("科技")] Technology = 2,
        [LabelText("任务")] Quest = 3,
        [LabelText("经济")] Economy = 4,
        [LabelText("库存")] Inventory = 5,
        [LabelText("驻军")] Garrison = 6,
        [LabelText("王室")] Royal = 7,
        [LabelText("人才")] Talent = 8,
        [LabelText("政策")] Policy = 9,
        [LabelText("远征")] Expedition = 10,
        [LabelText("历史")] History = 11,
        [LabelText("情报")] Intelligence = 12,
        [LabelText("战报")] BattleReport = 13,
        [LabelText("入夜确认")] NightConfirmation = 14,
        [LabelText("王朝终局")] DynastyEnd = 15,
        [LabelText("暂停与存档")] Pause = 16
    }
}
