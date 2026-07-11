using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.CameraSystem;
using Landsong.GameEventSystem;
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
        [SerializeField] private bool focusBuildingOnMessageClick = true;

        [FoldoutGroup("选填")]
        [SerializeField] private CameraController cameraController;

        [FoldoutGroup("选填")]
        [SerializeField] private Popup_GameEventMessage eventMessagePopup;

        private readonly List<GamePanel_BuildingEventMessageItem> activeItems = new List<GamePanel_BuildingEventMessageItem>();
        private readonly List<GamePanel_BuildingEventMessageItem> itemPool = new List<GamePanel_BuildingEventMessageItem>();
        private Landsong.GameSystem gameSystem;
        private GameEventService gameEvents;
        private Coroutine delayedResolveCoroutine;

        public event Action<GamePanel_BuildingEventMessageList, GameEventMessage> MessageClicked;
        public event Action<GamePanel_BuildingEventMessageList, GameEventMessage> MessageDeleted;

        private void Reset()
        {
            itemRoot = transform;
            itemPrefab = GetComponentInChildren<GamePanel_BuildingEventMessageItem>(true);
            eventMessagePopup = GetComponentInChildren<Popup_GameEventMessage>(true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeGameEvents();
            Refresh();
            QueueDelayedResolveIfNeeded();
        }

        private void OnDisable()
        {
            StopDelayedResolve();
            UnsubscribeGameEvents();
        }

        public void ClearMessages()
        {
            ResolveReferences();
            if (gameEvents == null)
            {
                Refresh();
                return;
            }

            gameEvents.ClearMessages();
        }

        public void AddMessage(GameEventMessage message)
        {
            if (!message.IsValid)
            {
                return;
            }

            ResolveReferences();
            gameEvents?.AddMessage(message);
        }

        public bool RemoveMessage(GameEventMessage message)
        {
            ResolveReferences();
            return gameEvents != null && gameEvents.RemoveMessage(message);
        }

        public void AddGameMessage(
            string eventTypeId,
            string message,
            int turnNumber,
            Action<GameEventMessage> onClicked = null)
        {
            AddMessage(GameEventMessage.ForGame(eventTypeId, message, turnNumber, onClicked));
        }

        public void Refresh()
        {
            ReleaseActiveItems();

            if (itemRoot == null || itemPrefab == null || gameEvents == null)
            {
                return;
            }

            var messages = gameEvents.Messages;
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (!message.IsValid)
                {
                    continue;
                }

                var item = GetItemFromPool();
                item.Bind(message, HandleMessageClicked, HandleMessageDeleted);
                activeItems.Add(item);
            }
        }

        private void ResolveReferences()
        {
            gameSystem = Landsong.GameSystem.Instance;

            var resolvedEvents = gameSystem == null ? null : gameSystem.Services.Events;
            if (resolvedEvents != gameEvents)
            {
                UnsubscribeGameEvents();
                gameEvents = resolvedEvents;
                SubscribeGameEvents();
            }

            gameEvents?.Configure(maxMessages, newestMessageFirst);

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            }

            ResolveEventMessagePopup();
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

        private void HandleMessageClicked(GameEventMessage message)
        {
            if (!message.IsValid)
            {
                return;
            }

            MessageClicked?.Invoke(this, message);

            if (message.IsBuildingEvent)
            {
                HandleBuildingEventMessageClicked(message);
            }

            message.Clicked?.Invoke(message);
            if (!message.SuppressDefaultPopup)
            {
                ShowMessagePopup(message);
            }
        }

        private void HandleMessageDeleted(GameEventMessage message)
        {
            if (!RemoveMessage(message))
            {
                return;
            }

            MessageDeleted?.Invoke(this, message);
        }

        private void HandleBuildingEventMessageClicked(GameEventMessage message)
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
        }

        private void ShowMessagePopup(GameEventMessage message)
        {
            ResolveEventMessagePopup();
            if (eventMessagePopup == null)
            {
                Debug.LogWarning(
                    $"{nameof(GamePanel_BuildingEventMessageList)} has no {nameof(Popup_GameEventMessage)} assigned.",
                    this);
                return;
            }

            eventMessagePopup.Show(message, HandleMessagePopupConfirmed);
        }

        private void HandleMessagePopupConfirmed(GameEventMessage message)
        {
            HandleMessageDeleted(message);
        }

        private void ResolveEventMessagePopup()
        {
            if (eventMessagePopup != null)
            {
                return;
            }

            eventMessagePopup = GetComponentInChildren<Popup_GameEventMessage>(true);
            if (eventMessagePopup != null)
            {
                return;
            }

            var gamePanel = GetComponentInParent<UIPanel_Game>();
            if (gamePanel != null)
            {
                eventMessagePopup = gamePanel.GetComponentInChildren<Popup_GameEventMessage>(true);
            }
        }

        private void SubscribeGameEvents()
        {
            if (gameEvents == null)
            {
                gameEvents = gameSystem == null ? null : gameSystem.Services.Events;
            }

            if (gameEvents == null)
            {
                return;
            }

            gameEvents.MessagesChanged -= HandleGameEventsChanged;
            gameEvents.MessagesChanged += HandleGameEventsChanged;
        }

        private void UnsubscribeGameEvents()
        {
            if (gameEvents == null)
            {
                return;
            }

            gameEvents.MessagesChanged -= HandleGameEventsChanged;
        }

        private void HandleGameEventsChanged(GameEventService changedEvents)
        {
            gameEvents = changedEvents;
            Refresh();
        }

        private void QueueDelayedResolveIfNeeded()
        {
            if (gameSystem != null && gameEvents != null)
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
            SubscribeGameEvents();
            Refresh();
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
