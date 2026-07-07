using System.Text;
using Landsong.InheritanceSystem;

namespace Landsong.UISystem
{
    internal static class GamePanel_InheritanceText
    {
        public static string FormatRole(RoyalCharacterRole role)
        {
            return role switch
            {
                RoyalCharacterRole.King => "国王",
                RoyalCharacterRole.Queen => "王后",
                RoyalCharacterRole.Prince => "王子",
                _ => role.ToString()
            };
        }

        public static string FormatStatus(RoyalCharacterStatus status)
        {
            return status switch
            {
                RoyalCharacterStatus.Reigning => "在位",
                RoyalCharacterStatus.Consort => "王后",
                RoyalCharacterStatus.Heir => "继承人",
                RoyalCharacterStatus.Retired => "退位",
                RoyalCharacterStatus.Dead => "死亡",
                _ => status.ToString()
            };
        }

        public static string FormatCharacterLine(RoyalCharacterState character, int legalHeirAge)
        {
            if (character == null)
            {
                return "无";
            }

            var heirText = character.IsPotentialHeir
                ? (character.IsLegalHeir(legalHeirAge) ? "合法继承人" : $"未成年，{legalHeirAge} 岁成年")
                : FormatStatus(character.Status);
            return $"{character.DisplayName} / {FormatRole(character.Role)} / {heirText} / {character.Age}/{character.EffectiveMaxLifespan} 岁";
        }

        public static string FormatTraits(RoyalCharacterState character)
        {
            if (character == null || character.Traits.Count == 0)
            {
                return "无特性";
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
                    builder.Append("未知特性");
                    continue;
                }

                var state = trait.IsActive ? "生效" : "已发现";
                var acquired = trait.IsAcquired ? "后天" : "先天";
                builder.Append($"{trait.Definition.TraitName}（{state} / {acquired}）");
            }

            return builder.Length == 0 ? "无特性" : builder.ToString();
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
                builder.Append($"父 {ShortId(character.FatherId)}");
            }

            if (!string.IsNullOrWhiteSpace(character.MotherId))
            {
                if (builder.Length > 0)
                {
                    builder.Append(" / ");
                }

                builder.Append($"母 {ShortId(character.MotherId)}");
            }

            if (character.ChildrenIds.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" / ");
                }

                builder.Append($"子嗣 {character.ChildrenIds.Count}");
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
