using System;
using Landsong.ExpeditionSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_ExpeditionStateItem : MonoBehaviour
    {
        [SerializeField, LabelText("图标")] private Image icon;
        [SerializeField, LabelText("名称文本")] private TMP_Text titleLabel;
        [SerializeField, LabelText("状态文本")] private TMP_Text statusLabel;
        [SerializeField, LabelText("回合文本")] private TMP_Text turnLabel;
        [SerializeField, LabelText("人口文本")] private TMP_Text populationLabel;
        [SerializeField, LabelText("成功率文本")] private TMP_Text chanceLabel;
        [SerializeField, LabelText("奖励文本")] private TMP_Text rewardLabel;
        [SerializeField, LabelText("补贴文本")] private TMP_Text subsidyLabel;
        [SerializeField, LabelText("领取按钮")] private Button claimButton;
        [SerializeField, LabelText("领取按钮文本")] private TMP_Text claimButtonLabel;
        [SerializeField, LabelText("进行中根节点")] private GameObject activeRoot;
        [SerializeField, LabelText("成功根节点")] private GameObject succeededRoot;
        [SerializeField, LabelText("失败根节点")] private GameObject failedRoot;
        [SerializeField, LabelText("待领取根节点")] private GameObject pendingRewardRoot;

        private ExpeditionState expedition;
        private Action<ExpeditionState> claimClicked;

        private void Reset()
        {
            icon = GetComponentInChildren<Image>(true);
            claimButton = GetComponentInChildren<Button>(true);
        }

        private void OnDestroy()
        {
            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(HandleClaimClicked);
            }
        }

        public void Bind(ExpeditionState sourceExpedition, GameSystem gameSystem, Action<ExpeditionState> onClaimClicked)
        {
            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(HandleClaimClicked);
            }

            expedition = sourceExpedition;
            claimClicked = onClaimClicked;
            Refresh(gameSystem);

            if (claimButton != null)
            {
                claimButton.onClick.AddListener(HandleClaimClicked);
            }
        }

        public void Unbind()
        {
            if (claimButton != null)
            {
                claimButton.onClick.RemoveListener(HandleClaimClicked);
                claimButton.interactable = false;
            }

            expedition = null;
            claimClicked = null;
            SetIcon(null);
            SetText(titleLabel, string.Empty);
            SetText(statusLabel, string.Empty);
            SetText(turnLabel, string.Empty);
            SetText(populationLabel, string.Empty);
            SetText(chanceLabel, string.Empty);
            SetText(rewardLabel, string.Empty);
            SetText(subsidyLabel, string.Empty);
            SetActive(activeRoot, false);
            SetActive(succeededRoot, false);
            SetActive(failedRoot, false);
            SetActive(pendingRewardRoot, false);
        }

        private void Refresh(GameSystem gameSystem)
        {
            if (expedition == null)
            {
                Unbind();
                return;
            }

            var destination = expedition.Definition;
            SetIcon(destination == null ? null : destination.Icon);
            SetText(titleLabel, destination == null ? expedition.DestinationId : destination.DisplayName);
            SetText(statusLabel, FormatStatus(expedition));
            SetText(turnLabel, FormatTurnText(expedition, gameSystem));
            SetText(populationLabel, $"人口 {expedition.AssignedPopulation}");
            SetText(chanceLabel, $"成功率 {expedition.SuccessChance * 100f:0.#}%");
            SetText(rewardLabel, FormatRewardText(expedition));
            SetText(subsidyLabel, FormatSubsidyText(expedition));
            SetActive(activeRoot, expedition.IsActive);
            SetActive(succeededRoot, expedition.IsSucceeded);
            SetActive(failedRoot, expedition.IsFailed);
            SetActive(pendingRewardRoot, expedition.HasPendingRewards);

            if (claimButton != null)
            {
                claimButton.gameObject.SetActive(expedition.HasPendingRewards);
                claimButton.interactable = expedition.HasPendingRewards;
            }

            SetText(claimButtonLabel, expedition.HasPendingRewards ? "领取" : string.Empty);
        }

        private void HandleClaimClicked()
        {
            claimClicked?.Invoke(expedition);
        }

        private static string FormatStatus(ExpeditionState expedition)
        {
            if (expedition == null)
            {
                return string.Empty;
            }

            if (expedition.IsActive)
            {
                return "远征中";
            }

            if (expedition.IsSucceeded)
            {
                return expedition.HasPendingRewards ? "成功，奖励待领取" : "成功";
            }

            return "失败";
        }

        private static string FormatTurnText(ExpeditionState expedition, GameSystem gameSystem)
        {
            if (expedition == null)
            {
                return string.Empty;
            }

            if (expedition.IsActive)
            {
                var currentTurn = gameSystem == null ? expedition.StartedTurn : gameSystem.CurrentTurn;
                return $"第 {expedition.ReturnTurn} 回合归来，剩余 {expedition.GetRemainingTurns(currentTurn)} 回合";
            }

            return $"第 {expedition.StartedTurn}-{expedition.ReturnTurn} 回合";
        }

        private static string FormatRewardText(ExpeditionState expedition)
        {
            if (expedition == null || expedition.Definition == null || !expedition.Definition.HasRewards)
            {
                return string.Empty;
            }

            return expedition.RewardsClaimed ? "奖励已领取" : "奖励未领取";
        }

        private static string FormatSubsidyText(ExpeditionState expedition)
        {
            if (expedition == null || !expedition.IsFailed || expedition.FailureSubsidyRequired <= 0)
            {
                return string.Empty;
            }

            return expedition.FailureSubsidyMissing > 0
                ? $"补贴 {expedition.FailureSubsidyPaid}/{expedition.FailureSubsidyRequired}，缺口 {expedition.FailureSubsidyMissing}"
                : $"补贴已支付 {expedition.FailureSubsidyPaid}";
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
