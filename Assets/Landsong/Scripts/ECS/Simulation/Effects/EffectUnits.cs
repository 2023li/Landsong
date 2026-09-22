using Landsong.ECS.Definitions;

namespace Landsong.ECS
{
    public static class EffectUnits
    {
        public static EffectValueUnit For(NumericEffectKind effect) => effect == NumericEffectKind.ActionPower || effect == NumericEffectKind.Armor || effect == NumericEffectKind.Penetration || effect == NumericEffectKind.BlastRadius ? EffectValueUnit.Flat : EffectValueUnit.Ratio;
    }
}
