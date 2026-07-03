using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Popup_BuildingDetails : MonoBehaviour
{
    [SerializeField] private GameObject go_补贴栏标题;
    [SerializeField] private GameObject go_补贴栏内容;
    [SerializeField] private Slider sld_补贴滑动条;
    [SerializeField] private Toggle tgl_自动补贴满岗位;
    [SerializeField] private TMP_Text txt_目标稳定工人;
    [SerializeField] private TMP_Text txt_当前补贴金币;
    [SerializeField] private Button btn_一键补满工人;
    [SerializeField] private TMP_Text txt_一键补满消耗;
    [SerializeField] private GameObject go_工人详情触发区;
    [SerializeField] private GameObject go_详情侧边栏;
    [SerializeField] private RectTransform root_详情侧边栏内容;
    [SerializeField] private GameObject prefab_详情侧边栏文本;
    [SerializeField, Min(0.1f)] private float mobileLongPressSeconds = 0.6f;

    [SerializeField] private TMP_Text txt_建筑名称;
    [SerializeField] private TMP_Text txt_建筑描述;
    [SerializeField] private Image img_建筑图标;
    [SerializeField] private Button btn_关闭弹窗;

    [SerializeField] private RectTransform root_内容栏;
    [SerializeField] private GameObject prefab_标题;
    [SerializeField] private GameObject prefab_内容文本;

    private readonly List<GameObject> activeContentItems = new List<GameObject>();
    private readonly List<GameObject> activeSidebarItems = new List<GameObject>();
    private BuildingBase building;
    private IBuildingWorkforceFundingSource workforceFundingSource;
    private Coroutine workerDetailLongPressCoroutine;
    private DetailSidebarContentKind activeSidebarContentKind;
    private bool suppressWorkforceControlCallback;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        if (btn_关闭弹窗 != null)
        {
            btn_关闭弹窗.onClick.AddListener(Hide);
        }

        if (sld_补贴滑动条 != null)
        {
            sld_补贴滑动条.wholeNumbers = true;
            sld_补贴滑动条.minValue = 0f;
            sld_补贴滑动条.value = 0f;
            sld_补贴滑动条.onValueChanged.AddListener(HandleTargetStableWorkersChanged);
        }

        if (tgl_自动补贴满岗位 != null)
        {
            tgl_自动补贴满岗位.onValueChanged.AddListener(HandleAutoSubsidyChanged);
        }

        if (btn_一键补满工人 != null)
        {
            btn_一键补满工人.onClick.AddListener(HandleRecruitToFullClicked);
        }

        BindWorkerDetailTrigger();
        ClearContentRoot();
        ClearDetailSidebarRoot();
        Hide();
    }

    private void OnDestroy()
    {
        UnsubscribeBuilding();

        if (btn_关闭弹窗 != null)
        {
            btn_关闭弹窗.onClick.RemoveListener(Hide);
        }

        if (sld_补贴滑动条 != null)
        {
            sld_补贴滑动条.onValueChanged.RemoveListener(HandleTargetStableWorkersChanged);
        }

        if (tgl_自动补贴满岗位 != null)
        {
            tgl_自动补贴满岗位.onValueChanged.RemoveListener(HandleAutoSubsidyChanged);
        }

        if (btn_一键补满工人 != null)
        {
            btn_一键补满工人.onClick.RemoveListener(HandleRecruitToFullClicked);
        }

        StopWorkerDetailLongPress();
    }

    public void ShowBuilding(BuildingBase targetBuilding)
    {
        if (targetBuilding == null)
        {
            Hide();
            return;
        }

        if (building != targetBuilding)
        {
            UnsubscribeBuilding();
            building = targetBuilding;
            building.StateChanged += HandleBuildingStateChanged;
        }

        Refresh();
        SetActive(gameObject, true);
    }

    public void Hide()
    {
        UnsubscribeBuilding();
        building = null;
        workforceFundingSource = null;
        SetText(txt_建筑名称, string.Empty);
        SetText(txt_建筑描述, string.Empty);
        SetIcon(null);
        SetSubsidyVisible(false);
        HideDetailSidebar();
        ClearContentRoot();
        SetActive(gameObject, false);
    }

    private void Refresh()
    {
        if (building == null)
        {
            return;
        }

        BuildingStatusDisplayData displayData = BuildingStatusUIFormatter.CreateDisplayData(building);
        SetText(txt_建筑名称, displayData.BuildingName);
        SetText(txt_建筑描述, BuildDescriptionText(building, displayData));
        SetIcon(building.Definition == null ? null : building.Definition.Icon);
        RefreshWorkforceFunding(building);
        ClearContentRoot();
    }

    private void HandleBuildingStateChanged(BuildingBase changedBuilding)
    {
        if (changedBuilding == building && IsVisible)
        {
            Refresh();
        }
    }

    private void RefreshWorkforceFunding(BuildingBase targetBuilding)
    {
        workforceFundingSource = targetBuilding as IBuildingWorkforceFundingSource;
        if (workforceFundingSource == null)
        {
            SetSubsidyVisible(false);
            HideDetailSidebar();
            return;
        }

        SetSubsidyVisible(true);
        suppressWorkforceControlCallback = true;

        if (tgl_自动补贴满岗位 != null)
        {
            tgl_自动补贴满岗位.isOn = workforceFundingSource.AutoFullWorkerSubsidyEnabled;
        }

        if (sld_补贴滑动条 != null)
        {
            var naturalStableWorkers = BuildingJobSystem.CalculateStableWorkers(
                workforceFundingSource.MaxWorkers,
                workforceFundingSource.JobAttractionWithoutSubsidy);
            sld_补贴滑动条.wholeNumbers = true;
            sld_补贴滑动条.minValue = naturalStableWorkers;
            sld_补贴滑动条.maxValue = workforceFundingSource.MaxWorkers;
            sld_补贴滑动条.value = Mathf.Clamp(
                workforceFundingSource.TargetStableWorkers,
                naturalStableWorkers,
                workforceFundingSource.MaxWorkers);
            sld_补贴滑动条.interactable = !workforceFundingSource.AutoFullWorkerSubsidyEnabled;
        }

        suppressWorkforceControlCallback = false;

        SetText(
            txt_目标稳定工人,
            $"{workforceFundingSource.TargetStableWorkers}/{workforceFundingSource.MaxWorkers}");
        SetText(
            txt_当前补贴金币,
            $"金币/回合 {workforceFundingSource.TargetSubsidyGoldPerTurn}");
        RefreshRecruitToFullControl();
        if (activeSidebarContentKind == DetailSidebarContentKind.Workforce)
        {
            ShowWorkerDetailSidebar();
        }
    }

    private void RefreshRecruitToFullControl()
    {
        if (workforceFundingSource == null)
        {
            SetText(txt_一键补满消耗, string.Empty);
            if (btn_一键补满工人 != null)
            {
                btn_一键补满工人.interactable = false;
            }

            return;
        }

        var missingWorkers = workforceFundingSource.MissingWorkersToFull;
        var recruitWorkers = workforceFundingSource.RecruitToFullWorkerCount;
        var recruitCost = workforceFundingSource.RecruitToFullCost;

        if (btn_一键补满工人 != null)
        {
            btn_一键补满工人.interactable = missingWorkers > 0;
        }

        if (missingWorkers <= 0)
        {
            SetText(txt_一键补满消耗, "金币 x0");
            return;
        }

        SetText(
            txt_一键补满消耗,
            recruitWorkers < missingWorkers
                ? $"最多 {recruitWorkers} 人，金币 x{recruitCost}"
                : $"需要 金币 x{recruitCost}");
    }

    private void HandleAutoSubsidyChanged(bool enabled)
    {
        if (suppressWorkforceControlCallback || workforceFundingSource == null)
        {
            return;
        }

        workforceFundingSource.SetAutoFullWorkerSubsidyEnabled(enabled);
        Refresh();
    }

    private void HandleTargetStableWorkersChanged(float value)
    {
        if (suppressWorkforceControlCallback || workforceFundingSource == null)
        {
            return;
        }

        workforceFundingSource.SetTargetStableWorkers(Mathf.RoundToInt(value));
        Refresh();
    }

    private void HandleRecruitToFullClicked()
    {
        if (workforceFundingSource == null)
        {
            return;
        }

        workforceFundingSource.TryRecruitToFull();
        Refresh();
    }

    private void UnsubscribeBuilding()
    {
        if (building != null)
        {
            building.StateChanged -= HandleBuildingStateChanged;
        }
    }

    private void BindWorkerDetailTrigger()
    {
        var triggerObject = go_工人详情触发区 != null
            ? go_工人详情触发区
            : (txt_建筑描述 == null ? null : txt_建筑描述.gameObject);
        if (triggerObject == null)
        {
            return;
        }

        EnsureWorkerDetailTriggerRaycastTarget(triggerObject);

        var eventTrigger = triggerObject.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = triggerObject.AddComponent<EventTrigger>();
        }

        AddEventTrigger(eventTrigger, EventTriggerType.PointerEnter, HandleWorkerDetailPointerEnter);
        AddEventTrigger(eventTrigger, EventTriggerType.PointerExit, _ => HandleWorkerDetailPointerExit());
        AddEventTrigger(eventTrigger, EventTriggerType.PointerDown, _ => HandleWorkerDetailPointerDown());
        AddEventTrigger(eventTrigger, EventTriggerType.PointerUp, _ => HandleWorkerDetailPointerUp());
    }

    private static void AddEventTrigger(
        EventTrigger eventTrigger,
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        if (eventTrigger == null || callback == null)
        {
            return;
        }

        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(callback);
        eventTrigger.triggers.Add(entry);
    }

    private static void EnsureWorkerDetailTriggerRaycastTarget(GameObject triggerObject)
    {
        if (triggerObject == null || HasRaycastableGraphic(triggerObject))
        {
            return;
        }

        if (triggerObject.GetComponent<RectTransform>() == null)
        {
            return;
        }

        var image = triggerObject.GetComponent<Image>();
        if (image == null)
        {
            image = triggerObject.AddComponent<Image>();
        }

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
    }

    private static bool HasRaycastableGraphic(GameObject target)
    {
        var graphics = target.GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && graphics[i].raycastTarget)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleWorkerDetailPointerEnter(BaseEventData eventData)
    {
        if (IsTouchPointer(eventData))
        {
            return;
        }

        if (workforceFundingSource == null)
        {
            return;
        }

        StopWorkerDetailLongPress();
        ShowWorkerDetailSidebar();
    }

    private static bool IsTouchPointer(BaseEventData eventData)
    {
        var pointerEventData = eventData as PointerEventData;
        return pointerEventData != null
               && pointerEventData.pointerId >= 0
               && (Application.isMobilePlatform || Input.touchCount > 0);
    }

    private void HandleWorkerDetailPointerExit()
    {
        StopWorkerDetailLongPress();
        HideDetailSidebar();
    }

    private void HandleWorkerDetailPointerDown()
    {
        StopWorkerDetailLongPress();
        if (workforceFundingSource == null || !isActiveAndEnabled)
        {
            return;
        }

        workerDetailLongPressCoroutine = StartCoroutine(WorkerDetailLongPressRoutine());
    }

    private void HandleWorkerDetailPointerUp()
    {
        StopWorkerDetailLongPress();
    }

    private IEnumerator WorkerDetailLongPressRoutine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, mobileLongPressSeconds));
        workerDetailLongPressCoroutine = null;

        if (workforceFundingSource == null)
        {
            yield break;
        }

        ShowWorkerDetailSidebar();
    }

    private void StopWorkerDetailLongPress()
    {
        if (workerDetailLongPressCoroutine == null)
        {
            return;
        }

        StopCoroutine(workerDetailLongPressCoroutine);
        workerDetailLongPressCoroutine = null;
    }

    private void ShowWorkerDetailSidebar()
    {
        if (workforceFundingSource == null)
        {
            return;
        }

        activeSidebarContentKind = DetailSidebarContentKind.Workforce;
        RebuildDetailSidebar(BuildWorkforceSidebarRows(workforceFundingSource));
        SetDetailSidebarVisible(true);
    }

    private void HideDetailSidebar()
    {
        activeSidebarContentKind = DetailSidebarContentKind.None;
        SetDetailSidebarVisible(false);
        ClearDetailSidebarRoot();
    }

    private List<DetailSidebarRow> BuildWorkforceSidebarRows(IBuildingWorkforceFundingSource source)
    {
        var rows = new List<DetailSidebarRow>
        {
            new DetailSidebarRow("满岗位需要的最少就业吸引力", FormatNumber(source.FullWorkerRequiredAttraction)),
            new DetailSidebarRow("当前就业吸引力", FormatNumber(source.JobAttraction)),
            new DetailSidebarRow("满岗位吸引力差值", FormatNumber(source.JobAttractionGapToFullWorkers)),
            new DetailSidebarRow("就业吸引力影响因素", string.Empty)
        };

        IReadOnlyList<BuildingWorkforceAttractionFactor> factors = source.WorkforceAttractionFactors;
        if (factors == null || factors.Count == 0)
        {
            rows.Add(new DetailSidebarRow("当前修正", "无"));
            return rows;
        }

        for (var i = 0; i < factors.Count; i++)
        {
            var factor = factors[i];
            if (!factor.IsValid)
            {
                continue;
            }

            rows.Add(new DetailSidebarRow(factor.Label, FormatSigned(factor.Value), factor.Value, true));
        }

        return rows;
    }

    private void RebuildDetailSidebar(IReadOnlyList<DetailSidebarRow> rows)
    {
        ClearDetailSidebarRoot();
        if (root_详情侧边栏内容 == null || prefab_详情侧边栏文本 == null)
        {
            return;
        }

        if (rows == null || rows.Count == 0)
        {
            AddDetailSidebarText(new DetailSidebarRow("暂无详情", string.Empty));
            return;
        }

        var hasAnyRow = false;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!row.IsValid)
            {
                continue;
            }

            AddDetailSidebarText(row);
            hasAnyRow = true;
        }

        if (!hasAnyRow)
        {
            AddDetailSidebarText(new DetailSidebarRow("暂无详情", string.Empty));
        }
    }

    private void AddDetailSidebarText(DetailSidebarRow row)
    {
        if (prefab_详情侧边栏文本 == null || root_详情侧边栏内容 == null)
        {
            return;
        }

        var item = Instantiate(prefab_详情侧边栏文本, root_详情侧边栏内容);
        item.SetActive(true);
        activeSidebarItems.Add(item);

        var texts = item.GetComponentsInChildren<TMP_Text>(true);
        if (texts == null || texts.Length == 0)
        {
            return;
        }

        if (texts.Length == 1)
        {
            texts[0].text = FormatSidebarRow(row);
            if (row.HasSignedValue)
            {
                texts[0].color = ResolveSignedColor(row.SignedValue, texts[0].color);
            }

            return;
        }

        texts[0].text = row.Label ?? string.Empty;
        texts[1].text = row.Value ?? string.Empty;
        if (row.HasSignedValue)
        {
            texts[1].color = ResolveSignedColor(row.SignedValue, texts[1].color);
        }
    }

    private void ClearDetailSidebarRoot()
    {
        for (var i = 0; i < activeSidebarItems.Count; i++)
        {
            DestroyIfNeeded(activeSidebarItems[i]);
        }

        activeSidebarItems.Clear();
    }

    private void AddTitle(string text)
    {
        GameObject item = InstantiateContent(prefab_标题);
        SetText(GetText(item), text);
    }

    private void AddContentText(string text)
    {
        GameObject item = InstantiateContent(prefab_内容文本);
        SetText(GetText(item), text);
    }

    private GameObject InstantiateContent(GameObject prefab)
    {
        if (prefab == null || root_内容栏 == null)
        {
            return null;
        }

        GameObject item = Instantiate(prefab, root_内容栏);
        item.SetActive(true);
        activeContentItems.Add(item);
        return item;
    }

    private void ClearContentRoot()
    {
        for (int i = 0; i < activeContentItems.Count; i++)
        {
            DestroyIfNeeded(activeContentItems[i]);
        }

        activeContentItems.Clear();
    }

    private void SetSubsidyVisible(bool visible)
    {
        SetActive(go_补贴栏标题, visible);
        SetActive(go_补贴栏内容, visible);
    }

    private void SetDetailSidebarVisible(bool visible)
    {
        var target = go_详情侧边栏 != null
            ? go_详情侧边栏
            : (root_详情侧边栏内容 == null ? null : root_详情侧边栏内容.gameObject);
        if (target == null)
        {
            return;
        }

        SetActive(target, visible);
    }

    private void SetIcon(Sprite sprite)
    {
        if (img_建筑图标 == null)
        {
            return;
        }

        img_建筑图标.sprite = sprite;
        img_建筑图标.enabled = sprite != null;
    }

    private static string BuildDescriptionText(BuildingBase targetBuilding, BuildingStatusDisplayData displayData)
    {
        if (!string.IsNullOrWhiteSpace(displayData.BaseInfoText))
        {
            return displayData.BaseInfoText;
        }

        if (!string.IsNullOrWhiteSpace(displayData.StatusInfoText))
        {
            return $"状态：{displayData.StatusInfoText}";
        }

        return targetBuilding.Definition == null ? string.Empty : targetBuilding.Definition.BuildingId;
    }

    private static string FormatRow(BuildingDetailRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Label))
        {
            return row.Value;
        }

        if (string.IsNullOrWhiteSpace(row.Value))
        {
            return row.Label;
        }

        return $"{row.Label}：{row.Value}";
    }

    private static string FormatSigned(float value)
    {
        return value.ToString("+0.##;-0.##;0");
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.##");
    }

    private static string FormatSidebarRow(DetailSidebarRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Label))
        {
            return row.Value ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(row.Value))
        {
            return row.Label;
        }

        return $"{row.Label}：{row.Value}";
    }

    private static Color ResolveSignedColor(float value, Color fallback)
    {
        if (value > 0f)
        {
            return new Color(0.25f, 0.75f, 0.35f, fallback.a);
        }

        if (value < 0f)
        {
            return new Color(0.9f, 0.25f, 0.25f, fallback.a);
        }

        return fallback;
    }

    private static TMP_Text GetText(GameObject item)
    {
        return item == null ? null : item.GetComponentInChildren<TMP_Text>(true);
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
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static void DestroyIfNeeded(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private enum DetailSidebarContentKind
    {
        None,
        Workforce
    }

    private readonly struct DetailSidebarRow
    {
        public DetailSidebarRow(string label, string value)
            : this(label, value, 0f, false)
        {
        }

        public DetailSidebarRow(string label, string value, float signedValue, bool hasSignedValue)
        {
            Label = label;
            Value = value;
            SignedValue = signedValue;
            HasSignedValue = hasSignedValue;
        }

        public string Label { get; }

        public string Value { get; }

        public float SignedValue { get; }

        public bool HasSignedValue { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Label) || !string.IsNullOrWhiteSpace(Value);
    }
}
