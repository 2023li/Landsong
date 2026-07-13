using System;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public sealed class BuildingModuleStateEntry
    {
        public string ModuleId;
        public string Json;

        public bool IsValid => !string.IsNullOrWhiteSpace(ModuleId);

        public void Normalize()
        {
            ModuleId = string.IsNullOrWhiteSpace(ModuleId) ? string.Empty : ModuleId.Trim();
            Json ??= string.Empty;
        }
    }
}
