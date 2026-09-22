using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class PersonalRiskEffects
    {
        public static float Modifier(EntityManager em, Entity root, Entity person)
        {
            var target = em.HasComponent<TalentDefinitionRef>(person) ? em.GetComponentData<TalentDefinitionRef>(person).Definition : TalentId.None;
            float total = 0;
            EffectContributions.Visit(em, root, EffectDomain.Personal, (ref DefinitionEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Talents.Length; i++)
                {
                    var effect = effects.Talents[i];
                    if (effect.Effect != NumericEffectKind.NaturalDeathRisk)
                        continue;
                    total += source.Apply(effect.Magnitude, EffectContributions.LevelMatches(effect.Level, source.Level, EffectDomain.Personal), !effect.Target.IsValid || effect.Target == target, null);
                }
            }, person: person);
            return total;
        }
    }
}
