#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class ContentResourceOrganizer
    {
        static readonly string[] Catalogs =
        {
            "Buff", "Building", "BuildingLimitGroup", "Crop", "Enemy", "Expedition", "Feature", "Hero", "Item", "ItemGroup", "Loot",
            "NightEvent", "Opportunity", "Policy", "PolicyGroup", "Projectile", "Quest", "RoyalTrait", "Soldier", "StorageSlot", "Talent", "TalentSlot", "Technology"
        };

        static readonly string[] DisplayCatalogs = Catalogs.Where(name => name != "NightEvent").ToArray();
        static readonly List<string> changes = new();

        [MenuItem("Landsong/内容制作/迁移到统一资源目录")]
        public static string Execute()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("资源目录迁移只能在 Edit Mode 执行。");

            changes.Clear();
            MoveCatalogs();
            MoveSharedArt();
            MoveCurrentUnits();
            MoveLegacyUnits();
            MoveBuildings();
            MovePresentationConfiguration();
            MoveOuterContent();
            CleanupCandidates();
            UpdateBuildingNamingConfiguration();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            return "统一资源目录迁移完成：" + changes.Count + " 项。\n" + string.Join("\n", changes);
        }

        static void MoveCatalogs()
        {
            EnsureFolder(ContentAssetPaths.CatalogSources);
            EnsureFolder(ContentAssetPaths.DisplayCatalogs);
            foreach (var name in Catalogs)
                MoveAsset(ContentAssetPaths.Root + "/Catalogs/" + name + "Catalog.asset", ContentAssetPaths.SourceCatalog(name));
            foreach (var name in DisplayCatalogs)
                MoveAsset(ContentAssetPaths.Presentation + "/" + name + "DisplayCatalog.asset", ContentAssetPaths.DisplayCatalog(name));
        }

        static void MoveSharedArt()
        {
            EnsureFolder(ContentAssetPaths.SharedShaders);
            MoveAsset("Assets/Landsong/Art/Animations/LandsongSoldier/SoldierAnimatedLit.shadergraph", ContentAssetPaths.AnimatedUnitShader);
            MoveTopLevelFiles("Assets/Landsong/Shaders", ContentAssetPaths.SharedShaders);
            MoveFolder("Assets/Landsong/Art/Material", "Assets/Landsong/Art/Shared/Materials");
            MoveFolder("Assets/Landsong/Art/Greybox", "Assets/Landsong/Art/Shared/Greybox");
        }

        static void MoveCurrentUnits()
        {
            EnsureFolder(ContentAssetPaths.Units + "/Soldier");
            EnsureFolder(ContentAssetPaths.Units + "/Enemy");
            EnsureFolder(ContentAssetPaths.Units + "/Worker");
            EnsureFolder(ContentAssetPaths.Units + "/Hero");

            var oldSoldier = "Assets/Landsong/Art/Animations/LandsongSoldier";
            if (AssetDatabase.IsValidFolder(oldSoldier))
            {
                MoveFolder(oldSoldier, ContentAssetPaths.UnitPackage("Soldier", "militia"));
                MoveAsset(ContentAssetPaths.UnitPackage("Soldier", "militia") + "/RomanSoldier.prefab", SoldierAnimationSetup.PrefabPath);
                MoveAsset(ContentAssetPaths.UnitPackage("Soldier", "militia") + "/RomanSoldierView.prefab", SoldierAnimationSetup.ViewPrefabPath);
                MoveAsset(ContentAssetPaths.UnitPackage("Soldier", "militia") + "/RomanSoldier.controller", SoldierAnimationSetup.ControllerPath);
            }

            MoveFolder(ContentAssetPaths.Presentation + "/Units/Enemy/wolf", ContentAssetPaths.UnitPackage("Enemy", "wolf"));
            var oldWolfLogic = ContentAssetPaths.Root + "/Prefabs/角色/Enemy/wolf/wolf.prefab";
            if (AssetDatabase.LoadMainAssetAtPath(oldWolfLogic) != null)
                MoveAsset(oldWolfLogic, ContentAssetPaths.UnitPackage("Enemy", "wolf") + "/wolf.prefab");
            var wolf = ContentAuthoringContext.Content().Get<EnemyCatalogAsset>().Definitions.Single(item => item.Metadata.Id == "wolf");
            DeleteIfUnused(Parent(AssetDatabase.GetAssetPath(wolf.Prefab)) + "/Models/wolfModel.prefab");

            MoveFolder(ContentAssetPaths.Presentation + "/Units/Worker/transport_worker", ContentAssetPaths.UnitPackage("Worker", "transport_worker"));
            MoveAsset(ContentAssetPaths.Root + "/Prefabs/角色/Worker/transport_worker/transport_worker_male.prefab", ContentAssetPaths.UnitPackage("Worker", "transport_worker") + "/transport_worker_male.prefab");
            MoveAsset(ContentAssetPaths.Root + "/Prefabs/角色/Worker/transport_worker/transport_worker_female.prefab", ContentAssetPaths.UnitPackage("Worker", "transport_worker") + "/transport_worker_female.prefab");
            DeleteIfUnused(ContentAssetPaths.UnitPackage("Worker", "transport_worker") + "/Models/transport_worker_maleModel.prefab");
            DeleteIfUnused(ContentAssetPaths.UnitPackage("Worker", "transport_worker") + "/Models/transport_worker_femaleModel.prefab");
        }

        static void MoveLegacyUnits()
        {
            var legacy = ContentAssetPaths.Units + "/Shared/LegacyActor";
            EnsureFolder(legacy + "/Clips");
            MoveAsset(ContentAssetPaths.Presentation + "/Actor.controller", legacy + "/Actor.controller");
            MoveTopLevelFiles(ContentAssetPaths.Presentation, legacy + "/Clips", ".anim");

            MoveLegacyUnit("Enemy", "boss", "boss.prefab", "boss_View.prefab", "boss.mat");
            MoveLegacyUnit("Enemy", "raider", "raider.prefab", "raider_View.prefab", "raider.mat");
            MoveLegacyUnit("Enemy", "invader", null, "invader_View.prefab", "invader.mat");
            MoveLegacyUnit("Hero", "titan", "titan.prefab", "titan_View.prefab", "titan.mat");

            DeleteIfUnused(ContentAssetPaths.Root + "/Prefabs/militia.prefab");
            DeleteIfUnused(ContentAssetPaths.Presentation + "/militia_View.prefab");
            DeleteIfUnused(ContentAssetPaths.Presentation + "/militia.mat");
        }

        static void MoveLegacyUnit(string domain, string id, string logic, string view, string material)
        {
            var package = ContentAssetPaths.UnitPackage(domain, id);
            EnsureFolder(package + "/Materials");
            if (!string.IsNullOrEmpty(logic)) MoveAsset(ContentAssetPaths.Root + "/Prefabs/" + logic, package + "/" + id + ".prefab");
            MoveAsset(ContentAssetPaths.Presentation + "/" + view, package + "/" + id + "View.prefab");
            MoveAsset(ContentAssetPaths.Presentation + "/" + material, package + "/Materials/" + material);
        }

        static void MoveBuildings()
        {
            EnsureFolder(ContentAssetPaths.Buildings);
            var old = ContentAssetPaths.Root + "/Prefabs/建筑";
            if (AssetDatabase.IsValidFolder(old))
            {
                foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { old }).Select(AssetDatabase.GUIDToAssetPath).Where(path => Parent(path) == old).OrderBy(path => path).ToArray())
                {
                    var id = Path.GetFileNameWithoutExtension(path);
                    var package = ContentAssetPaths.BuildingPackage(id);
                    EnsureFolder(package);
                    MoveAsset(path, package + "/" + id + ".prefab");
                }
            }
            MoveFolder("Assets/Landsong/Objects/Generated/BuildingVisuals", ContentAssetPaths.Buildings + "/LegacyGenerated");
        }

        static void MovePresentationConfiguration()
        {
            MoveAsset(ContentAssetPaths.Root + "/Resources/LandsongAudio.asset", ContentAssetPaths.Audio + "/LandsongAudio.asset");
            MoveAsset(ContentAssetPaths.Root + "/Resources/LandsongEffects.asset", ContentAssetPaths.Effects + "/LandsongEffects.asset");
            MoveAsset(ContentAssetPaths.Root + "/Resources/LandsongLocalization.asset", ContentAssetPaths.GeneratedLocalization + "/LandsongLocalization.asset");
            MoveAsset(ContentAssetPaths.Root + "/Resources/LandsongPortraitDisplay.asset", ContentAssetPaths.Portraits + "/LandsongPortraitDisplay.asset");
            MoveAsset(ContentAssetPaths.Root + "/PortraitConfig.asset", ContentAssetPaths.Portraits + "/PortraitConfig.asset");
            MoveAsset(ContentAssetPaths.Presentation + "/WorldPresentationRoot.prefab", ContentAssetPaths.World + "/WorldPresentationRoot.prefab");
            MoveAsset(ContentAssetPaths.Presentation + "/CueBurst.prefab", ContentAssetPaths.Effects + "/CueBurst.prefab");
            MoveAsset(ContentAssetPaths.Presentation + "/CueBurst.mat", ContentAssetPaths.Effects + "/CueBurst.mat");
            MoveFolder(ContentAssetPaths.Root + "/Materials", ContentAssetPaths.Presentation + "/Shared/Materials");
            MoveAsset(ContentAssetPaths.Root + "/Prefabs/presentation.loot.prefab", ContentAssetPaths.Root + "/Loot/presentation.loot.prefab");
            MoveAsset(ContentAssetPaths.Root + "/Prefabs/presentation.projectile.prefab", ContentAssetPaths.Root + "/Projectiles/presentation.projectile.prefab");
            MoveAsset(ContentAssetPaths.Root + "/Prefabs/night.visitor.prefab", ContentAssetPaths.Root + "/Opportunities/night.visitor.prefab");
        }

        static void MoveOuterContent()
        {
            MoveFolder("Assets/Landsong/Objects/Prefabs/UI", ContentAssetPaths.UiPrefabs);
            MoveFolder("Assets/Landsong/Objects/本地化", ContentAssetPaths.Localization);
            MoveFolder("Assets/Landsong/Objects/InputSystem", "Assets/Landsong/Input");
            MoveFolder("Assets/Landsong/Objects/Prefabs/CropVisuals", ContentAssetPaths.Root + "/Crops/Visuals");
            MoveFolder("Assets/Landsong/Objects/Materials/NightBaseline", ContentAssetPaths.Presentation + "/Materials/NightBaseline");
            MoveFolder("Assets/_Test", "Assets/Landsong/Tests/Experimental");
        }

        static void CleanupCandidates()
        {
            foreach (var name in new[] { "MilitaryGarrison_CubeView.prefab", "TitanTemple_CubeView.prefab", "WarningBell_CubeView.prefab", "Watchtower_CubeView.prefab" })
                DeleteIfUnused("Assets/Landsong/Objects/Prefabs/Night/BuildingViews/" + name);
            DeleteIfUnused("Assets/InputSystem_Actions.inputactions");
            DeleteFolderIfEmpty(ContentAssetPaths.Root + "/Authoring/UnitRecipes");
            var wolf = ContentAuthoringContext.Content().Get<EnemyCatalogAsset>().Definitions.Single(item => item.Metadata.Id == "wolf");
            DeleteFolderIfEmpty(Parent(AssetDatabase.GetAssetPath(wolf.Prefab)) + "/Models");
            DeleteFolderIfEmpty(ContentAssetPaths.UnitPackage("Worker", "transport_worker") + "/Models");
            foreach (var folder in new[]
            {
                ContentAssetPaths.Root + "/Prefabs/角色/Enemy/wolf", ContentAssetPaths.Root + "/Prefabs/角色/Enemy", ContentAssetPaths.Root + "/Prefabs/角色/Worker/transport_worker",
                ContentAssetPaths.Root + "/Prefabs/角色/Worker", ContentAssetPaths.Root + "/Prefabs/角色", ContentAssetPaths.Root + "/Prefabs/建筑", ContentAssetPaths.Root + "/Prefabs",
                ContentAssetPaths.Presentation + "/Units/Enemy", ContentAssetPaths.Presentation + "/Units/Worker", ContentAssetPaths.Presentation + "/Units",
                ContentAssetPaths.Root + "/Resources", "Assets/Landsong/Objects/Prefabs/Night/BuildingViews", "Assets/Landsong/Objects/Prefabs/Night",
                "Assets/Landsong/Objects/Prefabs", "Assets/Landsong/Objects/Materials", "Assets/Landsong/Objects", "Assets/Landsong/Shaders"
            }) DeleteFolderIfEmpty(folder);
        }

        static void UpdateBuildingNamingConfiguration()
        {
            var config = AssetDatabase.LoadAssetAtPath<Landsong.VisualSystem.LS_BuildingViewNamingConfig>("Assets/Landsong/Art/BuildingViewNamingConfig.asset");
            if (config == null) return;
            var serialized = new SerializedObject(config);
            serialized.FindProperty("viewOutputRoot").stringValue = ContentAssetPaths.Buildings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        static void Validate()
        {
            foreach (var name in Catalogs) RequireAsset(ContentAssetPaths.SourceCatalog(name));
            foreach (var name in DisplayCatalogs) RequireAsset(ContentAssetPaths.DisplayCatalog(name));
            foreach (var path in new[]
            {
                ContentAssetPaths.AnimatedUnitShader,
                ContentAssetPaths.Audio + "/LandsongAudio.asset", ContentAssetPaths.GeneratedLocalization + "/LandsongLocalization.asset",
                ContentAssetPaths.UiPrefabs + "/Bootstrap/UI_Root.prefab"
            }) RequireAsset(path);
            var content = ContentAuthoringContext.Content();
            UnitAuthoringWorkflow.Validate(content.Get<SoldierCatalogAsset>().Definitions.Single(item => item.Metadata.Id == "militia"));
            UnitAuthoringWorkflow.Validate(content.Get<EnemyCatalogAsset>().Definitions.Single(item => item.Metadata.Id == "wolf"));
            var world = AssetDatabase.LoadAssetAtPath<GameObject>(TransportWorkerContentAuthoring.Template);
            var workers = world != null ? world.GetComponent<Landsong.ECS.Authoring.TransportWorkerSettingsAuthoring>() : null;
            if (workers == null || workers.Male == null || workers.Female == null)
                throw new InvalidOperationException("世界模板缺少运输工人直接引用。");
            if (AssetDatabase.FindAssets("t:Prefab", new[] { ContentAssetPaths.Buildings }).Select(AssetDatabase.GUIDToAssetPath).Count(path => Parent(path) != ContentAssetPaths.Buildings + "/LegacyGenerated") < 29)
                throw new InvalidOperationException("建筑根迁移不完整。");
        }

        static void RequireAsset(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) throw new InvalidOperationException("迁移后缺少资源：" + path);
        }

        static void MoveTopLevelFiles(string source, string destination, string extension = null)
        {
            if (!AssetDatabase.IsValidFolder(source)) return;
            EnsureFolder(destination);
            foreach (var path in AssetDatabase.FindAssets("", new[] { source }).Select(AssetDatabase.GUIDToAssetPath).Where(path => Parent(path) == source && !AssetDatabase.IsValidFolder(path)).ToArray())
            {
                if (extension != null && !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
                MoveAsset(path, destination + "/" + Path.GetFileName(path));
            }
        }

        static void MoveFolder(string source, string destination)
        {
            if (!AssetDatabase.IsValidFolder(source)) return;
            if (AssetDatabase.IsValidFolder(destination)) throw new InvalidOperationException("迁移目标目录已存在：" + destination);
            EnsureFolder(Parent(destination));
            MoveAsset(source, destination);
        }

        static void MoveAsset(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(source) == null && !AssetDatabase.IsValidFolder(source))
            {
                if (AssetDatabase.LoadMainAssetAtPath(destination) != null || AssetDatabase.IsValidFolder(destination)) return;
                throw new InvalidOperationException("迁移源不存在：" + source);
            }
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null || AssetDatabase.IsValidFolder(destination))
                throw new InvalidOperationException("迁移目标已存在：" + destination);
            EnsureFolder(Parent(destination));
            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(source + " → " + destination + "：" + error);
            changes.Add(source + " → " + destination);
        }

        static void DeleteIfUnused(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) return;
            var dependents = AssetDatabase.GetAllAssetPaths()
                .Where(candidate => candidate.StartsWith("Assets/Landsong/", StringComparison.Ordinal) || candidate.StartsWith("Assets/AddressableAssetsData/", StringComparison.Ordinal))
                .Where(candidate => candidate != path && !candidate.StartsWith(path + "/", StringComparison.Ordinal))
                .Where(candidate => AssetDatabase.GetDependencies(candidate, false).Contains(path))
                .Take(4).ToArray();
            if (dependents.Length != 0) throw new InvalidOperationException(path + " 仍被引用：" + string.Join(", ", dependents));
            if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("无法删除无引用候选：" + path);
            changes.Add("删除无引用资源 " + path);
        }

        static void DeleteFolderIfEmpty(string path)
        {
            if (!AssetDatabase.IsValidFolder(path)) return;
            if (AssetDatabase.FindAssets("", new[] { path }).Select(AssetDatabase.GUIDToAssetPath).Any(candidate => candidate != path)) return;
            if (AssetDatabase.DeleteAsset(path)) changes.Add("删除空目录 " + path);
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "Assets" || AssetDatabase.IsValidFolder(path)) return;
            var parent = Parent(path);
            EnsureFolder(parent);
            if (AssetDatabase.CreateFolder(parent, Path.GetFileName(path)).Length == 0)
                throw new InvalidOperationException("无法创建目录：" + path);
        }

        static string Parent(string path) => (Path.GetDirectoryName(path) ?? "").Replace('\\', '/');
    }
}
#endif
