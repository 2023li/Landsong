using System.Text;
using Landsong.TalentSystem;
using Landsong.Localization;

namespace Landsong.UISystem
{
    internal static class GamePanel_TalentText
    {
        public static string FormatProfession(TalentProfession profession)
        {
            var key = profession switch
            {
                TalentProfession.大总管 => "steward",
                TalentProfession.大法师 => "archmage",
                TalentProfession.大将军 => "general",
                TalentProfession.大学者 => "scholar",
                _ => "any"
            };
            return L10n.Gameplay(
                $"gameplay.talent.profession.{key}",
                profession == TalentProfession.None ? "通用" : profession.ToString());
        }

        public static string FormatRarity(TalentRarity rarity)
        {
            return rarity switch
            {
                TalentRarity.Uncommon => L10n.Gameplay("gameplay.talent.rarity.uncommon", "优秀"),
                TalentRarity.Rare => L10n.Gameplay("gameplay.talent.rarity.rare", "稀有"),
                TalentRarity.Epic => L10n.Gameplay("gameplay.talent.rarity.epic", "史诗"),
                TalentRarity.Legendary => L10n.Gameplay("gameplay.talent.rarity.legendary", "传说"),
                _ => L10n.Gameplay("gameplay.talent.rarity.common", "普通")
            };
        }

        public static string FormatLevel(TalentState talent)
        {
            if (talent == null)
            {
                return string.Empty;
            }

            return talent.IsMaxLevel
                ? L10n.Gameplay("gameplay.talent.level.max", "Lv.{0} 最高级", talent.Level)
                : L10n.Gameplay("gameplay.talent.level.progress", "Lv.{0} 经验 {1}/{2}", talent.Level, talent.Experience, talent.ExperienceRequiredForNextLevel);
        }

        public static string FormatSalary(TalentState talent)
        {
            return talent == null ? string.Empty : L10n.Gameplay("gameplay.talent.salary", "薪资 {0}/回合", talent.SalaryGoldPerTurn);
        }

        public static string FormatSalary(TalentDefinition definition)
        {
            return definition == null ? string.Empty : L10n.Gameplay("gameplay.talent.salary", "薪资 {0}/回合", definition.CalculateSalaryGoldPerTurn(definition.StartingLevel));
        }

        public static string FormatOfferDetails(TalentDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return L10n.Gameplay("gameplay.talent.offer_details", "{0} / {1} / Lv.{2}", FormatProfession(definition.Profession), FormatRarity(definition.Rarity), definition.StartingLevel);
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

            return builder.Length == 0 ? L10n.Gameplay("gameplay.talent.no_unlocked_effects", "无已解锁效果") : builder.ToString();
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

            return builder.Length == 0 ? L10n.Gameplay("gameplay.talent.no_base_effects", "无基础效果") : builder.ToString();
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
                return L10n.Gameplay("gameplay.talent.no_hidden_traits", "无隐藏特性");
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
                    TalentHiddenTraitState.Active => L10n.Gameplay("gameplay.talent.trait_active", "已激活：{0}", definition.ActiveDescription),
                    TalentHiddenTraitState.Discovered => L10n.Gameplay("gameplay.talent.trait_discovered_label", "已发现：{0}", definition.DiscoveredDescription),
                    _ => definition.UndiscoveredDescription
                };
                AppendLine(builder, label);
            }

            return builder.Length == 0 ? L10n.Gameplay("gameplay.talent.no_hidden_traits", "无隐藏特性") : builder.ToString();
        }

        public static string FormatSlotRestriction(TalentSlotRuntimeState slot)
        {
            if (slot == null)
            {
                return string.Empty;
            }

            return slot.AcceptedProfession == TalentProfession.None
                ? L10n.Gameplay("gameplay.talent.any_profession", "任意职业")
                : L10n.Gameplay("gameplay.talent.restricted_profession", "限定 {0}", FormatProfession(slot.AcceptedProfession));
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
