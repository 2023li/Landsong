using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Landsong.ECS;

namespace Landsong.GridSystem
{
    public enum BlueprintLogicKind
    {
        [LabelText("地形")] Terrain,
        [LabelText("纯修饰")] Decoration,
        [LabelText("连接视觉")] Connection,
        [LabelText("突出式斜坡（仅道路可建）")] ProtrudingSlope
    }

    [Serializable]
    public sealed class BlueprintTerrainRule
    {
        [LabelText("共用规则名称"), Required,Tooltip("填写陆地、石地、斜坡等名称，不带 L0_ 等前缀。L3_石地会匹配石地规则；高度仍由所属 Layer 决定。")]
        public string BlueprintLayerName;
        [LabelText("逻辑用途")] public BlueprintLogicKind Kind;
        [ShowIf("@Kind == BlueprintLogicKind.Terrain"),LabelText("逻辑地形（单选）"),ValueDropdown(nameof(TerrainChoices))] public TerrainType Terrain = TerrainType.陆地;
        public static System.Collections.Generic.IEnumerable<TerrainType> TerrainChoices
        {
            get { foreach(TerrainType value in Enum.GetValues(typeof(TerrainType)))if(TerrainTypes.Single(value))yield return value; }
        }
        [LabelText("允许建造")] public bool Buildable = true;
        [LabelText("允许通行")] public bool Traversable = true;
    }

    // Endpoints reference Blueprint folders, never prefab transforms or authored heights.
    [Serializable]
    public sealed class LayerTerrainConnection
    {
        [LabelText("稳定连接 ID")] public int Id = 1;
        [HideInInspector] public string EntryLayerGuid;
        [HideInInspector] public string ExitLayerGuid;
        [LabelText("入口格")] public Vector2Int EntryCell;
        [LabelText("出口格")] public Vector2Int ExitCell;
        [LabelText("通道宽度"), MinValue(1)] public int Width = 3;
        [LabelText("双向通行")] public bool Bidirectional = true;
    }
}
