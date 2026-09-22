using System;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.GridSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class MapWorldComposition
    {
        public const string TemplatePath = Landsong.ECS.Editor.ContentAuthoringContext.TemplatePath;
        public static GameObject Template()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            if (template == null || template.GetComponent<GameWorldTemplateAuthoring>() == null)
                throw new InvalidOperationException("缺少正式模拟根组合模板。");
            return template;
        }

        public static GameContentSetAsset Content(MapContentAuthoring content)
        {
            if (content == null)
                throw new InvalidOperationException("缺少地图内容。");
            var contentSet = Template().GetComponent<GameContentSetAuthoring>();
            if (contentSet == null || contentSet.Content == null || contentSet.Content.Buildings != content.Buildings)
                throw new InvalidOperationException("模拟根模板必须包含通用世界模板和统一内容集合，且建筑目录必须与制图配置一致。");
            return contentSet.Content;
        }
    }
}
