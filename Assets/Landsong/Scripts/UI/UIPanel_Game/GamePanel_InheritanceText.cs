using System.Text;
using Landsong.InheritanceSystem;
using Landsong.Localization;

namespace Landsong.UISystem
{
    internal static class GamePanel_InheritanceText
    {
        public static string FormatRole(RoyalCharacterRole role)
        {
            return role switch
            {
                RoyalCharacterRole.King => L10n.Gameplay("gameplay.inheritance.role.king", "国王"),
                RoyalCharacterRole.Queen => L10n.Gameplay("gameplay.inheritance.role.queen", "王后"),
                RoyalCharacterRole.Prince => L10n.Gameplay("gameplay.inheritance.role.prince", "王子"),
                _ => role.ToString()
            };
        }

        public static string FormatStatus(RoyalCharacterStatus status)
        {
            return status switch
            {
                RoyalCharacterStatus.Reigning => L10n.Gameplay("gameplay.inheritance.status.reigning", "在位"),
                RoyalCharacterStatus.Consort => L10n.Gameplay("gameplay.inheritance.status.consort", "王后"),
                RoyalCharacterStatus.Heir => L10n.Gameplay("gameplay.inheritance.status.heir", "继承人"),
                RoyalCharacterStatus.Retired => L10n.Gameplay("gameplay.inheritance.status.retired", "退位"),
                RoyalCharacterStatus.Dead => L10n.Gameplay("gameplay.inheritance.status.dead", "死亡"),
                _ => status.ToString()
            };
        }

        public static string FormatCharacterLine(RoyalCharacterState character, int legalHeirAge)
        {
            if (character == null)
            {
                return L10n.Gameplay("gameplay.common.none", "无");
            }

            var heirText = character.IsPotentialHeir
                ? (character.IsLegalHeir(legalHeirAge)
                    ? L10n.Gameplay("gameplay.inheritance.legal_heir", "合法继承人")
                    : L10n.Gameplay("gameplay.inheritance.underage_heir", "未成年，{0} 岁成年", legalHeirAge))
                : FormatStatus(character.Status);
            return L10n.Gameplay("gameplay.inheritance.character_line", "{0} / {1} / {2} / {3}/{4} 岁", character.DisplayName, FormatRole(character.Role), heirText, character.Age, character.EffectiveMaxLifespan);
        }

        public static string FormatTraits(RoyalCharacterState character)
        {
            if (character == null || character.Traits.Count == 0)
            {
                return L10n.Gameplay("gameplay.inheritance.no_traits", "无特性");
            }

            var builder = new StringBuilder();
            var traits = character.Traits;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                if (trait.Definition == null || trait.IsHidden)
                {
                    builder.Append(L10n.Gameplay("gameplay.inheritance.unknown_trait", "未知特性"));
                    continue;
                }

                var state = trait.IsActive
                    ? L10n.Gameplay("gameplay.inheritance.trait_state.active", "生效")
                    : L10n.Gameplay("gameplay.inheritance.trait_state.discovered", "已发现");
                var acquired = trait.IsAcquired
                    ? L10n.Gameplay("gameplay.inheritance.trait_origin.acquired", "后天")
                    : L10n.Gameplay("gameplay.inheritance.trait_origin.innate", "先天");
                builder.Append(L10n.Gameplay("gameplay.inheritance.trait_line", "{0}（{1} / {2}）", trait.Definition.TraitName, state, acquired));
            }

            return builder.Length == 0 ? L10n.Gameplay("gameplay.inheritance.no_traits", "无特性") : builder.ToString();
        }

        public static string FormatRelations(RoyalCharacterState character)
        {
            if (character == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(character.FatherId))
            {
                builder.Append(L10n.Gameplay("gameplay.inheritance.relation.father", "父 {0}", ShortId(character.FatherId)));
            }

            if (!string.IsNullOrWhiteSpace(character.MotherId))
            {
                if (builder.Length > 0)
                {
                    builder.Append(" / ");
                }

                builder.Append(L10n.Gameplay("gameplay.inheritance.relation.mother", "母 {0}", ShortId(character.MotherId)));
            }

            if (character.ChildrenIds.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" / ");
                }

                builder.Append(L10n.Gameplay("gameplay.inheritance.relation.children", "子嗣 {0}", character.ChildrenIds.Count));
            }

            return builder.ToString();
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            return id.Length <= 6 ? id : id.Substring(0, 6);
        }
    }
}
