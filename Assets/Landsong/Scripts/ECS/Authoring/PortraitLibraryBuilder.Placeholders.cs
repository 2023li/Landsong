using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    public static partial class PortraitLibraryBuilder
    {
        // Deliberately simple procedural fixtures. Actual sprites replace these through PortraitConfig.
        static List<Part> Placeholders(int size)
        {
            var parts=new List<Part>();
            Part Part(string id,PortraitPartType type){var p=new Part{Name="placeholder."+id,Type=type};parts.Add(p);return p;}
            void Layer(Part p,PortraitLayer layer,PortraitTint tint,Func<int,int,Color32> draw)
            {var pixels=new Color32[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=draw(x*32/size,y*32/size);p.Renders.Add(new Render{Layer=layer,Tint=tint,Pixels=pixels});}
            Color32 White(byte shade=255)=>new Color32(shade,shade,shade,255);
            bool Oval(int x,int y,int cx,int cy,int rx,int ry)=>((x-cx)*(x-cx)*ry*ry+(y-cy)*(y-cy)*rx*rx)<=rx*rx*ry*ry;
            for(int variant=0;variant<3;variant++)
            {
                int v=variant;var face=Part("face."+v,PortraitPartType.Face);
                Layer(face,PortraitLayer.FaceBase,PortraitTint.Skin,(x,y)=>Oval(x,y,16,19,7+v%2,9)?White((byte)(x<11?205:245)):default);
                var eyes=Part("eyes."+v,PortraitPartType.Eyes);
                Layer(eyes,PortraitLayer.Eyes,PortraitTint.Eyes,(x,y)=>y==20&&((x>=11&&x<=13)||(x>=19&&x<=21))?White():default);
                Layer(eyes,PortraitLayer.EyeOverlay,PortraitTint.None,(x,y)=>y==20&&(x==12||x==20)?new Color32(18,20,25,255):default);
                var hair=Part("hair."+v,PortraitPartType.Hair);
                Layer(hair,PortraitLayer.HairBack,PortraitTint.Hair,(x,y)=>v==1&&x>=7&&x<=25&&y>=11&&y<=26?White(195):default);
                Layer(hair,PortraitLayer.HairFront,PortraitTint.Hair,(x,y)=>Oval(x,y,16,25,9,5)&&y>=24-(v==2&&x<15?3:0)?White((byte)(y>27?220:255)):default);
                var mouth=Part("mouth."+v,PortraitPartType.Mouth);Layer(mouth,PortraitLayer.Mouth,PortraitTint.None,(x,y)=>y==14&&x>=14-v%2&&x<=18?new Color32(126,65,60,255):default);
                var clothes=Part("clothes."+v,PortraitPartType.Clothes);Layer(clothes,PortraitLayer.ClothesFront,PortraitTint.None,(x,y)=>y<10&&x>=5+y/3&&x<=27-y/3?new Color32((byte)(60+v*35),(byte)(90+v*20),(byte)(145-v*20),255):default);
            }
            var body=Part("body",PortraitPartType.Body);Layer(body,PortraitLayer.Body,PortraitTint.Skin,(x,y)=>x>=12&&x<=20&&y>=7&&y<=13?White(210):default);
            var ear=Part("ears",PortraitPartType.Ear);Layer(ear,PortraitLayer.Ear,PortraitTint.Skin,(x,y)=>(x==8||x==24)&&y>=18&&y<=21?White(220):default);
            var brows=Part("brows",PortraitPartType.Eyebrows);Layer(brows,PortraitLayer.Eyebrows,PortraitTint.Hair,(x,y)=>y==23&&((x>=10&&x<=13)||(x>=19&&x<=22))?White(200):default);
            var nose=Part("nose",PortraitPartType.Nose);Layer(nose,PortraitLayer.Nose,PortraitTint.Skin,(x,y)=>x==16&&y>=17&&y<=19?White(170):default);
            var beard=Part("beard",PortraitPartType.FacialHair);beard.Genders=1;beard.Weight=.25f;Layer(beard,PortraitLayer.FacialHair,PortraitTint.Hair,(x,y)=>y>=10&&y<=12&&x>=13&&x<=19?White(225):default);
            var hat=Part("hat",PortraitPartType.HeadAccessory);hat.Weight=.12f;Layer(hat,PortraitLayer.HeadAccessoryFront,PortraitTint.None,(x,y)=>y>=28&&y<=30&&x>=9&&x<=23?new Color32(93,48,52,255):default);
            return parts;
        }
    }
}
