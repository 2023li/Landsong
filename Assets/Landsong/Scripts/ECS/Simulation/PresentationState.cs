using Unity.Entities;

namespace Landsong.ECS
{
    // Transient renderer ownership only; never serialized as gameplay or used by simulation rules.
    public struct ExternalVisual : IComponentData { public byte Active; }

    // Opt-in native animated view. Presentation only: these values are rebuilt, never saved.
    public struct AnimatedUnitVisual : IComponentData { }
    public struct UnitAnimationSignals : IComponentData
    {
        public uint AttackSequence, HitSequence;
    }
}
