using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;

namespace Landsong.EditorTools
{
    internal static class TileWorldCreatorSourceFingerprint
    {
        internal static string ComputeSourceHash(Configuration configuration, IReadOnlyList<GridMapCellRecord> cells, TileWorldCreatorMapBakeProfile profile)
        {
            var builder = new StringBuilder();
            builder.Append(configuration.width).Append('|').Append(configuration.height).Append('|').Append(configuration.cellSize.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(configuration.globalRandomSeed).Append('|').Append(profile.ElevationWorldStep.ToString("R", CultureInfo.InvariantCulture)).AppendLine();
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                builder.Append(cell.Position.X).Append(',').Append(cell.Position.Z).Append(',').Append(cell.EdgeZone ? '1' : '0').Append(',').Append(cell.Buildable ? '1' : '0').Append(',').Append(cell.Traversable ? '1' : '0').Append(',').Append(cell.ElevationLevel).Append(',').Append(cell.SurfaceLayer).Append(',').Append((int)cell.Terrain);
                builder.AppendLine();
            }

            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(builder.ToString());
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
