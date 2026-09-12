using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class ConditionsContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("完成前置"), ShowIf(nameof(Enabled))] public CompletionsConfiguration[] Completions = Array.Empty<CompletionsConfiguration>();
        [LabelText("显示前置"), ShowIf(nameof(Enabled))] public VisibilityConfiguration[] Visibility = Array.Empty<VisibilityConfiguration>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class CompletionsConfiguration : OrderedContentEntry
    {
        [LabelText("已获得或已完成内容"), ContentReference(false)] public GameDefinitionAsset Content;
        [LabelText("完成次数")] public int Count = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class VisibilityConfiguration : OrderedContentEntry
    {
        [LabelText("已拥有或已完成内容"), ContentReference(false)] public GameDefinitionAsset Content;
        [LabelText("所需数量")] public int Count = 1;
    }
}
