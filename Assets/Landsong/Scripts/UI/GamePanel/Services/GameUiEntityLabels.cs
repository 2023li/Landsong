using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    internal static class GameUiEntityLabels
    {
        internal static string EntityName(this GameUiSessionHandle session, ulong id)
        {
            var entity = WorldQueries.Find(session.em, id);
            return entity == Entity.Null ? "无驻地" : session.em.GetComponentData<Identity>(entity).Name.ToString();
        }
    }
}
