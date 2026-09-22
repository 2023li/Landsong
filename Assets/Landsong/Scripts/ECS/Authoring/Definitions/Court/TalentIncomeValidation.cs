using System;
using System.Collections.Generic;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentIncomeValidation
    {
        public static void Validate(TalentDefinitionAsset source)
        {
            if (source.InitialLevel < 1 || source.MaximumLevel < source.InitialLevel)
                throw new InvalidOperationException("人才等级范围无效。");
            var income = source.PeriodicIncome ?? throw new InvalidOperationException("缺少人才周期收益配置。");
            foreach (var row in income.Items)
            {
                if (row == null)
                    throw new InvalidOperationException("人才周期收益存在空条目。");
                foreach (int level in new[]
                {
                    source.InitialLevel,
                    source.MaximumLevel
                }

                )
                {
                    float increment = row.PerLevel * (level - 1);
                    double quantity = row.BaseQuantity + Math.Truncate((double)increment);
                    if (float.IsNaN(increment) || float.IsInfinity(increment) || (double)increment < int.MinValue || (double)increment > int.MaxValue || quantity < 0 || quantity > int.MaxValue)
                        throw new InvalidOperationException(source.Metadata.Id + "：人才每回合产物超出数量范围。");
                }
            }

            foreach (var row in income.ScaledItems)
                Numeric(source, row.BaseQuantity, row.PerLevel, row.Scaling, false, 0);
            foreach (var row in income.ResearchPoints)
                Numeric(source, row.BasePoints, row.PerLevel, row.Scaling, false, 0);
            foreach (var row in income.Blueprints)
            {
                if (row.Building == null)
                    throw new InvalidOperationException("蓝图周期收益缺少建筑。");
                Numeric(source, row.BaseLevel, row.PerLevel, row.Scaling, true, row.Building.MaximumLevel);
            }

            foreach (var row in income.Buffs)
                Numeric(source, row.BaseLevel, row.PerLevel, row.Scaling, true, int.MaxValue);
            foreach (var row in income.Features)
                Numeric(source, row.BaseLevel, row.PerLevel, row.Scaling, true, 1);
        }

        static void Numeric(TalentDefinitionAsset owner, float initial, float growth, TalentEffectScalingSource scaling, bool permission, int maximum)
        {
            if (scaling == null || !Enum.IsDefined(typeof(TalentScalingKind), scaling.Kind))
                throw new InvalidOperationException("人才缩放方式无效。");
            if (permission && scaling.Kind != TalentScalingKind.Fixed && scaling.Kind != TalentScalingKind.TalentLevel)
                throw new InvalidOperationException(owner.Metadata.Id + "：许可等级只支持固定或人才等级缩放。");
            bool counted = scaling.Kind == TalentScalingKind.PerHundredItems || scaling.Kind == TalentScalingKind.KingdomPopulation || scaling.Kind == TalentScalingKind.OperatingBuildings;
            foreach (int level in Levels(owner, initial, growth, scaling.Kind))
            {
                float value = initial + growth * (level - 1);
                if (scaling.Kind == TalentScalingKind.TalentLevel)
                    value *= level;
                double amount = counted ? value : Math.Floor(value);
                if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < (permission ? 1 : 0) || !counted && amount > (permission ? maximum : int.MaxValue))
                    throw new InvalidOperationException(owner.Metadata.Id + "：人才等级 " + level + " 计算出无效" + (counted ? "收益系数" : "周期收益") + value);
            }
        }

        static HashSet<int> Levels(TalentDefinitionAsset source, float initial, float growth, TalentScalingKind scaling)
        {
            var levels = new HashSet<int>
            {
                source.InitialLevel,
                source.MaximumLevel
            };
            if (scaling == TalentScalingKind.TalentLevel && growth != 0)
            {
                double vertex = ((double)growth - initial) / (2d * growth);
                if (vertex > source.InitialLevel && vertex < source.MaximumLevel)
                {
                    int at = (int)Math.Floor(vertex);
                    levels.Add(at);
                    levels.Add(at + 1);
                }
            }

            return levels;
        }
    }
}
