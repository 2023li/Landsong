#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    [Serializable] public sealed class PortraitImportLayer
    { public PortraitLayer Layer; public PortraitTint Tint; public string File = ""; }
    [Serializable] public sealed class PortraitImportDraft
    {
        public string Id = "";
        public PortraitPartType Type = PortraitPartType.Hair;
        public bool Male = true, Female = true;
        public float Weight = 1;
        public List<PortraitImportLayer> Layers = new List<PortraitImportLayer>();
        public byte Genders => (byte)((Male ? 1 : 0) | (Female ? 2 : 0));
        public void ResetLayers() => Layers = new List<PortraitImportLayer>();
        public bool AddLayer(PortraitLayer layer)
        {
            if (!PortraitPartRules.Allowed(Type).Contains(layer) || Layers.Any(item => item.Layer == layer)) return false;
            Layers.Add(new PortraitImportLayer { Layer = layer, Tint = PortraitPartRules.DefaultTint(layer) });
            return true;
        }
    }
    public static class PortraitPartImporter
    {
        public const string OutputRoot = "Assets/Landsong/Art/Portraits/Parts";
        public const int MaximumFileBytes = 4 * 1024 * 1024;
        sealed class Input { public PortraitLayer Layer; public PortraitTint Tint; public byte[] Bytes; }
        public static string Destination(PortraitImportDraft draft) => OutputRoot + "/" + draft.Type + "/" + draft.Id;
        public static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64 || !Regex.IsMatch(id, @"\A[A-Za-z][A-Za-z0-9_.-]*\z") || id.EndsWith(".", StringComparison.Ordinal))
                throw new InvalidOperationException("部件标识须以英文字母开头，最多 64 位，只允许英文、数字、点、下划线和短横线，不能以点结尾。");
            string first = id.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(first) || Regex.IsMatch(first, @"\A(COM|LPT)[1-9]\z") || id.StartsWith("placeholder.", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("该标识为系统或程序占位保留，请换一个标识。");
        }
        public static byte[] ReadPng(string file, int resolution)
        {
            if (resolution != 32 && resolution != 64) throw new InvalidOperationException("项目肖像尺寸必须为 32 或 64。");
            if (string.IsNullOrWhiteSpace(file) || !string.Equals(Path.GetExtension(file), ".png", StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(file))
                throw new InvalidOperationException("请选择存在的 PNG 文件。");
            var info = new FileInfo(file);
            if (info.Length < 33 || info.Length > MaximumFileBytes) throw new InvalidOperationException("PNG 文件无效或超过 4 MB。");
            byte[] bytes = System.IO.File.ReadAllBytes(file);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (!bytes.Take(8).SequenceEqual(signature) || bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R') throw new InvalidOperationException("文件内容不是有效的 PNG 图像。");
            uint Number(int at) => (uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3];
            uint width = Number(16), height = Number(20);
            if (width != resolution || height != resolution) throw new InvalidOperationException($"{Path.GetFileName(file)} 原始尺寸是 {width}×{height}，项目要求 {resolution}×{resolution}。不允许缩放或裁切后导入。");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false) || texture.width != resolution || texture.height != resolution) throw new InvalidOperationException("PNG 数据损坏，无法解码：" + Path.GetFileName(file));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            return bytes;
        }
        static List<Input> Prepare(PortraitConfig config, PortraitImportDraft draft)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("请退出 Play 并等待编译完成后再导入。");
            if (config == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(config))) throw new InvalidOperationException("需要已保存的项目 PortraitConfig 资产。");
            if (draft == null || draft.Layers == null || draft.Layers.Any(l => l == null)) throw new InvalidOperationException("部件草稿无效。");
            ValidateId(draft.Id);
            if (draft.Type > PortraitPartType.Accessory) throw new InvalidOperationException("当前导入窗口只接入运行时使用的 13 类单选部件。");
            var selected = draft.Layers.Where(l => !string.IsNullOrWhiteSpace(l.File)).ToArray();
            PortraitPartRules.ValidateShape(draft.Id, draft.Type, draft.Genders, draft.Weight, selected.Select(l => l.Layer).ToArray());
            int hash = PortraitLibraryBuilder.StableId(draft.Id);
            if ((config.Parts ?? Array.Empty<PortraitPartSource>()).Any(p => p != null && (string.Equals(p.Id, draft.Id, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(p.Id) && PortraitLibraryBuilder.StableId(p.Id) == hash)))
                throw new InvalidOperationException("部件标识已存在或与已有稳定 ID 冲突，不允许覆盖：" + draft.Id);
            string destination = Destination(draft);
            if (Directory.Exists(destination) || System.IO.File.Exists(destination) || System.IO.File.Exists(destination + ".meta")) throw new InvalidOperationException("目标路径已存在，不允许覆盖：" + destination);
            using (var current = PortraitLibraryBuilder.Build(config))
                if (PortraitOps.Find(ref current.Value, hash) >= 0) throw new InvalidOperationException("部件标识与当前库中的稳定 ID 冲突。");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inputs = new List<Input>();
            foreach (var layer in selected)
            {
                if (!Enum.IsDefined(typeof(PortraitTint), layer.Tint)) throw new InvalidOperationException("无效的染色通道。");
                if (draft.Type == PortraitPartType.Hair && layer.Tint != PortraitTint.Hair) throw new InvalidOperationException("前发和后发都必须使用发色染色。");
                if (!paths.Add(Path.GetFullPath(layer.File))) throw new InvalidOperationException("添加多个渲染层时，各层不能重复选择同一张文件。");
                inputs.Add(new Input { Layer = layer.Layer, Tint = layer.Tint, Bytes = ReadPng(layer.File, config.Resolution) });
            }
            return inputs;
        }
        public static string Validate(PortraitConfig config, PortraitImportDraft draft)
        { try { Prepare(config, draft); return ""; } catch (Exception e) { return e.Message; } }
        public static string Import(PortraitConfig config, PortraitImportDraft draft, Action<string> probe = null)
        {
            // Validate and retain the exact source bytes before creating any project files.
            var inputs = Prepare(config, draft); string directory = Destination(draft);
            var original = config.Parts; bool created = false, registered = false; string folderGuid = null;
            try
            {
                EnsureFolder(OutputRoot + "/" + draft.Type);
                folderGuid = AssetDatabase.CreateFolder(OutputRoot + "/" + draft.Type, draft.Id);
                if (string.IsNullOrEmpty(folderGuid)) throw new IOException("无法创建部件目录。");
                string actual = AssetDatabase.GUIDToAssetPath(folderGuid);
                if (actual != directory) { AssetDatabase.DeleteAsset(actual); throw new IOException("目标目录发生变化，已取消导入：" + directory); }
                created = true; var renders = new List<PortraitRenderSource>();
                foreach (var input in inputs)
                {
                    string path = directory + "/" + input.Layer + ".png";
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) stream.Write(input.Bytes, 0, input.Bytes.Length);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new IOException("无法读取 PNG 导入设置：" + path);
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.crunchedCompression = false; importer.mipmapEnabled = false; importer.isReadable = true; importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
                    importer.maxTextureSize = config.Resolution; importer.spritePixelsPerUnit = 32;
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = new Vector2(.5f, .5f); settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings); importer.SaveAndReimport();
                    renders.Add(new PortraitRenderSource { Layer = input.Layer, Tint = input.Tint, Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
                    probe?.Invoke("file-imported");
                }
                var part = new PortraitPartSource { Id = draft.Id, Type = draft.Type, Genders = draft.Genders, Weight = draft.Weight, Renders = renders.ToArray() };
                PortraitPartRules.ValidateSource(part, config.Resolution);
                var candidate = UnityEngine.Object.Instantiate(config);
                try { candidate.Parts = (original ?? Array.Empty<PortraitPartSource>()).Append(part).ToArray(); using var library = PortraitLibraryBuilder.Build(candidate); }
                finally { UnityEngine.Object.DestroyImmediate(candidate); }
                probe?.Invoke("validated");
                config.Parts = (original ?? Array.Empty<PortraitPartSource>()).Append(part).ToArray(); registered = true;
                EditorUtility.SetDirty(config); probe?.Invoke("registered"); AssetDatabase.SaveAssetIfDirty(config);
                return directory;
            }
            catch
            {
                if (registered) { config.Parts = original; EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config); }
                if (created && (AssetDatabase.AssetPathToGUID(directory) != folderGuid || !AssetDatabase.DeleteAsset(directory))) throw new IOException("导入失败且临时目录归属变化或清理失败，请检查：" + directory);
                throw;
            }
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (Directory.Exists(path)) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); if (AssetDatabase.IsValidFolder(path)) return; throw new IOException("已有资源目录尚未正确导入：" + path); }
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            if (string.IsNullOrEmpty(guid)) throw new IOException("无法创建资源目录：" + path);
            string actual = AssetDatabase.GUIDToAssetPath(guid);
            if (actual != path) { AssetDatabase.DeleteAsset(actual); throw new IOException("资源目录发生变化，已取消导入：" + path); }
        }
    }
}
#endif
