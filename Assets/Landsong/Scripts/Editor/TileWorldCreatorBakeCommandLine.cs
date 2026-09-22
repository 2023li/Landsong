using System;
using System.IO;
using System.Linq;
using GiantGrey.TileWorldCreator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.EditorTools
{
    internal static class TileWorldCreatorBakeCommandLine
    {
        private const string ScenePathArgument = "-landsongScenePath";
        internal static void BakeSceneFromCommandLine()
        {
            var scenePath = GetCommandLineArgument(ScenePathArgument);
            if (string.IsNullOrWhiteSpace(scenePath) || !scenePath.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(Path.GetFullPath(scenePath)))
            {
                throw new InvalidOperationException($"Pass a valid Unity scene using {ScenePathArgument} Assets/Path/Map.unity.");
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var managers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TileWorldCreatorManager>(true)).Where(manager => manager != null).ToArray();
            if (managers.Length != 1 || managers[0].configuration == null)
            {
                throw new InvalidOperationException($"Scene '{scenePath}' must contain exactly one configured TileWorldCreatorManager.");
            }

            var manager = managers[0];
            if (!TileWorldCreatorMapBaker.BakeAndSnapMapContent(manager, out var map, out var error))
            {
                throw new InvalidOperationException(error);
            }

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Could not save baked scene '{scenePath}'.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Landsong TWC bake complete: scene='{scenePath}', cells={map.Cells.Count}, hash={map.SourceHash}", map);
        }

        internal static string GetCommandLineArgument(string argumentName)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], argumentName, StringComparison.Ordinal))
                {
                    return arguments[i + 1].Replace('\\', '/');
                }
            }

            return string.Empty;
        }
    }
}
