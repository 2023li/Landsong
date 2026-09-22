using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class BuildingEnvironment
    {
        public static float Value(EntityManager em, Entity root, Entity target, BuildingEnvironmentKind kind)
        {
            return SpatialOps.Quote(em, root, target, kind).Value;
        }
    }
}
