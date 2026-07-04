using System;
using Landsong.TechnologySystem;

namespace Landsong.ConditionSystem
{
    [Serializable]
    public abstract class GameCondition
    {
        public abstract bool IsMet(GameSystem context);
    }

    [Serializable]
    public sealed class GameCondition_True : GameCondition
    {
        public override bool IsMet(GameSystem context)
        {
            return true;
        }
    }

    [Serializable]
    public sealed class GameCondition_False : GameCondition
    {
        public override bool IsMet(GameSystem context)
        {
            return false;
        }
    }



    [Serializable]
    public sealed class GameCondition_String : GameCondition
    {

        public string Value;
        public override bool IsMet(GameSystem context)
        {
            return true;
        }
    }

    [Serializable]
    public sealed class GameCondition_TechnologyUnlocked : GameCondition
    {
        public TechnologyDefinition TechnologyDefinition;

        public override bool IsMet(GameSystem context)
        {
            return context != null
                   && TechnologyDefinition != null
                   && context.IsTechnologyUnlocked(TechnologyDefinition.TechnologyId);
        }
    }

   
}
