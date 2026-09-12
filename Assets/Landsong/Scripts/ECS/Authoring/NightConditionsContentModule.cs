using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class NightConditionsContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("回合条件"), ShowIf(nameof(Enabled))] public NightTurnsCondition[] Turns = Array.Empty<NightTurnsCondition>();
        [LabelText("运营建筑条件"), ShowIf(nameof(Enabled))] public NightBuildingsCondition[] Buildings = Array.Empty<NightBuildingsCondition>();
        [LabelText("物品条件"), ShowIf(nameof(Enabled))] public NightItemsCondition[] Items = Array.Empty<NightItemsCondition>();
        [LabelText("科技条件"), ShowIf(nameof(Enabled))] public NightTechnologiesCondition[] Technologies = Array.Empty<NightTechnologiesCondition>();
        [LabelText("完成前置"), ShowIf(nameof(Enabled))] public NightCompletionsCondition[] Completions = Array.Empty<NightCompletionsCondition>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class NightTurnsCondition : OrderedContentEntry
    {
        [LabelText("最低回合")] public int MinimumTurn = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class NightBuildingsCondition : OrderedContentEntry
    {
        [LabelText("建筑"), ContentReference(false, ContentKind.Building)] public GameDefinitionAsset Building;
        [LabelText("数量")] public int Count = 1;
        [LabelText("最低等级")] public int MinimumLevel;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class NightItemsCondition : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class NightTechnologiesCondition : OrderedContentEntry
    {
        [LabelText("科技"), ContentReference(false, ContentKind.Technology)] public GameDefinitionAsset Technology;
        [LabelText("完成数量")] public int Count = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class NightCompletionsCondition : OrderedContentEntry
    {
        [LabelText("已获许可内容"), ContentReference(false)] public GameDefinitionAsset Content;
        [LabelText("次数")] public int Count = 1;
    }
}
