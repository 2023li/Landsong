using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.CameraSystem;
using Landsong.TurnSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingEventMessageList : MonoBehaviour
    {
        [SerializeField, Required] private Transform itemRoot;
        [SerializeField, Required] private GamePanel_BuildingEventMessageItem itemPrefab;

        [FoldoutGroup("选填")]
        [SerializeField] private Transform itemPoolRoot;

        [FoldoutGroup("选填")]
        [SerializeField, Min(1)] private int maxMessages = 20;

        [FoldoutGroup("选填")]
        [SerializeField] private bool newestMessageFirst = true;

        [FoldoutGroup("选填")]
        [SerializeField] private bool createOneMessagePerStatus;

        [FoldoutGroup("选填")]
        [SerializeField] private bool focusBuildingOnMessageClick = true;

        [FoldoutGroup("选填")]
        [SerializeField] private bool showMessageBarOnMessageClick = true;

        [FoldoutGroup("选填")]
        [SerializeField] private CameraController cameraController;

        [FoldoutGroup("选填")]
        [SerializeField] private GamePanel_BuildingMessageBar messageBar;

        private readonly List<BuildingEventMessage> messages = new List<BuildingEventMessage>();
        private readonly List<GamePanel_BuildingEventMessageItem> activeItems = new List<GamePanel_BuildingEventMessageItem>();
        private readonly List<GamePanel_BuildingEventMessageItem> itemPool = new List<GamePanel_BuildingEventMessageItem>();
        private Landsong.GameSystem gameSystem;
        private BuildingService buildings;
        private TurnService turn;
        private Coroutine delayedResolveCoroutine;

        private void Reset()
        {
            itemRoot = transform;
            itemPrefab = GetComponentInChildren<GamePanel_BuildingEventMessageItem>(true);
            messageBar = GetComponentInParent<UIPanel_Game>(true)?.BuildingMessageBar;
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeTurn();
            Refresh();
            QueueDelayedResolveIfNeeded();
        }

        private void OnDisable()
        {
            StopDelayedResolve();
            UnsubscribeTurn();
        }

        public void ClearMessages()
        {
            messages.Clear();
            Refresh();
        }

        public void AddMessage(BuildingEventMessage message)
        {
            if (!message.IsValid)
            {
                return;
            }

            if (newestMessageFirst)
            {
                messages.Insert(0, message);
            }
            else
            {
                messages.Add(message);
            }

            TrimMessages();
            Refresh();
        }

        public void AddCurrentBuildingStatusMessages(int turnNumber)
        {
            ResolveReferences();
            if (buildings == null)
            {
                return;
            }

            var source = buildings.Buildings;
            for (var i = 0; i < source.Count; i++)
            {
                AddBuildingStatusMessages(source[i], turnNumber);
            }

            TrimMessages();
            Refresh();
        }

        public void Refresh()
        {
            ReleaseActiveItems();

            if (itemRoot == null || itemPrefab == null)
            {
                return;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (!message.IsValid)
                {
                    continue;
                }

                var item = GetItemFromPool();
                item.Bind(message, HandleMessageClicked);
                activeItems.Add(item);
            }
        }

        private void ResolveReferences()
        {
            gameSystem = Landsong.GameSystem.Instance;
            buildings = gameSystem == null ? null : gameSystem.Buildings;

            if (gameSystem != null && turn != gameSystem.Turn)
            {
                UnsubscribeTurn();
                turn = gameSystem.Turn;
                SubscribeTurn();
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            }

            if (messageBar == null)
            {
                messageBar = GetComponentInParent<UIPanel_Game>(true)?.BuildingMessageBar;
            }
        }

        private void AddBuildingStatusMessages(BuildingBase building, int turnNumber)
        {
            if (building == null || !building.isActiveAndEnabled || building.IsDemolishing)
            {
                return;
            }

            var statuses = BuildingStatusUIFormatter.GetRuntimeStatuses(building);
            if (!BuildingStatusUIFormatter.HasAnyStatus(statuses))
            {
                return;
            }

            if (createOneMessagePerStatus)
            {
                for (var i = 0; i < statuses.Count; i++)
                {
                    TryAddStatusMessage(building, statuses[i], turnNumber);
                }

                return;
            }

            if (TryGetPrimaryStatus(statuses, out var primaryStatus))
            {
                TryAddStatusMessage(building, primaryStatus, turnNumber);
            }
        }

        private void TryAddStatusMessage(BuildingBase building, BuildingRuntimeStatus status, int turnNumber)
        {
            if (!status.IsValid)
            {
                return;
            }

            var messageText = FormatEventMessage(building, status);
            var message = new BuildingEventMessage(building, status, messageText, turnNumber);
            if (!message.IsValid)
            {
                return;
            }

            if (newestMessageFirst)
            {
                messages.Insert(0, message);
            }
            else
            {
                messages.Add(message);
            }
        }

        private static bool TryGetPrimaryStatus(IReadOnlyList<BuildingRuntimeStatus> statuses, out BuildingRuntimeStatus status)
        {
            status = default;
            if (statuses == null)
            {
                return false;
            }

            for (var i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].IsValid && !string.IsNullOrWhiteSpace(statuses[i].EventMessage))
                {
                    status = statuses[i];
                    return true;
                }
            }

            for (var i = 0; i < statuses.Count; i++)
            {
                if (!statuses[i].IsValid)
                {
                    continue;
                }

                status = statuses[i];
                return true;
            }

            return false;
        }

        private static string FormatEventMessage(BuildingBase building, BuildingRuntimeStatus status)
        {
            if (!string.IsNullOrWhiteSpace(status.EventMessage))
            {
                return status.EventMessage;
            }

            var buildingName = BuildingStatusUIFormatter.GetBuildingName(building);
            if (string.IsNullOrWhiteSpace(buildingName))
            {
                return $"{status.DisplayName}！";
            }

            return $"{buildingName}{status.DisplayName}！";
        }

        private GamePanel_BuildingEventMessageItem GetItemFromPool()
        {
            GamePanel_BuildingEventMessageItem item;
            var lastIndex = itemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = itemPool[lastIndex];
                itemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(itemPrefab);
            }

            item.transform.SetParent(itemRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ReleaseActiveItems()
        {
            for (var i = 0; i < activeItems.Count; i++)
            {
                var item = activeItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                item.transform.SetParent(itemPoolRoot == null ? itemRoot : itemPoolRoot, false);
                itemPool.Add(item);
            }

            activeItems.Clear();
        }

        private void HandleMessageClicked(BuildingEventMessage message)
        {
            var building = message.Building;
            if (building == null)
            {
                return;
            }

            if (focusBuildingOnMessageClick)
            {
                if (cameraController == null)
                {
                    cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
                }

                cameraController?.FocusOnBuilding(building);
            }

            if (showMessageBarOnMessageClick)
            {
                if (messageBar == null)
                {
                    messageBar = GetComponentInParent<UIPanel_Game>(true)?.BuildingMessageBar;
                }

                messageBar?.ShowBuildingMessage(building);
            }
        }

        private void TrimMessages()
        {
            maxMessages = Mathf.Max(1, maxMessages);
            while (messages.Count > maxMessages)
            {
                var removeIndex = newestMessageFirst ? messages.Count - 1 : 0;
                messages.RemoveAt(removeIndex);
            }
        }

        private void SubscribeTurn()
        {
            if (turn == null)
            {
                turn = gameSystem == null ? null : gameSystem.Turn;
            }

            if (turn == null)
            {
                return;
            }

            turn.TurnAdvanced -= HandleTurnAdvanced;
            turn.TurnAdvanced += HandleTurnAdvanced;
        }

        private void UnsubscribeTurn()
        {
            if (turn == null)
            {
                return;
            }

            turn.TurnAdvanced -= HandleTurnAdvanced;
        }

        private void HandleTurnAdvanced(TurnService changedTurn, TurnAdvanceSummary summary)
        {
            turn = changedTurn;
            AddCurrentBuildingStatusMessages(summary.ToTurn);
        }

        private void QueueDelayedResolveIfNeeded()
        {
            if (gameSystem != null && buildings != null && turn != null)
            {
                return;
            }

            if (delayedResolveCoroutine == null && isActiveAndEnabled)
            {
                delayedResolveCoroutine = StartCoroutine(ResolveReferencesNextFrame());
            }
        }

        private IEnumerator ResolveReferencesNextFrame()
        {
            yield return null;
            delayedResolveCoroutine = null;

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            ResolveReferences();
            SubscribeTurn();
        }

        private void StopDelayedResolve()
        {
            if (delayedResolveCoroutine == null)
            {
                return;
            }

            StopCoroutine(delayedResolveCoroutine);
            delayedResolveCoroutine = null;
        }
    }
}
