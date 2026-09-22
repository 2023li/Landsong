using System;
using System.Collections.Generic;
using System.Linq;
using GiantGrey.TileWorldCreator;
using GiantGrey.TileWorldCreator.Attributes;
using Landsong.EditorTools;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using GiantGrey.TileWorldCreator.UI;

#endif
namespace Landsong.GridSystem
{
    [BuildLayer("Landsong Slope", "Tiles.twc")]
    public sealed class ProtrudingSlopeBuildLayer : BuildLayer
    {
        [Sirenix.OdinInspector.LabelText("斜坡预设"), Sirenix.OdinInspector.Required]
        public ProtrudingSlopeTilePreset tileSet;
        [Sirenix.OdinInspector.LabelText("地形规则"), Sirenix.OdinInspector.Required]
        public MapTerrainRules rules;
        [Tooltip("仅偏移斜坡模型及左右衔接修整，不改变 Layer 逻辑高度和通行连接。")]
        [Sirenix.OdinInspector.LabelText("模型高度偏移")]
        public float layerYOffset;
        public override void ExecuteLayer(Configuration configuration, GameObject owner, TileWorldCreatorManager manager, HashSet<int> affectedClusters = null)
        {
            if (!isEnabled)
                return;
            if (!float.IsFinite(layerYOffset))
                throw new InvalidOperationException("斜坡 Layer Y Offset 必须为有限数值。");
            isExecuting = true;
            try
            {
                if (manager == null || rules == null || tileSet == null || tileSet.Left == null || tileSet.Middle == null || tileSet.Right == null)
                    throw new InvalidOperationException("斜坡构建层缺少 My斜坡 或 Blueprint 规则。");
                var map = LayerTerrainCompiler.Compile(configuration, rules);
                var strips = map.Slopes.Where(s => s.BlueprintGuid == assignedBlueprintLayerGuid).ToArray();
                var parent = GetLayerObject(manager.gameObject).transform;
                if (parent.GetComponent<ProtrudingSlopeVisualRoot>() == null)
                    parent.gameObject.AddComponent<ProtrudingSlopeVisualRoot>();
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    if (Application.isPlaying)
                        Destroy(parent.GetChild(i).gameObject);
                    else
                        DestroyImmediate(parent.GetChild(i).gameObject);
                }

                foreach (var strip in strips)
                    for (int i = 0; i < strip.Cells.Length; i++)
                    {
                        var prefab = i == 0 ? tileSet.Left : i == strip.Cells.Length - 1 ? tileSet.Right : tileSet.Middle;
                        GameObject instance;
#if UNITY_EDITOR
                        instance = !Application.isPlaying ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent) : Instantiate(prefab, parent);
#else
                    instance=Instantiate(prefab,parent);
#endif
                        var p = strip.Cells[i];
                        instance.name = prefab.name + " [" + p.x + "," + p.y + "]";
                        instance.transform.localPosition = new Vector3(p.x * configuration.cellSize, strip.Height + layerYOffset, p.y * configuration.cellSize);
                        instance.transform.localRotation = Quaternion.Euler(0, strip.Rotation * 90, 0);
                        instance.transform.localScale = new Vector3(configuration.cellSize, 1, configuration.cellSize);
                    }
            }
            finally
            {
                isExecuting = false;
            }
        }

        public override void PostExecuteLayer(Configuration configuration, GameObject owner, TileWorldCreatorManager manager)
        {
            if (!isEnabled)
                return;
            var map = LayerTerrainCompiler.Compile(configuration, rules);
            // Every slope post-pass uses the complete set so one layer cannot undo another's cuts.
            var offsets = configuration.buildLayerFolders.SelectMany(f => f.buildLayers).OfType<ProtrudingSlopeBuildLayer>().Where(b => b.isEnabled).GroupBy(b => b.assignedBlueprintLayerGuid).ToDictionary(g => g.Key, g =>
            {
                var values = g.Select(b => b.layerYOffset).Distinct().ToArray();
                if (values.Length != 1 || !float.IsFinite(values[0]))
                    throw new InvalidOperationException("同一斜坡 Blueprint 的视觉偏移必须一致且为有限数值。");
                return values[0];
            });
            var strips = map.Slopes.Where(s => offsets.ContainsKey(s.BlueprintGuid)).ToArray();
            var grass = tileSet.Middle.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var terrainIds = configuration.blueprintLayerFolders.SelectMany(f => f.blueprintLayers).Where(b => rules.blueprintRules.Any(r => r.BlueprintLayerName == BlueprintRuleNames.Key(b.layerName) && r.Kind == BlueprintLogicKind.Terrain)).Select(b => b.guid).ToHashSet();
            foreach (var layer in configuration.buildLayerFolders.SelectMany(f => f.buildLayers).OfType<TilesBuildLayer>())
                if (layer.LayerObject != null && terrainIds.Contains(layer.assignedBlueprintLayerGuid))
                    SlopeVisualCut.Apply(manager.transform, strips, grass, layer.LayerObject.transform, offsets);
        }

#if UNITY_EDITOR
        public override VisualElement CreateInspectorGUI(Configuration configuration, UnityEditor.Editor assetEditor, LayerFoldoutElement foldout)
        {
            var root = new VisualElement();
            root.Add(new HelpBox("在上层的斜坡 Blueprint 中绘制平台外侧一排，至少三格；坡向由相邻上层平台确定。仅道路可建。", HelpBoxMessageType.Info));
            var names = configuration.blueprintLayerFolders.SelectMany(f => f.blueprintLayers).Where(b => b != null).ToArray();
            var labels = names.Select(b => b.layerName).ToList();
            if (labels.Count > 0)
            {
                var picker = new PopupField<string>("Blueprint", labels, Mathf.Max(0, Array.FindIndex(names, b => b.guid == assignedBlueprintLayerGuid)));
                picker.RegisterValueChangedCallback(e =>
                {
                    assignedBlueprintLayerGuid = names[labels.IndexOf(e.newValue)].guid;
                    EditorUtility.SetDirty(this);
                });
                root.Add(picker);
            }

            var serialized = new SerializedObject(this);
            root.Add(new PropertyField(serialized.FindProperty(nameof(tileSet)), "My斜坡"));
            root.Add(new PropertyField(serialized.FindProperty(nameof(rules)), "Blueprint 地形规则"));
            root.Add(new Label("Global Offsets"));
            root.Add(new PropertyField(serialized.FindProperty(nameof(layerYOffset)), "Layer Y Offset"));
            root.Add(new HelpBox("此偏移同时作用于斜坡模型和左右衔接。逻辑仍连接 Layer n 与 n−1；应按实际模型端点对齐，数值不必与普通 Tiles 层相同。", HelpBoxMessageType.Info));
            root.Bind(serialized);
            return root;
        }
#endif
    }
}
