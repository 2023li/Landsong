using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum ProjectileMode : byte { Tracking, Ground }
    [System.Flags] public enum TacticalTraits : byte { None = 0, SiegeOnly = 1, IgnoreSeparation = 2 }
    [System.Serializable] public struct CombatProfile
    {
        public float DetectionRadius, ChaseRadius, ChaseSeconds, BodyRadius;
        public float Armor, Reduction, Penetration, BlastRadius, WarningSeconds, ProjectileLifetime;
        public ProjectileMode ProjectileMode;
        public TacticalTraits Traits;
        public bool BlocksProjectile;
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
