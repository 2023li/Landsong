using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    // 烘焙与运行时共同读取的数据契约；枚举序号和字段布局保持不变。
    public enum BuildingVisualPurpose : byte
    {
        [LabelText("施工表现")] Construction = 0,
        [LabelText("运营表现")] Operational = 1,
        [LabelText("放置预览")] Preview = 2,
        [LabelText("荒废表现")] Ruined = 3,
        [LabelText("修复表现")] Repairing = 4
    }

    public struct BuildingVisualSelection : IComponentData { public Entity Slot; }

    [InternalBufferCapacity(0)]
    public struct BuildingVisualSlot : IBufferElementData
    {
        public Entity Slot;
        public BuildingVisualPurpose Purpose;
        public int Level, Step;
        public FixedString64Bytes Skin;
        public byte Placeholder;
    }
}
