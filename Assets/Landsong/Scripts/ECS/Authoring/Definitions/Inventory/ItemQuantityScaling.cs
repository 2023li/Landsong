using System;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring.Definitions
{
    internal static class ItemQuantityScaling
    {
        internal static int Apply(int quantity, float scale)
        {
            if (quantity > 0 && scale == 1)
                return quantity;
            // Write through BlobBuilderArray before finalization; BlobArray offsets are not fixed up yet.
            float scaled = math.round(quantity * scale);
            if (!math.isfinite(scaled) || scaled < 1 || (double)scaled > int.MaxValue)
                throw new InvalidOperationException("缩放后的物品数量必须是有效的正整数。");
            return (int)scaled;
        }
    }
}
