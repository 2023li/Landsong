using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingMarketLevel
    {
        public int Level;
        public ItemId Currency;
        public int ValuePerMarketPoint;
        public float IncomeRatio;
    }
}
