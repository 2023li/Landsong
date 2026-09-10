using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static partial class MilitaryOps
    {
        public static void InitializePerson(EntityManager em,Entity root,Entity unit,int incarnation=0)
        {
            if(!PortraitOps.Ready(em,root))return;var settings=em.GetComponentData<PortraitLibrary>(root).Value.Value.Settings;ulong id=em.GetComponentData<Identity>(unit).Id;
            var random=new Random(math.max(1,math.hash(new uint3((uint)id,(uint)(id>>32),(uint)incarnation+0x51A76u))));
            Sim.Set(em,unit,new SoldierPerson{Age=random.NextInt(settings.SoldierRecruitMinAge,settings.SoldierRecruitMaxAge+1),Lifespan=random.NextInt(settings.SoldierLifeMin,settings.SoldierLifeMax+1),Gender=random.NextBool()?PersonGender.Male:PersonGender.Female,LastAgeTurn=em.GetComponentData<Session>(root).Turn-(incarnation==0?1:0),Incarnation=incarnation});
            var lib=em.GetComponentData<PortraitLibrary>(root).Value;Sim.Set(em,unit,PortraitOps.Generate(ref lib.Value,random.NextUInt(1,uint.MaxValue),PortraitOps.Gender(em,unit)));
            if(incarnation>0){var identity=em.GetComponentData<Identity>(unit);ulong next=id+(ulong)incarnation*1000003;var name=SoldierName(next);while(name==identity.Name)name=SoldierName(++next);identity.Name=name;em.SetComponentData(unit,identity);}
        }
        public static void MarkCustomName(EntityManager em,Entity unit)
        {if(!em.HasComponent<SoldierPerson>(unit))return;var p=em.GetComponentData<SoldierPerson>(unit);p.CustomName=1;em.SetComponentData(unit,p);}
        public static void RememberSoldier(EntityManager em,Entity root,Entity unit,bool natural)
        {
            if(!em.HasComponent<SoldierPerson>(unit)||EconomyJournalOps.Forecast(em,root))return;
            var person=em.GetComponentData<SoldierPerson>(unit);if(person.CustomName==0||person.DeathNotified!=0)return;person.DeathNotified=1;em.SetComponentData(unit,person);
            var identity=em.GetComponentData<Identity>(unit);string name=identity.Name.ToString();if(name.Length>12)name=name.Substring(0,12);
            Sim.Emit(em,root,EventKind.Message,new FixedString128Bytes(natural?name+"自然死亡。遗言：愿后来的人守住家园。":name+"阵亡。遗愿：替我看看太平的日子。"),identity.Id);
        }
        public static void AgeSoldiers(EntityManager em,Entity root)
        {
            if(!PortraitOps.Ready(em,root)||EconomyJournalOps.Forecast(em,root))return;int turn=em.GetComponentData<Session>(root).Turn;
            using var people=Sim.OrderedEntities<Soldier>(em);foreach(var unit in people)
            {
                if(!Sim.Alive(em,unit))continue;if(!em.HasComponent<SoldierPerson>(unit))InitializePerson(em,root,unit);
                var person=em.GetComponentData<SoldierPerson>(unit);if(person.LastAgeTurn>=turn)continue;person.LastAgeTurn=turn;person.Age++;em.SetComponentData(unit,person);
                if(person.Age<person.Lifespan)continue;RememberSoldier(em,root,unit,true);InitializePerson(em,root,unit,person.Incarnation+1);
                // Military identity, garrison, experience, costs and combat stats remain untouched.
            }
        }
    }
}
