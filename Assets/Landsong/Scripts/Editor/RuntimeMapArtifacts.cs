#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Landsong.ECS.Authoring;
using Landsong.ECS.Editor;
using Landsong.ECS.Presentation;
using Landsong.GridSystem;
using GiantGrey.TileWorldCreator;
using GiantGrey.TileWorldCreator.Components;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    /// <summary>
    /// Owns disposable map render artifacts. Authored TWC data stays in GameMaps while terrain
    /// meshes are regenerated locally and referenced by the lightweight ECS entity scene.
    /// </summary>
    public static class RuntimeMapArtifacts
    {
        public const string Root = "Assets/LandsongGenerated/GameMaps";
        const string ManifestRoot = "Library/LandsongGenerated/GameMaps";
        const int GeneratorVersion = 1;

        [Serializable]
        sealed class Manifest
        {
            public int GeneratorVersion;
            public string MapId;
            public string SourceHash;
            public string EntityScene;
            public string[] Meshes;
        }

        sealed class MeshRecord
        {
            public Mesh Source;
            public string Key;
            public string Path;
            public string Guid;
        }

        public static string MapRoot(string mapId) => Root + "/" + Safe(mapId);
        public static string MeshRoot(string mapId) => MapRoot(mapId) + "/Meshes";
        static string ManifestPath(string mapId) => ManifestRoot + "/" + Safe(mapId) + ".json";

        public static int ExternalizeMeshes(MapContentAuthoring content)
        {
            if (content == null || string.IsNullOrWhiteSpace(content.MapId))
                throw new InvalidOperationException("地图缺少稳定 MapId，不能生成运行 Mesh。");
            var roots = content.MapVisualRoots.Where(r => r != null).OrderBy(HierarchyKey, StringComparer.Ordinal).ToArray();
            if (roots.Length == 0)
                throw new InvalidOperationException("TWC 没有生成地图表现，不能生成运行 Mesh。");

            string folder = MeshRoot(content.MapId);
            GameMapPaths.Folder(folder);
            var records = new Dictionary<int, MeshRecord>();
            void Add(Mesh mesh, Component owner, string slot)
            {
                if (mesh == null || records.ContainsKey(mesh.GetInstanceID()))
                    return;
                records.Add(mesh.GetInstanceID(), new MeshRecord
                {
                    Source = mesh,
                    Key = HierarchyKey(owner.gameObject) + "/" + slot
                });
            }

            foreach (var root in roots)
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true).OrderBy(c => HierarchyKey(c.gameObject), StringComparer.Ordinal))
                    Add(filter.sharedMesh, filter, "filter");
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(c => HierarchyKey(c.gameObject), StringComparer.Ordinal))
                    Add(renderer.sharedMesh, renderer, "skinned");
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true).OrderBy(c => HierarchyKey(c.gameObject), StringComparer.Ordinal))
                    Add(collider.sharedMesh, collider, "collider");
            }

            var replacements = new Dictionary<int, Mesh>();
            foreach (var record in records.Values)
            {
                record.Path = folder + "/Mesh_" + Digest(content.MapId + "\n" + record.Key).Substring(0, 24) + ".asset";
                record.Guid = Digest("landsong-runtime-map-mesh-v1\n" + content.MapId + "\n" + record.Key).Substring(0, 32);
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var record in records.Values.OrderBy(r => r.Key, StringComparer.Ordinal))
                {
                    var target = AssetDatabase.LoadAssetAtPath<Mesh>(record.Path);
                    if (target == null)
                    {
                        target = Object.Instantiate(record.Source);
                        target.name = Path.GetFileNameWithoutExtension(record.Path);
                        AssetDatabase.CreateAsset(target, record.Path);
                    }
                    else if (record.Source != target)
                    {
                        EditorUtility.CopySerialized(record.Source, target);
                        target.name = Path.GetFileNameWithoutExtension(record.Path);
                        EditorUtility.SetDirty(target);
                    }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            bool changedGuid = false;
            foreach (var record in records.Values)
                changedGuid |= EnsureAssetGuid(record.Path, record.Guid);
            if (changedGuid)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var record in records.Values)
            {
                var target = AssetDatabase.LoadAssetAtPath<Mesh>(record.Path);
                if (target == null || AssetDatabase.AssetPathToGUID(record.Path) != record.Guid)
                    throw new InvalidOperationException("运行 Mesh 没有使用确定性 GUID：" + record.Path);
                replacements.Add(record.Source.GetInstanceID(), target);
            }

            foreach (var root in roots)
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null && replacements.TryGetValue(filter.sharedMesh.GetInstanceID(), out var mesh))
                    {
                        filter.sharedMesh = mesh;
                        EditorUtility.SetDirty(filter);
                    }
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (renderer.sharedMesh != null && replacements.TryGetValue(renderer.sharedMesh.GetInstanceID(), out var mesh))
                    {
                        renderer.sharedMesh = mesh;
                        EditorUtility.SetDirty(renderer);
                    }
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                    if (collider.sharedMesh != null && replacements.TryGetValue(collider.sharedMesh.GetInstanceID(), out var mesh))
                    {
                        collider.sharedMesh = mesh;
                        EditorUtility.SetDirty(collider);
                    }
            }
            AssetDatabase.SaveAssets();
            return records.Count;
        }

        public static void MakePreviewTransient(MapContentAuthoring content)
        {
            var roots = content.MapVisualRoots.Where(r => r != null).ToArray();
            foreach (var root in roots)
            {
                if (root == content.gameObject || !root.transform.IsChildOf(content.transform))
                    throw new InvalidOperationException("地图表现根必须是 MapContentAuthoring 的子对象：" + root.name);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    transform.gameObject.hideFlags |= HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }
            content.ConfigureFromTileWorldCreator(content.UnityGrid, content.MapDefinition, Array.Empty<GameObject>());
        }

        public static void GeneratePreview(MapContentAuthoring content)
        {
            GenerateTerrain(content);
            MakePreviewTransient(content);
        }

        public static void DeleteStaleMeshes(string mapId)
        {
            string folder = MeshRoot(mapId);
            if (!AssetDatabase.IsValidFolder(folder))
                return;
            // The entity scene is authoritative after a successful bake. Anything it does not
            // depend on is a stale disposable artifact from an older TWC result.
            var map = FindMap(mapId);
            if (map == null || string.IsNullOrEmpty(map.EntitySceneGuid))
                return;
            string entityScene = AssetDatabase.GUIDToAssetPath(map.EntitySceneGuid);
            var live = new HashSet<string>(AssetDatabase.GetDependencies(entityScene, true), StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!live.Contains(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        public static void WriteManifest(MapContentAuthoring content)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string scene = content.gameObject.scene.path;
            var meshes = AssetDatabase.FindAssets("t:Mesh", new[] { MeshRoot(content.MapId) })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var manifest = new Manifest
            {
                GeneratorVersion = GeneratorVersion,
                MapId = content.MapId,
                SourceHash = SourceHash(scene),
                EntityScene = AssetDatabase.GetAssetPath(content.EntityScene),
                Meshes = meshes
            };
            string path = ManifestPath(content.MapId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
        }

        public static void MarkCurrent(MapContentAuthoring content)
        {
            content.TargetMap.RuntimeArtifactGeneratorVersion = GeneratorVersion;
            content.TargetMap.RuntimeArtifactSourceHash = SourceHash(content.gameObject.scene.path);
            EditorUtility.SetDirty(content.TargetMap);
        }

        public static bool IsCurrent(MapAsset map, out string reason)
        {
            reason = null;
            if (map == null || string.IsNullOrWhiteSpace(map.MapId))
            {
                reason = "地图资产或 MapId 无效";
                return false;
            }
            string path = ManifestPath(map.MapId);
            if (!File.Exists(path))
            {
                reason = "缺少本地生成清单";
                return false;
            }
            Manifest manifest;
            try { manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path)); }
            catch (Exception e) { reason = "生成清单损坏：" + e.Message; return false; }
            string source;
            try { source = EcsMapIncrementalImport.SourceScene(map); }
            catch (Exception e) { reason = e.Message; return false; }
            if (manifest == null || manifest.GeneratorVersion != GeneratorVersion || manifest.MapId != map.MapId || manifest.SourceHash != SourceHash(source))
            {
                reason = "制图输入或生成器版本已变化";
                return false;
            }
            if (manifest.Meshes == null || manifest.Meshes.Length == 0 || manifest.Meshes.Any(p => AssetDatabase.LoadAssetAtPath<Mesh>(p) == null))
            {
                reason = "运行 Mesh 缺失";
                return false;
            }
            if (string.IsNullOrEmpty(manifest.EntityScene) || AssetDatabase.LoadAssetAtPath<SceneAsset>(manifest.EntityScene) == null)
            {
                reason = "地图 Entity Scene 缺失";
                return false;
            }
            return true;
        }

        public static bool AreAllCurrent(out string reason)
        {
            reason = null;
            var catalog = AssetDatabase.LoadAssetAtPath<EcsMapMenuCatalog>(GameMapPaths.Menu);
            if (catalog == null)
            {
                reason = "缺少地图菜单目录";
                return false;
            }
            var maps = GameMapPaths.BakedMapPaths().Select(AssetDatabase.LoadAssetAtPath<MapAsset>)
                .Where(m => m != null).ToDictionary(m => m.MapId, StringComparer.Ordinal);
            foreach (var entry in catalog.Maps)
            {
                if (!maps.TryGetValue(entry.Id, out var map))
                {
                    reason = entry.Id + "：地图菜单引用了未烘焙地图";
                    return false;
                }
                if (!IsCurrent(map, out var mapReason))
                {
                    reason = entry.Id + "：" + mapReason;
                    return false;
                }
            }
            return true;
        }

        [MenuItem("Landsong/地图/生成全部运行地图")]
        public static void GenerateAll() => EnsureAllCurrent(true);

        [MenuItem("Landsong/地图/清理并重建全部运行地图")]
        public static string RebuildAll()
        {
            if (AssetDatabase.IsValidFolder(Root) && !AssetDatabase.DeleteAsset(Root))
                throw new IOException("无法清理运行地图生成目录：" + Root);
            if (Directory.Exists(ManifestRoot))
                Directory.Delete(ManifestRoot, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return EnsureAllCurrent(false);
        }

        public static string EnsureAllCurrent(bool force)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EcsMapMenuCatalog>(GameMapPaths.Menu);
            if (catalog == null)
                throw new InvalidOperationException("缺少地图菜单目录。");
            var maps = GameMapPaths.BakedMapPaths().Select(AssetDatabase.LoadAssetAtPath<MapAsset>)
                .Where(m => m != null).ToDictionary(m => m.MapId, StringComparer.Ordinal);
            var results = new List<string>();
            foreach (var entry in catalog.Maps)
            {
                if (!maps.TryGetValue(entry.Id, out var map))
                    throw new InvalidOperationException("地图菜单引用了未烘焙地图：" + entry.Id);
                if (!force && IsCurrent(map, out _))
                {
                    results.Add(entry.Id + "：当前");
                    continue;
                }
                if (!force && CanRegenerateMeshes(map))
                    RegenerateMeshes(map);
                else
                    EcsMapIncrementalImport.Import(map);
                if (!IsCurrent(map, out var reason))
                    throw new InvalidOperationException(entry.Id + " 生成后仍无效：" + reason);
                results.Add(entry.Id + "：已生成");
            }
            return string.Join("\n", results);
        }

        static bool CanRegenerateMeshes(MapAsset map)
        {
            string source;
            try { source = EcsMapIncrementalImport.SourceScene(map); }
            catch { return false; }
            return map.RuntimeArtifactGeneratorVersion == GeneratorVersion
                && map.RuntimeArtifactSourceHash == SourceHash(source)
                && !string.IsNullOrEmpty(map.EntitySceneGuid)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(map.EntitySceneGuid)) != null;
        }

        static void RegenerateMeshes(MapAsset map)
        {
            string path = EcsMapIncrementalImport.SourceScene(map);
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("制图源场景有未保存修改，不能执行构建前 Mesh 重建：" + path);
            var active = SceneManager.GetActiveScene();
            if (opened)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var content = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapContentAuthoring>(true)).Single();
                GenerateTerrain(content);
                ExternalizeMeshes(content);
                MakePreviewTransient(content);
                AssetDatabase.SaveAssets();
                WriteManifest(content);
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid())
                    SceneManager.SetActiveScene(active);
            }
        }

        static void GenerateTerrain(MapContentAuthoring content)
        {
            var manager = content.GetComponent<TileWorldCreatorManager>();
            var profile = content.BakeProfile as TileWorldCreatorMapBakeProfile;
            if (manager == null || manager.configuration == null || profile == null)
                throw new InvalidOperationException("地图缺少 TWC Manager、Configuration 或烘焙配置：" + content.MapId);
            TileWorldCreatorWorldGenerator.EnsureDeterministicSeed(manager.configuration, profile);
            if (!TileWorldCreatorWorldGenerator.TryGenerate(manager, profile, out var error))
                throw new InvalidOperationException(error);
            var roots = manager.GetComponentsInChildren<LayerIdentifier>(true).Select(i => i == null ? null : i.gameObject).Where(r => r != null).Distinct().ToArray();
            content.ConfigureFromTileWorldCreator(content.UnityGrid, content.MapDefinition, roots);
        }

        static MapAsset FindMap(string mapId) => GameMapPaths.BakedMapPaths().Select(p => AssetDatabase.LoadAssetAtPath<MapAsset>(p)).FirstOrDefault(m => m != null && m.MapId == mapId);
        static string SourceHash(string scenePath)
        {
            var text = new StringBuilder().Append(GeneratorVersion).Append('\n').Append(scenePath).Append(':').Append(FileDigest(scenePath));
            foreach (string dependency in AssetDatabase.GetDependencies(scenePath, true)
                .Where(p => p != scenePath && !p.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase) && !p.Contains("/Generated/"))
                .OrderBy(p => p, StringComparer.Ordinal))
                text.Append('\n').Append(dependency).Append(':').Append(AssetDatabase.GetAssetDependencyHash(dependency));
            return Digest(text.ToString());
        }
        static string FileDigest(string path)
        {
            using var algorithm = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(algorithm.ComputeHash(stream).Select(b => b.ToString("x2")));
        }
        static string Digest(string text)
        {
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
        }
        static bool EnsureAssetGuid(string assetPath, string expected)
        {
            string actual = AssetDatabase.AssetPathToGUID(assetPath);
            if (actual == expected)
                return false;
            string collision = AssetDatabase.GUIDToAssetPath(expected);
            if (!string.IsNullOrEmpty(collision) && !string.Equals(collision, assetPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("确定性 Mesh GUID 冲突：" + assetPath + " / " + collision);
            string meta = assetPath + ".meta";
            if (!File.Exists(meta))
                throw new FileNotFoundException("运行 Mesh 缺少 meta。", meta);
            string yaml = File.ReadAllText(meta);
            string replaced = Regex.Replace(yaml, "(?m)^guid: [0-9a-f]{32}$", "guid: " + expected);
            if (replaced == yaml)
                throw new InvalidDataException("运行 Mesh meta 缺少 GUID：" + meta);
            File.WriteAllText(meta, replaced, new UTF8Encoding(false));
            return true;
        }
        static string Safe(string value) => string.Concat((value ?? "map").Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
        static string HierarchyKey(GameObject gameObject)
        {
            var parts = new List<string>();
            for (var t = gameObject.transform; t != null; t = t.parent)
                parts.Add(t.name + "[" + t.GetSiblingIndex() + "]");
            parts.Reverse();
            return string.Join("/", parts);
        }
    }

    public sealed class RuntimeMapArtifactBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -2000;
        public void OnPreprocessBuild(BuildReport report)
        {
            try { RuntimeMapArtifacts.EnsureAllCurrent(false); }
            catch (Exception e) { throw new BuildFailedException("运行地图生成失败：" + e.Message); }
        }
    }

    [InitializeOnLoad]
    internal static class RuntimeMapArtifactPlayGuard
    {
        const string PendingKey = "Landsong.RuntimeMapArtifacts.PreparePlay";
        public static bool Preparing => SessionState.GetBool(PendingKey, false);

        static RuntimeMapArtifactPlayGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || Preparing || RuntimeMapArtifacts.AreAllCurrent(out _))
                return;
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = false;
        }

        static void Tick()
        {
            if (!Preparing || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            SessionState.EraseBool(PendingKey);
            try
            {
                RuntimeMapArtifacts.EnsureAllCurrent(false);
                if (!RuntimeMapArtifacts.AreAllCurrent(out var reason))
                    throw new InvalidOperationException(reason);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("运行地图生成失败", "进入 Play 前无法生成运行地图：\n" + e.Message, "确定");
            }
        }
    }
}
#endif
