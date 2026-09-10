using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random=Unity.Mathematics.Random;

namespace Landsong.ECS
{
    public static class PortraitOps
    {
        public const int Slots=13;
        public static bool Ready(EntityManager em,Entity root)=>root!=Entity.Null&&em.Exists(root)&&em.HasComponent<PortraitLibrary>(root)&&em.GetComponentData<PortraitLibrary>(root).Value.IsCreated;
        public static int Age(EntityManager em,Entity person)=>em.HasComponent<Royal>(person)?em.GetComponentData<Royal>(person).Age:em.HasComponent<SoldierPerson>(person)?em.GetComponentData<SoldierPerson>(person).Age:25;
        public static PersonGender Gender(EntityManager em,Entity person)=>em.HasComponent<Royal>(person)?em.GetComponentData<Royal>(person).Gender:em.HasComponent<SoldierPerson>(person)?em.GetComponentData<SoldierPerson>(person).Gender:PersonGender.Male;
        public static int Find(ref PortraitLibraryBlob lib,int id){for(int i=0;i<lib.Parts.Length;i++)if(lib.Parts[i].Id==id)return i;return -1;}
        public static bool Compatible(ref PortraitLibraryBlob lib,int id,PortraitPartType type,PersonGender gender)
        {if(id==0)return type!=PortraitPartType.Face&&type!=PortraitPartType.Eyes&&type!=PortraitPartType.Body&&type!=PortraitPartType.Clothes;int i=Find(ref lib,id);return i>=0&&lib.Parts[i].Type==type&&(lib.Parts[i].Genders&(gender==PersonGender.Female?2:1))!=0;}
        public static PortraitDNA Generate(ref PortraitLibraryBlob lib,uint seed,PersonGender gender)
        {
            var random=new Random(math.max(1,seed));var dna=new PortraitDNA{Seed=math.max(1,seed)};
            for(int slot=0;slot<Slots;slot++)
            {
                var type=(PortraitPartType)slot;float total=slot==7||slot>=10?1:0;
                for(int i=0;i<lib.Parts.Length;i++){ref var p=ref lib.Parts[i];if(p.Type==type&&(p.Genders&(gender==PersonGender.Female?2:1))!=0)total+=p.Weight;}
                float pick=random.NextFloat()*total;int chosen=0;
                for(int i=0;i<lib.Parts.Length;i++){ref var p=ref lib.Parts[i];if(p.Type!=type||(p.Genders&(gender==PersonGender.Female?2:1))==0)continue;pick-=p.Weight;if(pick<0){chosen=p.Id;break;}}
                dna.Parts.Add(chosen);
            }
            dna.Skin=lib.SkinColors[random.NextInt(lib.SkinColors.Length)];dna.Hair=lib.HairColors[random.NextInt(lib.HairColors.Length)];dna.Eyes=lib.EyeColors[random.NextInt(lib.EyeColors.Length)];return dna;
        }
        public static void Ensure(EntityManager em,Entity root,Entity person)
        {
            if(!Ready(em,root)||em.HasComponent<PortraitDNA>(person))return;
            var id=em.GetComponentData<Identity>(person).Id;var seed=math.hash(new uint3((uint)id,(uint)(id>>32),em.GetComponentData<Session>(root).RandomState));
            var library=em.GetComponentData<PortraitLibrary>(root).Value;var dna=Generate(ref library.Value,seed,Gender(em,person));Sim.Set(em,person,dna);
        }
        public static void EnsurePeople(EntityManager em,Entity root)
        {using var people=Sim.OrderedEntities<Royal>(em);foreach(var person in people)Ensure(em,root,person);}
        public static void Inherit(EntityManager em,Entity root,Entity child,Entity first,Entity second)
        {
            if(!Ready(em,root))return;Ensure(em,root,first);Ensure(em,root,second);Ensure(em,root,child);
            var dna=em.GetComponentData<PortraitDNA>(child);var a=em.GetComponentData<PortraitDNA>(first);var b=em.GetComponentData<PortraitDNA>(second);var random=new Random(math.max(1,dna.Seed^0xAB2531u));
            float mutation=em.GetComponentData<PortraitLibrary>(root).Value.Value.Settings.ColorMutation;
            Color32 Color(Color32 own,Color32 one,Color32 two)=>random.NextFloat()<mutation?own:random.NextBool()?one:two;
            dna.Skin=Color(dna.Skin,a.Skin,b.Skin);dna.Hair=Color(dna.Hair,a.Hair,b.Hair);dna.Eyes=Color(dna.Eyes,a.Eyes,b.Eyes);em.SetComponentData(child,dna);
        }
        public static bool CanCustomize(EntityManager em,Entity root,Entity person)
        {return Ready(em,root)&&CourtOps.Alive(em,person)&&em.HasComponent<PortraitDNA>(person)&&em.GetComponentData<PortraitDNA>(person).Customized==0&&Age(em,person)>=em.GetComponentData<PortraitLibrary>(root).Value.Value.Settings.YouthAge&&CourtOps.HasTrait(em,root,person,"gene.beauty");}
        public static void Announce(EntityManager em,Entity root)
        {
            using var people=Sim.OrderedEntities<Royal>(em);foreach(var p in people)
            {if(!CanCustomize(em,root,p))continue;var dna=em.GetComponentData<PortraitDNA>(p);if(dna.InvitationAnnounced!=0)continue;dna.InvitationAnnounced=1;em.SetComponentData(p,dna);CourtOps.Log(em,root,"丽质初成，可在处理请求中塑造一次容貌",em.GetComponentData<Identity>(p).Id);}
        }
        public static bool Valid(ref PortraitLibraryBlob lib,PortraitDNA dna,PersonGender gender)
        {
            if(dna.Seed==0||dna.Customized>1||dna.InvitationAnnounced>1||dna.Parts.Length!=Slots||dna.SkinDetails.Length>8||dna.Skin.a!=255||dna.Hair.a!=255||dna.Eyes.a!=255)return false;
            for(int i=0;i<Slots;i++)if(!Compatible(ref lib,dna.Parts[i],(PortraitPartType)i,gender))return false;
            for(int i=0;i<dna.SkinDetails.Length;i++){if(!Compatible(ref lib,dna.SkinDetails[i],PortraitPartType.SkinDetail,gender)||dna.SkinDetails[i]==0)return false;for(int j=0;j<i;j++)if(dna.SkinDetails[i]==dna.SkinDetails[j])return false;}return true;
        }
        public static ResultCode Customize(EntityManager em,Entity root,Entity person,FixedString128Bytes payload,uint seed)
        {
            if(!CanCustomize(em,root,person))return ResultCode.Unavailable;var dna=em.GetComponentData<PortraitDNA>(person);if(dna.Seed!=seed)return ResultCode.Unavailable;
            // Compact one-command payload: stable part IDs plus RGB bytes, never preview mutations.
            byte[] bytes;try{bytes=Convert.FromBase64String(payload.ToString());}catch(FormatException){return ResultCode.InvalidContent;}
            if(bytes.Length!=Slots*4+9)return ResultCode.InvalidContent;var candidate=dna;
            for(int i=0;i<Slots;i++){int n=i*4;candidate.Parts[i]=bytes[n]|bytes[n+1]<<8|bytes[n+2]<<16|bytes[n+3]<<24;}
            int offset=Slots*4;candidate.Skin=new Color32(bytes[offset],bytes[offset+1],bytes[offset+2],255);candidate.Hair=new Color32(bytes[offset+3],bytes[offset+4],bytes[offset+5],255);candidate.Eyes=new Color32(bytes[offset+6],bytes[offset+7],bytes[offset+8],255);
            var lib=em.GetComponentData<PortraitLibrary>(root).Value;if(!Valid(ref lib.Value,candidate,Gender(em,person)))return ResultCode.InvalidContent;
            candidate.Customized=1;candidate.InvitationAnnounced=1;em.SetComponentData(person,candidate);CourtOps.Log(em,root,"丽质容貌已定，此后只能自然衰老",em.GetComponentData<Identity>(person).Id);return ResultCode.Success;
        }
        public static string Payload(PortraitDNA dna)
        {
            var bytes=new byte[Slots*4+9];for(int i=0;i<Slots;i++){uint v=(uint)dna.Parts[i];for(int b=0;b<4;b++)bytes[i*4+b]=(byte)(v>>(b*8));}
            int offset=Slots*4;foreach(var c in new[]{dna.Skin,dna.Hair,dna.Eyes}){bytes[offset++]=c.r;bytes[offset++]=c.g;bytes[offset++]=c.b;}return Convert.ToBase64String(bytes);
        }
    }
}
