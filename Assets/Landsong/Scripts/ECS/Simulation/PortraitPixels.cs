using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.ECS
{
    public static class PortraitPixels
    {
        struct Layer {public PortraitRenderBlob Render;public int Order;}
        public static int AgeBand(PortraitSettings s,int age)=>age<s.YouthAge?0:1+math.clamp((age-s.GreyAge)*10/(s.ElderAge-s.GreyAge),0,10);
        public static ulong Key(ref PortraitLibraryBlob lib,PortraitDNA dna,int age,PersonGender gender)
        {
            ulong h=lib.Revision;h=Hash(h,(uint)AgeBand(lib.Settings,age));h=Hash(h,(uint)gender);h=Hash(h,dna.Seed);
            for(int i=0;i<dna.Parts.Length;i++)h=Hash(h,(uint)dna.Parts[i]);for(int i=0;i<dna.SkinDetails.Length;i++)h=Hash(h,(uint)dna.SkinDetails[i]);
            h=Hash(h,RGB(dna.Skin));h=Hash(h,RGB(dna.Hair));return Hash(h,RGB(dna.Eyes));
        }
        static ulong Hash(ulong h,uint v)=>(h^v)*1099511628211;
        static uint RGB(Color32 c)=>(uint)(c.r|c.g<<8|c.b<<16|c.a<<24);
        public static Color32 Blend(Color32 dst,Color32 src)
        {
            if(src.a==0)return dst;if(src.a==255)return src;int inv=255-src.a;int alpha=src.a*255+dst.a*inv;if(alpha==0)return default;
            return new Color32((byte)((src.r*src.a*255+dst.r*dst.a*inv)/alpha),(byte)((src.g*src.a*255+dst.g*dst.a*inv)/alpha),(byte)((src.b*src.a*255+dst.b*dst.a*inv)/alpha),(byte)(alpha/255));
        }
        static Color32 Tint(Color32 p,Color32 c)=>new Color32((byte)(p.r*c.r/255),(byte)(p.g*c.g/255),(byte)(p.b*c.b/255),p.a);
        public static void Compose(ref PortraitLibraryBlob lib,PortraitDNA dna,int age,PersonGender gender,NativeArray<Color32> output)
        {
            int size=lib.Resolution,band=AgeBand(lib.Settings,age);
            for(int i=0;i<output.Length;i++)output[i]=default;
            if(band==0)
            {
                // Both cradle and swaddle are procedural; no infant face parts or age-specific art.
                Color32 cloth=dna.Eyes;cloth.r=(byte)((cloth.r+255)/2);cloth.g=(byte)((cloth.g+255)/2);cloth.b=(byte)((cloth.b+255)/2);
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    int px=x*32/size,py=y*32/size;Color32 c=default;
                    if((px-16)*(px-16)*49+(py-14)*(py-14)*64<=3136)c=cloth;
                    if((px-16)*(px-16)+(py-22)*(py-22)<=16)c=dna.Skin;
                    if(py==22&&(px==14||px==18))c=new Color32(45,38,35,255);
                    if((dna.Seed&1)==0&&px>=5&&px<=27&&py>=6&&py<=9)c=new Color32(134,95,60,255);
                    if(py==13&&px>=10&&px<=22)c=new Color32((byte)(cloth.r*3/4),(byte)(cloth.g*3/4),(byte)(cloth.b*3/4),255);
                    output[y*size+x]=c;
                }return;
            }
            var layers=new FixedList4096Bytes<Layer>();
            for(int i=0;i<dna.Parts.Length+dna.SkinDetails.Length;i++)
            {
                int id=i<dna.Parts.Length?dna.Parts[i]:dna.SkinDetails[i-dna.Parts.Length];int at=PortraitOps.Find(ref lib,id);if(at<0)continue;
                ref var part=ref lib.Parts[at];if((part.Genders&(gender==PersonGender.Female?2:1))==0)continue;
                for(int r=0;r<part.Count;r++)layers.Add(new Layer{Render=lib.Renders[part.Start+r],Order=i*4+r});
            }
            for(int i=1;i<layers.Length;i++){var item=layers[i];int j=i-1;while(j>=0&&(layers[j].Render.Layer>item.Render.Layer||layers[j].Render.Layer==item.Render.Layer&&layers[j].Order>item.Order)){layers[j+1]=layers[j];j--;}layers[j+1]=item;}
            int grey=band-1;var hair=new Color32((byte)((dna.Hair.r*(10-grey)+215*grey)/10),(byte)((dna.Hair.g*(10-grey)+215*grey)/10),(byte)((dna.Hair.b*(10-grey)+210*grey)/10),255);
            for(int layer=0;layer<layers.Length;layer++)
            {
                var r=layers[layer].Render;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    int n=y*size+x;var pixel=lib.Pixels[r.PixelStart+n];if(pixel.a==0)continue;
                    if(r.Tint!=PortraitTint.None)pixel=Tint(pixel,r.Tint==PortraitTint.Skin?dna.Skin:r.Tint==PortraitTint.Hair?hair:dna.Eyes);
                    // Procedural creases are confined to authored face pixels, beneath later facial layers.
                    if(r.Layer==PortraitLayer.FaceBase&&grey>0)
                    {
                        int px=x*32/size,py=y*32/size;
                        if((py==25&&px>=12&&px<=20)||(py==18&&(px==10||px==22))||(grey>=6&&py==16&&(px==12||px==20)))pixel=Blend(pixel,new Color32(55,39,35,(byte)(grey*8)));
                    }
                    output[n]=Blend(output[n],pixel);
                }
            }
        }
    }
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct PortraitComposeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {state.Dependency=new ComposeJob{Libraries=SystemAPI.GetComponentLookup<PortraitLibrary>(true)}.ScheduleParallel(state.Dependency);}
        [BurstCompile,WithDisabled(typeof(PortraitComposed))]
        partial struct ComposeJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PortraitLibrary> Libraries;
            void Execute(in PortraitComposeTask task,in SimulationOwner owner,DynamicBuffer<PortraitPixel> pixels,EnabledRefRW<PortraitComposed> completed)
            {
                if(!Libraries.HasComponent(owner.Root))return;var lib=Libraries[owner.Root].Value;if(!lib.IsCreated)return;
                pixels.ResizeUninitialized(lib.Value.Resolution*lib.Value.Resolution);PortraitPixels.Compose(ref lib.Value,task.DNA,task.Age,task.Gender,pixels.Reinterpret<Color32>().AsNativeArray());completed.ValueRW=true;
            }
        }
    }
}
