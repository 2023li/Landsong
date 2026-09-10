using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring
{
    public static class InvitationExpeditionValidation
    {
        public static void Validate(GameCatalogAsset catalog)
        {
            var settings = catalog.QuestGeneration;
            if (settings.StrengthStep <= 0 || settings.MarketValuePerStrength <= 0) throw new InvalidOperationException("Invalid quest strength conversion");
            foreach (var weights in new[] { settings.Low, settings.Medium, settings.High, settings.Maximum }) if (math.any(weights < 0) || math.all(weights == 0)) throw new InvalidOperationException("Invalid quest intensity weights");
            if (catalog.Expeditions.PenaltyTurns < 1 || !math.isfinite(catalog.Expeditions.AttractionPerStack) || catalog.Expeditions.AttractionPerStack < 0) throw new InvalidOperationException("Invalid expedition penalty");
            foreach (var d in catalog.Content)
            {
                if (d.Kind == ContentKind.Quest)
                {
                    if (d.QuestIntensity < 0 || d.QuestIntensity > 3 || !math.isfinite(d.QuestWeight) || d.QuestWeight < 0 || !math.isfinite(d.ItemQuantityScale) || d.ItemQuantityScale <= 0) throw new InvalidOperationException("Invalid quest selection/scale: " + d.Id);
                    foreach (var r in d.Rules) if (r.Kind == RuleKind.SubmitItem || r.Kind == RuleKind.RequireItem || r.Kind == RuleKind.RewardItem || r.Kind == RuleKind.FailureItem)
                        if (math.round((double)r.Amount * d.ItemQuantityScale) < 1 || (double)r.Amount * d.ItemQuantityScale > int.MaxValue) throw new InvalidOperationException("Quest scaled amount out of range: " + d.Id);
                }
                var sourceKeys = new HashSet<(int,int)>(); var supplyItems = new HashSet<string>();
                foreach (var r in d.Rules)
                {
                    if (r.Kind == RuleKind.QuestSource && (d.Kind != ContentKind.Building || r.B < 0 || r.B > 3 || r.Amount < 0 || r.Amount > 100 || r.C < 1 || !math.isfinite(r.Value) || r.Value < r.C || r.Value > 100000 || !sourceKeys.Add((r.Level, r.B)))) throw new InvalidOperationException("Invalid/duplicate quest source: " + d.Id);
                    if (r.Kind == RuleKind.ExpeditionSite && (d.Kind != ContentKind.Building || r.Amount < 1 || r.B < r.Amount || !math.isfinite(r.Value) || r.Value < 0)) throw new InvalidOperationException("Invalid expedition site: " + d.Id);
                    if (r.Kind == RuleKind.Supply && d.Kind == ContentKind.Expedition)
                    {
                        var item = catalog.Find(r.Target);
                        if (d.Kind != ContentKind.Expedition || item < 0 || catalog.Content[item].Kind != ContentKind.Item || r.Amount < 0 || r.Amount > 1000000 || r.B < 0 || r.B > r.Amount / 2 || !math.isfinite(r.Value) || r.Value < 0 || r.Value > 1 || !math.isfinite(r.Extra) || r.Extra < 0 || r.Extra > 1 || !supplyItems.Add(r.Target) || supplyItems.Count > 8) throw new InvalidOperationException("Invalid expedition supply (at most 8 unique items): " + d.Id);
                    }
                    if (r.Kind == RuleKind.VisiblePrerequisite && (d.Kind != ContentKind.Expedition || catalog.Find(r.Target) < 0 || r.Amount < 1)) throw new InvalidOperationException("Invalid destination visibility: " + d.Id);
                }
                if (d.Kind == ContentKind.Expedition && (d.Duration < 1 || d.Level < 1 || d.Population < 1 || d.Capacity < 0 || d.Capacity > 0 && d.Capacity < d.Population || (d.Flags & ~1) != 0 || d.Cost < 0 || d.Value < 0 || (long)d.Cost + (long)d.Value * math.max(d.Population, d.Capacity) > int.MaxValue || !math.isfinite(d.Chance) || d.Chance < 0 || d.Chance > 1 || !math.isfinite(d.Range) || d.Range < 0 || d.Range > 1 || !math.isfinite(d.Interval) || d.Interval < 0 || d.Interval > 1 || !math.isfinite(d.Loss) || d.Loss < 0 || d.Loss > 1)) throw new InvalidOperationException("Invalid expedition destination: " + d.Id);
            }
        }
    }
}
