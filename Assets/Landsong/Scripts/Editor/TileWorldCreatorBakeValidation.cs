using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.EditorTools
{
    // Validates generation preconditions before the manager or authored map can change.
    internal static class TileWorldCreatorBakeValidation
    {
        internal static bool TryValidate(TileWorldCreatorManager manager, TileWorldCreatorMapBakeProfile profile, out Configuration configuration, out string error)
        {
            configuration = null;
            error = string.Empty;
            if (manager == null || profile == null)
            {
                error = "TWC Manager 或场景烘焙配置为空。";
                return false;
            }

            configuration = profile.SourceConfiguration as Configuration;
            if (configuration == null || manager.configuration != configuration)
            {
                error = "场景烘焙配置与当前 TileWorldCreatorManager 使用的 Configuration 不一致。";
                return false;
            }

            if (!((manager.transform.lossyScale - Vector3.one).sqrMagnitude <= 0.000001f))
            {
                error = "TileWorldCreatorManager scale must be (1, 1, 1). Use Configuration Cell Size for grid scale.";
                return false;
            }

            if (profile.RequireDeterministicGlobalSeed && !configuration.useGlobalRandomSeed)
            {
                error = "Enable Use Global Random Seed on the TWC Configuration before baking.";
                return false;
            }

            return true;
        }
    }
}
