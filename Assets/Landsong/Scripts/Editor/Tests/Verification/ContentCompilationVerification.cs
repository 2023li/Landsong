#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    // Daily verification follows the current authored content, never a migration snapshot.
    public static class ContentCompilationVerification
    {
        [MenuItem("Landsong/ECS/Verification/Current content compilation")]
        public static string Run()
        {
            var log = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            int checks = 0;
            void Check(bool value, string message)
            {
                if (!value)
                    throw new InvalidOperationException(message);
                checks++;
                log.AppendLine("PASS " + message);
            }

            try
            {
                Check((int)TerrainType.陆地 == 1 && (int)TerrainType.石地 == 4, "Terrain enum protocol remains stable");
                var template = AssetDatabase.LoadAssetAtPath<GameObject>(Landsong.EditorTools.MapWorldComposition.TemplatePath);
                Check(template != null, "Explicit world composition prefab exists");
                var content = template.GetComponent<GameContentSetAuthoring>().Content;
                VerifyCurrentCatalogs(content, Check);
                VerifyFixtureIsolation(Check);
                VerifyContentEdits(template, Check);
                VerifyQuestScaling(content, Check);
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine("FAIL " + error);
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/content-compilation-verification.txt", log.ToString());
            }
        }

        // Explicit domain compilers preserve their real typed dependency graph.
        internal static void VerifyCurrentCatalogs(GameContentSetAsset content, Action<bool, string> check)
        {
            using (var blob = ItemCatalogBaking.Compile(content))
                VerifyCatalog("Item", content.Items.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = ItemGroupCatalogBaking.Compile(content))
                VerifyCatalog("ItemGroup", content.ItemGroups.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = StorageSlotCatalogBaking.Compile(content))
                VerifyCatalog("StorageSlot", content.StorageSlots.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = BuildingLimitGroupCatalogBaking.Compile(content))
                VerifyCatalog("BuildingLimitGroup", content.BuildingLimitGroups.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = PolicyGroupCatalogBaking.Compile(content))
                VerifyCatalog("PolicyGroup", content.PolicyGroups.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = TechnologyCatalogBaking.Compile(content))
                VerifyCatalog("Technology", content.Technologies.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = QuestCatalogBaking.Compile(content))
                VerifyCatalog("Quest", content.Quests.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = ExpeditionCatalogBaking.Compile(content))
                VerifyCatalog("Expedition", content.Expeditions.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = SoldierCatalogBaking.Compile(content))
                VerifyCatalog("Soldier", content.Soldiers.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = HeroCatalogBaking.Compile(content))
                VerifyCatalog("Hero", content.Heroes.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = EnemyCatalogBaking.Compile(content))
                VerifyCatalog("Enemy", content.Enemies.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = BuffCatalogBaking.Compile(content))
                VerifyCatalog("Buff", content.Buffs.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = PolicyCatalogBaking.Compile(content))
                VerifyCatalog("Policy", content.Policies.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = TalentCatalogBaking.Compile(content))
                VerifyCatalog("Talent", content.Talents.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = TalentSlotCatalogBaking.Compile(content))
                VerifyCatalog("TalentSlot", content.TalentSlots.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = RoyalTraitCatalogBaking.Compile(content))
                VerifyCatalog("RoyalTrait", content.RoyalTraits.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = FeatureCatalogBaking.Compile(content))
                VerifyCatalog("Feature", content.Features.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = CropCatalogBaking.Compile(content))
                VerifyCatalog("Crop", content.Crops.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = ProjectileCatalogBaking.Compile(content))
                VerifyCatalog("Projectile", content.Projectiles.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = OpportunityCatalogBaking.Compile(content))
                VerifyCatalog("Opportunity", content.Opportunities.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = LootCatalogBaking.Compile(content))
                VerifyCatalog("Loot", content.Loot.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
            using (var blob = BuildingCatalogBaking.Compile(content))
                VerifyCatalog("Building", content.Buildings.Definitions, blob.Value.Definitions.Length,
                    i => blob.Value.Definitions[i].Metadata, check);
        }

        static void VerifyCatalog(string domain, ScriptableObject[] definitions, int compiledCount,
            Func<int, DefinitionMetadata> metadataAt, Action<bool, string> check)
        {
            check(compiledCount == definitions.Length, domain + " compiler covers the current catalog");
            for (int i = 0; i < definitions.Length; i++)
            {
                var source = definitions[i];
                using var serialized = new SerializedObject(source);
                string id = serialized.FindProperty("Metadata.Id").stringValue;
                string name = serialized.FindProperty("Metadata.Name").stringValue;
                var metadata = metadataAt(i); // Only scalar metadata is copied, never a BlobArray owner.
                check(metadata.Id.ToString() == id && metadata.Name.ToString() == name,
                    domain + " current identity, name and registration order: " + id);
                VerifySerialization(source);
                check(true, domain + " current authored values and references survive serialization: " + id);
            }
        }

        internal static void VerifySerialization(ScriptableObject source)
        {
            string expected = EditorJsonUtility.ToJson(source);
            var copy = ScriptableObject.CreateInstance(source.GetType());
            try
            {
                EditorJsonUtility.FromJsonOverwrite(expected, copy);
                if (EditorJsonUtility.ToJson(copy) != expected)
                    throw new InvalidOperationException(source.name + " current asset serialization changed values or references.");
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        static void ExpectInvalid(Action action, Action<bool, string> check, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, message);
        }

        static void VerifyFixtureIsolation(Action<bool, string> check)
        {
            using var fixture = new DefinitionAuthoringFixture();
            var groups = fixture.Asset<ItemGroupCatalogAsset>();
            var group = fixture.Asset<ItemGroupDefinitionAsset>();
            group.Metadata.Id = "verification.group";
            groups.Definitions = new[] { group };
            fixture.Item.PrimaryGroup = group;
            using var scope = new CatalogFixture.Scope();
            var itemsCopy = scope.Clone(fixture.ItemCatalog);
            var groupsCopy = scope.Clone(groups);
            var items = new ItemCatalogIndex(itemsCopy);
            var groupIndex = new ItemGroupCatalogIndex(groupsCopy);
            check(itemsCopy.Definitions[0] != fixture.Item && groupIndex.Resolve(itemsCopy.Definitions[0].PrimaryGroup).IsValid,
                "Fixture clones identities and remaps cross-domain references");
            ExpectInvalid(() => items.Resolve(fixture.Item), check,
                "An unregistered asset cannot impersonate a registered stable ID");
            groupsCopy.Definitions[0].ParentGroup = groupsCopy.Definitions[0];
            ExpectInvalid(() => { using var invalid = ItemGroupCatalogCompiler.Build(groupsCopy); }, check,
                "Item group cycles are rejected before baking");
        }

        static void VerifyContentEdits(GameObject template, Action<bool, string> check)
        {
            using var scope = new CatalogFixture.Scope();
            using var added = new DefinitionAuthoringFixture();
            var composition = Object.Instantiate(template);
            var original = template.GetComponent<GameContentSetAuthoring>().Content;
            string originalBuilding = EditorJsonUtility.ToJson(original.Buildings.Definitions[0]);
            string originalSoldier = EditorJsonUtility.ToJson(original.Soldiers.Definitions[0]);
            string originalTechnology = EditorJsonUtility.ToJson(original.Technologies.Definitions[0]);
            try
            {
                scope.CloneCatalogsOn(composition);
                var edited = composition.GetComponent<GameContentSetAuthoring>().Content;
                // Change existing values as well as append valid definitions. No source asset is saved.
                edited.Buildings.Definitions[0].MaximumDurability = 137;
                edited.Soldiers.Definitions[0].CombatStats.MaximumHealth = 143;
                edited.Technologies.Definitions[0].ResearchPointCost = 137;

                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(edited.Buildings.Definitions[0]), added.Building);
                added.Building.Metadata.Id = "verification.added.building";
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(edited.Soldiers.Definitions[0]), added.Soldier);
                added.Soldier.Metadata.Id = "verification.added.soldier";
                added.Technology.Metadata.Id = "verification.added.technology";
                added.Technology.ResearchPointCost = 23;
                edited.Buildings.Definitions = edited.Buildings.Definitions.Append(added.Building).ToArray();
                edited.Soldiers.Definitions = edited.Soldiers.Definitions.Append(added.Soldier).ToArray();
                edited.Technologies.Definitions = edited.Technologies.Definitions.Append(added.Technology).ToArray();
                VerifyCurrentCatalogs(edited, check);
                check(edited.Buildings.Definitions.Length == original.Buildings.Definitions.Length + 1
                    && edited.Soldiers.Definitions.Length == original.Soldiers.Definitions.Length + 1
                    && edited.Technologies.Definitions.Length == original.Technologies.Definitions.Length + 1,
                    "Valid buildings, soldiers and technologies can be added without changing a historical baseline");
                using (var buildings = BuildingCatalogBaking.Compile(edited))
                    check(buildings.Value.Definitions[0].MaximumDurability == 137, "Existing building tuning reaches compiled data");
                using (var soldiers = SoldierCatalogBaking.Compile(edited))
                    check(soldiers.Value.Definitions[0].CombatStats.MaximumHealth == 143, "Existing soldier tuning reaches compiled data");
                using (var technologies = TechnologyCatalogBaking.Compile(edited))
                    check(technologies.Value.Definitions[0].ResearchPointCost == 137, "Existing technology cost tuning reaches compiled data");

                Array.Reverse(edited.Technologies.Definitions);
                VerifyCurrentCatalogs(edited, check);
                check(true, "Current catalog ordering is verified without freezing historical indices; save fingerprints remain authoritative");

                added.Technology.Metadata.Id = edited.Technologies.Definitions[1].Metadata.Id;
                ExpectInvalid(() => { using var invalid = TechnologyCatalogBaking.Compile(edited); }, check,
                    "Duplicate stable IDs are still rejected after content expansion");
                added.Technology.Metadata.Id = "verification.added.technology";
                added.Technology.ResearchPointCost = -1;
                ExpectInvalid(() => { using var invalid = TechnologyCatalogBaking.Compile(edited); }, check,
                    "Invalid tuned research costs are still rejected");
                added.Technology.ResearchPointCost = 23;

                var unregistered = added.Asset<TechnologyDefinitionAsset>();
                unregistered.Metadata.Id = added.Technology.Metadata.Id;
                added.Technology.Prerequisites.TechnologyRequirements = new[] { new TechnologyRequirementSource { Technology = unregistered } };
                ExpectInvalid(() => { using var invalid = TechnologyCatalogBaking.Compile(edited); }, check,
                    "An unregistered prerequisite with a matching stable ID is still rejected");

                unregistered.Metadata.Id = "verification.cycle.technology";
                unregistered.ResearchPointCost = 1;
                unregistered.Prerequisites.TechnologyRequirements = new[] { new TechnologyRequirementSource { Technology = added.Technology } };
                edited.Technologies.Definitions = edited.Technologies.Definitions.Append(unregistered).ToArray();
                ExpectInvalid(() => { using var invalid = TechnologyCatalogBaking.Compile(edited); }, check,
                    "Cycles between newly registered technologies are still rejected");
            }
            finally
            {
                Object.DestroyImmediate(composition);
            }
            check(EditorJsonUtility.ToJson(original.Buildings.Definitions[0]) == originalBuilding
                && EditorJsonUtility.ToJson(original.Soldiers.Definitions[0]) == originalSoldier
                && EditorJsonUtility.ToJson(original.Technologies.Definitions[0]) == originalTechnology,
                "Expansion and tuning fixtures leave the formal definitions unchanged");
        }

        static void VerifyQuestScaling(GameContentSetAsset content, Action<bool, string> check)
        {
            using var blob = QuestCatalogBaking.Compile(content);
            for (int i = 0; i < content.Quests.Definitions.Length; i++)
            {
                var source = content.Quests.Definitions[i];
                ref var runtime = ref blob.Value.Definitions[i];
                var owned = source.Objectives.OwnedItemObjectives.OrderBy(x => x.Order).ToArray();
                var submitted = source.Objectives.SubmittedItemObjectives.OrderBy(x => x.Order).ToArray();
                var rewards = source.Rewards.Items.OrderBy(x => x.Order).ToArray();
                var failures = source.FailurePenalties.OrderBy(x => x.Order).ToArray();
                for (int n = 0; n < owned.Length; n++)
                    check(runtime.Objectives.OwnedItemObjectives[n].Quantity == (int)Unity.Mathematics.math.round(owned[n].Quantity * source.ItemQuantityScale), "Quest owned-item quantity scales once");
                for (int n = 0; n < submitted.Length; n++)
                    check(runtime.Objectives.SubmittedItemObjectives[n].Quantity == (int)Unity.Mathematics.math.round(submitted[n].Quantity * source.ItemQuantityScale), "Quest submitted-item quantity scales once");
                for (int n = 0; n < rewards.Length; n++)
                    check(runtime.Rewards.Items[n].Quantity == (int)Unity.Mathematics.math.round(rewards[n].Quantity * source.ItemQuantityScale), "Quest item reward quantity scales once");
                for (int n = 0; n < failures.Length; n++)
                    check(runtime.FailurePenalties[n].Quantity == (int)Unity.Mathematics.math.round(failures[n].Quantity * source.ItemQuantityScale), "Quest item penalty quantity scales once");
            }
        }
    }
}
#endif
