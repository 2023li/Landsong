using System.Collections;
using Landsong.TurnSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Landsong
{
    public sealed partial class GameSystem
    {
        [SerializeField, FoldoutGroup(InspectorTurn), LabelText("起始回合"), Min(1)]
        private int startingTurn = 1;

        [SerializeField, FoldoutGroup(InspectorTurn), LabelText("允许键盘推进回合")]
        private bool allowKeyboardNextTurn = true;

        [SerializeField, FoldoutGroup(InspectorTurn), LabelText("下一回合按键")]
        private Key nextTurnKey = Key.N;

        [SerializeField, FoldoutGroup(InspectorTurn), LabelText("每帧处理建筑数"), Min(1)]
        private int turnBuildingsPerFrame = 4;

        [SerializeField, FoldoutGroup(InspectorTurn), LabelText("输出回合日志")]
        private bool logTurnResult = true;

        private Coroutine turnAdvanceCoroutine;
        private int pendingTechnologyPointsThisTurn;
        private int turnWithMissingResearchWarning = -1;

        internal int CurrentTurn => Turn == null ? startingTurn : Turn.CurrentTurn;

        [ShowInInspector, ReadOnly, FoldoutGroup(InspectorTurn), LabelText("正在推进回合")]
        internal bool IsAdvancingTurn => Turn != null && Turn.IsAdvancingTurn;

        [Button("下一回合")]
        public void NextTurn()
        {
            if (IsGameOver)
            {
                return;
            }

            if (Turn == null)
            {
                CreateTurnService();
            }

            if (Buildings == null)
            {
                CreateBuildingService();
            }

            if (turnAdvanceCoroutine != null || Turn.IsAdvancingTurn)
            {
                return;
            }

            if (Application.isPlaying && ShouldCancelNextTurnForMissingResearch())
            {
                return;
            }

            if (!Application.isPlaying)
            {
                LogTurnSummary(Turn.NextTurn(Buildings.Buildings));
                return;
            }

            turnAdvanceCoroutine = StartCoroutine(RunNextTurnRoutine(Turn));
        }

        private IEnumerator RunNextTurnRoutine(TurnService turn)
        {
            TurnAdvanceSummary summary = default;
            var completed = false;

            try
            {
                yield return turn.NextTurnRoutine(
                    Buildings == null ? null : Buildings.Buildings,
                    turnBuildingsPerFrame,
                    result =>
                    {
                        summary = result;
                        completed = true;
                    });

                if (completed)
                {
                    LogTurnSummary(summary);
                }
            }
            finally
            {
                turnAdvanceCoroutine = null;
            }
        }

        private void CreateTurnService()
        {
            if (Turn != null)
            {
                Turn.BeforeTurnAdvanced -= HandleBeforeTurnAdvanced;
                Turn.TurnAdvanced -= HandleTurnAdvanced;
                Turn.BuildingTechnologyPointsProvided -= HandleBuildingTechnologyPointsProvided;
            }

            Turn = new TurnService(startingTurn, Inventory);
            pendingTechnologyPointsThisTurn = 0;
            Turn.BeforeTurnAdvanced += HandleBeforeTurnAdvanced;
            Turn.BuildingTechnologyPointsProvided += HandleBuildingTechnologyPointsProvided;
            Turn.TurnAdvanced += HandleTurnAdvanced;
            RefreshQuestSubscriptions();
        }

        private void UpdateTurnInput()
        {
            if (!IsGameOver && allowKeyboardNextTurn && IsNextTurnKeyPressed())
            {
                NextTurn();
            }
        }

        private bool IsNextTurnKeyPressed()
        {
            return Keyboard.current != null && Keyboard.current[nextTurnKey].wasPressedThisFrame;
        }

        private void LogTurnSummary(TurnAdvanceSummary summary)
        {
            if (!logTurnResult)
            {
                return;
            }

            Debug.Log(
                $"已推进至回合 {summary.ToTurn}。已处理：{summary.OperatingConsumed}，失败：{summary.Failed}，跳过：{summary.Skipped}，自然损耗：{summary.InventoryLostQuantity}（{summary.InventorySlotsWithLoss} 格）。",
                this);
        }

        private void HandleBeforeTurnAdvanced(TurnService turn)
        {
            pendingTechnologyPointsThisTurn = 0;
        }

        private void HandleTurnAdvanced(TurnService turn, TurnAdvanceSummary summary)
        {
            ClearMissingResearchWarning();
            ApplyTechnologyResearchPoints(summary.ToTurn);
            SettleTalentsForTurn(summary.ToTurn);
            SettleInheritanceForTurn(summary.ToTurn);
            SettleExpeditionsForTurn(summary.ToTurn);

            if (!endGameWhenNoPalaceAtTurnEnd || IsGameOver)
            {
                return;
            }

            if (Dynasty == null)
            {
                CreateDynastyService();
            }

            Dynasty.Refresh();
            if (Dynasty.HasPalace)
            {
                return;
            }

            EndGame(GameOverReason.NoPalace);
        }

        public bool EndGame(GameOverReason reason)
        {
            if (IsGameOver)
            {
                return false;
            }

            IsGameOver = true;
            GameEnded?.Invoke(this, reason);
            Debug.Log($"Game over: {FormatGameOverReason(reason)}.", this);
            return true;
        }

        private static string FormatGameOverReason(GameOverReason reason)
        {
            return reason switch
            {
                GameOverReason.NoPalace => "no palace remains",
                _ => reason.ToString()
            };
        }
    }
}
