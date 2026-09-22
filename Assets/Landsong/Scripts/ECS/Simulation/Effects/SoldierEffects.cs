using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class SoldierEffects
    {
        public static float Modifier(EntityManager em, Entity root, NumericEffectKind effect, SoldierId target, bool courtOnly = false) => Evaluate(em, root, effect, target, null, courtOnly);
        public static EffectQuote Query(EntityManager em, Entity root, NumericEffectKind effect, SoldierId target, bool courtOnly = false)
        {
            var quote = new EffectQuote
            {
                Unit = EffectUnits.For(effect)
            };
            quote.Value = Evaluate(em, root, effect, target, quote.Sources, courtOnly);
            return quote;
        }

        static float Evaluate(EntityManager em, Entity root, NumericEffectKind effect, SoldierId target, List<EffectSource> sources, bool courtOnly)
        {
            float total = 0;
            var domain = courtOnly ? EffectDomain.Court : EffectDomain.Military;
            EffectContributions.Visit(em, root, domain, (ref DefinitionEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Soldiers.Length; i++)
                {
                    var value = effects.Soldiers[i];
                    if (value.Effect != effect)
                        continue;
                    total += source.Apply(value.Magnitude, EffectContributions.LevelMatches(value.Level, source.Level, domain), !value.Target.IsValid || value.Target == target, sources);
                }
            }, (ref TalentJobEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Soldiers.Length; i++)
                {
                    var value = effects.Soldiers[i];
                    if (value.Effect != effect)
                        continue;
                    float magnitude = TalentEffectScalingOps.Value(em, root, value.Scaling, value.BaseMagnitude, value.PerLevel, source.Level) / 100;
                    total += source.Apply(magnitude, true, !value.Recipient.IsValid || value.Recipient == target, sources);
                }
            });
            if (effect == NumericEffectKind.SoldierAttackMultiplier)
                total += EffectContributions.CourtState(em, root, false, sources);
            return total;
        }
    }
}
