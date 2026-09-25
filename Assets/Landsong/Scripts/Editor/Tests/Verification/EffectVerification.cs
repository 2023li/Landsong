#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class EffectVerification
    {
        static StringBuilder log;
        static int assertions;
        static void Check(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException("FAIL " + description);
            assertions++;
            log.AppendLine("PASS " + description);
        }

        static bool Near(float left, float right) => math.abs(left - right) < .0001f;
        [MenuItem("Landsong/ECS/Verification/Effects")]
        public static string Run()
        {
            log = new StringBuilder();
            assertions = 0;
            try
            {
                Configuration();
                TalentEffects();
                Sources();
                ProductionSettlement();
                log.AppendLine("Assertions: " + assertions);
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
                File.WriteAllText("Library/LandsongEcs/effect-verification.txt", log.ToString());
            }
        }

        static void Configuration()
        {
            using var f = new DefinitionAuthoringFixture();
            var effects = new DefinitionEffectsSource
            {
                FlatProduction = new[]
                {
                    new FlatProductionEffectSource
                    {
                        Item = f.Item,
                        Quantity = 3
                    }
                }
            };
            using (var blob = f.Compile(effects))
                Check(!blob.Value.FlatProduction[0].Building.IsValid, "Empty flat-production building remains an explicit wildcard");
            foreach (var owner in new (string name, Action action)[]
            {
                ("Buff", () =>
                {
                    f.Buff.Effects = effects;
                    using var blob = f.CompileBuff();
                }),
                ("Policy", () =>
                {
                    f.Policy.Effects = effects;
                    using var blob = f.CompilePolicy();
                }),
                ("Talent", () =>
                {
                    f.Talent.Effects = effects;
                    using var blob = f.CompileTalent();
                }),
                ("TalentSlot", () =>
                {
                    f.TalentSlot.Effects = effects;
                    using var blob = f.CompileTalentSlot();
                }),
                ("RoyalTrait", () =>
                {
                    f.RoyalTrait.Effects = effects;
                    using var blob = f.CompileRoyalTrait();
                })
            }

            )
            {
                owner.action();
                Check(true, "Flat production has an executable source: " + owner.name);
            }

            void Reject(DefinitionEffectsSource value, string message)
            {
                bool failed = false;
                try
                {
                    using var blob = f.Compile(value);
                }
                catch (InvalidOperationException)
                {
                    failed = true;
                }

                Check(failed, message);
            }

            Check(typeof(ItemNumericEffectSource).GetField("Target").FieldType == typeof(ItemDefinitionAsset), "Item production cannot assign a building recipient");
            Check(typeof(KingdomNumericEffectSource).GetField("Target") == null, "Kingdom research has no unused target field");
            Reject(new DefinitionEffectsSource { Buildings = new[] { new BuildingNumericEffectSource { Target = f.Building, Effect = NumericEffectKind.HealthMultiplier, Magnitude = .1f } } }, "Building health is rejected because no runtime path consumes it");
            using (var blob = f.Compile(new DefinitionEffectsSource { Buildings = new[] { new BuildingNumericEffectSource { Target = f.Building, Effect = NumericEffectKind.Armor, Magnitude = 2 } } }))
                Check(blob.Value.Buildings[0].Target == BuildingId.FromIndex(0), "Building armor remains configurable for the frozen defensive profile");
            foreach (NumericEffectKind kind in Enum.GetValues(typeof(NumericEffectKind)))
            {
                bool accepted = kind == NumericEffectKind.LossMultiplier || kind == NumericEffectKind.ProductionMultiplier || kind == NumericEffectKind.CropHarvestMultiplier;
                var value = new DefinitionEffectsSource
                {
                    Items = new[]
                    {
                        new ItemNumericEffectSource
                        {
                            Effect = kind,
                            Magnitude = .1f
                        }
                    }
                };
                if (accepted)
                {
                    using var blob = f.Compile(value);
                    Check(true, "Executable item effect accepted: " + kind);
                }
                else
                    Reject(value, "Unconsumed item effect rejected: " + kind);
            }

            f.TalentSlot.Effects = new DefinitionEffectsSource
            {
                Intelligence = new[]
                {
                    new IntelligenceEffectSource
                    {
                        Points = 1
                    }
                }
            };
            bool failed = false;
            try
            {
                using var blob = f.CompileTalentSlot();
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed, "Talent slots reject intelligence at their compiler boundary");
            f.Policy.Effects = new DefinitionEffectsSource
            {
                Intelligence = new[]
                {
                    new IntelligenceEffectSource
                    {
                        Level = 2,
                        Points = 1
                    }
                }
            };
            failed = false;
            try
            {
                using var blob = f.CompilePolicy();
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed, "Policy tier cannot masquerade as intelligence level");
            f.Buff.Effects = new DefinitionEffectsSource
            {
                Intelligence = new[]
                {
                    new IntelligenceEffectSource
                    {
                        Level = 2,
                        Points = 1
                    }
                }
            };
            using (var blob = f.CompileBuff())
                Check(blob.Value.Definitions[0].Effects.Intelligence[0].Level == 2, "Permanent Buff intelligence retains its authored current-level condition");
            f.Buff.Effects = new DefinitionEffectsSource
            {
                Soldiers = new[]
                {
                    new SoldierNumericEffectSource
                    {
                        Effect = NumericEffectKind.EquipmentBreakChanceMultiplier,
                        Magnitude = -.2f
                    }
                }
            };
            using (var blob = f.CompileBuff())
                Check(blob.Value.Definitions[0].Effects.Soldiers[0].Magnitude == -.2f,
                    "Buff can author a soldier equipment break chance modifier");
            f.Technology.Effects = new DefinitionEffectsSource
            {
                Soldiers = new[]
                {
                    new SoldierNumericEffectSource
                    {
                        Effect = NumericEffectKind.AttackMultiplier,
                        Magnitude = .1f
                    }
                }
            };
            using (var blob = f.CompileTechnology())
                Check(blob.Value.Definitions[0].Effects.Soldiers.Length == 1, "Technology supports executable military modifiers");
            f.Technology.Effects = new DefinitionEffectsSource
            {
                Kingdom = new[]
                {
                    new KingdomNumericEffectSource
                    {
                        Effect = KingdomEffectKind.ResearchOutput,
                        Magnitude = 1
                    }
                }
            };
            failed = false;
            try
            {
                using var blob = f.CompileTechnology();
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed, "Technology rejects kingdom economy effects with no consumer");
            f.Technology.Effects = new DefinitionEffectsSource
            {
                Intelligence = new[]
                {
                    new IntelligenceEffectSource
                    {
                        Level = 2,
                        Points = 10
                    }
                }
            };
            failed = false;
            try
            {
                using var blob = f.CompileTechnology();
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed, "Nonrepeatable technology rejects unreachable intelligence completion tiers");
            f.Technology.Repeatable = true;
            using (var blob = f.CompileTechnology())
                Check(true, "Repeatable technology supports higher intelligence tiers");
            f.Technology.Repeatable = false;
            f.Technology.Effects.Intelligence[0].Level = 1;
            using (var blob = f.CompileTechnology())
                Check(true, "Nonrepeatable technology accepts its first completion intelligence tier");
        }

        static void TalentEffects()
        {
            using var f = new DefinitionAuthoringFixture();
            void Reject(TalentJobEffectsSource effects, string message)
            {
                bool failed = false;
                try
                {
                    using var blob = f.Compile(effects);
                }
                catch (InvalidOperationException)
                {
                    failed = true;
                }

                Check(failed, message);
            }

            foreach (bool slot in new[]
            {
                false,
                true
            }

            )
            {
                foreach (NumericEffectKind kind in Enum.GetValues(typeof(NumericEffectKind)))
                {
                    var item = new TalentJobEffectsSource
                    {
                        Items = new[]
                        {
                            new TalentItemJobEffectSource
                            {
                                Recipient = f.Item,
                                Effect = kind,
                                BaseMagnitude = 1
                            }
                        }
                    };
                    var soldier = new TalentJobEffectsSource
                    {
                        Soldiers = new[]
                        {
                            new TalentSoldierJobEffectSource
                            {
                                Recipient = f.Soldier,
                                Effect = kind,
                                BaseMagnitude = 1
                            }
                        }
                    };
                    foreach (var row in new[]
                    {
                        (item, kind == NumericEffectKind.ProductionMultiplier, "item"),
                        (soldier, kind == NumericEffectKind.AttackMultiplier, "soldier")
                    }

                    )
                    {
                        string label = (slot ? "TalentSlot" : "Talent") + " / passive / " + row.Item3 + " / " + kind;
                        if (!row.Item2)
                        {
                            Reject(row.Item1, "Unconsumed job combination rejected: " + label);
                            continue;
                        }

                        if (slot)
                        {
                            f.TalentSlot.JobEffects = row.Item1;
                            using var blob = f.CompileTalentSlot();
                        }
                        else
                        {
                            f.Talent.JobEffects = row.Item1;
                            using var blob = f.CompileTalent();
                        }

                        Check(true, "Executable talent combination compiles: " + label);
                    }
                }

                foreach (KingdomEffectKind kind in Enum.GetValues(typeof(KingdomEffectKind)))
                {
                    var value = new TalentJobEffectsSource
                    {
                        Kingdom = new[]
                        {
                            new TalentKingdomJobEffectSource
                            {
                                Effect = kind,
                                BaseMagnitude = 1
                            }
                        }
                    };
                    if (kind == KingdomEffectKind.PublicOpinion)
                    {
                        using var blob = f.Compile(value);
                        Check(true, "Job public-opinion modifier is executable");
                    }
                    else
                        Reject(value, "Unconsumed kingdom job kind rejected: " + kind);
                }
            }

            Check(typeof(TalentJobEffectsSource).GetField("Buildings") == null, "Building job targets without a valid legacy effect are absent");
            Check(typeof(TalentSlotDefinitionAsset).GetField("PeriodicIncome") == null, "Slot-owned periodic income has no serializable entry point");
            Check(typeof(TalentJobEffectsSource).GetField("Blueprints") == null && typeof(TalentPeriodicIncomeSource).GetField("Soldiers") == null, "Passive permissions and periodic attack modifiers cannot be represented");
            f.Talent.JobEffects = new TalentJobEffectsSource();
            f.Talent.PeriodicIncome = new TalentPeriodicIncomeSource
            {
                ScaledItems = new[]
                {
                    new TalentScaledItemIncomeSource
                    {
                        BaseQuantity = 1
                    }
                }
            };
            bool failed = false;
            try
            {
                using var blob = f.CompileTalent();
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed, "Periodic item output requires an item");
            Check(typeof(TalentScaledItemIncomeSource).GetField("Item").FieldType == typeof(ItemDefinitionAsset), "Periodic item outputs cannot target a building");
            var itemEffect = new TalentItemJobEffectSource
            {
                Recipient = f.Item,
                Effect = NumericEffectKind.ProductionMultiplier,
                BaseMagnitude = 1,
                Scaling = new TalentEffectScalingSource
                {
                    Kind = TalentScalingKind.OperatingBuildings,
                    SourceBuilding = f.Building
                }
            };
            using (var blob = f.Compile(new TalentJobEffectsSource { Items = new[] { itemEffect } }))
                Check(blob.Value.Items[0].Scaling.SourceBuilding.IsValid && blob.Value.Items[0].Recipient.IsValid, "Recipient and counted building source are now separate typed references");
            var attack = new TalentSoldierJobEffectSource
            {
                Recipient = f.Soldier,
                Effect = NumericEffectKind.AttackMultiplier,
                BaseMagnitude = 1,
                Scaling = new TalentEffectScalingSource
                {
                    Kind = TalentScalingKind.PerHundredItems,
                    SourceItem = f.Item
                }
            };
            using (var blob = f.Compile(new TalentJobEffectsSource { Soldiers = new[] { attack } }))
                Check(blob.Value.Soldiers[0].Recipient.IsValid && blob.Value.Soldiers[0].Scaling.SourceItem.IsValid, "Recipient and stock source no longer share an ambiguous index");
            f.Talent.PeriodicIncome = new TalentPeriodicIncomeSource
            {
                ResearchPoints = new[]
                {
                    new TalentResearchIncomeSource
                    {
                        BasePoints = 1,
                        Scaling = new TalentEffectScalingSource
                        {
                            SourceItem = f.Item
                        }
                    }
                }
            };
            failed = false;
            try
            {
                using var blob = f.CompileTalent();
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed, "Fixed research rejects unused scaling source");
            f.Talent.PeriodicIncome.ResearchPoints[0].Scaling.Kind = TalentScalingKind.PerHundredItems;
            using (var blob = f.CompileTalent())
                Check(blob.Value.Definitions[0].PeriodicIncome.ResearchPoints[0].Scaling.SourceItem.IsValid, "Periodic research can scale with its explicit stock source");
            itemEffect.Recipient = null;
            itemEffect.Scaling.SourceBuilding = null;
            using (var blob = f.Compile(new TalentJobEffectsSource { Items = new[] { itemEffect } }))
                Check(!blob.Value.Items[0].Recipient.IsValid && !blob.Value.Items[0].Scaling.SourceBuilding.IsValid, "Global production can scale with all operational buildings");
        }

        static void ProductionSettlement()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("Effects actual production settlement", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var originalBuildings = em.GetComponentData<BuildingCatalog>(root);
                var originalBuffs = em.GetComponentData<BuffCatalog>(root);
                var originalItems = em.GetComponentData<ItemCatalog>(root);
                var core = Entity.Null;
                using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                    foreach (var building in buildings)
                        if (em.GetComponentData<BuildingHousingStats>(building).IsCore != 0)
                            core = building;
                Check(core != Entity.Null, "Production regression uses a genuinely baked operational core");
                var coreDefinition = em.GetComponentData<BuildingDefinitionRef>(core).Definition;
                ulong coreId = em.GetComponentData<Identity>(core).Id;
                var buff = BuffId.FromIndex(0);
                var item = em.GetComponentData<CurrencySettings>(root).Gold;
                PermanentBuffs.Grant(em, root, buff, 1);
                em.GetBuffer<PolicyChoice>(root).Clear();
                using (var people = WorldQueries.OrderedEntities<Talent>(em))
                    foreach (var person in people)
                    {
                        var talent = em.GetComponentData<Talent>(person);
                        talent.Recruited = talent.Paid = 0;
                        talent.Slot = default;
                        em.SetComponentData(person, talent);
                    }

                var king = CourtOps.Monarch(em);
                if (king != Entity.Null)
                    em.GetBuffer<TraitEntry>(king).Clear();
                foreach (var scenario in new[]
                {
                    (flat: -2, percentage: 0f, expected: 0, label: "flat reduction beyond base"),
                    (flat: -1, percentage: 0f, expected: 0, label: "flat reduction exactly to zero"),
                    (flat: 0, percentage: -2f, expected: 0, label: "percentage reduction beyond zero"),
                    (flat: 2, percentage: .5f, expected: 4, label: "flat then percentage then floor")
                }

                )
                {
                    using var content = new ProductionEffectFixture(coreDefinition, buff, item, scenario.flat, scenario.percentage);
                    try
                    {
                        content.Install(em, root);
                        using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                            foreach (var building in buildings)
                            {
                                var state = em.GetComponentData<Building>(building);
                                BuildingWorkforceState stateWorkforce = em.GetComponentData<BuildingWorkforceState>(building);
                                BuildingHousingState stateHousing = em.GetComponentData<BuildingHousingState>(building);
                                BuildingProductionState stateProduction = em.GetComponentData<BuildingProductionState>(building);
                                BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(building);
                                stateWorkforce.Workers = stateHousing.Population = stateProduction.Progress = 0;
                                stateFarming.Crop = default;
                                stateWorkforce.Subsidy = 0;
                                state.Stage = building == core ? LifeStage.Operational : LifeStage.Ruined;
                                {
                                    em.SetComponentData(building, state);
                                    em.SetComponentData(building, stateWorkforce);
                                    em.SetComponentData(building, stateHousing);
                                    em.SetComponentData(building, stateProduction);
                                    em.SetComponentData(building, stateFarming);
                                }

                                BuildingLevelConfiguration.Apply(em, root, building, false);
                            }

                        var inventory = em.GetBuffer<InventorySlot>(root);
                        for (int i = 0; i < inventory.Length; i++)
                        {
                            var slot = inventory[i];
                            slot.Item = default;
                            slot.Count = 0;
                            slot.LossRemainder = 0;
                            inventory[i] = slot;
                        }

                        em.GetBuffer<PendingItem>(root).Clear();
                        GameClock sessionClock = em.GetComponentData<GameClock>(root);
                        DaySettlementState sessionSettlement = em.GetComponentData<DaySettlementState>(root);
                        sessionSettlement.LastSettledTurn = 0;
                        {
                            em.SetComponentData(root, sessionClock);
                            em.SetComponentData(root, sessionSettlement);
                        }

                        // Isolate a production assertion from random royal events, while the economy
                        // itself still runs its real non-forecast settlement and inventory transaction.
                        em.SetComponentData(root, new CourtState { LastSettledTurn = sessionClock.Turn });
                        DailyEconomySettlement.Settle(em, root);
                        using var rows = em.GetBuffer<EconomyEntry>(root).ToNativeArray(Allocator.Temp);
                        var production = rows.ToArray().Where(row => row.Source == coreId && row.Reason == EconomyReason.Production).ToArray();
                        Check(em.GetComponentData<BuildingProductionState>(core).Progress == 0 && !production.Any(row => row.Note.ToString().Contains("产品放不下")), "Real settlement commits its cycle without a false capacity failure: " + scenario.label);
                        Check(InventoryOps.Count(em, root, item) == scenario.expected && production.Where(row => row.Item == item).Sum(row => row.Delta) == scenario.expected, "Actual inventory and production journal agree: " + scenario.label);
                    }
                    finally
                    {
                        em.SetComponentData(root, originalBuildings);
                        em.SetComponentData(root, originalBuffs);
                        em.SetComponentData(root, originalItems);
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Sources()
        {
            using var world = new World("Isolated effect provenance verification");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            using var fixture = new EffectSourceFixture(em, root);
            {
                em.AddComponentData(root, new Session { Phase = Phase.Day });
                em.AddComponentData(root, new GameClock() { Turn = 3 });
                em.AddComponentData(root, new SimulationControl() { });
                em.AddComponentData(root, new PopulationState() { });
                em.AddComponentData(root, new PublicOpinionState() { Value = 60 });
                em.AddComponentData(root, new ResearchState() { });
                em.AddComponentData(root, new ExpeditionPenaltyState() { });
                em.AddComponentData(root, new NightRuntimeState() { });
                em.AddComponentData(root, new DaySettlementState() { });
                em.AddComponentData(root, new RetryState() { });
                em.AddComponentData(root, new HeroSelection() { });
                em.AddComponentData(root, new BellState() { });
                em.AddComponentData(root, new IntelligenceModeState() { });
                em.AddComponentData(root, new PersistenceGate() { });
                em.AddComponentData(root, new SimulationRandomState() { State = 123 });
                em.AddComponentData(root, new IdentitySequence() { });
                em.AddComponentData(root, new DynastyIdentity() { });
            }

            em.AddComponentData(root, new CourtState());
            em.AddComponentData(root, new NightPlanState());
            em.AddBuffer<PreparedSoldier>(root);
            em.AddBuffer<OwnedBuff>(root).Add(new OwnedBuff { Buff = EffectSourceFixture.Buff, Level = 1 });
            em.AddBuffer<TechnologyProgress>(root).Add(new TechnologyProgress { Technology = EffectSourceFixture.Technology, Completions = 2 });
            em.AddBuffer<PolicyChoice>(root).Add(new PolicyChoice { Definition = EffectSourceFixture.Policy });
            Entity Person(ulong id, byte role)
            {
                var person = em.CreateEntity();
                em.AddComponentData(person, new Identity { Id = id, Name = "测试人物" });
                em.AddComponentData(person, new Royal { Alive = 1, Role = role, Age = 30 });
                em.AddBuffer<TraitEntry>(person);
                return person;
            }

            var employee = Person(101, 4);
            em.AddComponentData(employee, new TalentDefinitionRef { Definition = EffectSourceFixture.Talent });
            em.AddComponentData(employee, new Talent { Slot = EffectSourceFixture.Slot, Recruited = 1, Paid = 1, Level = 2 });
            em.GetBuffer<TraitEntry>(employee).Add(new TraitEntry { Definition = EffectSourceFixture.PersonalTrait, Active = 1, Revealed = 1 });
            var king = Person(201, 0);
            em.GetBuffer<TraitEntry>(king).Add(new TraitEntry { Definition = EffectSourceFixture.KingTrait, Active = 1, Revealed = 1 });
            var child = Person(202, 2);
            Entity Building(ulong id, BuildingId definition, int x)
            {
                var building = em.CreateEntity();
                em.AddComponentData(building, new Identity { Id = id, Name = "测试建筑" });
                em.AddComponentData(building, new BuildingDefinitionRef { Definition = definition });
                {
                    em.AddComponentData(building, new Building { Level = 1, Stage = LifeStage.Operational });
                    em.AddComponentData(building, new BuildingPlacementState() { Size = new int2(1), Cell = new int2(x, 0) });
                    em.AddComponentData(building, new BuildingAppearanceState() { });
                    em.AddComponentData(building, new BuildingConstructionState() { });
                    em.AddComponentData(building, new BuildingWorkforceState() { Workers = 2 });
                    em.AddComponentData(building, new BuildingHousingState() { });
                    em.AddComponentData(building, new BuildingProductionState() { });
                    em.AddComponentData(building, new BuildingFarmingState() { });
                    em.AddComponentData(building, new BuildingSanctumState() { });
                    em.AddComponentData(building, new BuildingGatheringState() { });
                    em.AddComponentData(building, new BuildingRecruitmentState() { });
                    em.AddComponentData(building, new BuildingMarketState() { });
                    em.AddComponentData(building, new BuildingExperienceState() { });
                    em.AddComponentData(building, new BuildingMaintenanceState() { Maintained = 1 });
                }

                {
                    em.AddComponentData(building, new BuildingHousingStats { });
                    em.AddComponentData(building, new BuildingWorkforceStats() { });
                    em.AddComponentData(building, new BuildingStorageStats() { });
                    em.AddComponentData(building, new BuildingGarrisonStats() { });
                    em.AddComponentData(building, new BuildingQuestStats() { });
                    em.AddComponentData(building, new BuildingIntelligenceStats() { });
                    em.AddComponentData(building, new BuildingSanctumStats() { RequiredWorkers = 2 });
                    em.AddComponentData(building, new BuildingRangeStats() { });
                    em.AddComponentData(building, new BuildingNavigationStats() { });
                    em.AddComponentData(building, new BuildingBellStats() { });
                }

                return building;
            }

            var target = Building(301, EffectSourceFixture.Target, 0);
            var aura = Building(302, EffectSourceFixture.Aura, 1);
            EffectQuote Flat() => ItemEffects.FlatProductionQuote(em, root, EffectSourceFixture.Item, EffectSourceFixture.Target);
            var quote = Flat();
            Check(Near(quote.Value, 53) && Near(quote.Value, quote.Sources.Sum(source => source.Applied)), "Wildcard and targeted flat production sum every authorized source and its explanation");
            Check(Near(ItemEffects.FlatProduction(em, root, EffectSourceFixture.Item, EffectSourceFixture.OtherBuilding), 50), "Specified building bonus excludes another building while wildcard remains");
            Check(ItemEffects.FlatProduction(em, root, ItemId.FromIndex(1), EffectSourceFixture.Target) == 0, "Item filter excludes an unrelated target");
            Check(Near(ItemEffects.Modifier(em, root, NumericEffectKind.ProductionMultiplier, EffectSourceFixture.Item), 1.6f), "Paid job passive effect and ordinary percentage sources share one sum");
            Check(KingdomEffects.Modifier(em, root, KingdomEffectKind.ResearchOutput) == 15, "Research includes buff, policy, talent, job and king trait");
            Check(Near(KingdomEffects.Modifier(em, root, KingdomEffectKind.PlotRisk), -.15f), "Plot risk includes the same global source set");
            Check(Near(PersonalRiskEffects.Modifier(em, root, child), -.10f) && Near(PersonalRiskEffects.Modifier(em, root, king), -.15f), "Natural risk keeps royal genes personal while combining global sources once");
            Check(Near(CourtOps.NaturalChance(em, root, child), CourtSettings.Default.DeathAdult * .90f), "Actual mortality calculation consumes the combined personal risk");
            Check(Near(PersonalRiskEffects.Modifier(em, root, employee), -.16f), "Employee personal trait is not counted twice through job and person paths");
            var talent = em.GetComponentData<Talent>(employee);
            talent.Paid = 0;
            em.SetComponentData(employee, talent);
            quote = Flat();
            Check(Near(quote.Value, 29) && quote.Sources.Any(source => source.Kind == EffectSourceKind.TalentSlot && source.Reason == "工资未支付" && source.Applied == 0), "Unpaid talent and job stop with a shared reason");
            talent.Paid = 1;
            talent.Slot = default;
            em.SetComponentData(employee, talent);
            Check(Near(Flat().Value, 29), "Leaving a job removes its dependent source contribution");
            talent.Slot = EffectSourceFixture.Slot;
            em.SetComponentData(employee, talent);
            var employeeTraits = em.GetBuffer<TraitEntry>(employee);
            var trait = employeeTraits[0];
            trait.Active = 0;
            employeeTraits[0] = trait;
            Check(Near(Flat().Value, 53), "Personal royal traits do not become employment-wide effects");
            trait.Active = 1;
            employeeTraits[0] = trait;
            var session = em.GetComponentData<Session>(root);
            GameClock sessionClock = em.GetComponentData<GameClock>(root);
            PublicOpinionState sessionOpinion = em.GetComponentData<PublicOpinionState>(root);
            sessionOpinion.Value = 0;
            {
                em.SetComponentData(root, session);
                em.SetComponentData(root, sessionClock);
                em.SetComponentData(root, sessionOpinion);
            }

            Check(Near(Flat().Value, 46) && em.GetBuffer<PolicyChoice>(root).Length == 1, "Policy threshold suspends contribution without deleting its selection");
            sessionOpinion.Value = 60;
            {
                em.SetComponentData(root, session);
                em.SetComponentData(root, sessionClock);
                em.SetComponentData(root, sessionOpinion);
            }

            em.GetBuffer<PolicyChoice>(root).Clear();
            Check(Near(Flat().Value, 46), "Policy cancellation removes its source");
            em.GetBuffer<PolicyChoice>(root).Add(new PolicyChoice { Definition = EffectSourceFixture.Policy });
            Check(IntelOps.Current(em, root) == 63, "Intelligence combines common plus exact current-level rules and real research completions");
            var ownedBuffs = em.GetBuffer<OwnedBuff>(root);
            ownedBuffs[0] = new OwnedBuff
            {
                Buff = EffectSourceFixture.Buff,
                Level = 2
            };
            Check(IntelOps.Current(em, root) == 73, "Level-two buff uses its level-two intelligence row instead of level one");
            quote = IntelligenceEffects.Query(em, root);
            Check(Near(quote.Value, quote.Sources.Sum(source => source.Applied)) && quote.Sources.Any(source => source.Kind == EffectSourceKind.Buff && source.Reason == "适用等级不符"), "Inactive intelligence tiers are explained by the same matching operation");
            em.GetBuffer<TechnologyProgress>(root).Clear();
            Check(IntelOps.Current(em, root) == 32, "Losing research completion disables technology and prerequisite-bound building intelligence");
            em.GetBuffer<TechnologyProgress>(root).Add(new TechnologyProgress { Technology = EffectSourceFixture.Technology, Completions = 2 });
            BuildingPlacementState buildingStatePlacement = em.GetComponentData<BuildingPlacementState>(aura);
            BuildingWorkforceState buildingStateWorkforce = em.GetComponentData<BuildingWorkforceState>(aura);
            BuildingMaintenanceState buildingStateMaintenance = em.GetComponentData<BuildingMaintenanceState>(aura);
            buildingStateWorkforce.Workers = 1;
            {
                em.SetComponentData(aura, buildingStatePlacement);
                em.SetComponentData(aura, buildingStateWorkforce);
                em.SetComponentData(aura, buildingStateMaintenance);
            }

            Check(IntelOps.Current(em, root) == 62, "Intelligence building worker requirement still gates its source");
            buildingStateWorkforce.Workers = 2;
            {
                em.SetComponentData(aura, buildingStatePlacement);
                em.SetComponentData(aura, buildingStateWorkforce);
                em.SetComponentData(aura, buildingStateMaintenance);
            }

            var current = SoldierCombatStats.Current(em, root, EffectSourceFixture.Soldier);
            Check(Near(current.Damage, 13), "Military preparation includes buff and completed technology through explicit soldier effects");
            Check(Near(SoldierEffects.Modifier(em, root, NumericEffectKind.EquipmentBreakChanceMultiplier, EffectSourceFixture.Soldier), -.2f)
                && Near(EquipmentOps.EffectiveBreakChance(.5f, SoldierEffects.Modifier(em, root, NumericEffectKind.EquipmentBreakChanceMultiplier, EffectSourceFixture.Soldier)), .4f),
                "Owned Buff reduces a wooden club's battle break chance from 50 to 40 percent");
            var plan = em.GetComponentData<NightPlanState>(root);
            plan.PreparedTurn = sessionClock.Turn;
            em.SetComponentData(root, plan);
            em.GetBuffer<PreparedSoldier>(root).Add(new PreparedSoldier { Definition = EffectSourceFixture.Soldier, Stats = current });
            session.Phase = Phase.Night;
            {
                em.SetComponentData(root, session);
                em.SetComponentData(root, sessionClock);
                em.SetComponentData(root, sessionOpinion);
            }

            buildingStateMaintenance.Maintained = 0;
            {
                em.SetComponentData(aura, buildingStatePlacement);
                em.SetComponentData(aura, buildingStateWorkforce);
                em.SetComponentData(aura, buildingStateMaintenance);
            }

            em.GetBuffer<OwnedBuff>(root).Clear();
            Check(Near(SoldierCombatStats.Current(em, root, EffectSourceFixture.Soldier).Damage, 12) && SoldierCombatStats.ForNight(em, root, EffectSourceFixture.Soldier).Equals(current), "Source loss changes the next military quote without modifying frozen night attributes");
            em.GetBuffer<OwnedBuff>(root).Add(new OwnedBuff { Buff = EffectSourceFixture.Buff, Level = 2 });
            buildingStateMaintenance.Maintained = 1;
            {
                em.SetComponentData(aura, buildingStatePlacement);
                em.SetComponentData(aura, buildingStateWorkforce);
                em.SetComponentData(aura, buildingStateMaintenance);
            }

            Check(SpatialOps.Quote(em, root, target, BuildingEnvironmentKind.Production).Value == 10, "Existing spatial contribution remains separate and active");
            var otherAura = Building(303, EffectSourceFixture.Aura, 2);
            Check(SpatialOps.Quote(em, root, target, BuildingEnvironmentKind.Production).Value == 10, "Same-kind highest spatial sources do not become a global additive buff");
            buildingStatePlacement.Cell = new int2(20, 0);
            {
                em.SetComponentData(aura, buildingStatePlacement);
                em.SetComponentData(aura, buildingStateWorkforce);
                em.SetComponentData(aura, buildingStateMaintenance);
            }

            var otherState = em.GetComponentData<Building>(otherAura);
            otherState.Stage = LifeStage.Ruined;
            em.SetComponentData(otherAura, otherState);
            Check(SpatialOps.Quote(em, root, target, BuildingEnvironmentKind.Production).Value == 0, "Moving or stopping spatial owners removes their effect naturally");
            uint random = em.GetComponentData<SimulationRandomState>(root).State;
            int grants = em.GetBuffer<OwnedBuff>(root).Length;
            for (int i = 0; i < 3; i++)
            {
                Flat();
                IntelligenceEffects.Query(em, root);
                SoldierCombatStats.Current(em, root, EffectSourceFixture.Soldier);
            }

            Check(em.GetComponentData<SimulationRandomState>(root).State == random && em.GetBuffer<OwnedBuff>(root).Length == grants, "Repeated explanations do not mutate randomness or grant state");
            em.GetBuffer<OwnedBuff>(root).Clear();
            em.GetBuffer<TechnologyProgress>(root).Clear();
            quote = IntelligenceEffects.Query(em, root);
            Check(quote.Sources.Count(source => source.Kind == EffectSourceKind.Buff && source.Reason == "尚未获得" && source.Applied == 0) == 3 && quote.Sources.Any(source => source.Kind == EffectSourceKind.Technology && source.Reason == "尚未获得" && source.Applied == 0), "Unowned buff and technology still explain every locked intelligence tier without applying one");
        }
    }
}
#endif
