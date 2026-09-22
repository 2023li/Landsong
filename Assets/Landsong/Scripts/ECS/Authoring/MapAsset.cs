using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [Serializable]
    public struct CellSource
    {
        [LabelText("边缘区")]
        public bool EdgeZone;
        [LabelText("存在地块")]
        public bool Exists;
        [LabelText("允许建造")]
        public bool Buildable;
        [LabelText("允许通行")]
        public bool Traversable;
        [Tooltip("环境射线阻挡，与可行走独立；启用后该格整列拦截飞行物。建筑可在定义 Combat 中单独配置。")]
        [LabelText("阻挡飞行物")]
        public bool BlocksProjectile;
        [LabelText("高度层级")]
        public int Elevation;
        [LabelText("表面层级")]
        public int Surface;
        [LabelText("地面高度")]
        public float Height;
        [LabelText("逻辑地形枚举值")]
        public ulong Terrain;
    }

    [Serializable]
    public struct InitialSource
    {
        [LabelText("建筑内容标识")]
        public Definitions.BuildingDefinitionAsset Definition;
        [LabelText("初始建筑名称")]
        public string Name;
        [LabelText("初始格坐标")]
        public Vector2Int Cell;
        [LabelText("初始等级")]
        public int Level;
        [LabelText("旋转方向")]
        public int Rotation;
    }

    [CreateAssetMenu(menuName = "Landsong/ECS/Map")]
    public sealed class MapAsset : ScriptableObject
    {
        [LabelText("地图标识")]
        public string MapId;
#if UNITY_EDITOR
        [Tooltip("TWC 制图源场景 GUID；仅供编辑器地形导入，不是游戏运行场景。")]
        [LabelText("制图源场景标识")]
        public string TwcSourceSceneGuid;
        [HideInInspector]
        public string EntitySceneGuid;
        [HideInInspector]
        public bool IsBaked;
        [HideInInspector]
        public int RuntimeArtifactGeneratorVersion;
        [HideInInspector]
        public string RuntimeArtifactSourceHash;
#endif
        [LabelText("网格最小坐标")]
        public Vector2Int Min;
        [Sirenix.OdinInspector.LabelText("网格尺寸")]
        public Vector2Int Size;
        [LabelText("世界原点")]
        public Vector3 Origin;
        [LabelText("格子尺寸")]
        public float CellSize = 1;
        [LabelText("高度单位")]
        public float ElevationStep = 1;
        [LabelText("额外导航地表")]
        public NavigationSurface[] NavigationSurfaces = Array.Empty<NavigationSurface>();
        [LabelText("固定地编连接")]
        public AuthoredConnection[] Connections = Array.Empty<AuthoredConnection>();
        [LabelText("地图地块")]
        public CellSource[] Cells = Array.Empty<CellSource>();
        [LabelText("初始建筑")]
        public InitialSource[] InitialBuildings = Array.Empty<InitialSource>();
    }
}
