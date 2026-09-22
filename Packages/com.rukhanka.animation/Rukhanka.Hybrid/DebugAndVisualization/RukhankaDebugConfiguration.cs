using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
public class RukhankaDebugConfiguration: MonoBehaviour
{
	[Header("Bone Visualization")]
	public bool visualizeAllRigs;
	public Color boneColorCPURig = new
	(
		DebugConfigurationComponent.CPU_RIG_COLOR.x,
		DebugConfigurationComponent.CPU_RIG_COLOR.y,
		DebugConfigurationComponent.CPU_RIG_COLOR.z,
		DebugConfigurationComponent.CPU_RIG_COLOR.w
	);
	public Color boneColorGPURig = new
	(
		DebugConfigurationComponent.GPU_RIG_COLOR.x,
		DebugConfigurationComponent.GPU_RIG_COLOR.y,
		DebugConfigurationComponent.GPU_RIG_COLOR.z,
		DebugConfigurationComponent.GPU_RIG_COLOR.w
	);
	
	[Header("Skinned Mesh Bounds")]
	public bool visualizeSkinnedMeshBounds;
	public Color staticBoundsColor = new
	(
		DebugConfigurationComponent.STATIC_BOUNDS_COLOR.x,
		DebugConfigurationComponent.STATIC_BOUNDS_COLOR.y,
		DebugConfigurationComponent.STATIC_BOUNDS_COLOR.z,
		DebugConfigurationComponent.STATIC_BOUNDS_COLOR.w
	);
	public Color dynamicBoundsColor = new
	(
		DebugConfigurationComponent.DYNAMIC_BOUNDS_COLOR.x,
		DebugConfigurationComponent.DYNAMIC_BOUNDS_COLOR.y,
		DebugConfigurationComponent.DYNAMIC_BOUNDS_COLOR.z,
		DebugConfigurationComponent.DYNAMIC_BOUNDS_COLOR.w
	);
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

public class DebugConfigurationBaker: Baker<RukhankaDebugConfiguration>
{
	public override void Bake(RukhankaDebugConfiguration a)
	{
		var dcc = DebugConfigurationComponent.Default();
		
		dcc.visualizeAllRigs = a.visualizeAllRigs;
		dcc.cpuRigColor = new float4(a.boneColorCPURig.r, a.boneColorCPURig.g, a.boneColorCPURig.b, a.boneColorCPURig.a);
		dcc.gpuRigColor = new float4(a.boneColorGPURig.r, a.boneColorGPURig.g, a.boneColorGPURig.b, a.boneColorGPURig.a);
		dcc.visualizeMeshBounds = a.visualizeSkinnedMeshBounds;
		dcc.staticMeshBoundsColor = new float4(a.staticBoundsColor.r, a.staticBoundsColor.g, a.staticBoundsColor.b, a.staticBoundsColor.a);
		dcc.dynamicMeshBoundsColor = new float4(a.dynamicBoundsColor.r, a.dynamicBoundsColor.g, a.dynamicBoundsColor.b, a.dynamicBoundsColor.a);

		var e = GetEntity(TransformUsageFlags.None);
		AddComponent(e, dcc);
		
	#if (RUKHANKA_NO_DEBUG_DRAWER && RUKHANKA_DEBUG_INFO)
		if (a.visualizeAllRigs)
			Debug.LogWarning("All rigs visualization was requested, but DebugDrawer is compiled out via RUKHANKA_NO_DEBUG_DRAWER script symbol. No visualization is available.");
	#endif
	}
}
}

