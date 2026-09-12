using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class ExpeditionsContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("远征补给"), ShowIf(nameof(Enabled))] public ExpeditionSupplyConfiguration[] Supplies = Array.Empty<ExpeditionSupplyConfiguration>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ExpeditionSupplyConfiguration : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("最低数量")] public int MinimumQuantity;
        [LabelText("额外数量上限（0 = 最低量的一半）")] public int ExtraLimit;
        [LabelText("每份额外补给的成功率加成")] public float SuccessPerExtra;
        [LabelText("每份额外补给的奖励加成")] public float RewardPerExtra;
    }
}
