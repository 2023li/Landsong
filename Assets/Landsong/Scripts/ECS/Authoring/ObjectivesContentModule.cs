using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class ObjectivesContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("建筑目标"), ShowIf(nameof(Enabled))] public BuildingObjective[] Buildings = Array.Empty<BuildingObjective>();
        [LabelText("种植目标"), ShowIf(nameof(Enabled))] public PlantingObjective[] PlantedBuildings = Array.Empty<PlantingObjective>();
        [LabelText("持有物品"), ShowIf(nameof(Enabled))] public OwnedItemsObjective[] OwnedItems = Array.Empty<OwnedItemsObjective>();
        [LabelText("提交物品"), ShowIf(nameof(Enabled))] public SubmittedItemsObjective[] SubmittedItems = Array.Empty<SubmittedItemsObjective>();
        [LabelText("移动镜头"), ShowIf(nameof(Enabled))] public CameraMovesObjective[] CameraMoves = Array.Empty<CameraMovesObjective>();
        [LabelText("缩放镜头"), ShowIf(nameof(Enabled))] public CameraZoomsObjective[] CameraZooms = Array.Empty<CameraZoomsObjective>();
        [LabelText("研究科技"), ShowIf(nameof(Enabled))] public ResearchObjective[] Technologies = Array.Empty<ResearchObjective>();
        [LabelText("回合目标"), ShowIf(nameof(Enabled))] public TurnObjective[] Turns = Array.Empty<TurnObjective>();
        public System.Collections.Generic.IEnumerable<ContentObjective> All { get {
            foreach(var entry in Buildings??Array.Empty<BuildingObjective>()) yield return entry;
            foreach(var entry in PlantedBuildings??Array.Empty<PlantingObjective>()) yield return entry;
            foreach(var entry in OwnedItems??Array.Empty<OwnedItemsObjective>()) yield return entry;
            foreach(var entry in SubmittedItems??Array.Empty<SubmittedItemsObjective>()) yield return entry;
            foreach(var entry in CameraMoves??Array.Empty<CameraMovesObjective>()) yield return entry;
            foreach(var entry in CameraZooms??Array.Empty<CameraZoomsObjective>()) yield return entry;
            foreach(var entry in Technologies??Array.Empty<ResearchObjective>()) yield return entry;
            foreach(var entry in Turns??Array.Empty<TurnObjective>()) yield return entry;
        } }
    }
    [Serializable, HideReferenceObjectPicker] public sealed class BuildingObjective : ContentObjective
    {
        [LabelText("建筑"), ContentReference(false, ContentKind.Building)] public GameDefinitionAsset Building;
        [LabelText("数量")] public int Count = 1;
        [LabelText("最低建筑等级")] public int MinimumLevel;
        [LabelText("仅统计已完工")] public bool CompletedOnly;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class PlantingObjective : ContentObjective
    {
        [LabelText("种植建筑"), ContentReference(false, ContentKind.Building)] public GameDefinitionAsset Building;
        [LabelText("已种植建筑数量")] public int Count = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class OwnedItemsObjective : ContentObjective
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SubmittedItemsObjective : ContentObjective
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class CameraMovesObjective : ContentObjective
    {
        [LabelText("操作次数")] public int Count = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class CameraZoomsObjective : ContentObjective
    {
        [LabelText("操作次数")] public int Count = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ResearchObjective : ContentObjective
    {
        [LabelText("科技（空 = 任意）"), ContentReference(true, ContentKind.Technology)] public GameDefinitionAsset Technology;
        [LabelText("完成数量")] public int Count = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class TurnObjective : ContentObjective
    {
        [LabelText("回合数")] public int Turns = 1;
        [LabelText("从接受任务起计算")] public bool SinceAccepted;
    }
}
