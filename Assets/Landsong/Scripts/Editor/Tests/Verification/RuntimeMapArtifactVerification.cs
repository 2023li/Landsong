#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Presentation;
using Landsong.EditorTools;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    public static class RuntimeMapArtifactVerification
    {
        [MenuItem("Landsong/ECS/Verification/Runtime map artifacts")]
        public static string Run()
        {
            var report = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            int assertions = 0;
            void Check(bool condition, string message)
            {
                if (!condition)
                    throw new InvalidOperationException(message);
                assertions++;
                report.AppendLine("PASS " + message);
            }

            try
            {
                RuntimeMapArtifacts.EnsureAllCurrent(false);
                var ignore = File.ReadAllText(".gitignore");
                Check(ignore.Contains("/Assets/LandsongGenerated/", StringComparison.Ordinal), "Runtime map artifact directory is ignored by Git");
                Check(ignore.Contains("/Assets/LandsongGenerated.meta", StringComparison.Ordinal), "Runtime map artifact root meta is ignored by Git");
                var catalog = AssetDatabase.LoadAssetAtPath<EcsMapMenuCatalog>(GameMapPaths.Menu);
                var maps = GameMapPaths.BakedMapPaths().Select(AssetDatabase.LoadAssetAtPath<MapAsset>)
                    .Where(map => map != null).ToDictionary(map => map.MapId, StringComparer.Ordinal);
                Check(catalog != null && catalog.Maps.Length > 0, "Official map catalog contains at least one map");
                foreach (var entry in catalog.Maps)
                {
                    Check(maps.TryGetValue(entry.Id, out var map), "Official map resolves to a baked MapAsset: " + entry.Id);
                    Check(RuntimeMapArtifacts.IsCurrent(map, out _), "Runtime map artifacts match current authored inputs: " + entry.Id);
                    string source = EcsMapIncrementalImport.SourceScene(map);
                    string entity = EcsMapIncrementalImport.TargetScene(map);
                    Check(new FileInfo(source).Length < 100L * 1024 * 1024, "Authored map scene stays below 100 MiB: " + entry.Id);
                    Check(new FileInfo(entity).Length < 100L * 1024 * 1024, "Entity scene stays below 100 MiB: " + entry.Id);
                    Check(!File.ReadAllText(source).Contains("--- !u!43", StringComparison.Ordinal), "Authored map scene contains no inline Mesh objects: " + entry.Id);
                    Check(!File.ReadAllText(entity).Contains("--- !u!43", StringComparison.Ordinal), "Entity scene contains no inline Mesh objects: " + entry.Id);
                    string meshRoot = RuntimeMapArtifacts.MeshRoot(entry.Id) + "/";
                    var meshes = AssetDatabase.GetDependencies(entity, true)
                        .Where(path => path.StartsWith(meshRoot, StringComparison.Ordinal)).ToArray();
                    Check(meshes.Length > 0 && meshes.All(path => AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path) != null), "Entity scene references external runtime Mesh assets: " + entry.Id);
                    Check(meshes.Select(AssetDatabase.AssetPathToGUID).Distinct(StringComparer.Ordinal).Count() == meshes.Length, "Runtime Mesh GUIDs are unique: " + entry.Id);
                }
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error)
            {
                report.AppendLine("FAIL " + error);
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/runtime-map-artifacts-verification.txt", report.ToString());
            }
        }
    }
}
#endif
