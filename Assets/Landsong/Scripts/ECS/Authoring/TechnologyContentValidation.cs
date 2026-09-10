using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring
{
    public static class TechnologyContentValidation
    {
        public static void Validate(GameCatalogAsset catalog)
        {
            var all = catalog.Content; var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < all.Length; i++) { if (string.IsNullOrWhiteSpace(all[i].Id) || ids.ContainsKey(all[i].Id)) throw new InvalidOperationException("Empty/duplicate content ID: " + all[i].Id); ids.Add(all[i].Id, i); }
            var marks = new byte[all.Length];
            void Visit(int at)
            {
                if (marks[at] == 1) throw new InvalidOperationException("Technology prerequisite cycle: " + all[at].Id);
                if (marks[at] == 2) return; marks[at] = 1;
                foreach (var r in all[at].Rules) if (r.Kind == RuleKind.Prerequisite) Visit(ids[r.Target]);
                marks[at] = 2;
            }
            foreach (var d in all)
            {
                if (d.Kind != ContentKind.Technology) continue;
                if (d.Cost < 0 || (d.Flags & ~1) != 0 || !math.isfinite(d.TechnologyPosition.x) || !math.isfinite(d.TechnologyPosition.y) || d.TechnologyPosition.x < 0 || d.TechnologyPosition.y < 0) throw new InvalidOperationException("Invalid technology cost/flags/position: " + d.Id);
                var parents = new HashSet<string>();
                foreach (var r in d.Rules)
                {
                    if (r.Kind == RuleKind.Intelligence) continue; // Passive source, validated by IntelligenceValidation, not a completion reward.
                    if (r.Kind != RuleKind.Prerequisite && (r.Kind < RuleKind.RewardItem || r.Kind > RuleKind.RewardFeature)) throw new InvalidOperationException("Technology supports explicit prerequisites/completion rewards only: " + d.Id + " / " + r.Kind);
                    if (!ids.TryGetValue(r.Target ?? "", out var target)) throw new InvalidOperationException("Missing technology rule target: " + d.Id + " / " + r.Target);
                    var kind = all[target].Kind;
                    if (r.Level != 0 || r.Amount <= 0) throw new InvalidOperationException("Technology rule needs Level=0 and positive amount: " + d.Id);
                    if (r.Kind == RuleKind.Prerequisite && (kind != ContentKind.Technology || r.Amount != 1 || r.Target == d.Id || !parents.Add(r.Target))) throw new InvalidOperationException("Invalid/duplicate technology prerequisite: " + d.Id);
                    if (r.Kind == RuleKind.RewardItem && kind != ContentKind.Item || r.Kind == RuleKind.RewardBlueprint && (kind != ContentKind.Building || r.Amount > all[target].Level) || r.Kind == RuleKind.RewardBuff && kind != ContentKind.Buff || r.Kind == RuleKind.RewardFeature && (kind != ContentKind.Feature || r.Target.StartsWith("limit.", StringComparison.Ordinal))) throw new InvalidOperationException("Invalid technology completion reward: " + d.Id);
                }
            }
            for (var i = 0; i < all.Length; i++) if (all[i].Kind == ContentKind.Technology) Visit(i);
        }
    }
}
