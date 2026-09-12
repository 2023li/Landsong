using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    // Order is explicit because reward/RNG/payment order is gameplay data, even across modules.
    [Serializable, HideReferenceObjectPicker] public abstract class OrderedContentEntry
    {
        [LabelText("执行顺序（小值优先，同值按模块列表顺序）"), MinValue(0)] public int Order;
    }
    [Serializable, HideReferenceObjectPicker] public abstract class ContentObjective : OrderedContentEntry
    {
        [LabelText("目标进度标识")] public string Key;
    }
    public enum DropRarity {
        [LabelText("普通")] 普通=1,
        [LabelText("稀有")] 稀有=2,
        [LabelText("史诗")] 史诗=3
    }
    public enum TalentEffectType {
        [LabelText("物品")] 物品=10,
        [LabelText("科研点")] 科研点=20,
        [LabelText("内容许可")] 内容许可=30,
        [LabelText("生产百分比")] 生产百分比=100,
        [LabelText("攻击百分比")] 攻击百分比=140,
        [LabelText("民意")] 民意=150
    }
    public enum TalentEffectTiming {
        [LabelText("被动")] 被动=0,
        [LabelText("每回合")] 每回合=10
    }
    public enum TalentEffectScaling {
        [LabelText("固定")] 固定=0,
        [LabelText("每百份物品")] 每百份物品=20,
        [LabelText("人才等级")] 人才等级=30,
        [LabelText("王国人口")] 王国人口=40,
        [LabelText("运营建筑数")] 运营建筑数=50
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ContentModules
    {
        [LabelText("解锁与显示条件")] public ConditionsContentModule Conditions = new ConditionsContentModule();
        [LabelText("任务目标")] public ObjectivesContentModule Objectives = new ObjectivesContentModule();
        [LabelText("奖励与失败惩罚")] public RewardsContentModule Rewards = new RewardsContentModule();
        [LabelText("招募、唤醒与供奉")] public UnitCostsContentModule UnitCosts = new UnitCostsContentModule();
        [LabelText("作物种植与收获")] public CropsContentModule Crops = new CropsContentModule();
        [LabelText("物品分组与库存规则")] public InventoryContentModule Inventory = new InventoryContentModule();
        [LabelText("远征补给")] public ExpeditionsContentModule Expeditions = new ExpeditionsContentModule();
        [LabelText("属性与经济效果")] public ModifiersContentModule Modifiers = new ModifiersContentModule();
        [LabelText("人物与任职")] public PeopleContentModule People = new PeopleContentModule();
    }
}
