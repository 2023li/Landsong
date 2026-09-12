using System;

namespace Landsong.ECS.Authoring
{
    /// <summary>所有作者奖励来源共用的类型与数值约束；授权范围复用运行核心规则。</summary>
    public static class RewardAuthoringValidation
    {
        public static void Validate(Rule rule, ContentSource target)
        {
            if (target == null) throw new InvalidOperationException("奖励目标没有定义");
            ContentKind expected;
            switch (rule.Kind)
            {
                case RuleKind.RewardItem:
                case RuleKind.FailureItem:
                case RuleKind.SpecialDrop:
                    expected = ContentKind.Item;
                    break;
                case RuleKind.RewardBlueprint: expected = ContentKind.Building; break;
                case RuleKind.RewardBuff: expected = ContentKind.Buff; break;
                case RuleKind.RewardFeature: expected = ContentKind.Feature; break;
                default: throw new InvalidOperationException("不是支持的奖励规则：" + rule.Kind);
            }
            if (target.Kind != expected)
                throw new InvalidOperationException("目标 " + target.Id + " 的内容类型必须为 " + expected);
            var error = expected == ContentKind.Item
                ? rule.Amount > 0 ? null : "物品数量必须为正整数"
                : EntitlementRules.Error(target.Kind, target.Id, target.Level, target.Flags, rule.Amount);
            if (error != null)
                throw new InvalidOperationException("目标 " + target.Id + "，数值 " + rule.Amount + "：" + error);
        }
    }
}
