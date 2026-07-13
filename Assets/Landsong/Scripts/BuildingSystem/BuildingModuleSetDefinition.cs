using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [CreateAssetMenu(menuName = "Landsong/Building/Building Module Set", fileName = "BuildingModuleSet")]
    public sealed class BuildingModuleSetDefinition : ScriptableObject
    {
        [SerializeReference, InspectorName("运行时模块模板"), LabelText("运行时模块模板")]
        private List<BuildingModuleBase> buildingModules = new List<BuildingModuleBase>();

        public IReadOnlyList<BuildingModuleBase> BuildingModules =>
            buildingModules ?? (IReadOnlyList<BuildingModuleBase>)Array.Empty<BuildingModuleBase>();

        public BuildingModuleSetDefinition CreateRuntimeClone()
        {
            var clone = Instantiate(this);
            clone.hideFlags = HideFlags.DontSave;
            clone.Normalize();
            return clone;
        }

        public void Normalize()
        {
            buildingModules ??= new List<BuildingModuleBase>();
            for (var i = buildingModules.Count - 1; i >= 0; i--)
            {
                if (buildingModules[i] == null)
                {
                    buildingModules.RemoveAt(i);
                    continue;
                }

                buildingModules[i].Normalize();
            }
        }

        public void Configure(IEnumerable<BuildingModuleBase> modules)
        {
            buildingModules = modules == null
                ? new List<BuildingModuleBase>()
                : new List<BuildingModuleBase>(modules);
            Normalize();
        }
    }
}
