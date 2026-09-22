using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingTerrainConnectionSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("出口相对入口高度等级")]
        [MinValue(0)]
        [ShowIf(nameof(Enabled))]
        public int Rise;
        [LabelText("双向通行")]
        [ShowIf(nameof(Enabled))]
        public bool Bidirectional = true;
        [LabelText("桥下最小净空（世界单位）")]
        [MinValue(.1f)]
        [ShowIf(nameof(Enabled))]
        public float Clearance = 1.5f;
        [LabelText("损坏后的移动成本倍率")]
        [MinValue(1)]
        [ShowIf(nameof(Enabled))]
        public float DamagedCost = 2;
    }
}
