using Sirenix.OdinInspector;
using System;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public enum VisitorKind : byte {
        [LabelText("小偷")] Thief,
        [LabelText("精灵")] Fairy
    }
    [Flags] public enum ItemProtection : byte {
        [LabelText("无")] None = 0,
        [LabelText("任务物品")] Quest = 1,
        [LabelText("唯一物品")] Unique = 2,
        [LabelText("绑定物品")] Bound = 4
    }
    [Serializable] public struct TheftProfile
    {
        [LabelText("防盗保护标记")] public ItemProtection Protection;
        [LabelText("被盗抽取权重")] public int Weight;
        [LabelText("单次被盗数量上限")] public int Maximum;
        [LabelText("单件盗窃预算")] public int UnitValue;
        public static TheftProfile Default => new TheftProfile { Weight = 100, Maximum = 5, UnitValue = 1 };
    }
    [Serializable] public struct OpportunityProfile
    {
        [LabelText("访客种类")] public VisitorKind Kind;
        [LabelText("出现权重")] public int Weight;
        [LabelText("每晚次数上限")] public int MaximumPerNight;
        [LabelText("最早出现时刻比例")] public float StartFraction;
        [LabelText("最晚出现时刻比例")] public float EndFraction;
        [LabelText("移动速度")] public float Speed;
        [LabelText("最短响应时间（秒）")] public float MinimumResponse;
        [LabelText("拦截半径")] public float CaptureRadius;
        [LabelText("响应半径")] public float ResponseRadius;
        [LabelText("路线长度")] public float RouteLength;
        [LabelText("允许士兵响应")] public bool Soldiers;
        [LabelText("允许英雄响应")] public bool Heroes;
        public static OpportunityProfile Default => new OpportunityProfile { Weight = 100, MaximumPerNight = 2, StartFraction = .15f, EndFraction = .5f, Speed = 1.2f, MinimumResponse = 4, CaptureRadius = 1.2f, ResponseRadius = 25, RouteLength = 8, Soldiers = true, Heroes = true };
    }
    [Serializable] public struct PeacefulRules
    {
        [LabelText("每夜访客数量上限")] public int MaximumPerNight;
        [LabelText("同时访客数量上限")] public int MaximumConcurrent;
        [LabelText("盗窃价值预算")] public int TheftValueBudget;
        [LabelText("首次访客时间（秒）")] public float FirstOpportunity;
        [LabelText("访客间隔（秒）")] public float Interval;
        public static PeacefulRules Default => new PeacefulRules { MaximumPerNight = 3, MaximumConcurrent = 2, TheftValueBudget = 15, FirstOpportunity = 2.5f, Interval = 2.5f };

    }
    // Rebuilt from the dusk seed. No mid-night save, and no separate global RNG is consumed.
    public struct PeacefulState : IComponentData { public uint Random; public int Spawned, StolenValue; public float Next; }
    [InternalBufferCapacity(0)] public struct OpportunityCount : IBufferElementData { public int Definition, Count; }
    public struct VisitorPathPoint : IBufferElementData { public Unity.Mathematics.float3 Position; }
}
