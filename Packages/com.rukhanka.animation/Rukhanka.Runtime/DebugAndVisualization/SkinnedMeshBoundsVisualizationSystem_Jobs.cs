#if RUKHANKA_DEBUG_INFO

/////////////////////////////////////////////////////////////////////////////////

#if !RUKHANKA_NO_DEBUG_DRAWER
using Rukhanka.DebugDrawer;
using Rukhanka.Toolbox;
#endif
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public partial struct SkinnedMeshBoundsVisualizationSystem
{
    
partial struct RenderSkinnedMeshBoundsJob: IJobEntity
{
    public Drawer dd;
    public DebugConfigurationComponent dcc;
    
    [ReadOnly]
    public ComponentLookup<ShouldUpdateBoundingBoxTag> shouldUpdateBoundingBoxTagLookup;
    
    void Execute(Entity e, WorldRenderBounds wrb, SkinnedMeshRendererComponent smrc)
    {
        var transform = new RigidTransform(quaternion.identity, wrb.Value.Center);
        var color = shouldUpdateBoundingBoxTagLookup.HasComponent(e) ? dcc.dynamicMeshBoundsColor : dcc.staticMeshBoundsColor;
        dd.DrawWireCuboid(wrb.Value.Size, ColorTools.ToUint(color), transform);
    }
}

}
}

/////////////////////////////////////////////////////////////////////////////////

#endif