using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_英雄选择Item : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("英雄肖像")]
        public Image Portrait;
        [Sirenix.OdinInspector.LabelText("肖像绑定")]
        public UI_Common_PortraitImageBinding PortraitBinding;
        [Sirenix.OdinInspector.LabelText("已选中状态")]
        public GameObject SelectedState;

        public ulong HeroId { get; private set; }

        public void ValidateConfiguration()
        {
            if (Select == null || Portrait == null || PortraitBinding == null || SelectedState == null)
                throw new InvalidOperationException(name + " 的英雄选择 Item 检查器引用不完整。");
            PortraitBinding.ValidateConfiguration();
        }

        public void Bind(EntityManager entityManager, Entity root, ulong heroId, bool selected, bool interactable, Action<ulong> select)
        {
            HeroId = heroId;
            SelectedState.SetActive(selected);
            Select.onClick.RemoveAllListeners();
            Select.interactable = interactable;
            Select.onClick.AddListener(() => select(heroId));
            Portrait.raycastTarget = false;
            PortraitBinding.Bind(entityManager, root, heroId);
        }

        public void Release()
        {
            HeroId = 0;
            Select.onClick.RemoveAllListeners();
            Select.interactable = false;
            PortraitBinding.Unbind();
            SelectedState.SetActive(false);
        }
    }
}
