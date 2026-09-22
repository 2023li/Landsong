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
    public sealed class NightRulesAuthoring : MonoBehaviour
    {
        [LabelText("夜晚战斗与入场设置")]
        public NightRules Settings = new NightRules
        {
            WarningSeconds = 1,
            ProtectionSeconds = 1,
            MinSpawnRegions = 2,
            MaxSpawnRegions = 4,
            SpawnRegionSize = 5,
            SpawnRegionGap = 4,
            HeroWeight = 0.35f,
            FacilityWeight = 1,
            TargetRadius = 24,
            ThreatFloor = 36,
            ThreatPerStrengthCap = 1.5f
        };
        public static void Validate(NightRules s)
        {
            foreach (var value in new[]
            {
                s.WarningSeconds,
                s.ProtectionSeconds,
                s.HeroWeight,
                s.FacilityWeight,
                s.TargetRadius,
                s.ThreatFloor,
                s.ThreatPerStrengthCap
            }

            )
                if (!math.isfinite(value) || value < 0)
                    throw new InvalidOperationException("夜晚入场与战力配置必须为非负有限数值。");
            if (s.MinSpawnRegions < 1 || s.MaxSpawnRegions < s.MinSpawnRegions || s.MaxSpawnRegions > 32 || s.SpawnRegionSize < 1 || s.SpawnRegionSize > 64 || s.SpawnRegionGap < 0 || s.SpawnRegionGap > 256 || s.HeroWeight > 1 || s.TargetRadius <= 0 || s.ThreatFloor < 1 || s.ThreatPerStrengthCap <= 0)
                throw new InvalidOperationException("夜晚刷怪区域、搜索半径或威胁预算配置无效。");
        }

        public sealed class Baker : Baker<NightRulesAuthoring>
        {
            public override void Bake(NightRulesAuthoring authoring)
            {
                var s = authoring.Settings;
                Validate(s);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
            }
        }
    }
}
