using System;
using System.Collections.Generic;

namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WriteRewards(RewardsContentModule module, ContentReferenceResolver references, ContentKind? owner,
            List<(int order, Rule rule)> result, string sourceId)
        {
            if (module == null) throw new InvalidOperationException(sourceId + "：缺少奖励与失败惩罚模块");
            if (!module.Enabled) return;
            var rewardOwners = new[] { ContentKind.Technology, ContentKind.Quest, ContentKind.Expedition,
                ContentKind.Enemy, ContentKind.Opportunity, ContentKind.Loot };

            void Write<T>(T[] entries, string label, Func<T, Rule> compile, params ContentKind[] allowedOwners)
                where T : OrderedContentEntry
            {
                if (entries == null) throw new InvalidOperationException(sourceId + " / " + label + "：列表为空引用");
                for (int index = 0; index < entries.Length; index++)
                {
                    var context = sourceId + " / 奖励与失败惩罚 / " + label + "[" + index + "]";
                    try
                    {
                        var entry = entries[index];
                        if (entry == null) throw new InvalidOperationException("条目为空引用");
                        if (owner.HasValue && Array.IndexOf(allowedOwners, owner.Value) < 0)
                            throw new InvalidOperationException(owner + " 不支持该奖励来源");
                        var rule = compile(entry);
                        RewardAuthoringValidation.Validate(rule, references.Definitions[rule.Target]);
                        Add(result, entry, rule);
                    }
                    catch (InvalidOperationException error)
                    {
                        throw new InvalidOperationException(context + "：" + error.Message, error);
                    }
                }
            }

            Write(module.Items, "物品奖励", entry => new Rule { Kind = RuleKind.RewardItem,
                Target = references.Resolve(entry.Item, false, "物品", ContentKind.Item), Secondary = -1, Amount = entry.Quantity }, rewardOwners);
            Write(module.Blueprints, "蓝图奖励", entry => new Rule { Kind = RuleKind.RewardBlueprint,
                Target = references.Resolve(entry.Building, false, "建筑", ContentKind.Building), Secondary = -1, Amount = entry.GrantedLevel }, rewardOwners);
            Write(module.Buffs, "增益奖励", entry => new Rule { Kind = RuleKind.RewardBuff,
                Target = references.Resolve(entry.Buff, false, "增益", ContentKind.Buff), Secondary = -1, Amount = entry.GrantedLevel }, rewardOwners);
            Write(module.Features, "功能许可", entry => new Rule { Kind = RuleKind.RewardFeature,
                Target = references.Resolve(entry.Feature, false, "功能", ContentKind.Feature), Secondary = -1, Amount = entry.GrantedLevel }, rewardOwners);
            Write(module.Failures, "失败扣除物品", entry => new Rule { Kind = RuleKind.FailureItem,
                Target = references.Resolve(entry.Item, false, "物品", ContentKind.Item), Secondary = -1, Amount = entry.Quantity }, ContentKind.Quest, ContentKind.Expedition);
            Write(module.SpecialDrops, "可点击特殊掉落", entry =>
            {
                if (!Enum.IsDefined(typeof(DropRarity), entry.Rarity)) throw new InvalidOperationException("稀有度枚举值无效");
                return new Rule { Kind = RuleKind.SpecialDrop, Target = references.Resolve(entry.Item, false, "物品", ContentKind.Item),
                    Secondary = -1, Amount = entry.Quantity, B = (int)entry.Rarity };
            }, ContentKind.Enemy);
        }
    }
}
