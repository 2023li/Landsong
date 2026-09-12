#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;

namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(GameDefinitionAsset))]
    public sealed class ContentInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("这里只编辑模板。运行状态请在 Entities Hierarchy / Inspector 中查看。ID 是存档内容键，请勿随意重命名。", MessageType.Info);
            base.OnInspectorGUI();
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Building) BuildingModuleInspector.DrawSummary(((GameDefinitionAsset)target).Data);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Opportunity) EditorGUILayout.HelpBox("平安夜访客设置：配置种类、权重、每晚次数、出现时窗、路线与速度、响应时间及可响应单位。小偷逃离时按物品被盗规则扣除来源建筑库存；精灵被抓到后的物品奖励在黎明结算。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Item) EditorGUILayout.HelpBox("物品被盗规则：保护标记排除任务、唯一和绑定物品；权重用于抽取物品类型；数量上限与单件预算共同限制盗窃数量。最大堆叠为 1 的物品不参与。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Enemy) EditorGUILayout.HelpBox("击杀后普通奖励进入夜间暂存。特殊掉落模块指定物品、数量与稀有度，生成可点击掉落，夜晚结束自动收取。逃离或强制清场没有击杀收益。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Soldier) EditorGUILayout.HelpBox("招募费用规则为整份多资源费用（同类相加，适用等级 0 或 1），替代基础金币费用。士兵成长配置升级经验、生命和攻击加成；仅存活且实际参战者获得战斗经验。建筑驻军模块的每回合募兵上限由所有兵种共享，0 表示使用槽数。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Expedition) EditorGUILayout.HelpBox("远征补给模块配置物品、最低数量、额外数量上限，以及每份额外补给的成功率和奖励加成。显示前置条件只控制可见性，前置条件控制能否派遣。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Technology && GUILayout.Button("打开科技关系与奖励配置")) TechnologyEditorWindow.Open((GameDefinitionAsset)target);
            if (GUILayout.Button("注册到正式 ECS 目录"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
                if (catalog == null) throw new InvalidOperationException("GameCatalog missing");
                if (!catalog.Definitions.Contains((GameDefinitionAsset)target)) { Undo.RecordObject(catalog, "Register ECS definition"); catalog.Definitions = catalog.Definitions.Append((GameDefinitionAsset)target).ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); }
                Selection.activeObject = catalog;
            }
        }
    }
    public static class ContentValidation
    {
        [MenuItem("Landsong/ECS/Night event catalog")]
        public static void OpenNightCatalog() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");

        [MenuItem("Landsong/ECS/Validate native content")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            if (catalog == null) throw new InvalidOperationException("GameCatalog missing");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in catalog.Content)
            {
                if (string.IsNullOrWhiteSpace(definition.Id) || !ids.Add(definition.Id)) throw new InvalidOperationException("Empty/duplicate content ID: " + definition.Id);
                if (System.Text.Encoding.UTF8.GetByteCount(definition.Id) > 124 || System.Text.Encoding.UTF8.GetByteCount(definition.Name ?? "") > 124) throw new InvalidOperationException("Content ID/name exceeds ECS string capacity: " + definition.Id);
                if (definition.Kind == ContentKind.Building && (definition.Size.x < 1 || definition.Size.y < 1)) throw new InvalidOperationException("Invalid footprint: " + definition.Id);
                if ((definition.Kind == ContentKind.Building || definition.Kind == ContentKind.Soldier || definition.Kind == ContentKind.Hero || definition.Kind == ContentKind.Enemy || definition.Kind == ContentKind.Projectile || definition.Kind == ContentKind.Loot || definition.Kind == ContentKind.Opportunity) && definition.Prefab == null) throw new InvalidOperationException("Missing baked visual prefab: " + definition.Id);
            }
            using var blob = GameWorldAuthoring.BuildCatalog(catalog);
            using var portraits = PortraitLibraryBuilder.Build(catalog.Portraits);
            Debug.Log("ECS content validation passed: " + ids.Count + " definitions.");
        }
    }
}
#endif
