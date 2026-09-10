using System;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [Serializable] public struct CellSource
    {
        public bool Exists, Buildable, Traversable;
        [Tooltip("环境射线阻挡，与可行走独立；启用后该格整列拦截飞行物。建筑可在定义 Combat 中单独配置。")] public bool BlocksProjectile;
        public int Elevation, Surface;
        public float Height;
        public ulong Terrain;
    }
    [Serializable] public struct InitialSource
    {
        public string Definition, Name;
        public Vector2Int Cell;
        public int Level, Rotation;
    }
    [Serializable] public struct RegionSource { public int Direction; public Vector3 Center, Size; }
    [CreateAssetMenu(menuName = "Landsong/ECS/Map")]
    public sealed class MapAsset : ScriptableObject
    {
        public string MapId;
#if UNITY_EDITOR
        [Tooltip("TWC 制图源场景 GUID；仅供编辑器地形导入，不是游戏运行场景。")]
        public string TwcSourceSceneGuid;
#endif
        public Vector2Int Min, Size;
        public Vector3 Origin;
        public float CellSize = 1;
        public CellSource[] Cells = Array.Empty<CellSource>();
        public InitialSource[] InitialBuildings = Array.Empty<InitialSource>();
        public RegionSource[] SpawnRegions = Array.Empty<RegionSource>();
    }
}
