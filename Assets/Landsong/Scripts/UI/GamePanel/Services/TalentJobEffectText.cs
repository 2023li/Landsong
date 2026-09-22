using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    internal static class TalentJobEffectText
    {
        internal static void Append(List<string> lines, EntityManager em, Entity root, ref TalentJobEffects effects)
        {
            for (int i = 0; i < effects.Items.Length; i++)
            {
                var effect = effects.Items[i];
                var name = effect.Recipient.IsValid ? ItemDefinitions.Get(em, root, effect.Recipient).Metadata.Name.ToString() : "全部物品";
                lines.Add($"{name} · {effect.Effect} · 基础 {effect.BaseMagnitude:0.##} / 每级 {effect.PerLevel:0.##}");
            }

            for (int i = 0; i < effects.Soldiers.Length; i++)
            {
                var effect = effects.Soldiers[i];
                var name = effect.Recipient.IsValid ? SoldierDefinitions.Get(em, root, effect.Recipient).Metadata.Name.ToString() : "全部士兵";
                lines.Add($"{name} · {effect.Effect} · 基础 {effect.BaseMagnitude:0.##} / 每级 {effect.PerLevel:0.##}");
            }

            for (int i = 0; i < effects.Heroes.Length; i++)
            {
                var effect = effects.Heroes[i];
                var name = effect.Recipient.IsValid ? HeroDefinitions.Get(em, root, effect.Recipient).Metadata.Name.ToString() : "全部英雄";
                lines.Add($"{name} · {effect.Effect} · 基础 {effect.BaseMagnitude:0.##} / 每级 {effect.PerLevel:0.##}");
            }

            for (int i = 0; i < effects.Kingdom.Length; i++)
            {
                var effect = effects.Kingdom[i];
                lines.Add($"王国 · {effect.Effect} · 基础 {effect.BaseMagnitude:0.##} / 每级 {effect.PerLevel:0.##}");
            }
        }
    }
}
