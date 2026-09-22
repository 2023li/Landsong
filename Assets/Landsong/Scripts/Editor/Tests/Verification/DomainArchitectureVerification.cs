#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Landsong.Content;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Presentation;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class DomainArchitectureVerification
    {
        static StringBuilder report;
        static int assertions;
        static readonly string[] Domains =
        {
            "Item",
            "ItemGroup",
            "StorageSlot",
            "Building",
            "BuildingLimitGroup",
            "Soldier",
            "Hero",
            "Enemy",
            "Projectile",
            "Loot",
            "Technology",
            "Quest",
            "Expedition",
            "Buff",
            "Policy",
            "PolicyGroup",
            "Talent",
            "TalentSlot",
            "RoyalTrait",
            "Feature",
            "Crop",
            "Opportunity"
        };
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            assertions++;
            report.AppendLine("PASS " + label);
        }

        [MenuItem("Landsong/ECS/Verification/Domain architecture boundaries")]
        public static string Run()
        {
            report = new StringBuilder();
            assertions = 0;
            try
            {
                RemovedGateways();
                DefinitionOwnership();
                RuntimeState();
                PresentationOwnership();
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/domain-architecture-verification.txt", report.ToString());
            }
        }

        static void RemovedGateways()
        {
            var assemblies = new[]
            {
                typeof(ItemId).Assembly,
                typeof(ItemDefinitionAsset).Assembly,
                typeof(ItemDisplayCatalog).Assembly,
                typeof(AudioCatalog).Assembly,
                typeof(UI_GamePanel).Assembly
            }.Distinct();
            var types = assemblies.SelectMany(assembly => assembly.GetTypes()).ToArray();
            foreach (var name in new[]
            {
                "GameDefinitionAsset",
                "GameCatalogAsset",
                "ContentKind",
                "RuleKind",
                "Rule",
                "ContentDefinition",
                "ContentBlob",
                "ContentCatalog",
                "ContentPrefab",
                "Command",
                "CommandRequests",
                "RuntimeContentCatalog",
                "GamePresentationCatalog",
                "PresentationRuntime",
                "GameUiSession",
                "GameUiDefinitionQueries",
                "Sim",
                "EntityFactory",
                "CombatantFactory",
                "MilitaryStats",
                "NightPreparation",
                "NightReward"
            }

            )
                Check(!types.Any(type => type.Name == name), "Removed universal entry point cannot reappear: " + name);
        }

        static void DefinitionOwnership()
        {
            var runtime = typeof(ItemId).Assembly;
            var authoring = typeof(ItemDefinitionAsset).Assembly;
            var display = typeof(ItemDisplayCatalog).Assembly;
            var identityTexts = new HashSet<string>(StringComparer.Ordinal);
            Check(Domains.Length == 22 && Domains.Distinct().Count() == 22, "Architecture covers every independent definition domain");
            foreach (var domain in Domains)
            {
                Type Runtime(string suffix) => runtime.GetType("Landsong.ECS.Definitions." + domain + suffix, true);
                var id = Runtime("Id");
                var definition = Runtime("Definition");
                var empty = Activator.CreateInstance(id);
                Check(id.IsValueType && !(bool)id.GetProperty("IsValid").GetValue(empty) && (int)id.GetProperty("Index").GetValue(empty) == -1 && empty.Equals(id.GetProperty("None").GetValue(null)), domain + " default ID means absent, never index zero");
                var first = id.GetMethod("FromIndex").Invoke(null, new object[] { 0 });
                var second = id.GetMethod("FromIndex").Invoke(null, new object[] { 1 });
                Check(identityTexts.Add(empty.ToString()) && identityTexts.Add(first.ToString()) && identityTexts.Add(second.ToString()), domain + " text identifies both the domain and its local value for stable UI keys");
                Check((bool)id.GetProperty("IsValid").GetValue(first) && (int)id.GetProperty("Index").GetValue(first) == 0, domain + " first entry is an explicit local index");
                Check(!id.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(method => method.Name == "op_Implicit" || method.Name == "op_Explicit"), domain + " ID cannot convert to a number or another domain ID");
                bool rejected = false;
                try
                {
                    id.GetMethod("FromIndex").Invoke(null, new object[] { -1 });
                }
                catch (TargetInvocationException error)when (error.InnerException is ArgumentOutOfRangeException)
                {
                    rejected = true;
                }

                Check(rejected, domain + " rejects negative local indexes");
                var lookup = Runtime("Definitions").GetMethod("Get", new[] { typeof(EntityManager), typeof(Entity), id });
                Check(lookup != null && lookup.ReturnType == definition.MakeByRefType(), domain + " runtime lookup requires its own ID and returns its own definition by reference");
                Check(Runtime("Catalog").GetField("Value").FieldType == typeof(BlobAssetReference<>).MakeGenericType(Runtime("CatalogBlob")), domain + " runtime catalog owns a distinct immutable blob type");
                var asset = authoring.GetType("Landsong.ECS.Authoring.Definitions." + domain + "DefinitionAsset", true);
                var catalog = authoring.GetType("Landsong.ECS.Authoring.Definitions." + domain + "CatalogAsset", true);
                var fields = asset.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Check(asset.BaseType == typeof(ScriptableObject) && fields.Any(field => field.Name == "Metadata" && field.FieldType == typeof(DefinitionMetadataSource)) && catalog.GetField("Definitions").FieldType == asset.MakeArrayType(), domain + " authoring asset directly declares its fields and its catalog contains only that asset type");
                Check(authoring.GetType("Landsong.ECS.Authoring.Definitions." + domain + "DefinitionSource", false) == null, domain + " removed whole-definition source wrapper cannot reappear");
                const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                Check(asset.GetField("Data", members) == null && asset.GetProperty("Data", members) == null, domain + " asset has no Data field or compatibility property");
                var compiler = authoring.GetType("Landsong.ECS.Authoring.Definitions." + domain + "CatalogCompiler", true);
                Check(compiler.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(method => method.Name == "Compile" && method.GetParameters().Any(parameter => parameter.ParameterType == asset)), domain + " compiler consumes the concrete asset directly");
                var displayType = display.GetType("Landsong.Content." + domain + "DisplayCatalog", true);
                var queries = displayType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Check(queries.Length == 1 && queries[0].Name == "Get" && queries[0].GetParameters().Length == 1 && queries[0].GetParameters()[0].ParameterType == id, domain + " display catalog exposes only its typed lookup");
            }
        }

        static void RuntimeState()
        {
            Check(typeof(Identity).GetField("Definition") == null, "Entity identity carries no untyped definition index");
            Check(typeof(GameEvent).GetField("Definition") == null, "Generic UI event carries no untyped definition index");
            Check(typeof(PreparedSoldier).GetField("Definition").FieldType == typeof(SoldierId) && typeof(PreparedHero).GetField("Definition").FieldType == typeof(HeroId) && typeof(PreparedBuildingDefense).GetField("Definition").FieldType == typeof(BuildingId), "Soldier, hero and building preparation each retain their own definition type");
            Check(typeof(PreparedSoldier).GetField("Stats").FieldType == typeof(CombatStatsSnapshot) && typeof(PreparedHero).GetField("Stats").FieldType == typeof(CombatStatsSnapshot) && typeof(CombatStatsSnapshot).GetField("Definition") == null, "Shared combat snapshot contains numeric combat data without a polymorphic identity");
            Check(typeof(NightItemReward).GetField("Item").FieldType == typeof(ItemId) && typeof(NightBlueprintReward).GetField("Building").FieldType == typeof(BuildingId) && typeof(NightBuffReward).GetField("Buff").FieldType == typeof(BuffId) && typeof(NightFeatureReward).GetField("Feature").FieldType == typeof(FeatureId), "Night rewards have four distinct payload schemas");
            Check(typeof(QueuedGameplayRequest).GetFields().All(field => field.Name != "Definition" && field.Name != "Argument" && field.Name != "Text"), "Shared request header contains routing metadata without operation-specific arguments");
        }

        static void PresentationOwnership()
        {
            var fields = typeof(GameUiSessionHandle).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(fields.All(field => field.FieldType == typeof(EntityManager) || field.FieldType == typeof(Entity) || field.FieldType == typeof(World)), "Game UI session handle contains only world and root binding state");
            Check(typeof(IGameUiNavigation).GetProperties().All(property => property.PropertyType == typeof(GamePanelId) || property.PropertyType == typeof(bool)), "Navigation contract exposes navigation state without domain services");
            Check(typeof(WorldPresentationView).GetField("Visuals").FieldType == typeof(WorldVisualCatalog) && typeof(WorldPresentationView).GetField("Effects").FieldType == typeof(EffectCatalog), "World visuals directly depend on independent model and effect catalogs");
            Check(typeof(AudioRuntime).GetProperty("Configuration").PropertyType == typeof(AudioCatalog) && typeof(LocalizationRuntime).GetProperty("Configuration").PropertyType == typeof(LocalizationCatalog), "Audio and localization services use separate authored types");
        }
    }
}
#endif
