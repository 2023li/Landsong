using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    public static class MapPopulationVerification
    {
        public static string ConfigureCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            var content = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapContentAuthoring>(true)).Single();
            bool clean = !scene.isDirty;
            int before = content.Previews.Length;
            string result = MapPopulationTools.FillDefaults(content);
            var forest = MapPopulationTools.Preview(content, MapGenerationKind.Forest);
            var buildings = MapPopulationTools.Preview(content, MapGenerationKind.Buildings);
            if (before != content.Previews.Length)
                throw new InvalidOperationException("Read-only planning changed buildings.");
            if (clean && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unable to save settings.");
            return result + $"\n现有 {before} 个建筑保持不变。设置已" + (clean ? "保存。" : "修改，场景原有未保存内容未自动保存。") + "\n当前地图树林规划：\n" + forest.Report + "\n当前地图建筑规划：\n" + buildings.Report;
        }

        public static string Run()
        {
            var log = new StringBuilder();
            int count = 0;
            void Check(bool value, string label)
            {
                if (!value)
                    throw new InvalidOperationException(label);
                count++;
                log.AppendLine("PASS " + label);
            }

            string Signature(MapPopulationTools.Plan p) => string.Join(";", p.Items.Select(i => $"{i.Definition.name}:{i.Cell}:{i.Rotation}:{i.Patch}"));
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Population verification");
            SceneManager.MoveGameObjectToScene(root, scene);
            var map = ScriptableObject.CreateInstance<MapAsset>();
            var sourceGrid = ScriptableObject.CreateInstance<GridMapDefinition>();
            Undo.IncrementCurrentGroup();
            int testGroup = Undo.GetCurrentGroup();
            try
            {
                var content = root.AddComponent<MapContentAuthoring>();
                content.Buildings = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset");
                var gridObject = new GameObject("grid");
                gridObject.transform.SetParent(root.transform);
                gridObject.transform.rotation = Quaternion.Euler(90, 0, 0);
                content.ConfigureFromTileWorldCreator(gridObject.AddComponent<Grid>(), sourceGrid, Array.Empty<GameObject>());
                MapPopulationTools.FillDefaults(content);
                var s = content.PopulationTools;
                Check(s.Trees.Count == 8 && s.Buildings.Count == 2, "Default lists resolve eight trees and two resource piles");
                MapPopulationTools.FillDefaults(content);
                Check(s.Trees.Count == 8 && s.Buildings.Count == 2, "Filling defaults preserves existing lists");
                map.Size = new Vector2Int(80, 80);
                map.CellSize = 1;
                map.Cells = new CellSource[6400];
                for (int z = 0; z < 80; z++)
                    for (int x = 0; x < 80; x++)
                    {
                        bool edge = x < 3 || z < 3 || x >= 77 || z >= 77;
                        map.Cells[z * 80 + x] = new CellSource
                        {
                            Exists = true,
                            EdgeZone = edge,
                            Buildable = !edge,
                            Traversable = true,
                            Terrain = (ulong)(x < 15 ? TerrainType.水域 : TerrainType.陆地),
                            Elevation = z >= 40 ? 1 : 0,
                            Surface = z >= 40 ? 1 : 0,
                            Height = z >= 40 ? 1 : 0
                        };
                    }

                s.PatchCount = 5;
                s.Radius = new Vector2(5, 7);
                s.IsolatedCount = 20;
                var a = MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Forest);
                var b = MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Forest);
                Check(Signature(a) == Signature(b), "Seed produces identical species, cells and rotations");
                s.Seed++;
                Check(Signature(a) != Signature(MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Forest)), "Changing seed changes layout");
                s.Seed--;
                Check(a.Items.Where(i => i.Patch >= 0).Select(i => i.Patch).Distinct().Count() == 5, "Forest has the requested separate patches");
                Check(a.Items.Count(i => i.Patch == -1) == 20, "Forest also has isolated trees");
                var groups = a.Items.Where(i => i.Patch >= 0).GroupBy(i => i.Patch).ToArray();
                Check(groups.All(g => g.Select(i => i.Definition).Distinct().Count() > 1), "Each grove mixes main and minority species");
                double mainRatio = groups.Sum(g => g.Count(i => i.Definition == g.First().Definition)) / (double)groups.Sum(g => g.Count());
                Check(mainRatio > .65 && mainRatio < .95, "Observed main species proportion is close to 80 percent");
                Check(a.Items.All(i => i.Cell.x >= 15 && i.Cell.x < 77 && i.Cell.y >= 3 && i.Cell.y < 77), "Terrain allow rules and reserved edges are respected");
                Check(a.Items.Select(i => i.Cell).Distinct().Count() == a.Items.Count, "Forest has no overlapping footprints");
                Check(a.Items.All(i => Mathf.Approximately(i.Position.y, i.Cell.y >= 40 ? 1 : 0)), "Placement height comes only from logical Layer");
                var isolated = a.Items.Where(i => i.Patch == -1).ToArray();
                Check(isolated.All(i => isolated.All(j => i == j || Unity.Mathematics.math.distance((Unity.Mathematics.float2)i.Cell, j.Cell) >= s.IsolatedSpacing)), "Isolated tree spacing is enforced");
                s.Layer = 1;
                Check(MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Forest).Items.All(i => i.Cell.y >= 40), "Explicit Layer filter is respected");
                s.Layer = -1;
                var manual = new GameObject("手工树木");
                var folder = GameMapWorkflow.Child(content, GameMapWorkflow.Buildings);
                manual.transform.SetParent(folder.transform);
                manual.transform.position = a.Items[0].Position;
                manual.AddComponent<BuildingVisualAuthoring>().Definition = s.Trees[0].Definition;
                var blocked = MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Forest);
                Check(blocked.Items.All(i => !i.Cell.Equals(a.Items[0].Cell)), "Unregistered formal manual prefab still reserves its footprint");
                var terrainVisual = new GameObject("TWC visual");
                terrainVisual.transform.SetParent(root.transform);
                MapPopulationTools.Apply(content, MapGenerationKind.Forest, blocked);
                Check(content.Previews.Length == blocked.Items.Count && manual != null, "Applying creates bound prefab instances and preserves manual content");
                Check(content.Previews.All(p => PrefabUtility.IsPartOfPrefabInstance(p.gameObject)), "Generated objects retain prefab connections");
                Check(Signature(blocked) == Signature(MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Forest)), "Regeneration ignores its own previous result");
                var scatter = MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Buildings);
                Check(scatter.Items.Count == 40, "Resource counts are fulfilled when legal space is available");
                Check(scatter.Items.All(i => blocked.Items.All(t => !t.Cell.Equals(i.Cell))), "Resource placement respects existing forest occupancy");
                MapPopulationTools.Apply(content, MapGenerationKind.Buildings, scatter);
                int generated = content.Previews.Length;
                MapPopulationTools.Clear(content, MapGenerationKind.Manual);
                Check(content.Previews.Length == 0 && manual != null && terrainVisual != null, "Clear generated preserves manual buildings and TWC visuals");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(content.Previews.Length == generated, "Clear generated is one-step undoable");
                MapPopulationTools.Clear(content, null);
                Check(content.Previews.Length == 0 && manual == null && terrainVisual != null, "Clear all removes manual and generated buildings, preserves terrain");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(content.Previews.Length == generated && folder.transform.Find("手工树木") != null, "Clear all is one-step undoable");
                MapPopulationTools.Clear(content, MapGenerationKind.Manual);
                s.Buildings[0].Count = 10000;
                var limited = MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Buildings);
                Check(limited.Items.Count < 10020 && limited.Report.Contains("合法空位不足"), "Saturation terminates with a partial-count report");
                s.Trees.Add(s.Trees[0]);
                bool rejected = false;
                try
                {
                    MapPopulationTools.BuildPlan(content, map, MapGenerationKind.Forest);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, "Duplicate species are rejected before modifying the scene");
                log.AppendLine("Assertions: " + count);
                return log.ToString();
            }
            finally
            {
                Undo.RevertAllDownToGroup(testGroup);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(map);
                Object.DestroyImmediate(sourceGrid);
                EditorSceneManager.ClosePreviewScene(scene);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/map-population-verification.txt", log.ToString());
            }
        }
    }
}
