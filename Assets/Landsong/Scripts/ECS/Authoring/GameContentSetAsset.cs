using System;
using Landsong.ECS.Authoring.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [CreateAssetMenu(menuName = "Landsong/ECS/Game Content Set")]
    public sealed class GameContentSetAsset : ScriptableObject
    {
        [Title("全局游戏流程")]
        [LabelText("昼夜流程")] public NightSettings Night;
        [LabelText("白天归营")] public DayReturnSettings DayReturn;

        [Title("领域目录")]
        [LabelText("物品目录"), Required] public ItemCatalogAsset Items;
        [LabelText("物品组目录"), Required] public ItemGroupCatalogAsset ItemGroups;
        [LabelText("库存槽目录"), Required] public StorageSlotCatalogAsset StorageSlots;
        [LabelText("建筑限制组目录"), Required] public BuildingLimitGroupCatalogAsset BuildingLimitGroups;
        [LabelText("政策组目录"), Required] public PolicyGroupCatalogAsset PolicyGroups;
        [LabelText("科技目录"), Required] public TechnologyCatalogAsset Technologies;
        [LabelText("任务目录"), Required] public QuestCatalogAsset Quests;
        [LabelText("远征目录"), Required] public ExpeditionCatalogAsset Expeditions;
        [LabelText("士兵目录"), Required] public SoldierCatalogAsset Soldiers;
        [LabelText("英雄目录"), Required] public HeroCatalogAsset Heroes;
        [LabelText("敌军目录"), Required] public EnemyCatalogAsset Enemies;
        [LabelText("增益目录"), Required] public BuffCatalogAsset Buffs;
        [LabelText("政策目录"), Required] public PolicyCatalogAsset Policies;
        [LabelText("人才目录"), Required] public TalentCatalogAsset Talents;
        [LabelText("人才槽目录"), Required] public TalentSlotCatalogAsset TalentSlots;
        [LabelText("王室特质目录"), Required] public RoyalTraitCatalogAsset RoyalTraits;
        [LabelText("功能许可目录"), Required] public FeatureCatalogAsset Features;
        [LabelText("作物目录"), Required] public CropCatalogAsset Crops;
        [LabelText("飞行物目录"), Required] public ProjectileCatalogAsset Projectiles;
        [LabelText("访客目录"), Required] public OpportunityCatalogAsset Opportunities;
        [LabelText("掉落目录"), Required] public LootCatalogAsset Loot;
        [LabelText("建筑目录"), Required] public BuildingCatalogAsset Buildings;
        [LabelText("夜晚事件目录"), Required] public NightEventCatalogAsset NightEvents;

        public T Get<T>() where T : ScriptableObject
        {
            ScriptableObject result = null;
            if (typeof(T) == typeof(ItemCatalogAsset)) result = Items;
            else if (typeof(T) == typeof(ItemGroupCatalogAsset)) result = ItemGroups;
            else if (typeof(T) == typeof(StorageSlotCatalogAsset)) result = StorageSlots;
            else if (typeof(T) == typeof(BuildingLimitGroupCatalogAsset)) result = BuildingLimitGroups;
            else if (typeof(T) == typeof(PolicyGroupCatalogAsset)) result = PolicyGroups;
            else if (typeof(T) == typeof(TechnologyCatalogAsset)) result = Technologies;
            else if (typeof(T) == typeof(QuestCatalogAsset)) result = Quests;
            else if (typeof(T) == typeof(ExpeditionCatalogAsset)) result = Expeditions;
            else if (typeof(T) == typeof(SoldierCatalogAsset)) result = Soldiers;
            else if (typeof(T) == typeof(HeroCatalogAsset)) result = Heroes;
            else if (typeof(T) == typeof(EnemyCatalogAsset)) result = Enemies;
            else if (typeof(T) == typeof(BuffCatalogAsset)) result = Buffs;
            else if (typeof(T) == typeof(PolicyCatalogAsset)) result = Policies;
            else if (typeof(T) == typeof(TalentCatalogAsset)) result = Talents;
            else if (typeof(T) == typeof(TalentSlotCatalogAsset)) result = TalentSlots;
            else if (typeof(T) == typeof(RoyalTraitCatalogAsset)) result = RoyalTraits;
            else if (typeof(T) == typeof(FeatureCatalogAsset)) result = Features;
            else if (typeof(T) == typeof(CropCatalogAsset)) result = Crops;
            else if (typeof(T) == typeof(ProjectileCatalogAsset)) result = Projectiles;
            else if (typeof(T) == typeof(OpportunityCatalogAsset)) result = Opportunities;
            else if (typeof(T) == typeof(LootCatalogAsset)) result = Loot;
            else if (typeof(T) == typeof(BuildingCatalogAsset)) result = Buildings;
            else if (typeof(T) == typeof(NightEventCatalogAsset)) result = NightEvents;
            if (result == null)
                throw new InvalidOperationException(name + " 缺少内容目录：" + typeof(T).Name);
            return (T)result;
        }
    }
}
