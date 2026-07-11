using System.Collections.Generic;
using Landsong;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GamePanel_Inventory : MonoBehaviour
{

    private UIPanel_Game gamePanel;
    [SerializeField]
    private Button btn_关闭;

    [SerializeField]
    private RectTransform slotRoot;

    [SerializeField]
    private GameObject slotPrefab;

    [SerializeField]
    private bool refreshWhenInventoryChanges = true;

    [SerializeField]
    private bool showQuantityWhenOne;

    [SerializeField]
    private RectTransform dragRoot;

    [SerializeField]
    private RectTransform trashDropTarget;

    private readonly List<GameObject> slotObjects = new List<GameObject>();
    private InventoryService inventory;
    private bool subscribedToInventory;

    private void Reset()
    {
        slotRoot = transform as RectTransform;
    }

    private void Awake()
    {
        gamePanel = GetComponentInParent<UIPanel_Game>();
        btn_关闭.onClick.AddListener(gamePanel.Hide_Inventory);
    }
    private void OnEnable()
    {
        ResolveInventory();
        SubscribeInventory();
        RefreshInventory();
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
    }
    
    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (!enabled)
        {
            enabled = true;
        }

        RefreshFromGameSystem();
    }
   
    public void Hide()
    {
        UnsubscribeInventory();
        gameObject.SetActive(false);
    }

    public void RefreshFromGameSystem()
    {
        UnsubscribeInventory();
        ResolveInventory();

        if (isActiveAndEnabled)
        {
            SubscribeInventory();
        }

        RefreshInventory();
    }

    [ContextMenu("Refresh Inventory")]
    public void RefreshInventory()
    {
        if (inventory == null)
        {
            ResolveInventory();
        }

        if (inventory == null || slotRoot == null || slotPrefab == null)
        {
            SyncSlotCount(0);
            return;
        }

        var slots = inventory.Inventory.Slots;
        SyncSlotCount(slots.Count);

        for (var i = 0; i < slots.Count; i++)
        {
            RenderSlot(slotObjects[i], i, slots[i], inventory.ItemCatalog);
        }
    }

    public void SwapSlots(int fromSlotIndex, int toSlotIndex)
    {
        if (inventory == null)
        {
            ResolveInventory();
        }

        if (inventory == null || fromSlotIndex == toSlotIndex)
        {
            return;
        }

        inventory.Swap(fromSlotIndex, toSlotIndex);
    }

    public bool DiscardSlot(int slotIndex)
    {
        if (inventory == null)
        {
            ResolveInventory();
        }

        return inventory != null && inventory.ClearSlot(slotIndex);
    }

    public bool TryDiscardSlotAtPointer(int slotIndex, PointerEventData eventData)
    {
        if (trashDropTarget == null || eventData == null)
        {
            return false;
        }

        var eventCamera = eventData.pressEventCamera == null ? eventData.enterEventCamera : eventData.pressEventCamera;
        if (!RectTransformUtility.RectangleContainsScreenPoint(trashDropTarget, eventData.position, eventCamera))
        {
            return false;
        }

        return DiscardSlot(slotIndex);
    }

    public RectTransform GetDragRoot()
    {
        if (dragRoot != null)
        {
            return dragRoot;
        }

        var rootCanvas = GetComponentInParent<Canvas>();
        return rootCanvas == null ? transform as RectTransform : rootCanvas.transform as RectTransform;
    }

    private void ResolveInventory()
    {
        var gameSystem = GameSystem.Instance;
        if (gameSystem == null)
        {
            inventory = null;
            return;
        }

        if (gameSystem.Services.Inventory == null)
        {
            gameSystem.ReinitializeInventory();
        }

        inventory = gameSystem.Services.Inventory;
    }

    private void SyncSlotCount(int count)
    {
        while (slotObjects.Count < count)
        {
            var slotObject = Instantiate(slotPrefab, slotRoot);
            slotObject.SetActive(true);
            slotObjects.Add(slotObject);
        }

        while (slotObjects.Count > count)
        {
            var lastIndex = slotObjects.Count - 1;
            var slotObject = slotObjects[lastIndex];
            slotObjects.RemoveAt(lastIndex);

            if (slotObject != null)
            {
                var slotView = slotObject.GetComponent<GamePanel_InventorySlot>();
                if (slotView != null)
                {
                    slotView.Unbind();
                }

                if (Application.isPlaying)
                {
                    Destroy(slotObject);
                }
                else
                {
                    DestroyImmediate(slotObject);
                }
            }
        }
    }

    private void RenderSlot(GameObject slotObject, int slotIndex, InventorySlot slot, ItemCatalog catalog)
    {
        if (slotObject == null)
        {
            return;
        }

        var definition = GetItemDefinition(slot, catalog);
        var icon = GetIcon(slotObject);
        var quantityLabel = GetQuantityLabel(slotObject);
        SetIcon(icon, definition == null ? null : definition.Icon);
        SetQuantity(quantityLabel, slot);
        BindSlot(slotObject, slotIndex, slot, icon, quantityLabel);

        slotObject.name = slot == null || slot.IsEmpty
            ? "InventorySlot_Empty"
            : $"InventorySlot_{slot.ItemId}_{slot.Quantity}";
    }

    private static ItemDefinition GetItemDefinition(InventorySlot slot, ItemCatalog catalog)
    {
        if (slot == null || slot.IsEmpty || catalog == null)
        {
            return null;
        }

        return catalog.TryGetDefinition(slot.ItemId, out var definition) ? definition : null;
    }

    private static Image GetIcon(GameObject slotObject)
    {
        return slotObject.transform.GetChild(1).GetComponent<Image>();
    }

    private static TMP_Text GetQuantityLabel(GameObject slotObject)
    {
        return slotObject.transform.GetChild(2).GetComponent<TMP_Text>();
    }

    private static void SetIcon(Image icon, Sprite sprite)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    private void SetQuantity(TMP_Text quantityLabel, InventorySlot slot)
    {
        quantityLabel.text = slot == null || slot.IsEmpty || (!showQuantityWhenOne && slot.Quantity <= 1)
            ? string.Empty
            : slot.Quantity.ToString();
    }

    private void BindSlot(GameObject slotObject, int slotIndex, InventorySlot slot, Image icon, TMP_Text quantityLabel)
    {
        var slotView = slotObject.GetComponent<GamePanel_InventorySlot>();
        if (slotView == null)
        {
            slotView = slotObject.AddComponent<GamePanel_InventorySlot>();
        }

        slotView.Bind(this, slotIndex, slot, icon, quantityLabel);
    }

    private void SubscribeInventory()
    {
        if (subscribedToInventory || inventory == null || !refreshWhenInventoryChanges)
        {
            return;
        }

        inventory.InventoryChanged += HandleInventoryChanged;
        subscribedToInventory = true;
    }

    private void UnsubscribeInventory()
    {
        if (!subscribedToInventory || inventory == null)
        {
            subscribedToInventory = false;
            return;
        }

        inventory.InventoryChanged -= HandleInventoryChanged;
        subscribedToInventory = false;
    }

    private void HandleInventoryChanged(InventoryService changedInventory)
    {
        RefreshInventory();
    }
}
