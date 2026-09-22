using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct DefinitionMetadata
    {
        public FixedString128Bytes Id;
        public FixedString128Bytes Name;
    }
}
