#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    public static class RewardAuthoringVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool passed, string message)
        {
            if (!passed)
                throw new InvalidOperationException("FAIL " + message);
            checks++;
            log.AppendLine("PASS " + message);
        }

        static void Reject(Action action, string message)
        {
            bool failed = false;
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed, message);
        }

        [MenuItem("Landsong/ECS/Verification/RewardAuthoring")]
        public static string Run()
        {
            log = new StringBuilder();
            checks = 0;
            using var fixture = new DefinitionAuthoringFixture();
            try
            {
                AllRewardSources(fixture);
                TalentGrants(fixture);
                PeriodicIncome(fixture);
                PeriodicEffectIncome(fixture);
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/reward-authoring-verification.txt", log.ToString());
            }
        }

        static void AllRewardSources(DefinitionAuthoringFixture f)
        {
            var sources = new (string name, Action<DefinitionRewardsSource> compile)[]
            {
                ("Starting", value =>
                {
                    using var blob = f.Compile(value);
                }),
                ("Technology", value =>
                {
                    f.Technology.Rewards = value;
                    using var blob = f.CompileTechnology();
                }),
                ("Quest", value =>
                {
                    f.Quest.Rewards = value;
                    using var blob = f.CompileQuest();
                }),
                ("Expedition", value =>
                {
                    f.Expedition.Rewards = value;
                    using var blob = f.CompileExpedition();
                }),
                ("Enemy", value =>
                {
                    f.Enemy.KillRewards = value;
                    using var blob = f.CompileEnemy();
                }),
                ("Opportunity", value =>
                {
                    f.Opportunity.Rewards = value;
                    using var blob = f.CompileOpportunity();
                }),
                ("Loot", value =>
                {
                    f.Loot.Rewards = value;
                    using var blob = f.CompileLoot();
                })
            };
            foreach (var source in sources)
            {
                var valid = new DefinitionRewardsSource
                {
                    Items = new[]
                    {
                        new ItemAmountSource
                        {
                            Item = f.Item,
                            Quantity = 7,
                            Order = 40
                        }
                    },
                    Blueprints = new[]
                    {
                        new BlueprintRewardSource
                        {
                            Building = f.Building,
                            GrantedLevel = 3,
                            Order = 10
                        }
                    },
                    Buffs = new[]
                    {
                        new BuffRewardSource
                        {
                            Buff = f.Buff,
                            GrantedLevel = int.MaxValue,
                            Order = 30
                        }
                    },
                    Features = new[]
                    {
                        new FeatureRewardSource
                        {
                            Feature = f.Feature,
                            GrantedLevel = 1,
                            Order = 20
                        }
                    }
                };
                source.compile(valid);
                using (var blob = f.Compile(valid))
                {
                    var values = new[]
                    {
                        (blob.Value.Items[0].Order, blob.Value.Items[0].Quantity),
                        (blob.Value.Blueprints[0].Order, blob.Value.Blueprints[0].GrantedLevel),
                        (blob.Value.Buffs[0].Order, blob.Value.Buffs[0].GrantedLevel),
                        (blob.Value.Features[0].Order, blob.Value.Features[0].GrantedLevel)
                    };
                    Check(values.OrderBy(row => row.Order).Select(row => row.Item2).SequenceEqual(new[] { 3, 1, int.MaxValue, 7 }), source.name + " preserves explicit reward ordering and uncapped Buff levels");
                }

                foreach (int level in new[]
                {
                    0,
                    -1,
                    4
                }

                )
                    Reject(() => source.compile(new DefinitionRewardsSource { Blueprints = new[] { new BlueprintRewardSource { Building = f.Building, GrantedLevel = level } } }), source.name + " rejects invalid blueprint level " + level);
                foreach (int quantity in new[]
                {
                    0,
                    -1
                }

                )
                {
                    Reject(() => source.compile(new DefinitionRewardsSource { Items = new[] { new ItemAmountSource { Item = f.Item, Quantity = quantity } } }), source.name + " rejects item quantity " + quantity);
                    Reject(() => source.compile(new DefinitionRewardsSource { Buffs = new[] { new BuffRewardSource { Buff = f.Buff, GrantedLevel = quantity } } }), source.name + " rejects Buff level " + quantity);
                    Reject(() => source.compile(new DefinitionRewardsSource { Features = new[] { new FeatureRewardSource { Feature = f.Feature, GrantedLevel = quantity } } }), source.name + " rejects feature level " + quantity);
                }

                Reject(() => source.compile(new DefinitionRewardsSource { Features = new[] { new FeatureRewardSource { Feature = f.Feature, GrantedLevel = 2 } } }), source.name + " treats feature permission as boolean");
                Check(typeof(FeatureRewardSource).GetField("Feature").FieldType == typeof(FeatureDefinitionAsset), source.name + " cannot use a building limit group as a feature grant");
                Check(typeof(BlueprintRewardSource).GetField("Building").FieldType == typeof(BuildingDefinitionAsset), source.name + " excludes item targets at the serialized type boundary");
                Reject(() => source.compile(new DefinitionRewardsSource { Blueprints = new[] { new BlueprintRewardSource() } }), source.name + " requires an explicitly registered target");
                var foreign = f.Asset<ItemDefinitionAsset>();
                foreign.Metadata.Id = f.Item.Metadata.Id;
                Reject(() => source.compile(new DefinitionRewardsSource { Items = new[] { new ItemAmountSource { Item = foreign, Quantity = 1 } } }), source.name + " rejects an unregistered asset with a registered stable ID");
                source.compile(valid);
            }

            f.Quest.FailurePenalties = new[]
            {
                new ItemAmountSource
                {
                    Item = f.Item,
                    Quantity = 0
                }
            };
            Reject(() =>
            {
                using var blob = f.CompileQuest();
            }, "Quest rejects zero item penalties");
            f.Quest.FailurePenalties = Array.Empty<ItemAmountSource>();
            f.Expedition.FailurePenalties = new[]
            {
                new ItemAmountSource
                {
                    Item = f.Item,
                    Quantity = 0
                }
            };
            Reject(() =>
            {
                using var blob = f.CompileExpedition();
            }, "Expedition rejects zero item penalties");
            f.Expedition.FailurePenalties = Array.Empty<ItemAmountSource>();
            f.Enemy.SpecialDrops = new[]
            {
                new SpecialItemDropSource
                {
                    Item = f.Item,
                    Quantity = 0,
                    Rarity = SpecialDropRarity.Common
                }
            };
            Reject(() =>
            {
                using var blob = f.CompileEnemy();
            }, "Special drops reject zero quantities before baking");
            f.Enemy.SpecialDrops = Array.Empty<SpecialItemDropSource>();
        }

        static void TalentGrants(DefinitionAuthoringFixture f)
        {
            var source = f.Talent;
            source.PeriodicIncome = new TalentPeriodicIncomeSource();
            void Compile()
            {
                using var blob = f.CompileTalent();
            }

            TalentBlueprintIncomeSource Grant(float value = 1, float growth = 0, TalentScalingKind scaling = TalentScalingKind.Fixed) => new TalentBlueprintIncomeSource
            {
                Building = f.Building,
                BaseLevel = value,
                PerLevel = growth,
                Scaling = new TalentEffectScalingSource
                {
                    Kind = scaling
                }
            };
            void Set(TalentBlueprintIncomeSource value)
            {
                source.PeriodicIncome = new TalentPeriodicIncomeSource
                {
                    Blueprints = new[]
                    {
                        value
                    }
                };
            }

            Set(Grant(growth: 1));
            Compile();
            Check(true, "Talent blueprint growth validates the complete reachable level interval");
            Set(Grant(scaling: TalentScalingKind.TalentLevel));
            Compile();
            Check(true, "Talent-level scaling remains supported within blueprint limits");
            source.PeriodicIncome = new TalentPeriodicIncomeSource
            {
                Buffs = new[]
                {
                    new TalentBuffIncomeSource
                    {
                        Buff = f.Buff,
                        BaseLevel = 100
                    }
                }
            };
            Compile();
            Check(true, "Talent Buff grants are not capped by a definition level");
            foreach (float value in new[]
            {
                0,
                -.5f,
                .99f,
                float.NaN,
                float.PositiveInfinity,
                1e30f
            }

            )
            {
                Set(Grant(value));
                Reject(Compile, "Talent rejects invalid effective grant " + value);
            }

            Check(typeof(TalentBlueprintIncomeSource).GetField("Building").FieldType == typeof(BuildingDefinitionAsset) && typeof(TalentBuffIncomeSource).GetField("Buff").FieldType == typeof(BuffDefinitionAsset) && typeof(TalentFeatureIncomeSource).GetField("Feature").FieldType == typeof(FeatureDefinitionAsset), "Periodic permissions exclude items, technology, quest, expedition and limit-group targets by type");
            Set(Grant());
            source.PeriodicIncome.Blueprints[0].Building = null;
            Reject(Compile, "Talent permission requires a target");
            foreach (var kind in new[]
            {
                TalentScalingKind.PerHundredItems,
                TalentScalingKind.KingdomPopulation,
                TalentScalingKind.OperatingBuildings
            }

            )
            {
                Set(Grant(scaling: kind));
                Reject(Compile, "Permission rejects counted scaling " + kind);
            }

            Check(typeof(TalentJobEffectsSource).GetField("Blueprints") == null && typeof(TalentSlotDefinitionAsset).GetField("PeriodicIncome") == null, "Passive and slot-owned permissions have no serializable entry point");
            source.PeriodicIncome = new TalentPeriodicIncomeSource
            {
                Features = new[]
                {
                    new TalentFeatureIncomeSource
                    {
                        Feature = f.Feature,
                        BaseLevel = 1,
                        PerLevel = 1
                    }
                }
            };
            Reject(Compile, "Feature permission stays boolean at later talent levels");
            Set(Grant(2, -1));
            Reject(Compile, "Permission cannot become zero at a later level");
            source.MaximumLevel = 7;
            f.Building.MaximumLevel = 12;
            Set(Grant(7, -1, TalentScalingKind.TalentLevel));
            Reject(Compile, "Quadratic permission scaling validates its interior peak");
            source.MaximumLevel = 3;
            f.Building.MaximumLevel = 3;
            source.PeriodicIncome = new TalentPeriodicIncomeSource();
        }

        static void PeriodicIncome(DefinitionAuthoringFixture f)
        {
            void Compile(int amount, float growth)
            {
                f.Talent.PeriodicIncome = new TalentPeriodicIncomeSource
                {
                    Items = new[]
                    {
                        new TalentItemIncomeSource
                        {
                            Item = f.Item,
                            BaseQuantity = amount,
                            PerLevel = growth
                        }
                    }
                };
                using var blob = f.CompileTalent();
            }

            Compile(0, 0);
            Check(true, "Zero periodic income remains an explicit no-op");
            Compile(0, 1);
            Check(true, "Zero initial income may grow at later levels");
            Reject(() => Compile(-1, 1), "Negative initial income rejected");
            Reject(() => Compile(1, -1), "Negative later income rejected");
            Reject(() => Compile(int.MaxValue, 1), "Later periodic income overflow rejected");
            f.Talent.PeriodicIncome = new TalentPeriodicIncomeSource();
        }

        static void PeriodicEffectIncome(DefinitionAuthoringFixture f)
        {
            foreach (bool item in new[]
            {
                true,
                false
            }

            )
            {
                void Compile(float value, float growth = 0, TalentScalingKind kind = TalentScalingKind.Fixed)
                {
                    var scaling = new TalentEffectScalingSource
                    {
                        Kind = kind,
                        SourceItem = kind == TalentScalingKind.PerHundredItems ? f.Item : null,
                        SourceBuilding = kind == TalentScalingKind.OperatingBuildings ? f.Building : null
                    };
                    f.Talent.PeriodicIncome = item ? new TalentPeriodicIncomeSource
                    {
                        ScaledItems = new[]
                        {
                            new TalentScaledItemIncomeSource
                            {
                                Item = f.Item,
                                BaseQuantity = value,
                                PerLevel = growth,
                                Scaling = scaling
                            }
                        }
                    }

                    : new TalentPeriodicIncomeSource
                    {
                        ResearchPoints = new[]
                        {
                            new TalentResearchIncomeSource
                            {
                                BasePoints = value,
                                PerLevel = growth,
                                Scaling = scaling
                            }
                        }
                    };
                    using var blob = f.CompileTalent();
                }

                string label = item ? "Item" : "Research";
                Compile(0);
                Check(true, label + " permits zero income");
                Compile(.5f);
                Check(true, label + " permits fractional income flooring to zero");
                Compile(0, 1);
                Check(true, label + " permits nonnegative growth from zero");
                foreach (float value in new[]
                {
                    -1,
                    -.01f,
                    float.NaN,
                    float.PositiveInfinity,
                    1e30f
                }

                )
                    Reject(() => Compile(value), label + " rejects invalid periodic income " + value);
                Reject(() => Compile(1, -1), label + " rejects later negative income");
                Reject(() => Compile(1e9f, kind: TalentScalingKind.TalentLevel), label + " rejects talent-level overflow");
                f.Talent.MaximumLevel = 7;
                Reject(() => Compile(1.05e9f, -1.5e8f, TalentScalingKind.TalentLevel), label + " rejects quadratic interior overflow despite valid endpoints");
                f.Talent.MaximumLevel = 3;
                foreach (var kind in new[]
                {
                    TalentScalingKind.PerHundredItems,
                    TalentScalingKind.KingdomPopulation,
                    TalentScalingKind.OperatingBuildings
                }

                )
                {
                    Compile(0, 1, kind);
                    Check(true, label + " permits nonnegative counted coefficient " + kind);
                    Reject(() => Compile(-.01f, 0, kind), label + " rejects negative counted coefficient " + kind);
                    Reject(() => Compile(1, -1, kind), label + " rejects later negative coefficient " + kind);
                    Reject(() => Compile(float.MaxValue, float.MaxValue, kind), label + " rejects nonfinite coefficient growth " + kind);
                }
            }

            f.Talent.PeriodicIncome = new TalentPeriodicIncomeSource();
            var passive = new TalentJobEffectsSource
            {
                Items = new[]
                {
                    new TalentItemJobEffectSource
                    {
                        Effect = NumericEffectKind.ProductionMultiplier,
                        BaseMagnitude = -10,
                        PerLevel = -1
                    }
                }
            };
            f.Talent.JobEffects = passive;
            using (var blob = f.CompileTalent())
                Check(blob.Value.Definitions[0].JobEffects.Items[0].BaseMagnitude == -10, "Talent retains valid negative passive modifiers");
            f.TalentSlot.JobEffects = passive;
            using (var blob = f.CompileTalentSlot())
                Check(blob.Value.Definitions[0].JobEffects.Items[0].BaseMagnitude == -10, "Talent slot retains valid negative passive modifiers");
        }
    }
}
#endif
