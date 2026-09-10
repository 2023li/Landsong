using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    public sealed class GameWorldAuthoring : MonoBehaviour
    {
        public GameCatalogAsset Catalog;
        public MapAsset Map;
        public string DynastyName = "新王朝";
        public int BasePopulation;
        public uint Seed = 13579;

        public sealed class Baker : Baker<GameWorldAuthoring>
        {
            public override void Bake(GameWorldAuthoring authoring)
            {
                if (authoring.Catalog == null || authoring.Map == null) throw new InvalidOperationException("ECS world requires an explicit Catalog and Map.");
                DependsOn(authoring.Catalog); DependsOn(authoring.Map);
                var catalog = authoring.Catalog;
                foreach (var definition in catalog.Definitions) DependsOn(definition);
                var map = authoring.Map;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var c in catalog.Content) if (string.IsNullOrWhiteSpace(c.Id) || !ids.Add(c.Id)) throw new InvalidOperationException("Empty or duplicate content ID: " + c.Id);
                var entity = GetEntity(TransformUsageFlags.None);
                var definitions = BuildCatalog(catalog);
                AddBlobAsset(ref definitions, out _);
                AddComponent(entity, new ContentCatalog { Value = definitions });
                if(catalog.Portraits!=null)
                {
                    DependsOn(catalog.Portraits);
                    foreach(var part in catalog.Portraits.Parts??Array.Empty<PortraitPartSource>())if(part!=null&&part.Renders!=null)foreach(var render in part.Renders)if(render?.Sprite!=null){DependsOn(render.Sprite);DependsOn(render.Sprite.texture);}
                    var portraits=PortraitLibraryBuilder.Build(catalog.Portraits);AddBlobAsset(ref portraits,out _);AddComponent(entity,new PortraitLibrary{Value=portraits});
                }
                AddComponent(entity, new MapIdentity { Id = new FixedString128Bytes(map.MapId) });
                var settings = catalog.Settings; settings.Gold = catalog.Find(catalog.GoldId);
                if (settings.Gold < 0) throw new InvalidOperationException("Gold item is missing from ECS catalog.");
                AddComponent(entity, settings);
                AddComponent(entity, catalog.Dynasty);
                var royalFamily = AddBuffer<InitialRoyal>(entity);
                foreach (var person in catalog.RoyalFamily)
                {
                    var royalInitial = new InitialRoyal { Name = new FixedString128Bytes(person.Name ?? ""), Age = person.Age, Role = person.Role, Gender=person.Gender };
                    foreach (var trait in person.Traits ?? Array.Empty<string>()) { var definition = catalog.Find(trait); if (definition < 0) throw new InvalidOperationException("Unknown initial royal trait: " + trait); royalInitial.Traits.Add(definition); }
                    royalFamily.Add(royalInitial);
                }
                AddComponent(entity, new Session { Turn = 1, BasePopulation = authoring.BasePopulation, Phase = Phase.Day, RandomState = math.max(1u, authoring.Seed), DynastyName = new FixedString128Bytes(authoring.DynastyName) });
                AddBuffer<Command>(entity); AddBuffer<GameEvent>(entity); AddBuffer<DamageRequest>(entity);
                AddBuffer<InventorySlot>(entity); AddBuffer<PendingItem>(entity); AddBuffer<Entitlement>(entity);
                AddBuffer<ResearchEntry>(entity); AddBuffer<PolicyChoice>(entity); AddBuffer<BattleReportEntry>(entity); AddBuffer<NightWave>(entity);
                AddComponent(entity, new QuestTracking());
                var prefabs = AddBuffer<ContentPrefab>(entity);
                for (var i = 0; i < catalog.Content.Length; i++) if (catalog.Content[i].Prefab != null)
                    prefabs.Add(new ContentPrefab { Definition = i, Prefab = GetEntity(catalog.Content[i].Prefab, TransformUsageFlags.Dynamic) });
                var initial = AddBuffer<InitialBuilding>(entity);
                foreach (var building in map.InitialBuildings)
                {
                    var index = catalog.Find(building.Definition);
                    if (index < 0) throw new InvalidOperationException("Initial building not registered: " + building.Definition);
                    initial.Add(new InitialBuilding { Definition = index, Level = math.max(1, building.Level), Cell = new int2(building.Cell.x, building.Cell.y), Rotation = building.Rotation, Name = new FixedString128Bytes(building.Name ?? "") });
                }
                var grants = AddBuffer<StartingGrant>(entity);
                foreach (var rule in catalog.StartingGrants) grants.Add(new StartingGrant { Rule = ConvertRule(catalog, rule) });
                var regions = AddBuffer<SpawnRegion>(entity);
                foreach (var r in map.SpawnRegions) regions.Add(NightSpatialOps.ProjectRegion(new float3(map.Origin), new int2(map.Min.x, map.Min.y), new int2(map.Size.x, map.Size.y), map.CellSize, new SpawnRegion { Direction = r.Direction, Center = r.Center, Size = r.Size }));
                var grid = BuildGrid(map);
                AddBlobAsset(ref grid, out _);
                AddComponent(entity, new GridData { Value = grid, Origin = map.Origin, CellSize = map.CellSize });
                var occupancy = AddBuffer<Occupancy>(entity);
                occupancy.ResizeUninitialized(map.Cells.Length);
                for (var i = 0; i < occupancy.Length; i++) occupancy[i] = default;
            }
        }

        public static Rule ConvertRule(GameCatalogAsset catalog, RuleSource source)
        {
            var target = catalog.Find(source.Target);
            if (!string.IsNullOrEmpty(source.Target) && target < 0) throw new InvalidOperationException("Unknown rule content ID: " + source.Target);
            var secondary = catalog.Find(source.Secondary);
            if (!string.IsNullOrEmpty(source.Secondary) && secondary < 0) throw new InvalidOperationException("Unknown secondary content ID: " + source.Secondary);
            return new Rule { Kind = source.Kind, Target = target, Secondary = secondary, Level = source.Level, Amount = source.Amount, B = source.B, C = source.C, Value = source.Value, Extra = source.Extra, Key = new FixedString64Bytes(source.Key ?? "") };
        }
        public static BlobAssetReference<ContentBlob> BuildCatalog(GameCatalogAsset catalog)
        {
            ValidateInventory(catalog);
            ValidateWorkforce(catalog);
            TechnologyContentValidation.Validate(catalog);
            QuestContentValidation.Validate(catalog);
            InvitationExpeditionValidation.Validate(catalog);
            CourtContentValidation.Validate(catalog);
            NightContentValidation.Validate(catalog);
            IntelligenceValidation.Validate(catalog);
            PeacefulContentValidation.Validate(catalog);
            foreach (var source in catalog.Content)
            {
                if (!CombatProfile.Valid(source.Combat)) throw new System.InvalidOperationException(source.Id + ": invalid combat profile");
                if (source.Building != null && source.Building.SoldierRecruitLimit < 0) throw new System.InvalidOperationException(source.Id + ": negative soldier recruitment limit");
                if (source.Kind != ContentKind.Soldier && source.Kind != ContentKind.Hero) continue;
                if (source.Kind == ContentKind.Hero)
                {
                    var h = source.HeroGrowth;
                    if (h.OfferingExperience < 0 || h.OfferingExperience > 1000000 || !math.isfinite(h.ContactSeconds) || h.ContactSeconds <= 0 || h.ContactSeconds > 30 || !math.isfinite(h.ExperiencePerSecond) || h.ExperiencePerSecond < 0 || h.ExperiencePerSecond > 1000 || !math.isfinite(h.ThreatReference) || h.ThreatReference <= 0 || !math.isfinite(h.MaximumThreatMultiplier) || h.MaximumThreatMultiplier < 1 || h.MaximumThreatMultiplier > 100) throw new System.InvalidOperationException(source.Id + ": invalid hero growth");
                }
                var growth = source.Kind == ContentKind.Hero ? source.HeroGrowth.Progression : source.SoldierGrowth;
                long ranks = growth.MaxLevel - 1L;
                if (ranks * growth.FirstLevelExperience + ranks * (ranks - 1) / 2 * growth.ExperienceStep > int.MaxValue) throw new System.InvalidOperationException(source.Id + ": soldier experience curve exceeds storage range");
                if (growth.MaxLevel < 1 || growth.MaxLevel > 100 || growth.FirstLevelExperience < 1 || growth.FirstLevelExperience > 1000000 || growth.ExperienceStep < 0 || growth.ExperienceStep > 1000000 || growth.BattleExperience < 0 || growth.BattleExperience > 1000000 || !math.isfinite(growth.HealthPerLevel) || !math.isfinite(growth.DamagePerLevel) || growth.HealthPerLevel < 0 || growth.HealthPerLevel > 10 || growth.DamagePerLevel < 0 || growth.DamagePerLevel > 10 || source.Population < 0 || source.Cost < 0)
                    throw new System.InvalidOperationException(source.Id + ": invalid soldier growth or recruitment configuration");
                foreach (var cost in source.Rules) if (cost.Kind == RuleKind.RecruitCost && (cost.Amount < 0 || cost.Level != 0 && cost.Level != 1 || catalog.Find(cost.Target) < 0 || catalog.Content[catalog.Find(cost.Target)].Kind != ContentKind.Item))
                    throw new System.InvalidOperationException(source.Id + ": RecruitCost requires an item, nonnegative amount and level 0/1");
            }
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ContentBlob>();
            root.Quests = catalog.QuestGeneration; root.Expeditions = catalog.Expeditions; root.Court = catalog.Court;
            root.Peaceful = catalog.Peaceful;
            NightContentValidation.Bake(catalog, builder, ref root);
            var total = 0; foreach (var c in catalog.Content) total += c.Rules.Length;
            var definitions = builder.Allocate(ref root.Definitions, catalog.Content.Length);
            var rules = builder.Allocate(ref root.Rules, total);
            var at = 0;
            for (var i = 0; i < catalog.Content.Length; i++)
            {
                var c = catalog.Content[i];
                definitions[i] = new ContentDefinition { Id = new FixedString128Bytes(c.Id), Name = new FixedString128Bytes(c.Name), Kind = c.Kind, RuleStart = at, RuleCount = c.Rules.Length, Group = catalog.Find(c.Group), Level = c.Level, Capacity = c.Capacity, Duration = c.Duration, Value = c.Value, Limit = c.Limit, Size = new int2(c.Size.x, c.Size.y), Health = c.Health, Damage = c.Damage, Range = c.Range, Interval = c.Interval, Speed = c.Speed, ProjectileSpeed = c.ProjectileSpeed, Chance = c.Chance, Loss = c.Loss, Population = c.Population, Cost = c.Cost, Flags = c.Flags, SoldierGrowth = c.SoldierGrowth };
                var entry = definitions[i];
                entry.HeroGrowth = c.HeroGrowth;
                entry.Combat = c.Combat;
                entry.Opportunity = c.Opportunity; entry.Theft = c.Theft;
                entry.TargetCategory = c.TargetCategory;
                entry.BuildingPolicy = (c.Building ?? new BuildingPolicySource()).Bake();
                entry.DefaultSkin = new FixedString64Bytes(c.Building?.DefaultSkin ?? "");
                entry.QuestIntensity = c.QuestIntensity; entry.QuestWeight = c.QuestWeight; entry.ItemQuantityScale = c.ItemQuantityScale;
                definitions[i] = entry;
                foreach (var r in c.Rules)
                {
                    var rule = ConvertRule(catalog, r);
                    if (c.Kind == ContentKind.Quest && (r.Kind == RuleKind.SubmitItem || r.Kind == RuleKind.RequireItem || r.Kind == RuleKind.RewardItem || r.Kind == RuleKind.FailureItem)) rule.Amount = (int)math.round(r.Amount * c.ItemQuantityScale);
                    rules[at++] = rule;
                }
            }
            return builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
        }
        public static void ValidateWorkforce(GameCatalogAsset catalog)
        {
            foreach (var d in catalog.Content) foreach (var r in d.Rules)
            {
                if (r.Kind == RuleKind.Workforce)
                {
                    var target = catalog.Find(r.Target);
                    if (d.Kind != ContentKind.Building || r.Amount < 0 || r.B < 0 || r.B > r.Amount || !math.isfinite(r.Value) || r.Value < 0 || !math.isfinite(r.Extra) || r.Extra < 0 || (double)r.Extra > int.MaxValue / 2d || target >= 0 && catalog.Content[target].Kind != ContentKind.Item)
                        throw new InvalidOperationException("Invalid workforce capacity/attraction/cost: " + d.Id);
                }
                if (r.Kind == RuleKind.SpatialEffect)
                {
                    var target = catalog.Find(r.Target);
                    if (d.Kind != ContentKind.Building || r.Amount < 0 || r.C < 0 || !math.isfinite(r.Value) || r.Value < 0 || r.Extra != 0 && r.Extra != 10 && r.Extra != 20 || target >= 0 && catalog.Content[target].Kind != ContentKind.Building)
                        throw new InvalidOperationException("Invalid spatial effect: " + d.Id);
                }
            }
        }
        public static void ValidateInventory(GameCatalogAsset catalog)
        {
            var content = catalog.Content;
            foreach (var d in content)
            {
                if ((d.Kind == ContentKind.Item || d.Kind == ContentKind.SlotType) && (!math.isfinite(d.Loss) || d.Loss < 0) || d.Kind == ContentKind.Item && d.Capacity <= 0) throw new InvalidOperationException("Invalid storage capacity/loss: " + d.Id);
                if ((d.Kind == ContentKind.Item || d.Kind == ContentKind.ItemGroup) && !string.IsNullOrEmpty(d.Group))
                {
                    var seen = new HashSet<string>(); var group = d.Group;
                    while (!string.IsNullOrEmpty(group)) { var at = catalog.Find(group); if (at < 0 || content[at].Kind != ContentKind.ItemGroup || !seen.Add(group)) throw new InvalidOperationException("Invalid/cyclic item group: " + d.Id); group = content[at].Group; }
                }
                foreach (var r in d.Rules)
                {
                    if (r.Kind != RuleKind.SlotAccept && r.Kind != RuleKind.SlotLoss && r.Kind != RuleKind.Warehouse && r.Kind != RuleKind.ItemGroup) continue;
                    var at = catalog.Find(r.Target); if (at < 0) throw new InvalidOperationException("Missing storage rule target: " + d.Id);
                    var kind = content[at].Kind;
                    if (r.Kind == RuleKind.SlotAccept && (d.Kind != ContentKind.SlotType || kind != ContentKind.Item && kind != ContentKind.ItemGroup) || r.Kind == RuleKind.SlotLoss && (d.Kind != ContentKind.SlotType || kind != ContentKind.Item && kind != ContentKind.ItemGroup || !math.isfinite(r.Value) || r.Value < 0) || r.Kind == RuleKind.Warehouse && (kind != ContentKind.SlotType || r.Amount < 0 || r.B < 0) || r.Kind == RuleKind.ItemGroup && (d.Kind != ContentKind.Item || kind != ContentKind.ItemGroup)) throw new InvalidOperationException("Invalid storage rule: " + d.Id);
                }
            }
        }
        public static BlobAssetReference<GridBlob> BuildGrid(MapAsset map)
        {
            if (map.Size.x <= 0 || map.Size.y <= 0 || map.Cells.Length != map.Size.x * map.Size.y) throw new InvalidOperationException("ECS map dimensions do not match its cells.");
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GridBlob>();
            root.Min = new int2(map.Min.x, map.Min.y); root.Size = new int2(map.Size.x, map.Size.y);
            var cells = builder.Allocate(ref root.Cells, map.Cells.Length);
            for (var i = 0; i < cells.Length; i++) { var c = map.Cells[i]; cells[i] = new GridCell { Exists = (byte)(c.Exists ? 1 : 0), Buildable = (byte)(c.Buildable ? 1 : 0), Traversable = (byte)(c.Traversable ? 1 : 0), BlocksProjectile = (byte)(c.BlocksProjectile ? 1 : 0), Elevation = c.Elevation, Surface = c.Surface, Height = c.Height, Terrain = c.Terrain }; }
            return builder.CreateBlobAssetReference<GridBlob>(Allocator.Persistent);
        }
    }
}
