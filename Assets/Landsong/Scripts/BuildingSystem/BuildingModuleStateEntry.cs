using System;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public sealed class BuildingModuleStateEntry
    {
        public int ModuleIndex;
        public string ModuleTypeId;
        public string Json;

        public bool IsValid => !string.IsNullOrWhiteSpace(ModuleTypeId);

        public void Normalize()
        {
            ModuleTypeId = string.IsNullOrWhiteSpace(ModuleTypeId) ? string.Empty : ModuleTypeId.Trim();
            Json ??= string.Empty;
        }
    }
}
