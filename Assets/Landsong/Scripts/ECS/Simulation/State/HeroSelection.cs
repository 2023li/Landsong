using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct HeroSelection : IComponentData
    {
        public Entity SelectedHero;
    }
}
