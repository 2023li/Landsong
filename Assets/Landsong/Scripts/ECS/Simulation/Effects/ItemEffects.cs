using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class ItemEffects
    {
        public static float Modifier(EntityManager em, Entity root, NumericEffectKind effect, ItemId target, bool courtOnly = false) => Evaluate(em, root, effect, target, null, courtOnly);
        public static EffectQuote Query(EntityManager em, Entity root, NumericEffectKind effect, ItemId target, bool courtOnly = false)
        {
            var quote = new EffectQuote
            {
                Unit = EffectUnits.For(effect)
            };
            quote.Value = Evaluate(em, root, effect, target, quote.Sources, courtOnly);
            return quote;
        }

        static float Evaluate(EntityManager em, Entity root, NumericEffectKind effect, ItemId target, List<EffectSource> sources, bool courtOnly)
        {
            float total = 0;
            var domain = courtOnly ? EffectDomain.Court : EffectDomain.Global;
            EffectContributions.Visit(em, root, domain, (ref DefinitionEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Items.Length; i++)
                {
                    var value = effects.Items[i];
                    if (value.Effect != effect)
                        continue;
                    total += source.Apply(value.Magnitude, EffectContributions.LevelMatches(value.Level, source.Level, domain), !value.Target.IsValid || value.Target == target, sources);
                }
            }, (ref TalentJobEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Items.Length; i++)
                {
                    var value = effects.Items[i];
                    if (value.Effect != effect)
                        continue;
                    float magnitude = TalentEffectScalingOps.Value(em, root, value.Scaling, value.BaseMagnitude, value.PerLevel, source.Level) / 100;
                    total += source.Apply(magnitude, true, !value.Recipient.IsValid || value.Recipient == target, sources);
                }
            });
            if (effect == NumericEffectKind.ProductionMultiplier)
                total += EffectContributions.CourtState(em, root, true, sources);
            return total;
        }

        public static float FlatProduction(EntityManager em, Entity root, ItemId item, BuildingId building) => FlatProductionQuote(em, root, item, building).Value;
        public static EffectQuote FlatProductionQuote(EntityManager em, Entity root, ItemId item, BuildingId building)
        {
            var quote = new EffectQuote
            {
                Unit = EffectValueUnit.Flat
            };
            EffectContributions.Visit(em, root, EffectDomain.Global, (ref DefinitionEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.FlatProduction.Length; i++)
                {
                    var value = effects.FlatProduction[i];
                    quote.Value += source.Apply(value.Quantity, EffectContributions.LevelMatches(value.Level, source.Level, EffectDomain.Global), value.Item == item && (!value.Building.IsValid || value.Building == building), quote.Sources);
                }
            });
            return quote;
        }
    }
}
