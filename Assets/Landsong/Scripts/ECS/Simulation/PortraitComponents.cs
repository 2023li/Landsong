using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS
{
    public enum PortraitPartType : byte { Face, Ear, Eyes, Eyebrows, Nose, Mouth, Hair, FacialHair, Body, Clothes, HeadAccessory, FaceAccessory, Accessory, SkinDetail, Effect }
    public enum PortraitTint : byte { None, Skin, Hair, Eyes }
    public enum PortraitLayer : byte { BackAccessory=0, HairBack=10, NeckBack=20, Body=30, ClothesBack=40, Neck=50, FaceBase=60, Ear=70, Eyes=80, EyeOverlay=90, Eyebrows=100, Nose=110, Mouth=120, SkinDetail=130, FacialHair=140, HairFront=150, ClothesFront=160, HeadAccessoryBack=170, HeadAccessoryFront=180, FaceAccessory=190, FrontAccessory=200, Effect=210 }
    [System.Serializable] public struct PortraitSettings
    {
        public int YouthAge, GreyAge, ElderAge, SoldierRecruitMinAge, SoldierRecruitMaxAge, SoldierLifeMin, SoldierLifeMax;
        public float ColorMutation;
        
        public static PortraitSettings Default=>new PortraitSettings{YouthAge=16,GreyAge=50,ElderAge=75,SoldierRecruitMinAge=18,SoldierRecruitMaxAge=30,SoldierLifeMin=65,SoldierLifeMax=85,ColorMutation=.05f};
    }
    // Permanent appearance. Age effects are derived and never overwrite inherited colors.
    public struct PortraitDNA : IComponentData
    {
        public FixedList128Bytes<int> Parts;
        public FixedList64Bytes<int> SkinDetails;
        public Color32 Skin,Hair,Eyes;
        public uint Seed;
        public byte Customized,InvitationAnnounced;
    }
    public struct SoldierPerson : IComponentData
    {public int Age,Lifespan,LastAgeTurn,Incarnation;public PersonGender Gender;public byte CustomName,DeathNotified;}
    public struct PortraitLibrary : IComponentData {public BlobAssetReference<PortraitLibraryBlob> Value;}
    public struct PortraitLibraryBlob
    {
        public int Resolution;
        public ulong Revision;
        public PortraitSettings Settings;
        public BlobArray<PortraitPartBlob> Parts;
        public BlobArray<PortraitRenderBlob> Renders;
        public BlobArray<Color32> Pixels,SkinColors,HairColors,EyeColors;
    }
    public struct PortraitPartBlob
    {public int Id,Start,Count;public PortraitPartType Type;public byte Genders;public float Weight;}
    public struct PortraitRenderBlob {public PortraitLayer Layer;public PortraitTint Tint;public int PixelStart;}
    // Task data has no persistent identity and belongs to one simulation root.
    public struct PortraitComposeTask : IComponentData
    {public Entity Person;public ulong Key;public int Age;public PersonGender Gender;public PortraitDNA DNA;}
    [InternalBufferCapacity(0)] public struct PortraitPixel : IBufferElementData {public Color32 Value;}
    public struct PortraitComposed : IComponentData,IEnableableComponent {}
}
