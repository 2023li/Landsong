using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GiantGrey.TileWorldCreator;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.GridSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class TerrainEnumVerification
    {
        [MenuItem("Landsong/地图/检查当前 Blueprint 地形映射")]
        static void Inspect() => Debug.Log(InspectCurrent());
        public static string InspectCurrent()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var managers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TileWorldCreatorManager>(true)).ToArray();
            if (managers.Length != 1)
                throw new InvalidOperationException("当前场景需要唯一 TWC Manager。");
            var content = managers[0].GetComponent<MapContentAuthoring>();
            if (content == null || content.TerrainRules == null)
                throw new InvalidOperationException("当前地图未绑定地形规则。");
            var result = LayerTerrainCompiler.Compile(managers[0].configuration, content);
            var text = new StringBuilder();
            text.AppendLine("当前地图：" + scene.name);
            text.AppendLine("同层覆盖顺序：" + string.Join(" > ", content.TerrainRules.overlapRules.HighToLow));
            foreach (var group in result.Groups)
            {
                text.AppendLine(group.Name + " 逻辑高度=" + group.Height);
                foreach (var terrain in result.Cells.Values.Where(c => c.Group == group).GroupBy(c => c.Rule.Terrain).OrderBy(g => (int)g.Key))
                    text.AppendLine("  " + terrain.Key + "：" + terrain.Count() + " 格（覆盖后）");
            }

            text.AppendLine("斜坡条数=" + result.Slopes.Count + "；下层导航格=" + result.Additional.Count);
            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText("Library/LandsongEcs/current-terrain-report.txt", text.ToString());
            return text.ToString();
        }

        public static string Run()
        {
            var log = new StringBuilder();
            int count = 0;
            var owned = new List<UnityEngine.Object>();
            void Check(bool ok, string message)
            {
                if (!ok)
                    throw new InvalidOperationException(message);
                count++;
                log.AppendLine("PASS " + message);
            }

            void Reject(Action action, string message)
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

            T Asset<T>()
                where T : ScriptableObject
            {
                var a = ScriptableObject.CreateInstance<T>();
                owned.Add(a);
                return a;
            }

            try
            {
                Check(TerrainTypes.ParseLegacy("石地") == TerrainType.石地 && TerrainTypes.ParseLegacy("石矿") == TerrainType.石地, "Stone names migrate to the explicit stone enum");
                Reject(() => TerrainTypes.ParseLegacy("unknown"), "Unknown terrain cannot silently hash to another terrain");
                Check(!TerrainTypes.Single(TerrainType.陆地 | TerrainType.石地), "A logical cell cannot contain multiple terrain types");
                Check(TerrainTypes.Allows(TerrainType.石地, TerrainType.石地 | TerrainType.泥地, TerrainType.None), "Allowed terrain supports multiple enum selections");
                Check(!TerrainTypes.Allows(TerrainType.石地, TerrainType.All, TerrainType.石地), "Excluded terrain takes precedence over allowed terrain");
                Check(!TerrainTypes.Allows(TerrainType.陆地, TerrainType.None, TerrainType.None), "Empty allowed terrain rejects placement");
                var order = Asset<TerrainOverlapRules>();
                var rules = Asset<MapTerrainRules>();
                rules.overlapRules = order;
                rules.useLayerBlueprintRules = true;
                var config = Asset<Configuration>();
                config.width = config.height = 2;
                config.cellSize = 1;
                config.blueprintLayerFolders.Clear();
                var folder = new BlueprintLayerFolder("Layer0");
                config.blueprintLayerFolders.Add(folder);
                foreach (var type in new[]
                {
                    TerrainType.陆地,
                    TerrainType.石地,
                    TerrainType.泥地
                }

                )
                {
                    var bp = Asset<BlueprintLayer>();
                    bp.layerName = "L0_" + type;
                    bp.defaultLayerHeight = 0;
                    bp.allPositions.Add(Vector2.zero);
                    folder.blueprintLayers.Add(bp);
                    rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = type.ToString(), Terrain = type, Buildable = type != TerrainType.泥地 });
                }

                GridMapCellRecord Winner() => LayerTerrainCompiler.Compile(config, rules).Primary.Single();
                Check(Winner().Terrain == TerrainType.石地 && Winner().Buildable, "Stone replaces land and mud using the order SO");
                folder.blueprintLayers.Reverse();
                Check(Winner().Terrain == TerrainType.石地, "TWC list order does not change the terrain winner");
                order.HighToLow.Remove(TerrainType.泥地);
                order.HighToLow.Insert(0, TerrainType.泥地);
                Check(Winner().Terrain == TerrainType.泥地 && !Winner().Buildable, "Changing only the SO order replaces both terrain and permissions");
                rules.blueprintRules[0].Terrain = TerrainType.All;
                Reject(() => Winner(), "Mapping a Blueprint to combined enum values is rejected");
                rules.blueprintRules[0].Terrain = TerrainType.陆地;
                order.HighToLow.Remove(TerrainType.石地);
                Reject(() => Winner(), "Mapped terrain missing from the order is rejected");
                order.HighToLow.Add(TerrainType.石地);
                var duplicate = Asset<BlueprintLayer>();
                duplicate.layerName = "重复泥地";
                duplicate.defaultLayerHeight = 0;
                duplicate.allPositions.Add(Vector2.zero);
                folder.blueprintLayers.Add(duplicate);
                rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = duplicate.layerName, Terrain = TerrainType.泥地 });
                Reject(() => Winner(), "Same terrain with conflicting permissions is rejected");
                var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset");
                var modules = new BuildingCapabilitiesSource();
                modules.Placement.AllowedTerrains = TerrainType.石地 | TerrainType.泥地;
                modules.Placement.ExcludedTerrains = TerrainType.泥地;
                var map = Asset<MapAsset>();
                map.Size = new Vector2Int(2, 1);
                map.CellSize = 1;
                map.ElevationStep = 1;
                map.Cells = new[]
                {
                    new CellSource
                    {
                        Exists = true,
                        Buildable = true,
                        Traversable = true,
                        Terrain = (ulong)TerrainType.石地
                    },
                    new CellSource
                    {
                        Exists = true,
                        Buildable = true,
                        Traversable = true,
                        Terrain = (ulong)TerrainType.泥地
                    }
                };
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var blob = ref builder.ConstructRoot<BuildingCatalogBlob>();
                    var defs = builder.Allocate(ref blob.Definitions, 2);
                    for (int i = 0; i < 2; i++)
                    {
                        defs[i].Metadata.Id = i == 0 ? "exclude-mud" : "allow-both";
                        defs[i].Footprint = new int2(2, 1);
                    }

                    // The placement record has no relative arrays; values are copied after compiling with a mutable builder.
                    defs[0].Capabilities.Placement.AllowedTerrains = modules.Placement.AllowedTerrains;
                    defs[0].Capabilities.Placement.ExcludedTerrains = modules.Placement.ExcludedTerrains;
                    defs[1].Capabilities.Placement.AllowedTerrains = modules.Placement.AllowedTerrains;
                    Check((int)defs[0].Capabilities.Placement.AllowedTerrains == 12 && (int)defs[0].Capabilities.Placement.ExcludedTerrains == 8, "Building placement stores the two enum masks directly");
                    using var content = builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
                    using var grid = GameWorldMapAuthoring.BuildGrid(map);
                    using var world = new World("Terrain enum verification");
                    var em = world.EntityManager;
                    var root = em.CreateEntity();
                    em.AddComponentData(root, new BuildingCatalog { Value = content });
                    em.AddComponentData(root, new GridData { Value = grid, CellSize = 1 });
                    em.AddBuffer<Occupancy>(root).Resize(2, NativeArrayOptions.ClearMemory);
                    Check(!GridOps.CanPlace(em, root, BuildingId.FromIndex(0), int2.zero, 0), "Runtime rejects excluded terrain on the second footprint cell");
                    Check(GridOps.CanPlace(em, root, BuildingId.FromIndex(1), int2.zero, 0), "Runtime permits a mixed footprint when every cell is allowed");
                    Check(!GridOps.TerrainAllowed(em, root, BuildingId.FromIndex(0), (ulong)TerrainType.泥地), "Connection endpoint checks use the same exclusion rule");
                }

                var authored = Asset<GridMapDefinition>();
                authored.ReplaceBakedData(Vector2Int.zero, new Vector2Int(2, 1), 1, 1, new[] { new GridMapCellRecord(new GridPosition(0, 0), true, true, 0, 0, TerrainType.石地), new GridMapCellRecord(new GridPosition(1, 0), true, true, 0, 0, TerrainType.泥地) }, "", "", "", "");
                var footprint = new GridFootprint(new GridPosition(0, 0), new Vector2Int(2, 1));
                Check(!GridPlacementRuleEvaluator.TryValidateStaticPlacement(authored, footprint, modules.Placement.AllowedTerrains, modules.Placement.ExcludedTerrains, out var failure, out var failedCell) && failure == GridPlacementFailureReason.TerrainMismatch && failedCell.Equals(new GridPosition(1, 0)), "Editor placement reports the excluded cell consistently with runtime");
                Check(GridPlacementRuleEvaluator.TryValidateStaticPlacement(authored, footprint, modules.Placement.AllowedTerrains, TerrainType.None, out _, out _), "Editor placement permits the same mixed footprint");
                Check(catalog.Definitions.First(d => d.Metadata.Id == "b采石场").Capabilities.Placement.AllowedTerrains == TerrainType.石地, "Existing quarry migrated from land plus ore to stone only");
                Check(catalog.Definitions.All(d => TerrainTypes.Valid(d.Capabilities.Placement.AllowedTerrains)), "All migrated building definitions have valid masks");
                log.AppendLine("Assertions: " + count);
                return log.ToString();
            }
            finally
            {
                foreach (var a in owned.AsEnumerable().Reverse())
                    UnityEngine.Object.DestroyImmediate(a);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/terrain-enum-verification.txt", log.ToString());
            }
        }
    }
}
