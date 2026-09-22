using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class BuildingStatEffects
    {
        public static float Modifier(EntityManager em, Entity root, NumericEffectKind effect, BuildingId target) => Evaluate(em, root, effect, target, null);
        public static EffectQuote Query(EntityManager em, Entity root, NumericEffectKind effect, BuildingId target)
        {
            var quote = new EffectQuote
            {
                Unit = EffectUnits.For(effect)
            };
            quote.Value = Evaluate(em, root, effect, target, quote.Sources);
            return quote;
        }

        static float Evaluate(EntityManager em, Entity root, NumericEffectKind effect, BuildingId target, List<EffectSource> sources)
        {
            float total = 0;
            var domain = effect == NumericEffectKind.ActionPower ? EffectDomain.Global : EffectDomain.Military;
            EffectContributions.Visit(em, root, domain, (ref DefinitionEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Buildings.Length; i++)
                {
                    var value = effects.Buildings[i];
                    if (value.Effect != effect)
                        continue;
                    total += source.Apply(value.Magnitude, EffectContributions.LevelMatches(value.Level, source.Level, domain), !value.Target.IsValid || value.Target == target, sources);
                }
            });
            return total;
        }
    }
}
