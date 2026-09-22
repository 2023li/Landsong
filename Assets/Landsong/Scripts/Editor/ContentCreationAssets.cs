#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools
{
    // Owns only newly created paths. Existing authoring assets are never regenerated.
    internal sealed class ContentCreationAssets : IDisposable
    {
        readonly List<string> assets = new List<string>();
        readonly List<string> folders = new List<string>();
        bool complete;

        public static void RequireIdentity(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, @"^[\p{L}\p{N}][\p{L}\p{N}_.-]*$")
                || Encoding.UTF8.GetByteCount(id) > 120 || string.IsNullOrWhiteSpace(name)
                || Encoding.UTF8.GetByteCount(name) > 120)
                throw new InvalidOperationException("稳定 ID 须为字母/数字开头，仅含字母、数字、下划线、点或连字符；ID 与名称均须填写且不超过 120 UTF-8 字节。");
        }

        public static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请退出运行模式并等待编译及资源导入完成。");
        }

        public void Folder(string path)
        {
            if (path == "Assets" || AssetDatabase.IsValidFolder(path)) return;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Split('/').Any(p => p == ".." || p == "."))
                throw new InvalidOperationException("创建路径必须位于 Assets 内。");
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("无法创建目录：" + path);
            folders.Add(path);
        }

        public void Reserve(string path)
        {
            if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                throw new InvalidOperationException("路径已经存在，创建操作不会覆盖：" + path);
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            assets.Add(path);
        }

        public void Create(UnityEngine.Object value, string path)
        {
            try
            {
                Reserve(path);
                AssetDatabase.CreateAsset(value, path);
            }
            catch
            {
                if (value != null && !EditorUtility.IsPersistent(value)) UnityEngine.Object.DestroyImmediate(value);
                throw;
            }
        }

        public GameObject Prefab(GameObject root, string path)
        {
            Reserve(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null) throw new IOException("预制体创建失败：" + path);
            return prefab;
        }

        public void Complete() => complete = true;

        public void Dispose()
        {
            if (complete) return;
            for (int i = assets.Count - 1; i >= 0; i--)
                if (File.Exists(assets[i])) AssetDatabase.DeleteAsset(assets[i]);
            for (int i = folders.Count - 1; i >= 0; i--)
                if (Directory.Exists(folders[i]) && !Directory.EnumerateFileSystemEntries(folders[i]).Any())
                    AssetDatabase.DeleteAsset(folders[i]);
        }
    }

    // Prefab assembly must not mark the author's currently open scene as modified.
    internal sealed class ContentCreationScene : IDisposable
    {
        readonly Scene staging = EditorSceneManager.NewPreviewScene();

        public GameObject Create(string name, params Type[] components)
        {
            var value = new GameObject(name);
            SceneManager.MoveGameObjectToScene(value, staging);
            foreach (var component in components) value.AddComponent(component);
            return value;
        }

        public GameObject Instantiate(GameObject source)
        {
            var parent = Create("Instantiation parent");
            try
            {
                var clone = UnityEngine.Object.Instantiate(source, parent.transform, false);
                clone.transform.SetParent(null, false);
                return clone;
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        public void Dispose()
        {
            if (staging.IsValid() && staging.isLoaded) EditorSceneManager.ClosePreviewScene(staging);
        }
    }
}
#endif
