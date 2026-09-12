#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class RewardAuthoringVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException("FAIL " + message);
            checks++; log.AppendLine("PASS " + message);
        }
        static void Reject(Action action, string message, params string[] diagnosticParts)
        {
            InvalidOperationException rejection = null;
            try { action(); } catch (InvalidOperationException error) { rejection = error; }
            Check(rejection != null && diagnosticParts.All(part => rejection.Message.Contains(part)), message);
        }
        [MenuItem("Landsong/ECS/Verification/RewardAuthoring")]
        public static string Run()
        {
            log = new StringBuilder(); checks = 0;
            using var fixture = new Fixture();
            try
            {
                AllRewardSources(fixture); TalentGrants(fixture); PeriodicIncome(fixture); PeriodicEffectIncome(fixture);
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/reward-authoring-verification.txt", log.ToString());
            }
        }

        static void AllRewardSources(Fixture f)
        {
            var owners = new ContentKind?[] { null, ContentKind.Technology, ContentKind.Quest, ContentKind.Expedition,
                ContentKind.Enemy, ContentKind.Opportunity, ContentKind.Loot };
            foreach (var owner in owners)
            {
                var source = new ContentSource { Id = "reward-source-" + owner, Kind = owner ?? ContentKind.Technology };
                Rule[] Compile(RewardsContentModule module)
                {
                    if (!owner.HasValue) return ContentModuleCompiler.CompileRewards(module, f.References);
                    source.Configuration.Rewards = module;
                    return ContentModuleCompiler.Compile(source, f.References);
                }
                string label = owner?.ToString() ?? "Starting";
                string context = owner.HasValue ? source.Id : "开局发放";
                var valid = new RewardsContentModule { Enabled = true,
                    Items = new[] { new ItemsReward { Item = f.Item, Quantity = 7, Order = 40 } },
                    Blueprints = new[] { new BlueprintsReward { Building = f.Building, GrantedLevel = 3, Order = 10 } },
                    Buffs = new[] { new BuffsReward { Buff = f.Buff, GrantedLevel = int.MaxValue, Order = 30 } },
                    Features = new[] { new FeaturesReward { Feature = f.Feature, GrantedLevel = 1, Order = 20 } } };
                var rules = Compile(valid);
                Check(rules.Select(rule => rule.Kind).SequenceEqual(new[] { RuleKind.RewardBlueprint, RuleKind.RewardFeature, RuleKind.RewardBuff, RuleKind.RewardItem })
                    && rules.Select(rule => rule.Amount).SequenceEqual(new[] { 3, 1, int.MaxValue, 7 }),
                    label + " preserves explicit ordering and does not apply building limits to Buff levels");
                foreach (int level in new[] { 0, -1, 4 })
                {
                    var invalid = new RewardsContentModule { Enabled = true, Blueprints = new[] { new BlueprintsReward { Building = f.Building, GrantedLevel = level } } };
                    Reject(() => Compile(invalid), label + " rejects invalid blueprint level " + level, context, "蓝图奖励[0]", f.Building.Data.Id, level.ToString());
                }
                foreach (int quantity in new[] { 0, -1 })
                {
                    Reject(() => Compile(new RewardsContentModule { Enabled = true, Items = new[] { new ItemsReward { Item = f.Item, Quantity = quantity } } }), label + " rejects item quantity " + quantity);
                    Reject(() => Compile(new RewardsContentModule { Enabled = true, Buffs = new[] { new BuffsReward { Buff = f.Buff, GrantedLevel = quantity } } }), label + " rejects Buff level " + quantity);
                    Reject(() => Compile(new RewardsContentModule { Enabled = true, Features = new[] { new FeaturesReward { Feature = f.Feature, GrantedLevel = quantity } } }), label + " rejects feature level " + quantity);
                }
                Reject(() => Compile(new RewardsContentModule { Enabled = true, Features = new[] { new FeaturesReward { Feature = f.Feature, GrantedLevel = 2 } } }), label + " treats feature permission as a boolean");
                Reject(() => Compile(new RewardsContentModule { Enabled = true, Features = new[] { new FeaturesReward { Feature = f.Limit } } }), label + " does not unlock a building limit group");
                Reject(() => Compile(new RewardsContentModule { Enabled = true, Blueprints = new[] { new BlueprintsReward { Building = f.Item } } }), label + " rejects an item used as a blueprint target");
                Reject(() => Compile(new RewardsContentModule { Enabled = true, Blueprints = new[] { new BlueprintsReward() } }), label + " requires an explicitly registered target");
                Reject(() => Compile(new RewardsContentModule { Enabled = true, Items = new[] { new ItemsReward { Item = f.ForeignItem } } }), label + " does not accept an unregistered asset with the same content ID");
            }
            foreach (var owner in new[] { ContentKind.Quest, ContentKind.Expedition })
            {
                var source = new ContentSource { Id = "penalty", Kind = owner };
                source.Configuration.Rewards = new RewardsContentModule { Enabled = true, Failures = new[] { new ItemPenalty { Item = f.Item, Quantity = 0 } } };
                Reject(() => ContentModuleCompiler.Compile(source, f.References), owner + " rejects zero item penalties");
            }
            var enemy = new ContentSource { Id = "enemy", Kind = ContentKind.Enemy };
            enemy.Configuration.Rewards = new RewardsContentModule { Enabled = true, SpecialDrops = new[] { new SpecialDropReward { Item = f.Item, Quantity = 0, Rarity = DropRarity.普通 } } };
            Reject(() => ContentModuleCompiler.Compile(enemy, f.References), "Special drops reject zero quantity before staging any reward");
        }

        static void TalentGrants(Fixture f)
        {
            var source = new ContentSource { Id = "talent-grant", Kind = ContentKind.Talent, Level = 1, Capacity = 3 };
            TalentJobEffect Grant(GameDefinitionAsset target) => new TalentJobEffect { Subject = target, Effect = TalentEffectType.内容许可,
                Timing = TalentEffectTiming.每回合, Scaling = TalentEffectScaling.固定, BaseValue = 1 };
            Rule[] Compile(TalentJobEffect effect)
            {
                source.Configuration.People = new PeopleContentModule { Enabled = true, Effects = new[] { effect } };
                return ContentModuleCompiler.Compile(source, f.References);
            }
            var fixedGrowth = Grant(f.Building); fixedGrowth.PerLevel = 1;
            Check(Compile(fixedGrowth).Single().Extra == 1, "Talent blueprint growth validates the complete reachable level interval");
            var scaled = Grant(f.Building); scaled.Scaling = TalentEffectScaling.人才等级;
            Check(Compile(scaled).Single().C == (int)TalentEffectScaling.人才等级, "Talent-level scaling remains supported within blueprint limits");
            var buff = Grant(f.Buff); buff.BaseValue = 100;
            Check(Compile(buff).Single().Value == 100, "Talent Buff grants are not capped by the Buff definition Level field");
            foreach (float value in new[] { 0, -.5f, .99f, float.NaN, float.PositiveInfinity, 1e30f })
            {
                var effect = Grant(f.Building); effect.BaseValue = value;
                Reject(() => Compile(effect), "Talent rejects invalid effective grant " + value);
            }
            foreach (var target in new[] { f.Item, f.Technology, f.Quest, f.Expedition, f.Limit })
                Reject(() => Compile(Grant(target)), "Talent cannot issue unsupported grant target " + target.Data.Id);
            Reject(() => Compile(Grant(null)), "Talent content grants require a target");
            foreach (var scaling in new[] { TalentEffectScaling.每百份物品, TalentEffectScaling.王国人口, TalentEffectScaling.运营建筑数 })
            {
                var effect = Grant(f.Building); effect.Scaling = scaling;
                Reject(() => Compile(effect), "Talent rejects ambiguous grant scaling " + scaling);
            }
            var passive = Grant(f.Building); passive.Timing = TalentEffectTiming.被动;
            Reject(() => Compile(passive), "Content ownership is not repeatedly issued by a passive effect");
            source.Kind = ContentKind.TalentSlot;
            Reject(() => Compile(Grant(f.Building)), "Unimplemented slot-owned periodic grants fail compilation");
            source.Kind = ContentKind.Talent;
            var featureGrowth = Grant(f.Feature); featureGrowth.PerLevel = 1;
            Reject(() => Compile(featureGrowth), "Feature grant stays boolean at later talent levels");
            var decreasing = Grant(f.Building); decreasing.BaseValue = 2; decreasing.PerLevel = -1;
            Reject(() => Compile(decreasing), "Talent grants cannot become zero at a later level");
            source.Capacity = 7; f.Building.Data.Level = 12;
            var curved = Grant(f.Building); curved.BaseValue = 7; curved.PerLevel = -1; curved.Scaling = TalentEffectScaling.人才等级;
            Reject(() => Compile(curved), "Quadratic level scaling checks its interior peak, not just valid endpoints");
            f.Building.Data.Level = 3;
        }

        static void PeriodicIncome(Fixture f)
        {
            var source = new ContentSource { Id = "periodic-income", Kind = ContentKind.Talent, Level = 1, Capacity = 3 };
            Rule[] Compile(int amount, float perLevel)
            {
                source.Configuration.People = new PeopleContentModule { Enabled = true,
                    PeriodicItems = new[] { new TalentItemIncome { Item = f.Item, BaseQuantity = amount, PerLevel = perLevel } } };
                return ContentModuleCompiler.Compile(source, f.References);
            }
            Check(Compile(0, 0).Single().Amount == 0, "Zero periodic income remains an explicit no-op");
            Check(Compile(0, 1).Single().Extra == 1, "Zero initial periodic income can grow at later talent levels");
            Reject(() => Compile(-1, 1), "Negative income at the initial level is rejected");
            Reject(() => Compile(1, -1), "Negative income at a later level is rejected");
            Reject(() => Compile(int.MaxValue, 1), "Periodic income cannot overflow at a later level");
        }

        static void PeriodicEffectIncome(Fixture f)
        {
            var source = new ContentSource { Id = "periodic-effect-income", Kind = ContentKind.Talent, Level = 1, Capacity = 3 };
            Rule[] Compile(TalentJobEffect effect)
            {
                source.Configuration.People = new PeopleContentModule { Enabled = true, Effects = new[] { effect } };
                return ContentModuleCompiler.Compile(source, f.References);
            }
            foreach (var kind in new[] { TalentEffectType.物品, TalentEffectType.科研点 })
            {
                TalentJobEffect Income(float value, float growth = 0, TalentEffectScaling scaling = TalentEffectScaling.固定)
                    => new TalentJobEffect { Effect = kind, Timing = TalentEffectTiming.每回合, Scaling = scaling,
                        Subject = kind == TalentEffectType.物品 ? f.Item : null, BaseValue = value, PerLevel = growth };
                Check(Compile(Income(0)).Single().Value == 0, kind + " permits zero periodic effect income");
                Check(Compile(Income(.5f)).Single().Value == .5f, kind + " permits positive fractional income that floors to zero");
                Check(Compile(Income(0, 1)).Single().Extra == 1, kind + " permits nonnegative growth from zero");
                foreach (float value in new[] { -1, -.01f, float.NaN, float.PositiveInfinity, 1e30f })
                    Reject(() => Compile(Income(value)), kind + " rejects invalid periodic effect income " + value, source.Id);
                Reject(() => Compile(Income(1, -1)), kind + " rejects income becoming negative at a later level", source.Id, "周期收益");
                Reject(() => Compile(Income(1e9f, scaling: TalentEffectScaling.人才等级)),
                    kind + " rejects talent-level quantity overflow", source.Id, "周期收益");

                source.Capacity = 7;
                Reject(() => Compile(Income(1.05e9f, -1.5e8f, TalentEffectScaling.人才等级)),
                    kind + " rejects an overflowing quadratic interior peak despite valid endpoints", source.Id, "周期收益");
                source.Capacity = 3;
                foreach (var scaling in new[] { TalentEffectScaling.每百份物品, TalentEffectScaling.王国人口, TalentEffectScaling.运营建筑数 })
                {
                    // Item outputs use their Subject as the output item, so building-count scaling is not supported.
                    if (kind == TalentEffectType.物品 && scaling == TalentEffectScaling.运营建筑数) continue;
                    var income = Income(0, 1, scaling);
                    if (scaling == TalentEffectScaling.每百份物品) income.Subject = f.Item;
                    if (scaling == TalentEffectScaling.运营建筑数) income.Subject = f.Building;
                    Check(Compile(income).Single().Extra == 1, kind + " permits nonnegative counted coefficient " + scaling);
                    income.BaseValue = -.01f; income.PerLevel = 0;
                    Reject(() => Compile(income), kind + " rejects a negative counted coefficient " + scaling, source.Id, "收益系数");
                    income.BaseValue = 1; income.PerLevel = -1;
                    Reject(() => Compile(income), kind + " rejects a counted coefficient becoming negative " + scaling, source.Id, "收益系数");
                    income.BaseValue = float.MaxValue; income.PerLevel = float.MaxValue;
                    Reject(() => Compile(income), kind + " rejects non-finite coefficient growth " + scaling, source.Id, "收益系数");
                }
            }
            foreach (var owner in new[] { ContentKind.Talent, ContentKind.TalentSlot })
            {
                source.Kind = owner;
                var passive = new TalentJobEffect { Effect = TalentEffectType.生产百分比, Timing = TalentEffectTiming.被动,
                    Scaling = TalentEffectScaling.固定, BaseValue = -10, PerLevel = -1 };
                Check(Compile(passive).Single().Value == -10, owner + " retains legitimate negative passive modifiers");
            }
        }

        sealed class Fixture : IDisposable
        {
            readonly GameCatalogAsset catalog;
            public GameDefinitionAsset Item, Building, Buff, Feature, Limit, Technology, Quest, Expedition, ForeignItem;
            public ContentReferenceResolver References;
            public Fixture()
            {
                catalog = ScriptableObject.CreateInstance<GameCatalogAsset>();
                GameDefinitionAsset Create(string id, ContentKind kind, int level = 1)
                {
                    var asset = ScriptableObject.CreateInstance<GameDefinitionAsset>();
                    asset.Data = new ContentSource { Id = id, Name = id, Kind = kind, Level = level };
                    return asset;
                }
                Item = Create("item", ContentKind.Item); Building = Create("building", ContentKind.Building, 3);
                Buff = Create("buff", ContentKind.Buff); Feature = Create("feature.Building", ContentKind.Feature);
                Limit = Create("limit.houses", ContentKind.Feature); Technology = Create("tech", ContentKind.Technology);
                Quest = Create("quest", ContentKind.Quest); Expedition = Create("expedition", ContentKind.Expedition);
                ForeignItem = Create("item", ContentKind.Item);
                catalog.Definitions = new[] { Item, Building, Buff, Feature, Limit, Technology, Quest, Expedition };
                References = new ContentReferenceResolver(catalog);
            }
            public void Dispose()
            {
                foreach (var asset in catalog.Definitions) UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(ForeignItem); UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }
}
#endif
