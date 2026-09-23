using System;
using Sirenix.OdinInspector;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_ExperienceHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [LabelText("所属建筑详情"), Required]
        public UI_GamePanel_BuildingDetails View;

        Func<string> content;

        public void Refresh(Entity entity)
        {
            var session = View.sessionController;
            bool hasWorkforce = session.em.GetComponentData<BuildingWorkforceStats>(entity).Capacity > 0;
            content = hasWorkforce ? () => WorkerEfficiencyOps.Describe(session.em, session.root, entity, "经验") : null;
            View.SetSidebarContent(this, content);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (content != null)
                View.ShowSidebar(this, content);
        }

        public void OnPointerExit(PointerEventData eventData) => View.LeaveSidebar(this);

        void OnDisable()
        {
            if (View != null)
                View.HideSidebar(this);
        }
    }
}
