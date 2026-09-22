using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    // Formats the explicit effect schema; it does not resolve unrelated definition identities.
    internal static class DefinitionEffectText
    {
        internal static void Append(List<string> lines, EntityManager em, Entity root, ref DefinitionEffects effects)
        {
            for (int i = 0; i < effects.Items.Length; i++)
            {
                var effect = effects.Items[i];
                var name = effect.Target.IsValid ? ItemDefinitions.Get(em, root, effect.Target).Metadata.Name.ToString() : "全部物资";
                lines.Add(effect.Effect == NumericEffectKind.LossMultiplier ? $"{name} 损耗降低 {effect.Magnitude:P0}（同类减损相加，上限 100%）" : $"{name} · {effect.Effect} · {effect.Magnitude:0.##}");
            }

            for (int i = 0; i < effects.Buildings.Length; i++)
            {
                var effect = effects.Buildings[i];
                var name = effect.Target.IsValid ? BuildingDefinitions.Get(em, root, effect.Target).Metadata.Name.ToString() : "全部建筑";
                lines.Add(effect.Effect == NumericEffectKind.LossMultiplier ? $"{name} 损耗降低 {effect.Magnitude:P0}（同类减损相加，上限 100%）" : $"{name} · {effect.Effect} · {effect.Magnitude:0.##}");
            }

            for (int i = 0; i < effects.Soldiers.Length; i++)
            {
                var effect = effects.Soldiers[i];
                var name = effect.Target.IsValid ? SoldierDefinitions.Get(em, root, effect.Target).Metadata.Name.ToString() : "全部士兵";
                lines.Add(effect.Effect == NumericEffectKind.LossMultiplier ? $"{name} 损耗降低 {effect.Magnitude:P0}（同类减损相加，上限 100%）" : $"{name} · {effect.Effect} · {effect.Magnitude:0.##}");
            }

            for (int i = 0; i < effects.Heroes.Length; i++)
            {
                var effect = effects.Heroes[i];
                var name = effect.Target.IsValid ? HeroDefinitions.Get(em, root, effect.Target).Metadata.Name.ToString() : "全部英雄";
                lines.Add(effect.Effect == NumericEffectKind.LossMultiplier ? $"{name} 损耗降低 {effect.Magnitude:P0}（同类减损相加，上限 100%）" : $"{name} · {effect.Effect} · {effect.Magnitude:0.##}");
            }

            for (int i = 0; i < effects.Talents.Length; i++)
            {
                var effect = effects.Talents[i];
                var name = effect.Target.IsValid ? TalentDefinitions.Get(em, root, effect.Target).Metadata.Name.ToString() : "全部人才";
                lines.Add(effect.Effect == NumericEffectKind.LossMultiplier ? $"{name} 损耗降低 {effect.Magnitude:P0}（同类减损相加，上限 100%）" : $"{name} · {effect.Effect} · {effect.Magnitude:0.##}");
            }

            for (int i = 0; i < effects.Kingdom.Length; i++)
            {
                var effect = effects.Kingdom[i];
                lines.Add($"王国 · {effect.Effect} · {effect.Magnitude:0.##}");
            }

            for (int i = 0; i < effects.Intelligence.Length; i++)
                lines.Add("情报完善度 +" + effects.Intelligence[i].Points);
            for (int i = 0; i < effects.FlatProduction.Length; i++)
            {
                var effect = effects.FlatProduction[i];
                lines.Add($"{BuildingDefinitions.Get(em, root, effect.Building).Metadata.Name}：{ItemDefinitions.Get(em, root, effect.Item).Metadata.Name} 固定产量 +{effect.Quantity}");
            }
        }
    }
}
