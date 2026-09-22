using System;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    // Owns the editor-only mutations required to generate deterministic TWC source geometry.
    internal static class TileWorldCreatorWorldGenerator
    {
        internal static void EnsureDeterministicSeed(Configuration configuration, TileWorldCreatorMapBakeProfile profile)
        {
            if (profile.RequireDeterministicGlobalSeed && !configuration.useGlobalRandomSeed)
            {
                configuration.useGlobalRandomSeed = true;
                if (configuration.globalRandomSeed == 0)
                {
                    configuration.globalRandomSeed = 1;
                }

                EditorUtility.SetDirty(configuration);
            }
        }

        internal static bool TryGenerate(TileWorldCreatorManager manager, TileWorldCreatorMapBakeProfile profile, out string error)
        {
            error = string.Empty;
            if (profile.RegenerateTileWorldBeforeBake || manager.GetComponent<MapContentAuthoring>() != null)
            {
                try
                {
                    GiantGrey.TileWorldCreator.Utilities.EditorCoroutines.RunToCompletion(() =>
                    {
                        manager.ExecuteBlueprintLayers();
                        manager.ExecuteBuildLayers(ExecutionMode.FromScratch);
                    });
                }
                catch (Exception exception)
                {
                    error = $"TWC regeneration failed before Landsong bake: {exception.Message}";
                    Debug.LogException(exception, manager);
                    return false;
                }
            }

            return true;
        }
    }
}
