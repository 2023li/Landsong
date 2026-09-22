using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct DefinitionPrerequisites
    {
        public BlobArray<BuildingRequirement> BuildingRequirements;
        public BlobArray<TechnologyRequirement> TechnologyRequirements;
        public BlobArray<BuffRequirement> BuffRequirements;
        public BlobArray<FeatureRequirement> FeatureRequirements;
        public BlobArray<QuestRequirement> QuestRequirements;
        public BlobArray<ExpeditionRequirement> ExpeditionRequirements;
    }
}
