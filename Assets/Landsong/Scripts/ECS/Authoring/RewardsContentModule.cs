using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class RewardsContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("物品奖励"), ShowIf(nameof(Enabled))] public ItemsReward[] Items = Array.Empty<ItemsReward>();
        [LabelText("蓝图奖励"), ShowIf(nameof(Enabled))] public BlueprintsReward[] Blueprints = Array.Empty<BlueprintsReward>();
        [LabelText("增益奖励"), ShowIf(nameof(Enabled))] public BuffsReward[] Buffs = Array.Empty<BuffsReward>();
        [LabelText("功能许可"), ShowIf(nameof(Enabled))] public FeaturesReward[] Features = Array.Empty<FeaturesReward>();
        [LabelText("失败扣除物品"), ShowIf(nameof(Enabled))] public ItemPenalty[] Failures = Array.Empty<ItemPenalty>();
        [LabelText("可点击特殊掉落"), ShowIf(nameof(Enabled))] public SpecialDropReward[] SpecialDrops = Array.Empty<SpecialDropReward>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ItemsReward : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class BlueprintsReward : OrderedContentEntry
    {
        [LabelText("建筑"), ContentReference(false, ContentKind.Building)] public GameDefinitionAsset Building;
        [LabelText("许可等级")] public int GrantedLevel = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class BuffsReward : OrderedContentEntry
    {
        [LabelText("增益"), ContentReference(false, ContentKind.Buff)] public GameDefinitionAsset Buff;
        [LabelText("许可等级")] public int GrantedLevel = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class FeaturesReward : OrderedContentEntry
    {
        [LabelText("功能"), ContentReference(false, ContentKind.Feature)] public GameDefinitionAsset Feature;
        [LabelText("许可等级")] public int GrantedLevel = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ItemPenalty : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("扣除数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SpecialDropReward : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
        [LabelText("稀有度")] public DropRarity Rarity;
    }
}
