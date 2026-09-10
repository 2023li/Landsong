using System;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring
{
    public static class IntelligenceValidation
    {
        public static void Validate(GameCatalogAsset catalog)
        {
            var s = catalog.Settings;
            if (s.LowIntel < 1 || s.LowIntel >= s.MediumIntel || s.MediumIntel >= s.HighIntel || s.HighIntel > 100
                || !math.isfinite(s.MediumIntelLead) || !math.isfinite(s.HighIntelLead) || s.MediumIntelLead <= 0 || s.HighIntelLead < s.MediumIntelLead || s.HighIntelLead > 120)
                throw new InvalidOperationException("情报档位须递增且在 1～100 内；高档提前量须不低于中档且最多 120 秒。");
            foreach (var definition in catalog.Content) foreach (var rule in definition.Rules)
            {
                if (rule.Kind != RuleKind.Intelligence) continue;
                if (definition.Kind != ContentKind.Building && definition.Kind != ContentKind.Technology && definition.Kind != ContentKind.Buff && definition.Kind != ContentKind.Policy)
                    throw new InvalidOperationException(definition.Id + ": 情报来源只支持建筑、科技、Buff 或政策。");
                if (rule.Amount < 1 || rule.Amount > 100 || rule.B < 0 || definition.Kind != ContentKind.Building && rule.B != 0)
                    throw new InvalidOperationException(definition.Id + ": 情报点数须为 1～100，工人条件只用于建筑。");
                if (!string.IsNullOrEmpty(rule.Target))
                {
                    int at = catalog.Find(rule.Target);
                    if (at < 0 || catalog.Definitions[at].Data.Kind != ContentKind.Technology) throw new InvalidOperationException(definition.Id + ": 情报 Target 必须是科技 ID。");
                }
            }
        }
    }
}
