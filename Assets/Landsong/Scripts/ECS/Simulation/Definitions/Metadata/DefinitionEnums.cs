using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public enum SpecialDropRarity : byte
    {
        [UnityEngine.InspectorName("普通")]
        Common = 1,
        [UnityEngine.InspectorName("稀有")]
        Rare = 2,
        [UnityEngine.InspectorName("史诗")]
        Epic = 3
    }

    [Flags]
    public enum QuestBehaviorFlags : byte
    {
        [UnityEngine.InspectorName("无")]
        None = 0,
        [UnityEngine.InspectorName("主线")]
        Mainline = 1,
        [UnityEngine.InspectorName("草稿")]
        Draft = 2
    }

    public enum QuestOfferType : byte
    {
        [UnityEngine.InspectorName("贸易")]
        Trade = 0,
        [UnityEngine.InspectorName("建设")]
        Construction = 1,
        [UnityEngine.InspectorName("民生")]
        Livelihood = 2,
        [UnityEngine.InspectorName("探索")]
        Exploration = 3
    }

    [Flags]
    public enum EnemyBehaviorFlags : byte
    {
        [UnityEngine.InspectorName("无")]
        None = 0,
        [UnityEngine.InspectorName("首领")]
        Boss = 1,
        [UnityEngine.InspectorName("目标模式第一位")]
        TargetModeBit0 = 2,
        [UnityEngine.InspectorName("目标模式第二位")]
        TargetModeBit1 = 4
    }

    public enum NumericEffectKind : byte
    {
        [UnityEngine.InspectorName("损耗倍率")]
        LossMultiplier,
        [UnityEngine.InspectorName("产出倍率")]
        ProductionMultiplier,
        [UnityEngine.InspectorName("攻击倍率")]
        AttackMultiplier,
        [UnityEngine.InspectorName("生命倍率")]
        HealthMultiplier,
        [UnityEngine.InspectorName("士兵攻击倍率")]
        SoldierAttackMultiplier,
        [UnityEngine.InspectorName("士兵移动倍率")]
        SoldierSpeedMultiplier,
        [UnityEngine.InspectorName("行动力")]
        ActionPower,
        [UnityEngine.InspectorName("作物收获倍率")]
        CropHarvestMultiplier,
        [UnityEngine.InspectorName("自然死亡风险")]
        NaturalDeathRisk,
        [UnityEngine.InspectorName("攻击范围倍率")]
        AttackRangeMultiplier,
        [UnityEngine.InspectorName("攻击速度倍率")]
        AttackSpeedMultiplier,
        [UnityEngine.InspectorName("移动速度倍率")]
        MovementSpeedMultiplier,
        [UnityEngine.InspectorName("护甲")]
        Armor,
        [UnityEngine.InspectorName("伤害减免")]
        DamageReduction,
        [UnityEngine.InspectorName("穿透")]
        Penetration,
        [UnityEngine.InspectorName("弹体速度倍率")]
        ProjectileSpeedMultiplier,
        [UnityEngine.InspectorName("爆炸半径")]
        BlastRadius
    }

    public enum KingdomEffectKind : byte
    {
        [UnityEngine.InspectorName("民心")]
        PublicOpinion,
        [UnityEngine.InspectorName("阴谋风险")]
        PlotRisk,
        [UnityEngine.InspectorName("研究产出")]
        ResearchOutput
    }

    public enum TalentScalingKind : byte
    {
        [UnityEngine.InspectorName("固定数值")]
        Fixed = 0,
        [UnityEngine.InspectorName("每百单位物品")]
        PerHundredItems = 20,
        [UnityEngine.InspectorName("人才等级")]
        TalentLevel = 30,
        [UnityEngine.InspectorName("王国人口")]
        KingdomPopulation = 40,
        [UnityEngine.InspectorName("运转建筑数量")]
        OperatingBuildings = 50
    }

    public enum BuildingEnvironmentKind
    {
        [UnityEngine.InspectorName("生产")]
        Production = 10,
        [UnityEngine.InspectorName("美观")]
        Beauty = 20,
        [UnityEngine.InspectorName("医疗")]
        Medical = 30,
        [UnityEngine.InspectorName("治安")]
        Security = 40,
        [UnityEngine.InspectorName("生产")]
        生产 = 10,
        [UnityEngine.InspectorName("美观")]
        美观 = 20,
        [UnityEngine.InspectorName("医疗")]
        医疗 = 30,
        [UnityEngine.InspectorName("治安")]
        治安 = 40
    }

    public enum BuildingInvitationKind
    {
        [UnityEngine.InspectorName("贸易")]
        Trade = 0,
        [UnityEngine.InspectorName("建设")]
        Construction = 1,
        [UnityEngine.InspectorName("民生")]
        Livelihood = 2,
        [UnityEngine.InspectorName("探索")]
        Exploration = 3,
        [UnityEngine.InspectorName("贸易")]
        贸易 = 0,
        [UnityEngine.InspectorName("建设")]
        建设 = 1,
        [UnityEngine.InspectorName("民生")]
        民生 = 2,
        [UnityEngine.InspectorName("探索")]
        探索 = 3
    }

    public enum BuildingEffectStacking
    {
        [UnityEngine.InspectorName("同组最高")]
        HighestInGroup = 0,
        [UnityEngine.InspectorName("相加")]
        Additive = 10,
        [UnityEngine.InspectorName("同类最高")]
        HighestOfKind = 20,
        [UnityEngine.InspectorName("同组最高")]
        同组最高 = 0,
        [UnityEngine.InspectorName("相加")]
        相加 = 10,
        [UnityEngine.InspectorName("同类最高")]
        同类最高 = 20
    }
}
