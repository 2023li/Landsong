using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [Serializable] public sealed class PortraitRenderSource
    {[LabelText("程序图层")] public PortraitLayer Layer;[LabelText("染色通道")] public PortraitTint Tint;[LabelText("部件图片")] public Sprite Sprite;}
    [Serializable] public sealed class PortraitPartSource
    {
        [LabelText("稳定部件标识")] public string Id;
        [LabelText("部件类别")] public PortraitPartType Type;
        [Tooltip("1 男，2 女，3 通用")] [LabelText("适用性别掩码")] public byte Genders=3;
        [Min(0)] [LabelText("随机权重")] public float Weight=1;
        [LabelText("渲染图层")] public PortraitRenderSource[] Renders=Array.Empty<PortraitRenderSource>();
    }
    [CreateAssetMenu(menuName="Landsong/ECS/Portrait config")]
    public sealed class PortraitConfig : ScriptableObject
    {
        [Tooltip("只能为 32 或 64；不自动缩放美术素材。")] [LabelText("肖像像素尺寸")] public int Resolution=64;
        [LabelText("年龄与遗传设置")] public PortraitSettings Settings=PortraitSettings.Default;
        [Tooltip("同时加载程序占位与已导入部件。正式素材准备好且校验通过后可关闭。")] [LabelText("启用程序占位")] public bool Placeholders=true;
        [LabelText("肖像部件")] public PortraitPartSource[] Parts=Array.Empty<PortraitPartSource>();
        [LabelText("基础肤色色盘")] public Color[] SkinColors={new Color32(244,204,167,255),new Color32(211,165,119,255),new Color32(160,110,76,255),new Color32(105,68,49,255)};
        [LabelText("基础发色色盘")] public Color[] HairColors={new Color32(40,31,29,255),new Color32(105,60,35,255),new Color32(179,132,58,255),new Color32(126,48,32,255)};
        [LabelText("基础眼色色盘")] public Color[] EyeColors={new Color32(77,49,27,255),new Color32(54,103,135,255),new Color32(67,114,69,255),new Color32(116,113,113,255)};
    }
}
