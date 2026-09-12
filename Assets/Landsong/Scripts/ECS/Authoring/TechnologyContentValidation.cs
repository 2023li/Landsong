using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring
{
    public static class TechnologyContentValidation
    {
        public static void Validate(GameCatalogAsset catalog, ContentCompilation compiled=null)
        {
            compiled??=new ContentCompilation(catalog);
            var all = catalog.Content; var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < all.Length; i++) { if (string.IsNullOrWhiteSpace(all[i].Id) || ids.ContainsKey(all[i].Id)) throw new InvalidOperationException("Empty/duplicate content ID: " + all[i].Id); ids.Add(all[i].Id, i); }
            var marks = new byte[all.Length];
            void Visit(int at)
            {
                if (marks[at] == 1) throw new InvalidOperationException("Technology prerequisite cycle: " + all[at].Id);
                if (marks[at] == 2) return; marks[at] = 1;
                foreach (var r in compiled.For(all[at])) if (r.Kind == RuleKind.Prerequisite) Visit(ids[compiled.Id(r.Target)]);
                marks[at] = 2;
            }
            foreach (var d in all)
            {
                if (d.Kind != ContentKind.Technology) continue;
                if (d.Cost < 0 || (d.Flags & ~1) != 0 || !math.isfinite(d.TechnologyPosition.x) || !math.isfinite(d.TechnologyPosition.y) || d.TechnologyPosition.x < 0 || d.TechnologyPosition.y < 0) throw new InvalidOperationException("Invalid technology cost/flags/position: " + d.Id);
                var parents = new HashSet<string>();
                foreach (var r in compiled.For(d))
                {
                    // Passive sources use the shared effect support/target contract, not reward quantities.
                    if (r.Kind == RuleKind.Intelligence)
                    {
                        if ((d.Flags & 1) == 0 && r.Level > 1)
                            throw new InvalidOperationException(d.Id + "：不可重复科技的情报只能使用通用或一级条目，不能配置无法达到的完成次数");
                        continue;
                    }
                    if (EffectRules.IsMilitary(r.Kind)) continue;
                    if (r.Kind != RuleKind.Prerequisite && (r.Kind < RuleKind.RewardItem || r.Kind > RuleKind.RewardFeature)) throw new InvalidOperationException("Technology supports prerequisites, completion rewards and supported passive effects only: " + d.Id + " / " + r.Kind);
                    if (!ids.TryGetValue(compiled.Id(r.Target) ?? "", out var target)) throw new InvalidOperationException("Missing technology rule target: " + d.Id + " / " + compiled.Id(r.Target));
                    var kind = all[target].Kind;
                    if (r.Level != 0 || r.Amount <= 0) throw new InvalidOperationException("Technology rule needs Level=0 and positive amount: " + d.Id);
                    if (r.Kind == RuleKind.Prerequisite && (kind != ContentKind.Technology || r.Amount != 1 || compiled.Id(r.Target) == d.Id || !parents.Add(compiled.Id(r.Target)))) throw new InvalidOperationException("Invalid/duplicate technology prerequisite: " + d.Id);
                    if (r.Kind >= RuleKind.RewardItem && r.Kind <= RuleKind.RewardFeature)
                        RewardAuthoringValidation.Validate(r, all[target]);
                }
            }
            for (var i = 0; i < all.Length; i++) if (all[i].Kind == ContentKind.Technology) Visit(i);
        }
    }
}
