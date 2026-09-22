using System;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;

namespace Landsong.EditorTools
{
    internal static class TileWorldCreatorLayerResolver
    {
        internal static bool SyncLayerReferences(TileWorldCreatorMapBakeProfile profile, Configuration configuration, out string error)
        {
            error = string.Empty;
            if (profile.UseLayerBlueprintRules)
                return true; // Preset / folder GUID resolution happens in the logical compiler.
            var baseLayer = ResolveLayer(configuration, profile.BaseLayerGuid, profile.BaseLayerName);
            if (baseLayer == null)
            {
                error = $"Base layer '{profile.BaseLayerName}' was not found.";
                return false;
            }

            profile.SetResolvedBaseLayer(baseLayer.guid, baseLayer.layerName);
            for (var i = 0; i < profile.LayerBindings.Count; i++)
            {
                var binding = profile.LayerBindings[i];
                if (binding == null)
                {
                    error = $"Layer binding {i} is null.";
                    return false;
                }

                var layer = ResolveLayer(configuration, binding.LayerGuid, binding.LayerName);
                if (layer == null)
                {
                    error = $"Layer '{binding.LayerName}' was not found.";
                    return false;
                }

                binding.SetResolvedLayer(layer.guid, layer.layerName);
            }

            return true;
        }

        internal static BlueprintLayer ResolveLayer(Configuration configuration, string layerGuid, string layerName)
        {
            if (configuration == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(layerGuid))
            {
                var byGuid = configuration.GetBlueprintLayerByGuid(layerGuid.Trim());
                if (byGuid != null)
                {
                    return byGuid;
                }
            }

            var normalizedName = string.IsNullOrWhiteSpace(layerName) ? string.Empty : layerName.Trim();
            BlueprintLayer match = null;
            for (var folderIndex = 0; folderIndex < configuration.blueprintLayerFolders.Count; folderIndex++)
            {
                var folder = configuration.blueprintLayerFolders[folderIndex];
                if (folder?.blueprintLayers == null)
                {
                    continue;
                }

                for (var layerIndex = 0; layerIndex < folder.blueprintLayers.Count; layerIndex++)
                {
                    var candidate = folder.blueprintLayers[layerIndex];
                    if (candidate == null || !string.Equals(candidate.layerName, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (match != null)
                    {
                        return null;
                    }

                    match = candidate;
                }
            }

            return match;
        }
    }
}
