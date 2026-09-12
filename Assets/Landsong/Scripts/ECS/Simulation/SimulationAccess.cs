using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    // Stateless operations shared by ECS systems and isolated simulation tests.
    public static class Sim
    {
        public static Entity Root(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<Session>());
            return q.CalculateEntityCount() == 1 ? q.GetSingletonEntity() : Entity.Null;
        }
        public static NativeArray<Entity> Entities<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return q.ToEntityArray(Allocator.Temp);
        }
        public static Entity Find(EntityManager em, ulong id)
        {
            if (id == 0) return Entity.Null;
            using var all = Entities<Identity>(em);
            foreach (var e in all) if (em.GetComponentData<Identity>(e).Id == id) return e;
            return Entity.Null;
        }
        public static NativeArray<Entity> OrderedEntities<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var all = Entities<T>(em);
            all.Sort(new StableIdentityComparer { Manager = em });
            return all;
        }
        struct StableIdentityComparer : System.Collections.Generic.IComparer<Entity>
        {
            public EntityManager Manager;
            public int Compare(Entity a, Entity b) => Manager.GetComponentData<Identity>(a).Id.CompareTo(Manager.GetComponentData<Identity>(b).Id);
        }
        public static ContentDefinition Definition(EntityManager em, Entity root, int index) =>
            em.GetComponentData<ContentCatalog>(root).Value.Value.Definitions[index];
        public static int FindDefinition(EntityManager em, Entity root, FixedString128Bytes id)
        {
            ref var data = ref em.GetComponentData<ContentCatalog>(root).Value.Value;
            for (var i = 0; i < data.Definitions.Length; i++) if (data.Definitions[i].Id == id) return i;
            return -1;
        }
        public static bool ValidDefinition(EntityManager em, Entity root, int index) => index >= 0 && index < em.GetComponentData<ContentCatalog>(root).Value.Value.Definitions.Length;
        public static int FirstDefinition(EntityManager em, Entity root, ContentKind kind)
        {
            var blob = em.GetComponentData<ContentCatalog>(root).Value;
            for (var i = 0; i < blob.Value.Definitions.Length; i++) if (blob.Value.Definitions[i].Kind == kind) return i;
            return -1;
        }
        public static Rule GetRule(EntityManager em, Entity root, int index) => em.GetComponentData<ContentCatalog>(root).Value.Value.Rules[index];
        public static Rule Rule(EntityManager em, Entity root, int definition, RuleKind kind, int level = 1)
        {
            var d = Definition(em, root, definition);
            var found = new Rule { Kind = kind, Target = -1, Level = -1 };
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = GetRule(em, root, d.RuleStart + i);
                if (r.Kind == kind && r.Level <= level && r.Level >= found.Level) found = r;
            }
            return found;
        }
        public static bool Operational(EntityManager em, Entity e) => e != Entity.Null && em.Exists(e) && em.HasComponent<Building>(e) && em.GetComponentData<Building>(e).Stage == LifeStage.Operational;
        public static bool Alive(EntityManager em, Entity e) => e != Entity.Null && em.Exists(e) && em.HasComponent<Health>(e) && em.GetComponentData<Health>(e).Current > 0;
        public static ulong AllocateId(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            var id = ++s.NextId;
            em.SetComponentData(root, s);
            return id;
        }
        public static uint NextRandom(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            var r = new Random(math.max(1u, s.RandomState));
            var result = r.NextUInt(); s.RandomState = r.state; em.SetComponentData(root, s);
            return result;
        }
        public static void Emit(EntityManager em, Entity root, EventKind kind, FixedString128Bytes message, ulong target = 0, int definition = -1, int amount = 0, HistoryCategory? category = null)
        {
            var historyCategory = category ?? HistoryOps.DefaultCategory(kind);
            em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = kind, Category = historyCategory, Message = message, Target = target, Definition = definition, Amount = amount });
            HistoryOps.Message(em, root, kind, message, target, historyCategory);
        }
        public static Entity Spawn(EntityManager em, Entity root, int definition, float3 position, bool persistent)
        {
            if (!ValidDefinition(em, root, definition)) throw new System.InvalidOperationException("Cannot spawn an unregistered ECS definition.");
            var prefab = Entity.Null;
            foreach (var p in em.GetBuffer<ContentPrefab>(root)) if (p.Definition == definition) { prefab = p.Prefab; break; }
            var kind = Definition(em, root, definition).Kind;
            if (prefab == Entity.Null && (kind == ContentKind.Building || kind == ContentKind.Soldier || kind == ContentKind.Hero || kind == ContentKind.Enemy || kind == ContentKind.Projectile || kind == ContentKind.Loot || kind == ContentKind.Opportunity))
                throw new System.InvalidOperationException("Visual ECS definition is missing its baked Entity Prefab: " + Definition(em, root, definition).Id);
            var e = prefab == Entity.Null ? em.CreateEntity() : em.Instantiate(prefab);
            Set(em, e, new Identity { Id = AllocateId(em, root), Definition = definition, Name = Definition(em, root, definition).Name });
            Set(em, e, LocalTransform.FromPosition(position));
            Set(em, e, new SimulationOwner { Root = root });
            if (persistent) Set(em, e, new Persistent()); else Set(em, e, new NightTransient());
            return e;
        }
        public static void Set<T>(EntityManager em, Entity entity, T value) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity)) em.SetComponentData(entity, value); else em.AddComponentData(entity, value);
        }
        public static void Buffer<T>(EntityManager em, Entity entity) where T : unmanaged, IBufferElementData
        { if (!em.HasBuffer<T>(entity)) em.AddBuffer<T>(entity); }
        public static int Population(EntityManager em, Entity root)
        {
            var count = em.GetComponentData<Session>(root).BasePopulation;
            using var all = Entities<Building>(em);
            foreach (var e in all) { var b = em.GetComponentData<Building>(e); if (Operational(em, e) || b.RuinPending != 0) count += b.Population + em.GetComponentData<BuildingStats>(e).BasePopulation; }
            return math.max(0, count);
        }
        public static int Employed(EntityManager em)
        {
            var count = 0;
            using var buildings = Entities<Building>(em);
            foreach (var e in buildings) count += em.GetComponentData<Building>(e).Workers;
            using var soldiers = Entities<Soldier>(em);
            foreach (var e in soldiers) count += em.GetComponentData<Soldier>(e).PopulationCost;
            using var heroes = Entities<Hero>(em);
            var root = Root(em);
            foreach (var e in heroes) { var hero = em.GetComponentData<Hero>(e); if (hero.Recruited != 0 || hero.DeathPending != 0) count += Definition(em, root, em.GetComponentData<Identity>(e).Definition).Population; }
            return count;
        }
        public static float3 Position(EntityManager em, Entity e) => em.GetComponentData<LocalTransform>(e).Position;

    }
}
