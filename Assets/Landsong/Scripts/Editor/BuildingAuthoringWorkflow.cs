#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.Content;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Editor;
using Landsong.ECS.Presentation;
using Landsong.VisualSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    public static class BuildingAuthoringWorkflow
    {
        public const string Root = "Assets/Landsong/ECSContent";

        public static BuildingDefinitionAsset Create(string id, string name)
            => Create(ContentAuthoringContext.Content(), id, name, Root);

        internal static BuildingDefinitionAsset Create(GameContentSetAsset content, string id, string name, string output)
        {
            ContentCreationAssets.RequireEditMode();
            ContentCreationAssets.RequireIdentity(id, name);
            var catalog = content.Get<BuildingCatalogAsset>();
            var path = output + "/Definitions/Building/" + id + ".asset";
            var package = output + "/Buildings/" + id;
            var prefabPath = package + "/" + id + ".prefab";
            var existing = catalog.Definitions.FirstOrDefault(d => d != null && d.Metadata.Id == id);
            if (existing != null)
            {
                if (AssetDatabase.GetAssetPath(existing) != path || AssetDatabase.GetAssetPath(existing.Prefab) != prefabPath)
                    throw new InvalidOperationException("稳定 ID 已由其他资产占用：" + id);
                Validate(existing);
                return existing;
            }

            using var staging = new ContentCreationScene();
            using var creation = new ContentCreationAssets();
            var definition = ScriptableObject.CreateInstance<BuildingDefinitionAsset>();
            var candidate = Object.Instantiate(content);
            var candidateCatalog = Object.Instantiate(catalog);
            var root = staging.Create(id);
            var original = catalog.Definitions;
            try
            {
                definition.Metadata.Id = id;
                definition.Metadata.Name = name;
                creation.Create(definition, path);
                root.AddComponent<BuildingVisualAuthoring>().Definition = definition;
                var anchors = staging.Create("Anchors");
                anchors.transform.SetParent(root.transform, false);
                staging.Create("SelectionAnchor").transform.SetParent(anchors.transform, false);
                staging.Create("StatusAnchor").transform.SetParent(anchors.transform, false);
                staging.Create("FloatTextAnchor").transform.SetParent(anchors.transform, false);
                var viewRoot = staging.Create("ViewRoot");
                viewRoot.transform.SetParent(root.transform, false);
                var slot = staging.Create("Operational_L1");
                slot.transform.SetParent(viewRoot.transform, false);
                var binding = slot.AddComponent<BuildingVisualSlotAuthoring>();
                binding.Purpose = BuildingVisualPurpose.Operational;
                binding.Placeholder = true;
                var container = staging.Create("Content");
                container.transform.SetParent(slot.transform, false);
                var model = staging.Create("Placeholder", typeof(MeshFilter), typeof(MeshRenderer));
                model.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                model.transform.SetParent(container.transform, false);
                model.transform.localPosition = Vector3.up * .5f;
                model.AddComponent<EntityVisualAuthoring>().Owner = root;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("缺少 URP Lit 占位材质着色器。");
                var material = new Material(shader) { name = id + " Placeholder", enableInstancing = true };
                creation.Create(material, package + "/Materials/Placeholder.mat");
                model.GetComponent<MeshRenderer>().sharedMaterial = material;
                BuildingPreviewBindingAuthoring.Rebind(root);
                definition.Prefab = creation.Prefab(root, prefabPath);
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                Validate(definition);
                candidate.Buildings = candidateCatalog;
                candidateCatalog.Definitions = original.Append(definition).ToArray();
                using (var compiled = BuildingCatalogBaking.Compile(candidate)) { }
                if (EditorUtility.IsPersistent(catalog)) Undo.RecordObject(catalog, "注册新建筑");
                catalog.Definitions = candidateCatalog.Definitions;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                creation.Complete();
                return definition;
            }
            catch
            {
                catalog.Definitions = original;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(candidateCatalog);
                Object.DestroyImmediate(candidate);
                if (!AssetDatabase.Contains(definition)) Object.DestroyImmediate(definition);
            }
        }

        public static void Validate(BuildingDefinitionAsset definition)
        {
            if (definition == null || definition.Prefab == null)
                throw new InvalidOperationException("建筑缺少完整根预制体。");
            ValidateStructure(definition.Prefab, definition);
        }

        public static void ValidateStructure(GameObject root, BuildingDefinitionAsset definition)
        {
            void Require(bool value, string message)
            {
                if (!value) throw new InvalidOperationException(root.name + "：" + message);
            }
            var roots = root.GetComponentsInChildren<BuildingVisualAuthoring>(true);
            Require(definition != null, "缺少建筑定义。");
            Require(roots.Length == 1 && roots[0].gameObject == root && roots[0].Definition == definition, "根组件与建筑定义须一一对应。");
            Require(root.activeSelf && root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "SelectionAnchor") == 1, "根须启用并且必须有唯一 SelectionAnchor。");
            var slots = root.GetComponentsInChildren<BuildingVisualSlotAuthoring>(true);
            var preview = root.GetComponent<BuildingPlacementPreviewBinding>();
            Require(preview != null, "缺少放置预览绑定；请补齐 Baking 与预览接线。");
            preview.ValidateConfiguration();
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Require(preview.Parts.Select(p => p.Renderer).Distinct().Count() == renderers.Length
                && renderers.All(r => preview.Parts.Count(p => p.Renderer == r) == 1), "放置预览渲染器引用已过期。");
            Require(slots.Any(s => s.Purpose == BuildingVisualPurpose.Operational && s.Level == 1 && (s.SkinId ?? "") == (definition.PlacementAndVisuals.DefaultSkin ?? "") && s.GetComponentsInChildren<MeshRenderer>(true).Length > 0), "默认皮肤须有可见的一级运营槽。");
            foreach (var slot in slots)
            {
                // Prefab assets have no active scene hierarchy; inspect authored flags instead.
                for (var parent = slot.transform; parent != null; parent = parent.parent)
                    Require(parent.gameObject.activeSelf, slot.name + " 槽位及其父节点必须启用。");
                // Artists may prepare higher-level views before gameplay unlocks those levels.
                Require(slot.Level >= 1 && slot.Step >= 0 && Enum.IsDefined(typeof(BuildingVisualPurpose), slot.Purpose), slot.name + " 等级、阶段或用途无效。");
            }
            foreach (var renderer in renderers)
            {
                var visual = renderer.GetComponent<EntityVisualAuthoring>();
                Require(visual != null && visual.Owner == root, renderer.name + " 的 EntityVisualAuthoring.Owner 必须指向建筑根。");
                var slot = renderer.GetComponentInParent<BuildingVisualSlotAuthoring>(true);
                Require(preview.Parts.Single(p => p.Renderer == renderer).Slot == slot, renderer.name + " 的预览槽引用已过期（无槽表示各等级共享的模型）。");
            }
        }
    }

    public sealed class BuildingCreationWindow : EditorWindow
    {
        string stableId = "", displayName = "", result = "";
        [MenuItem("Landsong/内容制作/创建建筑")]
        public static void Open() => GetWindow<BuildingCreationWindow>("创建建筑");
        void OnGUI()
        {
            ContentAuthoringContext.DrawContext();
            stableId = EditorGUILayout.TextField("稳定 ID", stableId);
            displayName = EditorGUILayout.TextField("显示名称", displayName);
            EditorGUILayout.HelpBox("生成定义、完整根、选择锚点和可见占位槽，并注册正式建筑目录。重复创建只检查并打开已有结果；手工编辑不会被重建。随后配置功能、美术与蓝图获得途径。", MessageType.Info);
            if (GUILayout.Button("创建并注册建筑"))
            {
                try { Selection.activeObject = BuildingAuthoringWorkflow.Create(stableId, displayName); result = "创建完成。请按建筑制作手册继续配置并验收。"; }
                catch (Exception error) { result = error.Message; }
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
        }
    }
}
#endif
