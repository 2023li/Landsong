using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Landsong.ECS.Authoring
{
    public static class QuestContentValidation
    {
        public static void Validate(GameCatalogAsset catalog)
        {
            var all = catalog.Content; var ids = all.ToDictionary(d => d.Id, StringComparer.Ordinal); var marks = new Dictionary<string, byte>();
            void Visit(ContentSource d)
            {
                if (marks.TryGetValue(d.Id, out var mark)) { if (mark == 1) throw new InvalidOperationException("Quest prerequisite cycle: " + d.Id); return; }
                marks[d.Id] = 1; foreach (var r in d.Rules) if (r.Kind == RuleKind.Prerequisite && ids[r.Target].Kind == ContentKind.Quest) Visit(ids[r.Target]); marks[d.Id] = 2;
            }
            foreach (var d in all.Where(d => d.Kind == ContentKind.Quest))
            {
                if ((d.Flags & ~3) != 0 || d.Duration < 0 || d.Value < 0 || d.Value > 3) throw new InvalidOperationException("Invalid quest flags/duration/type: " + d.Id);
                if ((d.Flags & 2) != 0) continue; // Explicit draft remains registered but is never discovered/offered.
                var keys = new HashSet<string>(); var parents = new HashSet<string>(); var requirements = 0;
                foreach (var r in d.Rules)
                {
                    var required = QuestOps.Requirement(r.Kind);
                    if (r.Kind < RuleKind.Prerequisite || r.Kind > RuleKind.FailureItem || r.Amount <= 0 || r.Level != 0) throw new InvalidOperationException("Unsupported quest rule/amount/level: " + d.Id);
                    ContentSource target = null; if (!string.IsNullOrEmpty(r.Target) && !ids.TryGetValue(r.Target, out target)) throw new InvalidOperationException("Missing quest rule target: " + d.Id);
                    if (required)
                    {
                        requirements++;
                        if (string.IsNullOrWhiteSpace(r.Key) || Encoding.UTF8.GetByteCount(r.Key) > 61 || r.Key.Contains("|") || !keys.Add(r.Key)) throw new InvalidOperationException("Quest requires unique stable requirement keys: " + d.Id);
                    }
                    bool Is(ContentKind k) => target != null && target.Kind == k;
                    if (r.Kind == RuleKind.Prerequisite && (target == null || target.Kind != ContentKind.Quest && target.Kind != ContentKind.Technology || r.Amount != 1 || r.Target == d.Id || !parents.Add(r.Target) || target.Kind == ContentKind.Quest && (target.Flags & 2) != 0)) throw new InvalidOperationException("Invalid quest prerequisite: " + d.Id);
                    if ((r.Kind == RuleKind.RequireBuilding || r.Kind == RuleKind.RequireCrop || r.Kind == RuleKind.RewardBlueprint) && !Is(ContentKind.Building) || (r.Kind == RuleKind.RequireItem || r.Kind == RuleKind.SubmitItem || r.Kind == RuleKind.RewardItem || r.Kind == RuleKind.FailureItem) && !Is(ContentKind.Item) || r.Kind == RuleKind.RequireTechnology && target != null && !Is(ContentKind.Technology) || r.Kind == RuleKind.RewardBuff && !Is(ContentKind.Buff) || r.Kind == RuleKind.RewardFeature && (!Is(ContentKind.Feature) || r.Target.StartsWith("limit."))) throw new InvalidOperationException("Quest target type mismatch: " + d.Id);
                    if (r.Kind == RuleKind.RewardBlueprint && r.Amount > target.Level || r.Kind == RuleKind.RequireBuilding && (r.B < 0 || r.B > target.Level || r.C < 0 || r.C > 1) || r.Kind == RuleKind.RequireTurn && r.B != 0 && r.B != 1) throw new InvalidOperationException("Invalid quest level/condition: " + d.Id);
                }
                if (requirements == 0) throw new InvalidOperationException("Empty quest must be marked draft (Flags |= 2): " + d.Id);
            }
            foreach (var d in all.Where(d => d.Kind == ContentKind.Quest && (d.Flags & 2) == 0)) Visit(d);
        }
    }
}
