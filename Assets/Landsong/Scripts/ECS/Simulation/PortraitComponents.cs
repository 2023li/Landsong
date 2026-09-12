using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS
{
    public enum PortraitPartType : byte {
        [LabelText("脸型")] Face,
        [LabelText("耳朵")] Ear,
        [LabelText("眼睛")] Eyes,
        [LabelText("眉毛")] Eyebrows,
        [LabelText("鼻子")] Nose,
        [LabelText("嘴型")] Mouth,
        [LabelText("发型")] Hair,
        [LabelText("胡须")] FacialHair,
        [LabelText("身体")] Body,
        [LabelText("服装")] Clothes,
        [LabelText("头饰")] HeadAccessory,
        [LabelText("面饰")] FaceAccessory,
        [LabelText("饰品")] Accessory,
        [LabelText("皮肤细节")] SkinDetail,
        [LabelText("临时效果")] Effect
    }
    public enum PortraitTint : byte {
        [LabelText("固定颜色")] None,
        [LabelText("肤色")] Skin,
        [LabelText("发色")] Hair,
        [LabelText("眼睛颜色")] Eyes
    }
    public enum PortraitLayer : byte {
        [LabelText("饰品后层")] BackAccessory=0,
        [LabelText("后发")] HairBack=10,
        [LabelText("颈部后层")] NeckBack=20,
        [LabelText("身体")] Body=30,
        [LabelText("服装后层")] ClothesBack=40,
        [LabelText("颈部")] Neck=50,
        [LabelText("脸部底图")] FaceBase=60,
        [LabelText("耳朵")] Ear=70,
        [LabelText("眼睛底图")] Eyes=80,
        [LabelText("瞳孔覆盖层")] EyeOverlay=90,
        [LabelText("眉毛")] Eyebrows=100,
        [LabelText("鼻子")] Nose=110,
        [LabelText("嘴型")] Mouth=120,
        [LabelText("皮肤细节")] SkinDetail=130,
        [LabelText("胡须")] FacialHair=140,
        [LabelText("前发")] HairFront=150,
        [LabelText("服装前层")] ClothesFront=160,
        [LabelText("头饰后层")] HeadAccessoryBack=170,
        [LabelText("头饰前层")] HeadAccessoryFront=180,
        [LabelText("面饰")] FaceAccessory=190,
        [LabelText("饰品前层")] FrontAccessory=200,
        [LabelText("临时效果")] Effect=210
    }
    [System.Serializable] public struct PortraitSettings
    {
        [LabelText("青年年龄")] public int YouthAge;
        [LabelText("开始灰发年龄")] public int GreyAge;
        [LabelText("老年年龄")] public int ElderAge;
        [LabelText("士兵招募最小年龄")] public int SoldierRecruitMinAge;
        [LabelText("士兵招募最大年龄")] public int SoldierRecruitMaxAge;
        [LabelText("士兵最短寿命")] public int SoldierLifeMin;
        [LabelText("士兵最长寿命")] public int SoldierLifeMax;
        [LabelText("遗传颜色变异幅度")] public float ColorMutation;

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
    {public int Age,Lifespan,LastAgeTurn,Incarnation;public PersonGender Gender;public byte SpecialAttention,DeathNotified;}
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
