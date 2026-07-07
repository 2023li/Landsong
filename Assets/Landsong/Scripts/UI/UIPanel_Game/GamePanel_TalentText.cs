using System.Text;
using Landsong.TalentSystem;

namespace Landsong.UISystem
{
    internal static class GamePanel_TalentText
    {
        public static string FormatProfession(TalentProfession profession)
        {
            return profession == TalentProfession.None ? "通用" : profession.ToString();
        }

        public static string FormatRarity(TalentRarity rarity)
        {
            return rarity switch
            {
                TalentRarity.Uncommon => "优秀",
                TalentRarity.Rare => "稀有",
                TalentRarity.Epic => "史诗",
                TalentRarity.Legendary => "传说",
                _ => "普通"
            };
        }

        public static string FormatLevel(TalentState talent)
        {
            if (talent == null)
            {
                return string.Empty;
            }

            return talent.IsMaxLevel
                ? $"Lv.{talent.Level} 最高级"
                : $"Lv.{talent.Level} 经验 {talent.Experience}/{talent.ExperienceRequiredForNextLevel}";
        }

        public static string FormatSalary(TalentState talent)
        {
            return talent == null ? string.Empty : $"薪资 {talent.SalaryGoldPerTurn}/回合";
        }

        public static string FormatSalary(TalentDefinition definition)
        {
            return definition == null ? string.Empty : $"薪资 {definition.CalculateSalaryGoldPerTurn(definition.StartingLevel)}/回合";
        }

        public static string FormatOfferDetails(TalentDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return $"{FormatProfession(definition.Profession)} / {FormatRarity(definition.Rarity)} / Lv.{definition.StartingLevel}";
        }

        public static string FormatEffects(TalentState talent)
        {
            if (talent == null || talent.Definition == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var effects = talent.Definition.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                AppendLine(builder, effects[i]?.GetDescription(talent));
            }

            var traits = talent.HiddenTraits;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null || !trait.IsActive || trait.Definition == null)
                {
                    continue;
                }

                var traitEffects = trait.Definition.Effects;
                for (var j = 0; j < traitEffects.Count; j++)
                {
                    AppendLine(builder, traitEffects[j]?.GetDescription(talent));
                }
            }

            return builder.Length == 0 ? "无已解锁效果" : builder.ToString();
        }

        public static string FormatEffects(TalentDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var effects = definition.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                AppendLine(builder, effects[i]?.GetDescription(null));
            }

            return builder.Length == 0 ? "无基础效果" : builder.ToString();
        }

        public static string FormatHiddenTraits(TalentState talent)
        {
            if (talent == null)
            {
                return string.Empty;
            }

            var traits = talent.HiddenTraits;
            if (traits.Count == 0)
            {
                return "无隐藏特性";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                var definition = trait == null ? null : trait.Definition;
                if (definition == null)
                {
                    continue;
                }

                var label = trait.State switch
                {
                    TalentHiddenTraitState.Active => $"已激活：{definition.ActiveDescription}",
                    TalentHiddenTraitState.Discovered => $"已发现：{definition.DiscoveredDescription}",
                    _ => definition.UndiscoveredDescription
                };
                AppendLine(builder, label);
            }

            return builder.Length == 0 ? "无隐藏特性" : builder.ToString();
        }

        public static string FormatSlotRestriction(TalentSlotRuntimeState slot)
        {
            if (slot == null)
            {
                return string.Empty;
            }

            return slot.AcceptedProfession == TalentProfession.None
                ? "任意职业"
                : $"限定 {slot.AcceptedProfession}";
        }

        private static void AppendLine(StringBuilder builder, string line)
        {
            if (builder == null || string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(line.Trim());
        }
    }
}
