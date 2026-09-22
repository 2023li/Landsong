
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Serialization;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
	public struct DebugConfigurationComponent: IComponentData
	{
		public static readonly float4 CPU_RIG_COLOR = new (0, 1, 0, 1);
		public static readonly float4 GPU_RIG_COLOR = new (0, 1, 1, 1);
		public static readonly float4 STATIC_BOUNDS_COLOR = new (1, 1, 1, 1);
		public static readonly float4 DYNAMIC_BOUNDS_COLOR = new (0.0f, 0.7490196f, 1f, 1f);
	
		public bool visualizeAllRigs;
		public float4 cpuRigColor;
		public float4 gpuRigColor;
		
		public bool visualizeMeshBounds;
		public float4 staticMeshBoundsColor;
		public float4 dynamicMeshBoundsColor;

/////////////////////////////////////////////////////////////////////////////////

		public static DebugConfigurationComponent Default()
		{
			var rv = new DebugConfigurationComponent()
			{
				cpuRigColor = CPU_RIG_COLOR,
				gpuRigColor = GPU_RIG_COLOR,
				staticMeshBoundsColor = STATIC_BOUNDS_COLOR,
				dynamicMeshBoundsColor = DYNAMIC_BOUNDS_COLOR,
			};
			return rv;
		}
	}
}

