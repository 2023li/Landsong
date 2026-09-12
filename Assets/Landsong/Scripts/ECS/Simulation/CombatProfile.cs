using Sirenix.OdinInspector;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum ProjectileMode : byte {
        [LabelText("追踪弹道")] Tracking,
        [LabelText("地面落点")] Ground
    }
    [System.Flags] public enum TacticalTraits : byte {
        [LabelText("无")] None = 0,
        [LabelText("仅攻击建筑")] SiegeOnly = 1,
        [LabelText("忽略单位避让")] IgnoreSeparation = 2
    }
    [System.Serializable] public struct CombatProfile
    {
        [LabelText("索敌半径")] public float DetectionRadius;
        [LabelText("追击半径")] public float ChaseRadius;
        [LabelText("最长追击时间（秒）")] public float ChaseSeconds;
        [LabelText("碰撞半径")] public float BodyRadius;
        [LabelText("护甲")] public float Armor;
        [LabelText("伤害减免比例")] public float Reduction;
        [LabelText("穿甲")] public float Penetration;
        [LabelText("爆炸半径")] public float BlastRadius;
        [LabelText("落点预警时间（秒）")] public float WarningSeconds;
        [LabelText("弹体存在时间（秒）")] public float ProjectileLifetime;
        [LabelText("弹道类型")] public ProjectileMode ProjectileMode;
        [LabelText("战术特性")] public TacticalTraits Traits;
        [LabelText("阻挡弹体")] public bool BlocksProjectile;
        public static CombatProfile Default => new CombatProfile { DetectionRadius = 24, ChaseRadius = 12, ChaseSeconds = 8, BodyRadius = .35f, ProjectileLifetime = 10 };
        public static bool Valid(CombatProfile p) => math.all(math.isfinite(new float4(p.DetectionRadius, p.ChaseRadius, p.ChaseSeconds, p.BodyRadius))) && math.all(math.isfinite(new float4(p.Armor, p.Reduction, p.Penetration, p.BlastRadius))) && math.all(math.isfinite(new float2(p.WarningSeconds, p.ProjectileLifetime))) && p.DetectionRadius > 0 && p.DetectionRadius <= 64 && p.ChaseRadius > 0 && p.ChaseRadius <= 128 && p.ChaseSeconds > 0 && p.ChaseSeconds <= 120 && p.BodyRadius > 0 && p.BodyRadius <= 4 && p.Armor >= 0 && p.Reduction >= 0 && p.Reduction <= .95f && p.Penetration >= 0 && p.BlastRadius >= 0 && p.BlastRadius <= 32 && p.WarningSeconds >= 0 && p.WarningSeconds <= 30 && p.ProjectileLifetime > p.WarningSeconds && p.ProjectileLifetime <= 120 && p.ProjectileMode <= ProjectileMode.Ground && ((byte)p.Traits & ~3) == 0;
    }
    // Rebuilt at deployment / node restore. DBP reads decisions; it does not own reservations.
    public struct TacticalState : IComponentData
    {
        public Entity Pursued;
        public float3 Origin, Engagement;
        public float Started;
        public byte Returning, HasSlot;
    }
}
