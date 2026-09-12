using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // Game-scene owner. Only queued work is pumped; no scan of all characters or per-frame recomposition.
    public sealed class PortraitCache : MonoBehaviour
    {
        sealed class Entry
        {
            public Sprite Sprite;
            public Texture2D Texture;
            public Entity Task, Person;
            public PortraitDNA DNA;
            public int Age;
            public PersonGender Gender;
            public float Used;
            public readonly HashSet<UI_Common_PortraitImageBinding> Views = new HashSet<UI_Common_PortraitImageBinding>();
        }

        readonly Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();
        EntityManager em;
        Entity root;
        World world;
        const int Capacity = 128, Concurrent = 8;
        public int SpriteCount
        {
            get
            {
                int n = 0;
                foreach (var e in entries.Values)
                    if (e.Sprite != null)
                        n++;
                return n;
            }
        }

        public void Bind(UI_Common_PortraitImageBinding view, EntityManager manager, Entity owner, Entity person, PortraitDNA dna, int age, PersonGender gender)
        {
            if (world != manager.World || root != owner)
            {
                Clear();
                em = manager;
                world = manager.World;
                root = owner;
            }

            var library = em.GetComponentData<PortraitLibrary>(root).Value;
            ulong key = PortraitPixels.Key(ref library.Value, dna, age, gender);
            if (view.BoundCache == this && view.Key == key && entries.TryGetValue(key, out var same))
            {
                same.Person = person;
                same.Used = Time.unscaledTime;
                view.Apply(same.Sprite);
                return;
            }

            view.Release();
            view.BoundCache = this;
            view.Key = key;
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new Entry
                {
                    Person = person,
                    DNA = dna,
                    Age = age,
                    Gender = gender
                };
                entries.Add(key, entry);
            }

            entry.Person = person;
            entry.Views.Add(view);
            entry.Used = Time.unscaledTime;
            view.Apply(entry.Sprite);
        }

        public void Release(UI_Common_PortraitImageBinding view)
        {
            if (entries.TryGetValue(view.Key, out var e))
                e.Views.Remove(view);
        }

        void LateUpdate()
        {
            if (world == null || !world.IsCreated || !em.Exists(root) || !PortraitOps.Ready(em, root))
            {
                Clear();
                return;
            }

            em.CompleteAllTrackedJobs();
            int active = 0;
            foreach (var pair in entries)
            {
                var e = pair.Value;
                e.Views.RemoveWhere(v => v == null);
                if (e.Task == Entity.Null)
                    continue;
                if (!em.Exists(e.Task))
                {
                    e.Task = Entity.Null;
                    continue;
                }

                if (!em.Exists(e.Person) || e.Views.Count == 0)
                {
                    em.DestroyEntity(e.Task);
                    e.Task = Entity.Null;
                    continue;
                }

                if (!em.IsComponentEnabled<PortraitComposed>(e.Task))
                {
                    active++;
                    continue;
                }

                var lib = em.GetComponentData<PortraitLibrary>(root).Value;
                var pixels = em.GetBuffer<PortraitPixel>(e.Task);
                e.Texture = new Texture2D(lib.Value.Resolution, lib.Value.Resolution, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "Portrait " + pair.Key
                };
                e.Texture.SetPixelData(pixels.Reinterpret<Color32>().AsNativeArray(), 0);
                e.Texture.Apply(false, true);
                e.Sprite = Sprite.Create(e.Texture, new Rect(0, 0, e.Texture.width, e.Texture.height), new Vector2(.5f, .5f), 32);
                e.Sprite.name = e.Texture.name;
                em.DestroyEntity(e.Task);
                e.Task = Entity.Null;
                foreach (var view in e.Views)
                    view.Apply(e.Sprite);
            }

            foreach (var pair in entries)
            {
                if (active >= Concurrent)
                    break;
                var e = pair.Value;
                if (e.Sprite != null || e.Task != Entity.Null || e.Views.Count == 0)
                    continue;
                // Stable IDs can be restored into new entities. Bind supplies the new owner on the next UI refresh.
                if (!em.Exists(e.Person))
                    continue;
                e.Task = em.CreateEntity();
                em.AddComponentData(e.Task, new SimulationOwner { Root = root });
                em.AddComponentData(e.Task, new PortraitComposeTask { Person = e.Person, Key = pair.Key, DNA = e.DNA, Age = e.Age, Gender = e.Gender });
                em.AddBuffer<PortraitPixel>(e.Task);
                em.AddComponent<PortraitComposed>(e.Task);
                em.SetComponentEnabled<PortraitComposed>(e.Task, false);
                active++;
            }

            while (entries.Count > Capacity)
            {
                ulong oldest = 0;
                float time = float.MaxValue;
                bool found = false;
                foreach (var p in entries)
                    if (p.Value.Views.Count == 0 && p.Value.Used < time)
                    {
                        oldest = p.Key;
                        time = p.Value.Used;
                        found = true;
                    }

                if (!found)
                    break;
                Dispose(entries[oldest]);
                entries.Remove(oldest);
            }
        }

        void Dispose(Entry e)
        {
            if (e.Sprite != null)
                Destroy(e.Sprite);
            if (e.Texture != null)
                Destroy(e.Texture);
            if (world != null && world.IsCreated && e.Task != Entity.Null && em.Exists(e.Task))
                em.DestroyEntity(e.Task);
        }

        void Clear()
        {
            foreach (var e in entries.Values)
                Dispose(e);
            entries.Clear();
            world = null;
            root = Entity.Null;
        }

        void OnDestroy()
        {
            Clear();
        }
    }
}
