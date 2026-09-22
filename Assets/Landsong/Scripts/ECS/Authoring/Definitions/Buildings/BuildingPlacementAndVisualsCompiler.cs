using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingPlacementAndVisualsCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingPlacementAndVisualsSource source, ref global::Landsong.ECS.Definitions.BuildingPlacementAndVisuals target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingPlacementAndVisuals 配置。");
            target.Category = source.Category;
            target.SpawnExclusionPadding = source.SpawnExclusionPadding;
            target.MenuOrder = source.MenuOrder;
            target.ProviderPriority = source.ProviderPriority;
            target.CanMove = source.CanMove;
            target.CanRotate = source.CanRotate;
            if (!math.isfinite(source.MoveMaterialRatio))
                throw new InvalidOperationException("配置项（MoveMaterialRatio）必须是有限数值。");
            target.MoveMaterialRatio = source.MoveMaterialRatio;
            if (!math.isfinite(source.MoveExperienceRatio))
                throw new InvalidOperationException("配置项（MoveExperienceRatio）必须是有限数值。");
            target.MoveExperienceRatio = source.MoveExperienceRatio;
            if (!math.isfinite(source.RuinMovementCost))
                throw new InvalidOperationException("配置项（RuinMovementCost）必须是有限数值。");
            target.RuinMovementCost = source.RuinMovementCost;
            target.DefaultSkin = new FixedString128Bytes(source.DefaultSkin ?? "");
        }
    }
}
