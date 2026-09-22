using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class ExpeditionSettingsAuthoring : MonoBehaviour
    {
        [LabelText("远征设置")]
        public ExpeditionSettings Settings = new ExpeditionSettings
        {
            PenaltyTurns = 5,
            AttractionPerStack = 5
        };
        public static void Validate(ExpeditionSettings s)
        {
            if (s.PenaltyTurns < 1 || !math.isfinite(s.AttractionPerStack) || s.AttractionPerStack < 0)
                throw new InvalidOperationException("远征抚恤不足的惩罚配置无效。");
        }

        public sealed class Baker : Baker<ExpeditionSettingsAuthoring>
        {
            public override void Bake(ExpeditionSettingsAuthoring authoring)
            {
                var s = authoring.Settings;
                Validate(s);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
            }
        }
    }
}
