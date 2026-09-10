using System;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public enum VisitorKind : byte { Thief, Fairy }
    [Flags] public enum ItemProtection : byte { None = 0, Quest = 1, Unique = 2, Bound = 4 }
    [Serializable] public struct TheftProfile
    {
        public ItemProtection Protection;
        public int Weight, Maximum, UnitValue;
        public static TheftProfile Default => new TheftProfile { Weight = 100, Maximum = 5, UnitValue = 1 };
    }
    [Serializable] public struct OpportunityProfile
    {
        public VisitorKind Kind;
        public int Weight, MaximumPerNight;
        public float StartFraction, EndFraction, Speed, MinimumResponse, CaptureRadius, ResponseRadius, RouteLength;
        public bool Soldiers, Heroes;
        public static OpportunityProfile Default => new OpportunityProfile { Weight = 100, MaximumPerNight = 2, StartFraction = .15f, EndFraction = .5f, Speed = 1.2f, MinimumResponse = 4, CaptureRadius = 1.2f, ResponseRadius = 25, RouteLength = 8, Soldiers = true, Heroes = true };
    }
    [Serializable] public struct PeacefulRules
    {
        public int MaximumPerNight, MaximumConcurrent, TheftValueBudget;
        public float FirstOpportunity, Interval;
        public static PeacefulRules Default => new PeacefulRules { MaximumPerNight = 3, MaximumConcurrent = 2, TheftValueBudget = 15, FirstOpportunity = 2.5f, Interval = 2.5f };
    }
    // Rebuilt from the dusk seed. No mid-night save, and no separate global RNG is consumed.
    public struct PeacefulState : IComponentData { public uint Random; public int Spawned, StolenValue; public float Next; }
    [InternalBufferCapacity(0)] public struct OpportunityCount : IBufferElementData { public int Definition, Count; }
    public struct VisitorPathPoint : IBufferElementData { public Unity.Mathematics.float3 Position; }
}
