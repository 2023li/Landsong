using System;
using System.Linq;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    // Shared by the editor importer and baking, so editing Parts directly cannot bypass the rules.
    public static class PortraitPartRules
    {
        public static PortraitLayer[] Allowed(PortraitPartType type) => type switch
        {
            PortraitPartType.Face => new[]{PortraitLayer.FaceBase},
            PortraitPartType.Ear => new[]{PortraitLayer.Ear},
            PortraitPartType.Eyes => new[]{PortraitLayer.Eyes,PortraitLayer.EyeOverlay},
            PortraitPartType.Eyebrows => new[]{PortraitLayer.Eyebrows},
            PortraitPartType.Nose => new[]{PortraitLayer.Nose},
            PortraitPartType.Mouth => new[]{PortraitLayer.Mouth},
            PortraitPartType.Hair => new[]{PortraitLayer.HairFront,PortraitLayer.HairBack},
            PortraitPartType.FacialHair => new[]{PortraitLayer.FacialHair},
            PortraitPartType.Body => new[]{PortraitLayer.Body,PortraitLayer.NeckBack,PortraitLayer.Neck},
            PortraitPartType.Clothes => new[]{PortraitLayer.ClothesFront,PortraitLayer.ClothesBack},
            PortraitPartType.HeadAccessory => new[]{PortraitLayer.HeadAccessoryFront,PortraitLayer.HeadAccessoryBack},
            PortraitPartType.FaceAccessory => new[]{PortraitLayer.FaceAccessory},
            PortraitPartType.Accessory => new[]{PortraitLayer.FrontAccessory,PortraitLayer.BackAccessory},
            PortraitPartType.SkinDetail => new[]{PortraitLayer.SkinDetail},
            PortraitPartType.Effect => new[]{PortraitLayer.Effect},
            _ => throw new InvalidOperationException("无效的肖像部件类别。")
        };
        public static PortraitTint DefaultTint(PortraitLayer layer) => layer switch
        {
            PortraitLayer.FaceBase or PortraitLayer.Ear or PortraitLayer.Nose or PortraitLayer.Body or PortraitLayer.Neck or PortraitLayer.NeckBack => PortraitTint.Skin,
            PortraitLayer.HairBack or PortraitLayer.HairFront or PortraitLayer.FacialHair or PortraitLayer.Eyebrows => PortraitTint.Hair,
            PortraitLayer.Eyes => PortraitTint.Eyes,
            _ => PortraitTint.None
        };
        public static void ValidateShape(string id,PortraitPartType type,byte genders,float weight,PortraitLayer[] layers)
        {
            if(string.IsNullOrWhiteSpace(id))throw new InvalidOperationException("部件标识不能为空。");
            if(genders<1||genders>3)throw new InvalidOperationException("至少勾选男用或女用，可同时勾选。");
            if(!float.IsFinite(weight)||weight<0)throw new InvalidOperationException("随机权重必须是非负有限数值。");
            if(layers==null||layers.Length==0)throw new InvalidOperationException("请按需添加图层，至少提供一张部件 PNG。");
            if(layers.Length>4||layers.Distinct().Count()!=layers.Length)throw new InvalidOperationException("部件最多包含 4 个不重复的渲染层。");
            var allowed=Allowed(type);
            if(layers.Any(layer=>!allowed.Contains(layer)))throw new InvalidOperationException("渲染层与部件类别不匹配："+id);
        }
        public static void ValidateSource(PortraitPartSource part,int resolution)
        {
            if(part==null||part.Renders==null||part.Renders.Any(r=>r==null))throw new InvalidOperationException("肖像部件或渲染层为空。");
            ValidateShape(part.Id,part.Type,part.Genders,part.Weight,part.Renders.Select(r=>r.Layer).ToArray());
            foreach(var render in part.Renders)
            {
                var sprite=render.Sprite;
                if(sprite==null||sprite.packed||!sprite.texture.isReadable)throw new InvalidOperationException("部件需要可读、未打包的 Sprite："+part.Id);
                var texture=sprite.texture;
                if(texture.width!=resolution||texture.height!=resolution||sprite.rect!=new Rect(0,0,resolution,resolution)||sprite.pivot!=new Vector2(resolution*.5f,resolution*.5f))throw new InvalidOperationException($"{part.Id} 必须使用完整的 {resolution}×{resolution} 画布及中心 Pivot。");
                if(texture.filterMode!=FilterMode.Point||texture.mipmapCount!=1)throw new InvalidOperationException("部件须使用 Point 过滤且关闭 Mipmap："+part.Id);
                if(!Enum.IsDefined(typeof(PortraitTint),render.Tint))throw new InvalidOperationException("无效的染色通道："+part.Id);
                if(part.Type==PortraitPartType.Hair&&render.Tint!=PortraitTint.Hair)throw new InvalidOperationException("前发和后发均须使用发色染色，以支持统一换色和程序衰老。");
            }
            if(part.Type==PortraitPartType.Hair&&part.Renders.Length>1&&part.Renders[0].Sprite.texture==part.Renders[1].Sprite.texture)throw new InvalidOperationException("添加多个发型层时，请分别提供独立素材，不能重复使用同一张。");
        }
    }
}
