#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(GameDefinitionAsset))]
    public sealed class ContentInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("这里只编辑模板。运行状态请在 Entities Hierarchy / Inspector 中查看。ID 是存档内容键，请勿随意重命名。", MessageType.Info);
            DrawDefaultInspector();
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Opportunity) EditorGUILayout.HelpBox("平安访客：Opportunity 独立配置种类、权重、每晚次数、前半夜时窗、路线长度/速度、最低响应时间和可响应士兵/英雄。小偷逃离时才按物品 Theft 配置从来源建筑扣款；精灵 RewardItem 在抓到后记入黎明奖励。两者都需要合法路线和能及时响应的单位。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Item) EditorGUILayout.HelpBox("Theft：Protection 排除任务/唯一/绑定物；Weight 是同一来源内物品类型的抽取权重，Maximum 为单次数量上限，UnitValue 为每件占用的夜间盗窃预算。Capacity=1 的不可堆叠物也不参与。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Enemy) EditorGUILayout.HelpBox("普通 RewardItem/Blueprint/Buff/Feature 在击杀时直接进入夜间暂存。SpecialDrop：Target 物品、Amount 数量、B 稀有度 1～3；只生成特殊可点击掉落，结束时全部自动收取。逃离或强制清场没有击杀收益。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Soldier) EditorGUILayout.HelpBox("士兵：RecruitCost 规则为整份多资源招募费（同类相加，Level=0/1）；有规则则替代 Cost 金币，无规则沿用 Cost。SoldierGrowth 配置累计升级曲线、每级生命/攻击比例和有效战斗夜经验；仅存活且实际参战者获经验。驻地 Building.SoldierRecruitLimit 为每回合所有兵种共享额度，0 使用槽数。", MessageType.Info);
            if (((GameDefinitionAsset)target).Data.Kind == ContentKind.Expedition) EditorGUILayout.HelpBox("远征字段：Level 最低驻地等级；Population/Capacity 最少/最多人数（Capacity=0 不限）；Duration 时长；Chance 基础成功率、Interval 每人成功率、Range 成功率上限；Loss 失败伤亡率；Cost/Value 基础/每人抚恤。Supply：Target 物品、Amount 最低量、B 额外上限（0=最低量的50%）、Value/Extra 每个额外物品成功率/奖励加成。VisiblePrerequisite 仅控制显示，Prerequisite 控制可用。两个正式旧目的地补给原本为空。", MessageType.Info);
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
                foreach (var rule in definition.Rules) GameWorldAuthoring.ConvertRule(catalog, rule);
            }
            using var blob = GameWorldAuthoring.BuildCatalog(catalog);
            using var portraits = PortraitLibraryBuilder.Build(catalog.Portraits);
            Debug.Log("ECS content validation passed: " + ids.Count + " definitions.");
        }
    }
}
#endif
