using System;
using Landsong.ECS;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    [Serializable] public sealed class TwcNavigationLayer
    {
        [LabelText("稳定地表 ID（正整数）")] public int SurfaceId = 1;
        [LabelText("TWC 蓝图层 GUID")] public string BlueprintGuid;
        [LabelText("整数高度等级")] public int Elevation;
    }
    [AddComponentMenu("Landsong/Map/地图导航地表与连接")]
    public sealed class MapNavigationAuthoring : MonoBehaviour
    {
        [HideInInspector] public LayerTerrainConnection[] LayerConnections = Array.Empty<LayerTerrainConnection>();
        [LabelText("额外地表（保留同 XZ 的下层地面）")] public TwcNavigationLayer[] Surfaces = Array.Empty<TwcNavigationLayer>();
        [LabelText("固定斜坡/桥梁连接（逻辑格坐标）")] public AuthoredConnection[] Connections = Array.Empty<AuthoredConnection>();
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var content = GetComponentInParent<MapContentAuthoring>();
            if (content == null || content.UnityGrid == null || content.MapDefinition == null) return;
            var layout = new GridLayoutService(content.UnityGrid);
            var origin = layout.GridToWorldPoint(0, 0);
            float cellSize = content.MapDefinition.CellSize, step = content.MapDefinition.ElevationWorldStep;
            foreach (var c in Connections ?? Array.Empty<AuthoredConnection>())
            {
                if (c.Size.x < 1 || c.Size.x > 32 || c.Size.y < 3 || c.Size.y > 128 || c.Rotation < 0 || c.Rotation > 3) continue;
                for (int x = 0; x < c.Size.x; x++)
                {
                    var a = TerrainConnectionOps.Port(c.Cell, c.Size, c.Rotation, x, 0);
                    var b = TerrainConnectionOps.Port(c.Cell, c.Size, c.Rotation, x, c.Size.y - 1);
                    var start = origin + new Vector3((a.x + .5f) * cellSize, c.EntryElevation * step + .03f, (a.y + .5f) * cellSize);
                    var end = origin + new Vector3((b.x + .5f) * cellSize, (c.EntryElevation + c.Rise) * step + .03f, (b.y + .5f) * cellSize);
                    Gizmos.color = Color.cyan; Gizmos.DrawWireCube(start, new Vector3(cellSize, .08f, cellSize));
                    Gizmos.color = Color.yellow; Gizmos.DrawWireCube(end, new Vector3(cellSize, .08f, cellSize));
                    Gizmos.color = Color.magenta; Gizmos.DrawLine(start, end);
                    if (x == 0) UnityEditor.Handles.Label(start, "连接 " + c.Id + (c.Bidirectional ? " 双向" : " 单向 → 出口"));
                }
            }
        }
#endif
    }
}
