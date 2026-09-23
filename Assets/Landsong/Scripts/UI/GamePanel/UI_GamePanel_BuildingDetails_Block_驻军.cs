using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_驻军 : UI_GamePanel_BuildingDetails_Block
    {
        public readonly struct SlotModel
        {
            public readonly ulong Id;
            public readonly string Name;
            public readonly bool Alive;

            public SlotModel(ulong id, string name, bool alive)
            {
                Id = id;
                Name = name;
                Alive = alive;
            }
        }

        [LabelText("驻军文字"), Required]
        public TMP_Text Label;
        [LabelText("调整驻军"), Required]
        public Button Adjust;
        [LabelText("驻军槽位"), Required]
        public RectTransform Slots;
        [LabelText("驻军槽位模板"), Required]
        public UI_GamePanel_GarrisonSlot SlotTemplate;

        readonly List<(UI_Common_PortraitImageBinding Binding, ulong Id)> portraits = new List<(UI_Common_PortraitImageBinding, ulong)>();
        string signature;

        public void AppendDetails(Entity site, Action<string, Action, string> detail)
        {
            var session = View.sessionController;
            if (session.em.GetComponentData<BuildingGarrisonStats>(site).Capacity <= 0 || !BuildingStatus.Operational(session.em, site))
                return;
            var phase = session.em.GetComponentData<Session>(session.root).Phase;
            if (phase != Phase.Night && phase != Phase.Retreat)
                return;
            ulong id = session.em.GetComponentData<Identity>(site).Id;
            detail("召回所属士兵", () => View.commandsController.TryQueue(new RecallGarrisonRequest { Garrison = id, Cancel = false }), null);
            detail("取消途中召回", () => View.commandsController.TryQueue(new RecallGarrisonRequest { Garrison = id, Cancel = true }), null);
        }

        public void Refresh(Entity site)
        {
            var session = View.sessionController;
            var manager = session.em;
            var simulation = session.root;
            int capacity = manager.GetComponentData<BuildingGarrisonStats>(site).Capacity;
            ulong buildingId = manager.GetComponentData<Identity>(site).Id;
            var models = new List<SlotModel>(capacity);
            for (int slot = 1; slot <= capacity; slot++)
            {
                var unit = GarrisonOps.AtSlot(manager, buildingId, slot);
                if (unit == Entity.Null)
                    models.Add(new SlotModel(0, "", false));
                else
                {
                    var identity = manager.GetComponentData<Identity>(unit);
                    models.Add(new SlotModel(identity.Id, identity.Name.ToString(), EntityState.Alive(manager, unit)));
                }
            }

            gameObject.SetActive(capacity > 0);
            Bind(Adjust, capacity > 0 ? () => View.navigation.OpenPanel(GamePanelId.Garrison) : null);
            if (capacity <= 0)
            {
                signature = null;
                portraits.Clear();
                return;
            }

            int occupied = 0;
            string nextSignature = buildingId + ":" + capacity;
            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i];
                if (model.Id != 0)
                    occupied++;
                nextSignature += "/" + model.Id + ":" + model.Name + ":" + model.Alive;
            }

            Label.text = $"驻军：{occupied}/{capacity}";
            if (signature == nextSignature)
            {
                foreach (var portrait in portraits)
                    portrait.Binding.Bind(manager, simulation, portrait.Id);
                return;
            }

            signature = nextSignature;
            portraits.Clear();
            foreach (Transform child in Slots)
                if (child.gameObject != SlotTemplate.gameObject)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }

            Slots.sizeDelta = new Vector2(capacity * 90 + 8, 0);
            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i];
                int slotNumber = i + 1;
                var slot = Instantiate(SlotTemplate, Slots);
                slot.name = "驻兵槽 " + slotNumber;
                var area = (RectTransform)slot.transform;
                area.anchoredPosition = new Vector2(4 + i * 90, 0);
                slot.gameObject.SetActive(true);
                slot.Select.onClick.RemoveAllListeners();
                slot.Select.onClick.AddListener(() =>
                {
                    if (model.Id == 0)
                        View.navigation.OpenPanel(GamePanelId.Garrison);
                    else
                        View.soldierController.OpenSoldierDetails(model.Id);
                });
                slot.EmptyLabel.gameObject.SetActive(model.Id == 0);
                slot.NameLabel.gameObject.SetActive(model.Id != 0);
                slot.Portrait.gameObject.SetActive(model.Id != 0);
                if (model.Id == 0)
                    continue;
                slot.NameLabel.text = model.Name;
                portraits.Add((slot.PortraitBinding, model.Id));
                slot.PortraitBinding.Bind(manager, simulation, model.Id);
            }
        }
    }
}
