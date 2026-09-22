#if RUKHANKA_DEBUG_INFO

#if !RUKHANKA_NO_DEBUG_DRAWER
using Rukhanka.DebugDrawer;
#endif

using Unity.Entities;
using Unity.Rendering;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

[WorldSystemFilter(WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(RukhankaDeformationSystemGroup))]
public partial struct SkinnedMeshBoundsVisualizationSystem: ISystem
{
	EntityQuery skinnedMeshQuery;
	
/////////////////////////////////////////////////////////////////////////////////

	public void OnUpdate(ref SystemState ss)
	{
        if (!SystemAPI.TryGetSingletonRW<Drawer>(out var dRef))
            return;
        
        if (!SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var debugConfig))
            return;
        
        if (!debugConfig.visualizeMeshBounds)
	        return;
        
		var shouldUpdateBoundingBoxTagLookup = SystemAPI.GetComponentLookup<ShouldUpdateBoundingBoxTag>(true);
		var renderSkinnedMeshBoundsJob = new RenderSkinnedMeshBoundsJob()
		{
			shouldUpdateBoundingBoxTagLookup = shouldUpdateBoundingBoxTagLookup,
			dcc = debugConfig,
			dd = dRef.ValueRW
		};
		ss.Dependency = renderSkinnedMeshBoundsJob.ScheduleParallel(ss.Dependency);
	}
	
}
}

#endif