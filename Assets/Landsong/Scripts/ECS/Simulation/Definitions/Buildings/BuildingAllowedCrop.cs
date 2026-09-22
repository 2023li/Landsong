using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingAllowedCrop
    {
        public int Level;
        public CropId Crop;
    }
}
