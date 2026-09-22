using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class KingdomEffects
    {
        public static float Modifier(EntityManager em, Entity root, KingdomEffectKind effect, bool courtOnly = false) => Query(em, root, effect, courtOnly).Value;
        public static EffectQuote Query(EntityManager em, Entity root, KingdomEffectKind effect, bool courtOnly = false)
        {
            var result = new EffectQuote
            {
                Unit = effect == KingdomEffectKind.PlotRisk ? EffectValueUnit.Ratio : EffectValueUnit.Flat
            };
            var domain = courtOnly ? EffectDomain.Court : EffectDomain.Global;
            EffectContributions.Visit(em, root, domain, (ref DefinitionEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Kingdom.Length; i++)
                {
                    var value = effects.Kingdom[i];
                    if (value.Effect != effect)
                        continue;
                    result.Value += source.Apply(value.Magnitude, EffectContributions.LevelMatches(value.Level, source.Level, domain), true, result.Sources);
                }
            }, (ref TalentJobEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Kingdom.Length; i++)
                {
                    var value = effects.Kingdom[i];
                    if (value.Effect != effect)
                        continue;
                    result.Value += source.Apply(TalentEffectScalingOps.Value(em, root, value.Scaling, value.BaseMagnitude, value.PerLevel, source.Level), true, true, result.Sources);
                }
            });
            return result;
        }
    }
}
