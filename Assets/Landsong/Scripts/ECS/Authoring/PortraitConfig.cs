using System;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [Serializable] public sealed class PortraitRenderSource
    {public PortraitLayer Layer;public PortraitTint Tint;public Sprite Sprite;}
    [Serializable] public sealed class PortraitPartSource
    {
        public string Id;
        public PortraitPartType Type;
        [Tooltip("1 男，2 女，3 通用")] public byte Genders=3;
        [Min(0)] public float Weight=1;
        public PortraitRenderSource[] Renders=Array.Empty<PortraitRenderSource>();
    }
    [CreateAssetMenu(menuName="Landsong/ECS/Portrait config")]
    public sealed class PortraitConfig : ScriptableObject
    {
        [Tooltip("只能为 32 或 64；不自动缩放美术素材。")] public int Resolution=64;
        public PortraitSettings Settings=PortraitSettings.Default;
        [Tooltip("同时加载程序占位与已导入部件。正式素材准备好且校验通过后可关闭。")] public bool Placeholders=true;
        public PortraitPartSource[] Parts=Array.Empty<PortraitPartSource>();
        public Color[] SkinColors={new Color32(244,204,167,255),new Color32(211,165,119,255),new Color32(160,110,76,255),new Color32(105,68,49,255)};
        public Color[] HairColors={new Color32(40,31,29,255),new Color32(105,60,35,255),new Color32(179,132,58,255),new Color32(126,48,32,255)};
        public Color[] EyeColors={new Color32(77,49,27,255),new Color32(54,103,135,255),new Color32(67,114,69,255),new Color32(116,113,113,255)};
    }
}
