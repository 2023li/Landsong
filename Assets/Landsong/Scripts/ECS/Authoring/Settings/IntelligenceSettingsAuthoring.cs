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
    public sealed class IntelligenceSettingsAuthoring : MonoBehaviour
    {
        [LabelText("情报设置")]
        public IntelligenceSettings Settings = new IntelligenceSettings
        {
            LowIntel = 1,
            MediumIntel = 40,
            HighIntel = 75,
            MediumIntelLead = 12,
            HighIntelLead = 24
        };
        public static void Validate(IntelligenceSettings s)
        {
            if (s.LowIntel < 1 || s.LowIntel >= s.MediumIntel || s.MediumIntel >= s.HighIntel || s.HighIntel > 100 || !math.isfinite(s.MediumIntelLead) || !math.isfinite(s.HighIntelLead) || s.MediumIntelLead <= 0 || s.HighIntelLead < s.MediumIntelLead || s.HighIntelLead > 120)
                throw new InvalidOperationException("情报档位须在一到一百内递增；高档提前量不得低于中档且最多120秒。");
        }

        public sealed class Baker : Baker<IntelligenceSettingsAuthoring>
        {
            public override void Bake(IntelligenceSettingsAuthoring authoring)
            {
                var s = authoring.Settings;
                Validate(s);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
            }
        }
    }
}
