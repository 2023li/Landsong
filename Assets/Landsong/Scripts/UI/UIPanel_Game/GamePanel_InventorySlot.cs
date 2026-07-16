using Landsong.InventorySystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class GamePanel_InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField, Min(1f)]
    private float iconHoverScale = 1.08f;

    [Header("Visual References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemQuantityLabel;
    [SerializeField] private Image presentationOverlay;
    [SerializeField] private TMP_Text providerLabel;

    private GamePanel_Inventory panel;
    private InventorySlot slot;
    private Image icon;
    private TMP_Text quantityLabel;
    private CanvasGroup canvasGroup;
    private RectTransform iconRectTransform;
    private RectTransform dragIconRectTransform;
    private GameObject dragIconObject;
    private Vector3 iconDefaultScale = Vector3.one;
    private int slotIndex = -1;
    private bool pointerHeld;
    private bool dragging;
    private bool capturedDefaultVisuals;
    private Sprite defaultBackgroundSprite;
    private Color defaultBackgroundColor = Color.white;
    private Color defaultIconColor = Color.white;

    public int SlotIndex => slotIndex;
    public bool HasItem => slot != null && !slot.IsEmpty;

    private void Awake()
    {
        EnsureCanvasGroup();
        ResolveVisualReferences(out _, out _);
        CaptureDefaultVisuals();
    }

    public void ResolveVisualReferences(out Image resolvedIcon, out TMP_Text resolvedQuantityLabel)
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null && transform.childCount > 0)
            {
                backgroundImage = transform.GetChild(0).GetComponent<Image>();
            }
        }

        if (itemIcon == null && transform.childCount > 1)
        {
            itemIcon = transform.GetChild(1).GetComponent<Image>();
        }

        if (itemQuantityLabel == null && transform.childCount > 2)
        {
            itemQuantityLabel = transform.GetChild(2).GetComponent<TMP_Text>();
        }

        resolvedIcon = itemIcon;
        resolvedQuantityLabel = itemQuantityLabel;
    }

    public void ApplySlotTypePresentation(InventorySlot inventorySlot)
    {
        ResolveVisualReferences(out _, out _);
        CaptureDefaultVisuals();

        var slotType = inventorySlot == null
            ? InventorySlotType.Default
            : inventorySlot.SlotType;
        ResolveSlotTypeColors(slotType, out var backgroundTint, out var contentTint, out var overlayTint);

        if (backgroundImage != null)
        {
            backgroundImage.sprite = defaultBackgroundSprite;
            backgroundImage.color = backgroundTint;
        }

        if (itemIcon != null)
        {
            itemIcon.color = contentTint;
        }

        if (itemQuantityLabel != null)
        {
            itemQuantityLabel.color = contentTint;
        }

        if (presentationOverlay != null)
        {
            presentationOverlay.color = overlayTint;
            presentationOverlay.enabled = presentationOverlay.sprite != null;
        }

        if (providerLabel != null)
        {
            providerLabel.text = inventorySlot == null
                ? string.Empty
                : inventorySlot.ProviderDisplayName;
        }
    }

    public void Bind(GamePanel_Inventory owner, int index, InventorySlot inventorySlot, Image slotIcon, TMP_Text slotQuantityLabel)
    {
        panel = owner;
        slotIndex = index;
        slot = inventorySlot;
        icon = slotIcon;
        quantityLabel = slotQuantityLabel;
        iconRectTransform = icon == null ? null : icon.rectTransform;
        iconDefaultScale = iconRectTransform == null ? Vector3.one : iconRectTransform.localScale;
        pointerHeld = false;
        dragging = false;

        EnsureCanvasGroup();
        canvasGroup.blocksRaycasts = true;
        SetIconHighlighted(false);
    }

    public void Unbind()
    {
        DestroyDragIcon();

        panel = null;
        slotIndex = -1;
        slot = null;
        icon = null;
        quantityLabel = null;
        iconRectTransform = null;
        pointerHeld = false;
        dragging = false;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetIconHighlighted(HasItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pointerHeld && !dragging)
        {
            SetIconHighlighted(false);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerHeld = true;
        SetIconHighlighted(HasItem);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerHeld = false;

        if (!dragging)
        {
            SetIconHighlighted(false);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasItem || panel == null || icon == null || icon.sprite == null)
        {
            eventData.pointerDrag = null;
            return;
        }

        dragging = true;
        pointerHeld = false;
        EnsureCanvasGroup();
        canvasGroup.blocksRaycasts = false;
        SetIconHighlighted(false);
        CreateDragIcon();
        UpdateDragIconPosition(eventData);

        icon.enabled = false;
        if (quantityLabel != null)
        {
            quantityLabel.enabled = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var hadDragIcon = dragIconObject != null;
        if (!dragging && !hadDragIcon)
        {
            return;
        }

        dragging = false;
        pointerHeld = false;
        EnsureCanvasGroup();
        canvasGroup.blocksRaycasts = true;
        DestroyDragIcon();

        if (panel == null || !panel.TryDiscardSlotAtPointer(slotIndex, eventData))
        {
            RestoreSlotVisuals();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        var sourceSlot = eventData.pointerDrag == null
            ? null
            : eventData.pointerDrag.GetComponent<GamePanel_InventorySlot>();

        if (sourceSlot == null || sourceSlot == this || panel == null)
        {
            return;
        }

        panel.SwapSlots(sourceSlot.SlotIndex, slotIndex);
    }

    public bool Discard()
    {
        return panel != null && panel.DiscardSlot(slotIndex);
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void CaptureDefaultVisuals()
    {
        if (capturedDefaultVisuals)
        {
            return;
        }

        defaultBackgroundSprite = backgroundImage == null ? null : backgroundImage.sprite;
        defaultBackgroundColor = backgroundImage == null ? Color.white : backgroundImage.color;
        defaultIconColor = itemIcon == null ? Color.white : itemIcon.color;
        capturedDefaultVisuals = true;
    }

    private void ResolveSlotTypeColors(
        InventorySlotType slotType,
        out Color backgroundTint,
        out Color contentTint,
        out Color overlayTint)
    {
        contentTint = defaultIconColor;
        overlayTint = Color.white;
        switch (slotType)
        {
            case InventorySlotType.Palace:
            case InventorySlotType.PalaceImproved:
            case InventorySlotType.PalaceAdvanced:
            case InventorySlotType.PalaceRoyal:
                backgroundTint = new Color(0.72f, 0.53f, 0.22f, 1f);
                overlayTint = new Color(1f, 0.86f, 0.38f, 1f);
                break;
            case InventorySlotType.BasicWarehouse:
                backgroundTint = new Color(0.45f, 0.31f, 0.20f, 1f);
                overlayTint = new Color(0.75f, 0.55f, 0.32f, 1f);
                break;
            case InventorySlotType.Warehouse:
                backgroundTint = new Color(0.35f, 0.43f, 0.48f, 1f);
                overlayTint = new Color(0.70f, 0.78f, 0.82f, 1f);
                break;
            case InventorySlotType.AdvancedWarehouse:
                backgroundTint = new Color(0.20f, 0.34f, 0.52f, 1f);
                overlayTint = new Color(0.96f, 0.76f, 0.30f, 1f);
                break;
            case InventorySlotType.ColdStorage:
                backgroundTint = new Color(0.48f, 0.78f, 0.94f, 1f);
                contentTint = new Color(0.92f, 0.98f, 1f, 1f);
                overlayTint = new Color(0.78f, 0.95f, 1f, 1f);
                break;
            case InventorySlotType.Cellar:
                backgroundTint = new Color(0.30f, 0.23f, 0.18f, 1f);
                break;
            case InventorySlotType.Granary:
                backgroundTint = new Color(0.66f, 0.52f, 0.25f, 1f);
                break;
            case InventorySlotType.Armory:
                backgroundTint = new Color(0.31f, 0.34f, 0.38f, 1f);
                break;
            default:
                backgroundTint = defaultBackgroundColor;
                break;
        }
    }

    private void SetIconHighlighted(bool highlighted)
    {
        if (iconRectTransform == null)
        {
            return;
        }

        iconRectTransform.localScale = highlighted ? iconDefaultScale * iconHoverScale : iconDefaultScale;
    }

    private void CreateDragIcon()
    {
        DestroyDragIcon();

        var dragRoot = panel == null ? null : panel.GetDragRoot();
        if (dragRoot == null || icon == null)
        {
            return;
        }

        dragIconObject = new GameObject("InventorySlot_DragIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragIconObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        dragIconObject.transform.SetParent(dragRoot, false);
        dragIconObject.transform.SetAsLastSibling();

        dragIconRectTransform = dragIconObject.GetComponent<RectTransform>();
        dragIconRectTransform.sizeDelta = icon.rectTransform.rect.size;
        dragIconRectTransform.pivot = new Vector2(0.5f, 0.5f);

        var dragImage = dragIconObject.GetComponent<Image>();
        dragImage.sprite = icon.sprite;
        dragImage.color = icon.color;
        dragImage.material = icon.material;
        dragImage.preserveAspect = icon.preserveAspect;
        dragImage.raycastTarget = false;
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (dragIconRectTransform == null)
        {
            return;
        }

        var dragRoot = dragIconRectTransform.parent as RectTransform;
        if (dragRoot == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragRoot,
            eventData.position,
            eventData.pressEventCamera,
            out var localPoint);

        dragIconRectTransform.anchoredPosition = localPoint;
    }

    private void DestroyDragIcon()
    {
        if (dragIconObject == null)
        {
            return;
        }

        ClearEditorSelectionIfNeeded(dragIconObject);
        Destroy(dragIconObject);
        dragIconObject = null;
        dragIconRectTransform = null;
    }

    private static void ClearEditorSelectionIfNeeded(GameObject target)
    {
#if UNITY_EDITOR
        var activeObject = Selection.activeObject;
        var activeComponent = activeObject as Component;
        if (activeObject == target || activeComponent != null && activeComponent.gameObject == target)
        {
            Selection.activeObject = null;
        }
#endif
    }

    private void RestoreSlotVisuals()
    {
        if (icon != null)
        {
            icon.enabled = icon.sprite != null;
        }

        if (quantityLabel != null)
        {
            quantityLabel.enabled = true;
        }
    }
}
