#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
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
            if (!condition) throw new InvalidOperationException("FAIL " + description);
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
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/effect-verification.txt", log.ToString());
            }
        }

        static void Configuration()
        {
            var catalog = ScriptableObject.CreateInstance<GameCatalogAsset>();
            var item = ScriptableObject.CreateInstance<GameDefinitionAsset>();
            var building = ScriptableObject.CreateInstance<GameDefinitionAsset>();
            var owner = ScriptableObject.CreateInstance<GameDefinitionAsset>();
            try
            {
                item.Data = new ContentSource { Id = "effect.item", Kind = ContentKind.Item };
                building.Data = new ContentSource { Id = "effect.building", Kind = ContentKind.Building };
                owner.Data = new ContentSource { Id = "effect.owner", Kind = ContentKind.Buff };
                catalog.Definitions = new[] { item, building, owner };
                var resolver = new ContentReferenceResolver(catalog);
                Rule[] Compile() => ContentModuleCompiler.Compile(owner.Data, resolver);
                void Reject(Action configure, string description)
                {
                    configure();
                    bool failed = false;
                    try { Compile(); } catch (InvalidOperationException) { failed = true; }
                    Check(failed, description);
                }

                owner.Data.Configuration.Modifiers = new ModifiersContentModule
                    { Enabled = true, FlatProduction = new[] { new FlatProductionModifier { Item = item, Quantity = 3 } } };
                Check(Compile().Single().Secondary == -1, "Empty flat-production building compiles as an explicit wildcard");
                foreach (var kind in new[] { ContentKind.Buff, ContentKind.Policy, ContentKind.Talent, ContentKind.TalentSlot, ContentKind.RoyalTrait })
                {
                    owner.Data.Kind = kind;
                    Check(Compile().Single().Amount == 3, "Flat production has an executable source: " + kind);
                }
                owner.Data.Kind = ContentKind.Buff;
                Reject(() => owner.Data.Configuration.Modifiers = new ModifiersContentModule
                { Enabled = true, ProductionBonus = new[] { new ProductionBonusModifier { Subject = building, Magnitude = .1f } } },
                    "Item-production multiplier rejects a building recipient instead of silently ignoring it");
                Reject(() => owner.Data.Configuration.Modifiers = new ModifiersContentModule
                { Enabled = true, ResearchOutput = new[] { new ResearchOutputModifier { Subject = item, Magnitude = 1 } } },
                    "Kingdom research rejects a target with no runtime meaning");
                Reject(() => owner.Data.Configuration.Modifiers = new ModifiersContentModule
                { Enabled = true, HealthBonus = new[] { new HealthBonusModifier { Subject = building, Magnitude = .1f } } },
                    "Building health rejects a frozen combatant field that the building never consumes");
                owner.Data.Configuration.Modifiers = new ModifiersContentModule
                    { Enabled = true, ArmorBonus = new[] { new ArmorBonusModifier { Subject = building, Magnitude = 2 } } };
                Check(Compile().Single().Target == 1, "Building armor remains configurable because combat consumes the frozen defensive profile");
                owner.Data.Kind = ContentKind.TalentSlot;
                Reject(() => owner.Data.Configuration.Modifiers = new ModifiersContentModule
                { Enabled = true, Intelligence = new[] { new PassiveIntelligence { Points = 1 } } },
                    "Intelligence rejects unsupported job sources at the shared compiler boundary");
                owner.Data.Kind = ContentKind.Policy;
                Reject(() => owner.Data.Configuration.Modifiers = new ModifiersContentModule
                { Enabled = true, Intelligence = new[] { new PassiveIntelligence { Level = 2, Points = 1 } } },
                    "Policy layer cannot masquerade as an intelligence effect level");
                owner.Data.Kind = ContentKind.Buff;
                owner.Data.Configuration.Modifiers = new ModifiersContentModule
                    { Enabled = true, Intelligence = new[] { new PassiveIntelligence { Level = 2, Points = 1 } } };
                Check(Compile().Single().Level == 2, "Permanent buff intelligence retains its authored current-level condition");
                owner.Data.Kind = ContentKind.Technology;
                owner.Data.Configuration.Modifiers = new ModifiersContentModule
                    { Enabled = true, AttackBonus = new[] { new AttackBonusModifier { Magnitude = .1f } } };
                Check(Compile().Single().Kind == RuleKind.AttackBonus, "Completed technology can author an executable military modifier");
                TechnologyContentValidation.Validate(catalog);
                Check(true, "Technology post-validation shares the military passive-effect contract");
                Reject(() => owner.Data.Configuration.Modifiers = new ModifiersContentModule
                { Enabled = true, ResearchOutput = new[] { new ResearchOutputModifier { Magnitude = 1 } } },
                    "Technology does not accept unsupported kingdom economy modifiers");
                owner.Data.Configuration.Modifiers = new ModifiersContentModule
                    { Enabled = true, Intelligence = new[] { new PassiveIntelligence { Level = 2, Points = 10 } } };
                bool unreachable = false;
                try { TechnologyContentValidation.Validate(catalog); } catch (InvalidOperationException) { unreachable = true; }
                Check(unreachable, "Nonrepeatable technology rejects an unreachable intelligence completion tier");
                owner.Data.Flags = 1;
                TechnologyContentValidation.Validate(catalog);
                Check(true, "Repeatable technology retains reachable higher intelligence tiers");
                owner.Data.Flags = 0;
                owner.Data.Configuration.Modifiers.Intelligence[0].Level = 1;
                TechnologyContentValidation.Validate(catalog);
                Check(true, "Nonrepeatable technology still accepts its first completion intelligence tier");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(item);
                UnityEngine.Object.DestroyImmediate(building);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        static void TalentEffects()
        {
            var catalog = ScriptableObject.CreateInstance<GameCatalogAsset>();
            var owner = ScriptableObject.CreateInstance<GameDefinitionAsset>();
            var item = ScriptableObject.CreateInstance<GameDefinitionAsset>();
            var building = ScriptableObject.CreateInstance<GameDefinitionAsset>();
            var soldier = ScriptableObject.CreateInstance<GameDefinitionAsset>();
            try
            {
                owner.Data = new ContentSource { Id = "effect.talent", Kind = ContentKind.Talent, Level = 1, Capacity = 3, Cost = 1 };
                item.Data = new ContentSource { Id = "effect.item", Kind = ContentKind.Item };
                building.Data = new ContentSource { Id = "effect.building", Kind = ContentKind.Building, Level = 3 };
                soldier.Data = new ContentSource { Id = "effect.soldier", Kind = ContentKind.Soldier };
                catalog.Definitions = new[] { owner, item, building, soldier };
                var resolver = new ContentReferenceResolver(catalog);
                Rule[] Compile(TalentJobEffect entry)
                {
                    owner.Data.Configuration.People = new PeopleContentModule { Enabled = true, Effects = new[] { entry } };
                    return ContentModuleCompiler.Compile(owner.Data, resolver);
                }
                void Reject(TalentJobEffect entry, string description)
                {
                    bool failed = false;
                    try { Compile(entry); } catch (InvalidOperationException error)
                    { failed = error.Message.Contains("effect.talent") && error.Message.Contains("人才任职效果"); }
                    Check(failed, description);
                }
                foreach (var kind in new[] { ContentKind.Talent, ContentKind.TalentSlot })
                foreach (TalentEffectTiming timing in Enum.GetValues(typeof(TalentEffectTiming)))
                foreach (TalentEffectType effect in Enum.GetValues(typeof(TalentEffectType)))
                {
                    owner.Data.Kind = kind;
                    var entry = new TalentJobEffect { Effect = effect, Timing = timing, BaseValue = 1,
                        Subject = effect == TalentEffectType.物品 || effect == TalentEffectType.生产百分比 ? item
                            : effect == TalentEffectType.内容许可 ? building : effect == TalentEffectType.攻击百分比 ? soldier : null };
                    bool passive = effect == TalentEffectType.生产百分比 || effect == TalentEffectType.攻击百分比 || effect == TalentEffectType.民意;
                    bool accepted = timing == TalentEffectTiming.被动 ? passive : kind == ContentKind.Talent && !passive;
                    string description = kind + " / " + timing + " / " + effect;
                    if (accepted) Check(Compile(entry).Single().Kind == RuleKind.TalentEffect, "Executable talent combination compiles: " + description);
                    else Reject(entry, "Unconsumed talent combination rejected: " + description);
                }
                owner.Data.Kind = ContentKind.Talent;
                Reject(new TalentJobEffect { Effect = TalentEffectType.物品, Timing = TalentEffectTiming.每回合, BaseValue = 1 },
                    "Periodic item output cannot omit its item and silently do nothing");
                Reject(new TalentJobEffect { Effect = TalentEffectType.物品, Timing = TalentEffectTiming.每回合, Subject = building, BaseValue = 1 },
                    "Periodic item output cannot use a building as an inventory item");
                Reject(new TalentJobEffect { Effect = TalentEffectType.攻击百分比, Subject = item, Scaling = TalentEffectScaling.每百份物品, BaseValue = 1 },
                    "One subject cannot simultaneously mean stock item and attack recipient");
                Reject(new TalentJobEffect { Effect = TalentEffectType.生产百分比, Subject = building, Scaling = TalentEffectScaling.运营建筑数, BaseValue = 1 },
                    "One subject cannot simultaneously mean counted building and produced item");
                Reject(new TalentJobEffect { Effect = TalentEffectType.科研点, Timing = TalentEffectTiming.每回合, Subject = item, BaseValue = 1 },
                    "Fixed research rejects an unused subject");
                Check(Compile(new TalentJobEffect { Effect = TalentEffectType.科研点, Timing = TalentEffectTiming.每回合,
                    Subject = item, Scaling = TalentEffectScaling.每百份物品, BaseValue = 1 }).Single().Target == 1,
                    "Periodic research can use an item as its actual stock-scaling source");
                Check(Compile(new TalentJobEffect { Effect = TalentEffectType.生产百分比,
                    Scaling = TalentEffectScaling.运营建筑数, BaseValue = 1 }).Single().Target == -1,
                    "Global production can scale with all operational buildings without conflicting target meanings");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(item);
                UnityEngine.Object.DestroyImmediate(building);
                UnityEngine.Object.DestroyImmediate(soldier);
            }
        }

        static BlobAssetReference<ContentBlob> ProductionContent(BlobAssetReference<ContentBlob> original,
            int core, int buff, int item, int flat, float percentage)
        {
            var rules = new Dictionary<int, Rule[]>();
            for (int i = 0; i < original.Value.Definitions.Length; i++)
            {
                var definition = original.Value.Definitions[i];
                rules[i] = Enumerable.Range(definition.RuleStart, definition.RuleCount)
                    .Select(index => original.Value.Rules[index]).ToArray();
            }
            rules[core] = rules[core].Where(rule => rule.Kind == RuleKind.Population || rule.Kind == RuleKind.Warehouse
                || rule.Kind == RuleKind.Provider || rule.Kind == RuleKind.QuestCapacity || rule.Kind == RuleKind.Garrison)
                .Concat(new[]
                {
                    new Rule { Kind = RuleKind.Production, Amount = 1, Target = -1 },
                    new Rule { Kind = RuleKind.ProductionTier, Target = item, Amount = 1 }
                }).ToArray();
            rules[buff] = new[]
            {
                new Rule { Kind = RuleKind.FlatProductionBonus, Target = item, Secondary = -1, Amount = flat },
                new Rule { Kind = RuleKind.ProductionBonus, Target = item, Value = percentage }
            };
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var content = ref builder.ConstructRoot<ContentBlob>();
            NightPlanningVerification.CopyNight(builder, ref content, original);
            content.Court = original.Value.Court;
            content.Quests = original.Value.Quests;
            content.Expeditions = original.Value.Expeditions;
            content.Peaceful = original.Value.Peaceful;
            var tiers = builder.Allocate(ref content.WorkerTiers, original.Value.WorkerTiers.Length);
            for (int i = 0; i < tiers.Length; i++) tiers[i] = original.Value.WorkerTiers[i];
            var definitions = builder.Allocate(ref content.Definitions, original.Value.Definitions.Length);
            var terms = builder.Allocate(ref content.Rules, rules.Sum(pair => pair.Value.Length));
            int at = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = original.Value.Definitions[i];
                definition.RuleStart = at;
                definition.RuleCount = rules[i].Length;
                if (definition.Kind == ContentKind.Item) definition.Loss = 0;
                definitions[i] = definition;
                foreach (var rule in rules[i]) terms[at++] = rule;
            }
            return builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
        }

        static void ProductionSettlement()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");
            using var store = new BlobAssetStore(128);
            using var world = new World("Effects actual production settlement", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = Sim.Root(em);
                GameLoopSystem.Initialize(em, root);
                var original = em.GetComponentData<ContentCatalog>(root).Value;
                var core = Entity.Null;
                using (var buildings = Sim.OrderedEntities<Building>(em))
                    foreach (var building in buildings)
                        if (em.GetComponentData<BuildingStats>(building).IsCore != 0) core = building;
                Check(core != Entity.Null, "Production regression uses a genuinely baked operational core");
                int coreDefinition = em.GetComponentData<Identity>(core).Definition;
                ulong coreId = em.GetComponentData<Identity>(core).Id;
                int buff = Sim.FirstDefinition(em, root, ContentKind.Buff);
                int item = em.GetComponentData<GameSettings>(root).Gold;
                PermanentBuffOps.Grant(em, root, buff);
                em.GetBuffer<PolicyChoice>(root).Clear();
                using (var people = Sim.OrderedEntities<Talent>(em))
                    foreach (var person in people)
                    {
                        var talent = em.GetComponentData<Talent>(person);
                        talent.Recruited = talent.Paid = 0;
                        talent.Slot = -1;
                        em.SetComponentData(person, talent);
                    }
                var king = CourtOps.Monarch(em);
                if (king != Entity.Null) em.GetBuffer<TraitEntry>(king).Clear();

                foreach (var scenario in new[]
                {
                    (flat: -2, percentage: 0f, expected: 0, label: "flat reduction beyond base"),
                    (flat: -1, percentage: 0f, expected: 0, label: "flat reduction exactly to zero"),
                    (flat: 0, percentage: -2f, expected: 0, label: "percentage reduction beyond zero"),
                    (flat: 2, percentage: .5f, expected: 4, label: "flat then percentage then floor")
                })
                {
                    using var content = ProductionContent(original, coreDefinition, buff, item, scenario.flat, scenario.percentage);
                    try
                    {
                        em.SetComponentData(root, new ContentCatalog { Value = content });
                        using (var buildings = Sim.OrderedEntities<Building>(em))
                            foreach (var building in buildings)
                            {
                                var state = em.GetComponentData<Building>(building);
                                state.Workers = state.Population = state.ProductionProgress = 0;
                                state.Crop = -1;
                                state.Subsidy = 0;
                                state.Stage = building == core ? LifeStage.Operational : LifeStage.Ruined;
                                em.SetComponentData(building, state);
                                BuildingOps.ApplyLevel(em, root, building, false);
                            }
                        var inventory = em.GetBuffer<InventorySlot>(root);
                        for (int i = 0; i < inventory.Length; i++)
                        {
                            var slot = inventory[i];
                            slot.Item = -1; slot.Count = 0; slot.LossRemainder = 0;
                            inventory[i] = slot;
                        }
                        em.GetBuffer<PendingItem>(root).Clear();
                        var session = em.GetComponentData<Session>(root);
                        session.LastSettledTurn = 0;
                        em.SetComponentData(root, session);
                        // Isolate a production assertion from random royal events, while the economy
                        // itself still runs its real non-forecast settlement and inventory transaction.
                        em.SetComponentData(root, new CourtState { LastSettledTurn = session.Turn });

                        EconomyOps.Settle(em, root);

                        using var rows = em.GetBuffer<EconomyEntry>(root).ToNativeArray(Allocator.Temp);
                        var production = rows.ToArray().Where(row => row.Source == coreId && row.Reason == EconomyReason.Production).ToArray();
                        Check(em.GetComponentData<Building>(core).ProductionProgress == 0
                            && !production.Any(row => row.Note.ToString().Contains("产品放不下")),
                            "Real settlement commits its cycle without a false capacity failure: " + scenario.label);
                        Check(InventoryOps.Count(em, root, item) == scenario.expected
                            && production.Where(row => row.Item == item).Sum(row => row.Delta) == scenario.expected,
                            "Actual inventory and production journal agree: " + scenario.label);
                    }
                    finally { em.SetComponentData(root, new ContentCatalog { Value = original }); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static BlobAssetReference<ContentBlob> Content()
        {
            var kinds = new[] { ContentKind.Item, ContentKind.Building, ContentKind.Building, ContentKind.Buff,
                ContentKind.Policy, ContentKind.Talent, ContentKind.TalentSlot, ContentKind.RoyalTrait,
                ContentKind.Talent, ContentKind.Technology, ContentKind.Soldier, ContentKind.Building };
            var rules = new Dictionary<int, List<Rule>>();
            Rule Term(RuleKind kind, float value, int target = -1) => new Rule { Kind = kind, Target = target, Secondary = -1, Value = value };
            foreach (int definition in new[] { 3, 4, 5, 6, 7, 8 })
            {
                int n = definition - 2;
                rules[definition] = new List<Rule>
                {
                    new Rule { Kind = RuleKind.FlatProductionBonus, Target = 0, Secondary = -1, Amount = new[] { 2, 7, 11, 13, 17, 19 }[n - 1] },
                    Term(RuleKind.ProductionBonus, n / 10f, 0), Term(RuleKind.ResearchOutput, n),
                    Term(RuleKind.NaturalDeathRisk, -n / 100f), Term(RuleKind.PlotRisk, -n / 100f)
                };
            }
            rules[3].Add(new Rule { Kind = RuleKind.FlatProductionBonus, Target = 0, Secondary = 1, Amount = 3 });
            rules[3].Add(Term(RuleKind.AttackBonus, .1f));
            rules[3].AddRange(new[]
            {
                new Rule { Kind = RuleKind.Intelligence, Target = -1, Level = 0, Amount = 5 },
                new Rule { Kind = RuleKind.Intelligence, Target = -1, Level = 1, Amount = 10 },
                new Rule { Kind = RuleKind.Intelligence, Target = -1, Level = 2, Amount = 20 }
            });
            rules[4].Add(new Rule { Kind = RuleKind.Intelligence, Target = -1, Amount = 7 });
            rules[6].Add(new Rule { Kind = RuleKind.TalentEffect, Target = 0, Amount = 100, Value = 10 });
            rules[9] = new List<Rule>
            {
                new Rule { Kind = RuleKind.Intelligence, Target = -1, Level = 2, Amount = 30 },
                new Rule { Kind = RuleKind.AttackBonus, Target = -1, Level = 1, Value = .2f }
            };
            rules[11] = new List<Rule>
            {
                new Rule { Kind = RuleKind.Intelligence, Target = 9, Level = 1, Amount = 11, B = 2 },
                new Rule { Kind = RuleKind.AttackBonus, Target = -1, Level = 1, Value = .3f },
                new Rule { Kind = RuleKind.SpatialEffect, Target = -1, Level = 1, Amount = 10, B = 10, Value = 4, Extra = 20 }
            };
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var result = ref builder.ConstructRoot<ContentBlob>();
            result.Court = CourtSettings.Default;
            var definitions = builder.Allocate(ref result.Definitions, kinds.Length);
            var terms = builder.Allocate(ref result.Rules, rules.Sum(pair => pair.Value.Count));
            int at = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                rules.TryGetValue(i, out var entries);
                definitions[i] = new ContentDefinition { Kind = kinds[i], Id = "effect." + i, Name = "效果测试 " + i,
                    Level = 2, Capacity = 100, Size = new int2(1), Cost = i == 4 ? 50 : 0,
                    Health = 100, Damage = 10, Speed = 2, Range = 2, Interval = 1, ProjectileSpeed = 10,
                    Combat = CombatProfile.Default, RuleStart = at, RuleCount = entries?.Count ?? 0 };
                if (entries != null) foreach (var rule in entries) terms[at++] = rule;
            }
            return builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
        }

        static void Sources()
        {
            using var content = Content();
            using var world = new World("Isolated effect provenance verification");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            em.AddComponentData(root, new ContentCatalog { Value = content });
            em.AddComponentData(root, new Session { Turn = 3, Phase = Phase.Day, PublicOpinion = 60, RandomState = 123 });
            em.AddComponentData(root, new CourtState());
            em.AddComponentData(root, new NightPlanState());
            em.AddBuffer<NightPreparation>(root);
            em.AddBuffer<Entitlement>(root).Add(new Entitlement { Definition = 3, Level = 1 });
            em.AddBuffer<ResearchEntry>(root).Add(new ResearchEntry { Definition = 9, Completions = 2 });
            em.AddBuffer<PolicyChoice>(root).Add(new PolicyChoice { Definition = 4 });
            Entity Person(ulong id, int definition, byte role)
            {
                var person = em.CreateEntity();
                em.AddComponentData(person, new Identity { Id = id, Definition = definition, Name = "测试人物" });
                em.AddComponentData(person, new Royal { Alive = 1, Role = role, Age = 30 });
                em.AddBuffer<TraitEntry>(person);
                return person;
            }
            var employee = Person(101, 5, 4);
            em.AddComponentData(employee, new Talent { Slot = 6, Recruited = 1, Paid = 1, Level = 2 });
            em.GetBuffer<TraitEntry>(employee).Add(new TraitEntry { Definition = 8, Active = 1, Revealed = 1 });
            var king = Person(201, -1, 0);
            em.GetBuffer<TraitEntry>(king).Add(new TraitEntry { Definition = 7, Active = 1, Revealed = 1 });
            var child = Person(202, -1, 2);
            Entity Building(ulong id, int definition, int x)
            {
                var building = em.CreateEntity();
                em.AddComponentData(building, new Identity { Id = id, Definition = definition, Name = "测试建筑" });
                em.AddComponentData(building, new Building { Level = 1, Stage = LifeStage.Operational, Maintained = 1, Workers = 2,
                    Size = new int2(1), Cell = new int2(x, 0) });
                em.AddComponentData(building, new BuildingStats { RequiredWorkers = 2 });
                return building;
            }
            var target = Building(301, 1, 0);
            var aura = Building(302, 11, 1);

            var flatQuery = new EffectQuery(RuleKind.FlatProductionBonus, 0, 1);
            var quote = EffectOps.Query(em, root, flatQuery);
            Check(Near(quote.Value, 72) && Near(quote.Value, quote.Sources.Sum(source => source.Applied)),
                "Wildcard and targeted flat production sum every authorized source and its explanation");
            Check(Near(EffectOps.FlatProduction(em, root, 0, 2), 69), "Specified building bonus excludes another building while wildcard remains");
            Check(EffectOps.FlatProduction(em, root, 10, 1) == 0, "Item filter excludes an unrelated target");
            Check(Near(EffectOps.Modifier(em, root, RuleKind.ProductionBonus, 0), 2.2f),
                "Paid job passive effect and ordinary percentage sources share one sum");
            Check(EffectOps.Modifier(em, root, RuleKind.ResearchOutput, -1) == 21,
                "Research includes buff, policy, talent, job, active talent trait and king trait");
            Check(Near(EffectOps.Modifier(em, root, RuleKind.PlotRisk, -1), -.21f), "Plot risk includes the same global source set");
            Check(Near(EffectOps.PersonalRisk(em, root, child), -.16f) && Near(EffectOps.PersonalRisk(em, root, king), -.21f),
                "Natural risk keeps royal genes personal while combining global sources once");
            Check(Near(CourtOps.NaturalChance(em, root, child), content.Value.Court.DeathAdult * .84f),
                "Actual mortality calculation consumes the combined personal risk");
            Check(Near(EffectOps.PersonalRisk(em, root, employee), -.16f), "Employee personal trait is not counted twice through job and person paths");

            var talent = em.GetComponentData<Talent>(employee);
            talent.Paid = 0; em.SetComponentData(employee, talent);
            quote = EffectOps.Query(em, root, flatQuery);
            Check(Near(quote.Value, 29) && quote.Sources.Any(source => source.Kind == EffectSourceKind.TalentSlot && source.Reason == "工资未支付" && source.Applied == 0),
                "Unpaid talent, job and job-bound trait all stop with a shared reason");
            talent.Paid = 1; talent.Slot = -1; em.SetComponentData(employee, talent);
            Check(Near(EffectOps.Value(em, root, flatQuery), 29), "Leaving a job removes its dependent source contribution");
            talent.Slot = 6; em.SetComponentData(employee, talent);
            var employeeTraits = em.GetBuffer<TraitEntry>(employee);
            var trait = employeeTraits[0]; trait.Active = 0; employeeTraits[0] = trait;
            Check(Near(EffectOps.Value(em, root, flatQuery), 53), "Inactive talent trait contributes nothing");
            trait.Active = 1; employeeTraits[0] = trait;
            var session = em.GetComponentData<Session>(root); session.PublicOpinion = 0; em.SetComponentData(root, session);
            Check(Near(EffectOps.Value(em, root, flatQuery), 65) && em.GetBuffer<PolicyChoice>(root).Length == 1,
                "Policy threshold suspends contribution without deleting its selection");
            session.PublicOpinion = 60; em.SetComponentData(root, session);
            em.GetBuffer<PolicyChoice>(root).Clear();
            Check(Near(EffectOps.Value(em, root, flatQuery), 65), "Policy cancellation removes its source");
            em.GetBuffer<PolicyChoice>(root).Add(new PolicyChoice { Definition = 4 });

            Check(IntelOps.Current(em, root) == 63, "Intelligence combines common plus exact current-level rules and real research completions");
            var ownedBuffs = em.GetBuffer<Entitlement>(root);
            ownedBuffs[0] = new Entitlement { Definition = 3, Level = 2 };
            Check(IntelOps.Current(em, root) == 73, "Level-two buff uses its level-two intelligence row instead of level one");
            quote = EffectOps.Query(em, root, new EffectQuery(RuleKind.Intelligence, domain: EffectDomain.Intelligence));
            Check(Near(quote.Value, quote.Sources.Sum(source => source.Applied))
                && quote.Sources.Any(source => source.Definition == 3 && source.Reason == "适用等级不符"),
                "Inactive intelligence tiers are explained by the same matching operation");
            em.GetBuffer<ResearchEntry>(root).Clear();
            Check(IntelOps.Current(em, root) == 32, "Losing research completion disables technology and prerequisite-bound building intelligence");
            em.GetBuffer<ResearchEntry>(root).Add(new ResearchEntry { Definition = 9, Completions = 2 });
            var buildingState = em.GetComponentData<Building>(aura); buildingState.Workers = 1; em.SetComponentData(aura, buildingState);
            Check(IntelOps.Current(em, root) == 62, "Intelligence building worker requirement still gates its source");
            buildingState.Workers = 2; em.SetComponentData(aura, buildingState);

            var current = MilitaryOps.CurrentStats(em, root, 10, false);
            Check(Near(current.Damage, 16), "Military preparation includes buff, completed technology and staffed maintained building");
            var plan = em.GetComponentData<NightPlanState>(root); plan.PreparedTurn = session.Turn; em.SetComponentData(root, plan);
            em.GetBuffer<NightPreparation>(root).Add(current);
            session.Phase = Phase.Night; em.SetComponentData(root, session);
            buildingState.Maintained = 0; em.SetComponentData(aura, buildingState);
            Check(Near(MilitaryOps.CurrentStats(em, root, 10, false).Damage, 13)
                && MilitaryOps.Stats(em, root, 10, false).Equals(current),
                "Source loss changes the next military quote without modifying frozen night attributes");
            buildingState.Maintained = 1; em.SetComponentData(aura, buildingState);
            Check(SpatialOps.Quote(em, root, target, 10).Value == 10, "Existing spatial contribution remains separate and active");
            var otherAura = Building(303, 11, 2);
            Check(SpatialOps.Quote(em, root, target, 10).Value == 10, "Same-kind highest spatial sources do not become a global additive buff");
            buildingState.Cell = new int2(20, 0); em.SetComponentData(aura, buildingState);
            var otherState = em.GetComponentData<Building>(otherAura); otherState.Stage = LifeStage.Ruined; em.SetComponentData(otherAura, otherState);
            Check(SpatialOps.Quote(em, root, target, 10).Value == 0, "Moving or stopping spatial owners removes their effect naturally");

            uint random = em.GetComponentData<Session>(root).RandomState;
            int grants = em.GetBuffer<Entitlement>(root).Length;
            for (int i = 0; i < 3; i++)
            {
                EffectOps.Query(em, root, flatQuery);
                EffectOps.Query(em, root, new EffectQuery(RuleKind.Intelligence, domain: EffectDomain.Intelligence));
                MilitaryOps.CurrentStats(em, root, 10, false);
            }
            Check(em.GetComponentData<Session>(root).RandomState == random && em.GetBuffer<Entitlement>(root).Length == grants,
                "Repeated explanations do not mutate randomness or grant state");
            em.GetBuffer<Entitlement>(root).Clear();
            em.GetBuffer<ResearchEntry>(root).Clear();
            quote = EffectOps.Query(em, root, new EffectQuery(RuleKind.Intelligence, domain: EffectDomain.Intelligence));
            Check(quote.Sources.Count(source => source.Definition == 3 && source.Reason == "尚未获得" && source.Applied == 0) == 3
                && quote.Sources.Any(source => source.Definition == 9 && source.Reason == "尚未获得" && source.Applied == 0),
                "Unowned buff and technology still explain every locked intelligence tier without applying one");
        }
    }
}
#endif
