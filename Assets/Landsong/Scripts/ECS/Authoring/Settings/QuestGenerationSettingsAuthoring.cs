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
    public sealed class QuestGenerationSettingsAuthoring : MonoBehaviour
    {
        [LabelText("任务抽取设置")]
        public QuestGenerationSettings Settings = new QuestGenerationSettings
        {
            StrengthStep = 10,
            MarketValuePerStrength = 100,
            Low = new int4(100, 35, 8, 1),
            Medium = new int4(45, 100, 35, 8),
            High = new int4(12, 45, 100, 35),
            Maximum = new int4(4, 15, 55, 100)
        };
        public static void Validate(QuestGenerationSettings s)
        {
            if (s.StrengthStep <= 0 || s.MarketValuePerStrength <= 0)
                throw new InvalidOperationException("任务强度换算配置无效。");
            foreach (var weights in new[]
            {
                s.Low,
                s.Medium,
                s.High,
                s.Maximum
            }

            )
                if (math.any(weights < 0) || math.all(weights == 0))
                    throw new InvalidOperationException("任务强度权重必须非负且不能全部为零。");
        }

        public sealed class Baker : Baker<QuestGenerationSettingsAuthoring>
        {
            public override void Bake(QuestGenerationSettingsAuthoring authoring)
            {
                var s = authoring.Settings;
                Validate(s);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
            }
        }
    }
}
