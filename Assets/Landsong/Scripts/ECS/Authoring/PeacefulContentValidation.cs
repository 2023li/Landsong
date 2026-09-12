using System;
using System.Linq;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring
{
    public static class PeacefulContentValidation
    {
        public static void Validate(GameCatalogAsset catalog, ContentCompilation compiled=null)
        {
            compiled??=new ContentCompilation(catalog);
            var r = catalog.Peaceful;
            if (r.MaximumPerNight < 0 || r.MaximumPerNight > 32 || r.MaximumConcurrent < 1 || r.MaximumConcurrent > 8 || r.TheftValueBudget < 0 || r.TheftValueBudget > 100000 || !math.isfinite(r.FirstOpportunity) || r.FirstOpportunity < 1 || !math.isfinite(r.Interval) || r.Interval < 1) throw new InvalidOperationException("Invalid peaceful night budget/timing");
            foreach (var d in catalog.Content)
            {
                if (d.Kind == ContentKind.Opportunity)
                {
                    if (d.Prefab == null) throw new InvalidOperationException(d.Id + ": missing visitor prefab");
                    var p = d.Opportunity;
                    if ((byte)p.Kind > 1 || p.Weight < 0 || p.Weight > 10000 || p.MaximumPerNight < 0 || p.MaximumPerNight > 32 || !math.all(math.isfinite(new float4(p.StartFraction, p.EndFraction, p.Speed, p.MinimumResponse))) || !math.all(math.isfinite(new float3(p.CaptureRadius, p.ResponseRadius, p.RouteLength))) || p.StartFraction < 0 || p.EndFraction > .5f || p.EndFraction <= p.StartFraction || p.Speed <= 0 || p.Speed > 10 || p.MinimumResponse < 2 || p.MinimumResponse > 30 || p.CaptureRadius < .5f || p.CaptureRadius > 3 || p.ResponseRadius < 1 || p.ResponseRadius > 100 || p.RouteLength < p.Speed * p.MinimumResponse || p.RouteLength > 100 || !p.Heroes && !p.Soldiers)
                        throw new InvalidOperationException(d.Id + ": invalid visitor window/route/responder profile");
                }
                if (d.Kind == ContentKind.Item)
                {
                    var t = d.Theft;
                    if ((byte)t.Protection > 7 || t.Weight < 0 || t.Weight > 10000 || t.Maximum < 0 || t.Maximum > 10000 || t.UnitValue < 1 || t.UnitValue > 100000) throw new InvalidOperationException(d.Id + ": invalid theft value/weight/cap");
                }
                foreach (var rule in compiled.For(d)) if (rule.Kind == RuleKind.SpecialDrop)
                {
                    int item = rule.Target;
                    if (d.Kind != ContentKind.Enemy || item < 0 || catalog.Content[item].Kind != ContentKind.Item || rule.Amount < 1 || rule.Amount > 100000 || rule.B < 1 || rule.B > 3) throw new InvalidOperationException(d.Id + ": SpecialDrop needs an item, positive quantity and rarity B=1..3");
                    if (!catalog.Content.Any(c => c.Kind == ContentKind.Loot && c.Prefab != null)) throw new InvalidOperationException(d.Id + ": missing special loot visual definition");
                }
            }
        }
    }
}
