using System;
using Landsong.ExpeditionSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_ExpeditionDestinationItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text txt_名称;
        [SerializeField] private Image icon;
        //如果已经结束 则是领取奖励
        [SerializeField] private Button btn;
        [SerializeField] private GameObject go_被选中;

        //进行中设置为激活
        [SerializeField, FoldoutGroup("进行状态")] private GameObject root_进行中;
        [SerializeField, FoldoutGroup("进行状态")] private Slider sld_进度条;
        [SerializeField, FoldoutGroup("进行状态")] private TMP_Text txt_进度文本;

        //结束设置为激活
        [SerializeField,FoldoutGroup("结束状态")] private GameObject root_结束;
        [SerializeField, FoldoutGroup("结束状态")] private GameObject go_成功;
        [SerializeField, FoldoutGroup("结束状态")] private GameObject go_失败;

        private ExpeditionDestinationAvailability availability;
        private ExpeditionState expedition;
        private int currentTurn = 1;
        private Action<ExpeditionDestinationDefinition> clicked;
        private Action<ExpeditionState> claimClicked;

        private void Reset()
        {
            btn = GetComponent<Button>();
            icon = GetComponentInChildren<Image>(true);
        }

        private void OnDestroy()
        {
            if (btn != null)
            {
                btn.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(
            ExpeditionDestinationAvailability sourceAvailability,
            bool selected,
            ExpeditionState sourceExpedition,
            int sourceCurrentTurn,
            Action<ExpeditionDestinationDefinition> onClicked,
            Action<ExpeditionState> onClaimClicked)
        {
            if (btn != null)
            {
                btn.onClick.RemoveListener(HandleClicked);
            }

            availability = sourceAvailability;
            expedition = sourceExpedition;
            currentTurn = Mathf.Max(1, sourceCurrentTurn);
            clicked = onClicked;
            claimClicked = onClaimClicked;
            Refresh(selected);

            if (btn != null)
            {
                btn.interactable = availability.Destination != null;
                btn.onClick.AddListener(HandleClicked);
            }
        }

        public void Unbind()
        {
            if (btn != null)
            {
                btn.onClick.RemoveListener(HandleClicked);
                btn.interactable = false;
            }

            availability = default;
            expedition = null;
            currentTurn = 1;
            clicked = null;
            claimClicked = null;

            SetIcon(null);
            SetText(txt_名称, string.Empty);
            SetText(txt_进度文本, string.Empty);
            SetActive(go_被选中, false);
            SetActive(root_进行中, false);
            SetActive(root_结束, false);
            SetActive(go_成功, false);
            SetActive(go_失败, false);
            SetProgress(0f, false);
        }

        private void Refresh(bool selected)
        {
            var destination = availability.Destination;
            if (destination == null)
            {
                Unbind();
                return;
            }

            SetIcon(destination.Icon);
            SetText(txt_名称, destination.DisplayName);
            SetActive(go_被选中, selected);
            RefreshExpeditionState();
        }

        private void RefreshExpeditionState()
        {
            var isActive = expedition != null && expedition.IsActive;
            var isEnded = expedition != null && !expedition.IsActive;

            SetActive(root_进行中, isActive);
            SetActive(root_结束, isEnded);
            SetActive(go_成功, isEnded && expedition.IsSucceeded);
            SetActive(go_失败, isEnded && expedition.IsFailed);
            SetProgress(isActive ? CalculateProgress(expedition, currentTurn) : 0f, isActive);

            if (!isActive)
            {
                SetText(txt_进度文本, string.Empty);
                return;
            }

            var remaining = expedition.GetRemainingTurns(currentTurn);
            SetText(txt_进度文本, Landsong.Localization.L10n.Gameplay(
                "gameplay.expedition.ui.return_progress",
                "第 {0} 回合归来，剩余 {1} 回合",
                expedition.ReturnTurn,
                remaining));
        }

        private void HandleClicked()
        {
            if (expedition != null && expedition.HasPendingRewards)
            {
                claimClicked?.Invoke(expedition);
                return;
            }

            clicked?.Invoke(availability.Destination);
        }

        private static float CalculateProgress(ExpeditionState expedition, int currentTurn)
        {
            if (expedition == null)
            {
                return 0f;
            }

            var total = Mathf.Max(1, expedition.ReturnTurn - expedition.StartedTurn);
            var elapsed = Mathf.Clamp(Mathf.Max(1, currentTurn) - expedition.StartedTurn, 0, total);
            return Mathf.Clamp01(elapsed / (float)total);
        }

        private void SetIcon(Sprite sprite)
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        private void SetProgress(float value, bool visible)
        {
            if (sld_进度条 == null)
            {
                return;
            }

            sld_进度条.gameObject.SetActive(visible);
            sld_进度条.minValue = 0f;
            sld_进度条.maxValue = 1f;
            sld_进度条.value = Mathf.Clamp01(value);
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
