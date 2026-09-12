using System;
using System.Collections.Generic;
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

        [Sirenix.OdinInspector.LabelText("驻军文字")]
        public TMP_Text Label;
        [Sirenix.OdinInspector.LabelText("调整驻军")]
        public Button Adjust;
        [Sirenix.OdinInspector.LabelText("驻军槽位")]
        public RectTransform Slots;
        [Sirenix.OdinInspector.LabelText("驻军槽位模板")]
        public UI_GamePanel_GarrisonSlot SlotTemplate;

        readonly List<(UI_Common_PortraitImageBinding Binding, ulong Id)> portraits = new List<(UI_Common_PortraitImageBinding, ulong)>();
        string signature;

        public override void ValidateConfiguration()
        {
            ValidateReferences((Label, nameof(Label)), (Adjust, nameof(Adjust)), (Slots, nameof(Slots)),
                (SlotTemplate, nameof(SlotTemplate)));
            SlotTemplate.ValidateConfiguration();
        }

        public void Refresh(ulong buildingId, IReadOnlyList<SlotModel> models, EntityManager manager, Entity simulation,
            Action adjust, Action<ulong> select)
        {
            int capacity = models?.Count ?? 0;
            gameObject.SetActive(capacity > 0);
            Bind(Adjust, capacity > 0 ? adjust : null);
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
                slot.Select.onClick.AddListener(() => select(model.Id));
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
