using Unity.Entities;

namespace Landsong.ECS
{
    // Transient renderer ownership only; never serialized as gameplay or used by simulation rules.
    public struct ExternalVisual : IComponentData { public byte Active; }
}
