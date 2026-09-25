#if UNITY_EDITOR
using System.Linq;
using Landsong.Animation;
using Landsong.ECS.Presentation;
using Landsong.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class ContentAuthoringValidation
    {
        [MenuItem("Landsong/内容制作/校验正式制作内容")]
        public static void Validate()
        {
            DomainCatalogBuild.Validate();
            DisplayCatalogBuild.Validate();
            ValidateCurrent();
            Debug.Log("当前内容编译、显示同步与制作结构通过。玩法获得途径、实际画面和生命周期按制作手册另行验收。");
        }

        public static void ValidateCurrent()
        {
            var content = ContentAuthoringContext.Content();
            foreach (var definition in content.Buildings.Definitions)
                BuildingAuthoringWorkflow.Validate(definition);
            foreach (var definition in content.Soldiers.Definitions)
                UnitAuthoringWorkflow.Validate(definition);
            foreach (var definition in content.Heroes.Definitions)
                UnitAuthoringWorkflow.Validate(definition);
            foreach (var definition in content.Enemies.Definitions)
                UnitAuthoringWorkflow.Validate(definition);
        }
    }
}
#endif
