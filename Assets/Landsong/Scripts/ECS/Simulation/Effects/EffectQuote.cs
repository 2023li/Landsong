using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Landsong.ECS
{
    internal enum EffectDomain
    {
        Global,
        Military,
        Personal,
        Court,
        Intelligence
    }

    public enum EffectSourceKind
    {
        [LabelText("永久增益")]
        Buff,
        [LabelText("政策")]
        Policy,
        [LabelText("人才")]
        Talent,
        [LabelText("人才岗位")]
        TalentSlot,
        [LabelText("人物特性")]
        PersonalTrait,
        [LabelText("君王特性")]
        MonarchTrait,
        [LabelText("科技")]
        Technology,
        [LabelText("建筑")]
        Building,
        [LabelText("政治遗产")]
        Legacy,
        [LabelText("临时朝局")]
        Temporary,
        [LabelText("动荡")]
        Disorder
    }

    public enum EffectValueUnit
    {
        Flat,
        Ratio
    }

    public sealed class EffectSource
    {
        public EffectSourceKind Kind;
        public int Level;
        public ulong Owner;
        public string Name, Reason;
        public float Value, Applied;
    }

    public sealed class EffectQuote
    {
        public float Value;
        public EffectValueUnit Unit;
        public readonly List<EffectSource> Sources = new List<EffectSource>();
    }
}
