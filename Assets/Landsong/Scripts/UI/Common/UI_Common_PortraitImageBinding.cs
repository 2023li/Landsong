using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_Common_PortraitImageBinding : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("目标")]
        public Image Target;
        [Sirenix.OdinInspector.LabelText("缓存")]
        public PortraitCache Cache;
        internal PortraitCache BoundCache;
        internal ulong Key;
        bool dead, configured, listening;
        EntityManager manager;
        World world;
        Entity root;
        ulong id;
        PortraitDNA? preview;
        public ulong BoundPersonId => configured ? id : 0;
        public void ValidateConfiguration()
        {
            if (Target == null || Cache == null)
                throw new InvalidOperationException(name + " 的肖像绑定检查器引用不完整。");
        }

        public void Bind(EntityManager em, Entity owner, ulong personId, PortraitDNA? portraitPreview = null)
        {
            ValidateConfiguration();
            if (!listening)
            {
                Target.onCullStateChanged.AddListener(Culled);
                listening = true;
            }

            manager = em;
            world = em.World;
            root = owner;
            id = personId;
            preview = portraitPreview;
            configured = true;
            Target.preserveAspect = true;
            Target.raycastTarget = false;
            Rebind();
        }

        public void Unbind()
        {
            configured = false;
            manager = default;
            world = null;
            root = Entity.Null;
            id = 0;
            preview = null;
            dead = false;
            Release();
            if (Target != null)
                Target.sprite = null;
        }

        void Rebind()
        {
            if (!configured || !isActiveAndEnabled || Target.canvasRenderer.cull || world == null || !world.IsCreated)
                return;
            var em = manager;
            var person = Sim.Find(em, id);
            if (person == Entity.Null || !PortraitOps.Ready(em, root) || !em.HasComponent<PortraitDNA>(person))
            {
                Release();
                Target.sprite = null;
                return;
            }

            dead = em.HasComponent<Royal>(person) ? !CourtOps.Alive(em, person) : em.HasComponent<Health>(person) && !Sim.Alive(em, person);
            var dna = preview ?? em.GetComponentData<PortraitDNA>(person);
            var identity = em.GetComponentData<Identity>(person);
            string definition = Sim.ValidDefinition(em, root, identity.Definition) ? Sim.Definition(em, root, identity.Definition).Id.ToString() : "";
            var fixedPortrait = PresentationRuntime.Instance?.Catalog?.Face(definition, id);
            if (preview == null && dna.Customized == 0 && fixedPortrait != null)
            {
                Release();
                Apply(fixedPortrait);
                return;
            }

            Cache.Bind(this, em, root, person, dna, PortraitOps.Age(em, person), PortraitOps.Gender(em, person));
        }

        void Culled(bool culled)
        {
            if (culled)
            {
                Release();
                Target.sprite = null;
            }
            else
                Rebind();
        }

        internal void Apply(Sprite sprite)
        {
            if (Target == null)
                return;
            Target.sprite = sprite;
            Target.color = dead ? Color.gray : Color.white;
        }

        internal void Release()
        {
            if (BoundCache != null)
                BoundCache.Release(this);
            BoundCache = null;
            Key = 0;
        }

        void OnEnable()
        {
            Rebind();
        }

        void OnDisable()
        {
            Release();
        }

        void OnDestroy()
        {
            Release();
            if (listening && Target != null)
                Target.onCullStateChanged.RemoveListener(Culled);
        }
    }
}
