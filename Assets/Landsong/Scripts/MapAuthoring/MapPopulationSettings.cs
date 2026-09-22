using System;
using System.Collections.Generic;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    public enum MapGenerationKind
    {
        [LabelText("手工放置")]
        Manual,
        [LabelText("森林生成")]
        Forest,
        [LabelText("建筑生成")]
        Buildings
    }

    [Serializable]
    public sealed class MapTreeChoice
    {
        [LabelText("树木定义"), Required]
        public BuildingDefinitionAsset Definition;
        [LabelText("抽取权重"), MinValue(1)]
        public int Weight = 1;
    }

    [Serializable]
    public sealed class MapBuildingChoice
    {
        [LabelText("建筑定义"), Required]
        public BuildingDefinitionAsset Definition;
        [LabelText("数量"), MinValue(0)]
        public int Count = 20;
        [LabelText("初始等级"), MinValue(1)]
        public int Level = 1;
    }

    [Serializable]
    public sealed class MapPopulationSettings
    {
        [LabelText("生成种子"), Tooltip("与游戏运行种子独立。地形、配置、手工建筑和其他类型生成内容相同时，结果可复现。")]
        public int Seed = 12345;
        [LabelText("限定 Layer"), Tooltip("-1 表示所有 Layer；其他值按逻辑高度筛选。"), MinValue(-1)]
        public int Layer = -1;
        [LabelText("随机朝向")]
        public bool RandomRotation = true;
        [FoldoutGroup("树林"), LabelText("树种列表")]
        public List<MapTreeChoice> Trees = new List<MapTreeChoice>();
        [FoldoutGroup("树林"), LabelText("树林片数"), MinValue(0)]
        public int PatchCount = 8;
        [FoldoutGroup("树林"), LabelText("半径范围（格）"), MinMaxSlider(1, 40, true)]
        public Vector2 Radius = new Vector2(5, 10);
        [FoldoutGroup("树林"), LabelText("树林密度"), Range(.01f, 1)]
        public float Density = .65f;
        [FoldoutGroup("树林"), LabelText("主树种比例"), Range(.5f, 1)]
        public float DominantRatio = .8f;
        [FoldoutGroup("树林"), LabelText("零散树数量"), MinValue(0)]
        public int IsolatedCount = 30;
        [FoldoutGroup("树林"), LabelText("零散树间距（格）"), MinValue(1)]
        public int IsolatedSpacing = 3;
        [FoldoutGroup("随机建筑"), LabelText("建筑列表")]
        public List<MapBuildingChoice> Buildings = new List<MapBuildingChoice>();
    }
}
