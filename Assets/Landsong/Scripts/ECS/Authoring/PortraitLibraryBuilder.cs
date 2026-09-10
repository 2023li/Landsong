using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    public static partial class PortraitLibraryBuilder
    {
        public sealed class Part
        {public string Name;public PortraitPartType Type;public byte Genders=3;public float Weight=1;public List<Render> Renders=new List<Render>();}
        public sealed class Render {public PortraitLayer Layer;public PortraitTint Tint;public Color32[] Pixels;}
        public static int StableId(string name){uint h=2166136261;foreach(char c in name){h=(h^(byte)c)*16777619;h=(h^(byte)(c>>8))*16777619;}return (int)(h&0x7fffffff)|1;}
        public static BlobAssetReference<PortraitLibraryBlob> Build(PortraitConfig config)
        {
            if(config==null)throw new InvalidOperationException("Missing project portrait config");
            int size=config.Resolution;var settings=config.Settings;
            if(size!=32&&size!=64)throw new InvalidOperationException("Portrait resolution must be 32 or 64");
            if(settings.YouthAge<1||settings.GreyAge<settings.YouthAge||settings.ElderAge<=settings.GreyAge||settings.SoldierRecruitMinAge<settings.YouthAge||settings.SoldierRecruitMaxAge<settings.SoldierRecruitMinAge||settings.SoldierLifeMin<=settings.SoldierRecruitMaxAge||settings.SoldierLifeMax<settings.SoldierLifeMin||!math.isfinite(settings.ColorMutation)||settings.ColorMutation<0||settings.ColorMutation>1)throw new InvalidOperationException("Invalid portrait ages or inheritance settings");
            foreach(var palette in new[]{config.SkinColors,config.HairColors,config.EyeColors}){if(palette==null||palette.Length==0||palette.Length>256)throw new InvalidOperationException("Portrait palette must contain 1..256 opaque colors");foreach(var color in palette)if(color.a!=1 || !float.IsFinite(color.r) || !float.IsFinite(color.g) || !float.IsFinite(color.b) || color.r<0 || color.r>1 || color.g<0 || color.g>1 || color.b<0 || color.b>1)throw new InvalidOperationException("Portrait palette colors must be opaque");}
            var parts=config.Placeholders?Placeholders(size):new List<Part>();
            foreach(var source in config.Parts??Array.Empty<PortraitPartSource>())
            {
                PortraitPartRules.ValidateSource(source,size);
                var part=new Part{Name=source.Id,Type=source.Type,Genders=source.Genders,Weight=source.Weight};
                foreach(var r in source.Renders)
                {
                    if(r==null||r.Sprite==null||!r.Sprite.texture.isReadable||r.Sprite.packed)throw new InvalidOperationException("Portrait sprites require readable, unpacked source textures");
                    var rect=r.Sprite.rect;if(rect.width!=size||rect.height!=size||r.Sprite.pivot!=new Vector2(size*.5f,size*.5f))throw new InvalidOperationException("Portrait sprite size/pivot mismatch: "+source.Id);
                    var texture=r.Sprite.texture;var pixels=texture.GetPixels32();var cropped=new Color32[size*size];
                    for(int y=0;y<size;y++)Array.Copy(pixels,((int)rect.y+y)*texture.width+(int)rect.x,cropped,y*size,size);
                    part.Renders.Add(new Render{Layer=r.Layer,Tint=r.Tint,Pixels=cropped});
                }
                parts.Add(part);
            }
            var ids=new HashSet<int>();int renders=0;
            foreach(var part in parts)
            {
                if(string.IsNullOrWhiteSpace(part.Name)||!ids.Add(StableId(part.Name))||part.Type>PortraitPartType.Effect||part.Genders<1||part.Genders>3||!math.isfinite(part.Weight)||part.Weight<0||part.Renders.Count==0||part.Renders.Count>4)throw new InvalidOperationException("Invalid/duplicate portrait part: "+part.Name);
                foreach(var r in part.Renders)if(!Enum.IsDefined(typeof(PortraitLayer),r.Layer)||r.Tint>PortraitTint.Eyes||r.Pixels.Length!=size*size)throw new InvalidOperationException("Invalid portrait render: "+part.Name);
                renders+=part.Renders.Count;
            }
            if(parts.Count==0||parts.Count>4096)throw new InvalidOperationException("Portrait library must contain 1..4096 parts");
            foreach(byte gender in new byte[]{1,2})foreach(var type in new[]{PortraitPartType.Face,PortraitPartType.Eyes,PortraitPartType.Body,PortraitPartType.Clothes})
                if(!parts.Exists(p=>p.Type==type&&(p.Genders&gender)!=0&&p.Weight>0&&p.Renders.Count>0))throw new InvalidOperationException("Missing mandatory portrait part "+type);
            parts.Sort((a,b)=>StableId(a.Name).CompareTo(StableId(b.Name)));
            using var builder=new BlobBuilder(Allocator.Temp);ref var blob=ref builder.ConstructRoot<PortraitLibraryBlob>();blob.Resolution=size;blob.Settings=settings;
            var target=builder.Allocate(ref blob.Parts,parts.Count);var layers=builder.Allocate(ref blob.Renders,renders);var data=builder.Allocate(ref blob.Pixels,checked(renders*size*size));
            ulong hash=14695981039346656037;void Mix(uint n){hash=(hash^n)*1099511628211;}
            int ri=0;for(int i=0;i<parts.Count;i++)
            {
                var p=parts[i];int id=StableId(p.Name);target[i]=new PortraitPartBlob{Id=id,Type=p.Type,Genders=p.Genders,Weight=p.Weight,Start=ri,Count=p.Renders.Count};Mix((uint)id);Mix(p.Genders);Mix((uint)p.Type);
                foreach(var r in p.Renders){int start=ri*size*size;layers[ri++]=new PortraitRenderBlob{Layer=r.Layer,Tint=r.Tint,PixelStart=start};Mix((uint)r.Layer);Mix((uint)r.Tint);for(int n=0;n<r.Pixels.Length;n++){var c=r.Pixels[n];data[start+n]=c;Mix((uint)(c.r|c.g<<8|c.b<<16|c.a<<24));}}
            }
            void Palette(ref BlobArray<Color32> array,Color[] source){var a=builder.Allocate(ref array,source.Length);for(int i=0;i<source.Length;i++)a[i]=source[i];}
            Palette(ref blob.SkinColors,config.SkinColors);Palette(ref blob.HairColors,config.HairColors);Palette(ref blob.EyeColors,config.EyeColors);Mix((uint)size);blob.Revision=hash;
            return builder.CreateBlobAssetReference<PortraitLibraryBlob>(Allocator.Persistent);
        }
    }
}
