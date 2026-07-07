using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BuildingDetailsBlock_Workforce : BuildingDetailsBlockBase
{
    [SerializeField, LabelText("自动补贴满岗位开关")] private Toggle tgl_自动补贴满岗位;
    [SerializeField, LabelText("目标稳定工人上一档按钮")] private Button btn_目标稳定工人上一档;
    [SerializeField, LabelText("当前工人文本")] private TMP_Text txt_当前工人;
    [SerializeField, LabelText("目标稳定工人下一档按钮")] private Button btn_目标稳定工人下一档;
    [SerializeField, LabelText("当前补贴金币文本")] private TMP_Text txt_当前补贴金币;
    [SerializeField, LabelText("招募工人按钮")] private Button btn_招募工人;
    [SerializeField, LabelText("招募消耗文本")] private TMP_Text txt_招募消耗;
    [SerializeField, LabelText("工人详情触发区")] private GameObject go_工人详情触发区;

    private Popup_BuildingDetails owner;
    private IBuildingWorkforceFundingSource workforceFundingSource;
    private bool suppressWorkforceControlCallback;
    private bool listenersBound;
    private bool workerDetailTriggerBound;
    private bool workerDetailSidebarVisible;

    public override bool CanShow(BuildingBase targetBuilding)
    {
        return targetBuilding is IBuildingWorkforceFundingSource;
    }

    public override void Initialize(
        Popup_BuildingDetails detailOwner)
    {
        owner = detailOwner;
        ResolveTargetStableWorkerStepControls();
        BindControlListeners();
        BindWorkerDetailTrigger();
    }

    public override void Bind(BuildingBase targetBuilding)
    {
        workforceFundingSource = targetBuilding as IBuildingWorkforceFundingSource;
        if (workforceFundingSource == null)
        {
            Unbind();
            return;
        }

        SetBlockVisible(true);
        Refresh();
    }

    public override void Refresh()
    {
        if (workforceFundingSource == null)
        {
            SetBlockVisible(false);
            return;
        }

        suppressWorkforceControlCallback = true;
        if (tgl_自动补贴满岗位 != null)
        {
            tgl_自动补贴满岗位.isOn = workforceFundingSource.AutoFullWorkerSubsidyEnabled;
        }

        suppressWorkforceControlCallback = false;

        RefreshTargetStableWorkerControls();
        RefreshWorkerCountText();
        SetText(
            txt_当前补贴金币,
            $"金币/回合 {workforceFundingSource.TargetSubsidyGoldPerTurn}");
        RefreshRecruitControl();

        if (workerDetailSidebarVisible)
        {
            ShowWorkerDetailSidebar();
        }
    }

    public override void Unbind()
    {
        workforceFundingSource = null;
        workerDetailSidebarVisible = false;
        SetText(txt_当前工人, string.Empty);
        SetText(txt_当前补贴金币, string.Empty);
        SetText(txt_招募消耗, string.Empty);
        SetTargetStableWorkerButtonsInteractable(false, false);

        if (btn_招募工人 != null)
        {
            btn_招募工人.interactable = false;
        }

        HideWorkerDetailSidebar();
        SetBlockVisible(false);
    }

    private void OnDestroy()
    {
        UnbindControlListeners();
    }

    private void BindControlListeners()
    {
        if (listenersBound)
        {
            return;
        }

        if (tgl_自动补贴满岗位 != null)
        {
            tgl_自动补贴满岗位.onValueChanged.AddListener(HandleAutoSubsidyChanged);
        }

        if (btn_目标稳定工人上一档 != null)
        {
            btn_目标稳定工人上一档.onClick.AddListener(HandlePreviousTargetStableWorkersClicked);
        }

        if (btn_目标稳定工人下一档 != null)
        {
            btn_目标稳定工人下一档.onClick.AddListener(HandleNextTargetStableWorkersClicked);
        }

        if (btn_招募工人 != null)
        {
            btn_招募工人.onClick.AddListener(HandleRecruitWorkerClicked);
        }

        listenersBound = true;
    }

    private void UnbindControlListeners()
    {
        if (!listenersBound)
        {
            return;
        }

        if (tgl_自动补贴满岗位 != null)
        {
            tgl_自动补贴满岗位.onValueChanged.RemoveListener(HandleAutoSubsidyChanged);
        }

        if (btn_目标稳定工人上一档 != null)
        {
            btn_目标稳定工人上一档.onClick.RemoveListener(HandlePreviousTargetStableWorkersClicked);
        }

        if (btn_目标稳定工人下一档 != null)
        {
            btn_目标稳定工人下一档.onClick.RemoveListener(HandleNextTargetStableWorkersClicked);
        }

        if (btn_招募工人 != null)
        {
            btn_招募工人.onClick.RemoveListener(HandleRecruitWorkerClicked);
        }

        listenersBound = false;
    }

    private void RefreshRecruitControl()
    {
        if (workforceFundingSource == null)
        {
            SetText(txt_招募消耗, string.Empty);
            if (btn_招募工人 != null)
            {
                btn_招募工人.interactable = false;
            }

            return;
        }

        var recruitCost = workforceFundingSource.RecruitToFullCost;

        if (btn_招募工人 != null)
        {
            btn_招募工人.interactable = workforceFundingSource.CanRecruitToFull;
        }

        SetText(txt_招募消耗, $"消耗{recruitCost}金币 招募1名工人");
    }

    private void ResolveTargetStableWorkerStepControls()
    {
        if (btn_目标稳定工人上一档 == null)
        {
            btn_目标稳定工人上一档 =
                FindButtonByNameOrText(transform, "上一档")
                ?? FindButtonByNameOrText(transform, "btn_上")
                ?? FindButtonByNameOrText(transform, "上");
        }

        if (btn_目标稳定工人下一档 == null)
        {
            btn_目标稳定工人下一档 =
                FindButtonByNameOrText(transform, "下一档")
                ?? FindButtonByNameOrText(transform, "btn_下")
                ?? FindButtonByNameOrText(transform, "下");
        }
    }

    private static Button FindButtonByNameOrText(Transform root, string keyword)
    {
        if (root == null || string.IsNullOrWhiteSpace(keyword))
        {
            return null;
        }

        var buttons = root.GetComponentsInChildren<Button>(true);
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (button.name.Contains(keyword))
            {
                return button;
            }

            var texts = button.GetComponentsInChildren<TMP_Text>(true);
            for (var j = 0; j < texts.Length; j++)
            {
                if (texts[j] != null && texts[j].text.Contains(keyword))
                {
                    return button;
                }
            }
        }

        return null;
    }

    private void HandleAutoSubsidyChanged(bool enabled)
    {
        if (suppressWorkforceControlCallback || workforceFundingSource == null)
        {
            return;
        }

        workforceFundingSource.SetAutoFullWorkerSubsidyEnabled(enabled);
        RefreshOwner();
    }

    private void HandlePreviousTargetStableWorkersClicked()
    {
        AdjustTargetStableWorkers(-1);
    }

    private void HandleNextTargetStableWorkersClicked()
    {
        AdjustTargetStableWorkers(1);
    }

    private void AdjustTargetStableWorkers(int delta)
    {
        if (suppressWorkforceControlCallback || workforceFundingSource == null)
        {
            return;
        }

        var minTarget = CalculateNaturalStableWorkers(workforceFundingSource);
        var currentTarget = Mathf.Clamp(
            workforceFundingSource.TargetStableWorkers,
            minTarget,
            workforceFundingSource.MaxWorkers);
        var nextTarget = Mathf.Clamp(currentTarget + delta, minTarget, workforceFundingSource.MaxWorkers);
        if (nextTarget == workforceFundingSource.TargetStableWorkers)
        {
            RefreshTargetStableWorkerControls();
            return;
        }

        workforceFundingSource.SetTargetStableWorkers(nextTarget);
        RefreshOwner();
    }

    private void RefreshTargetStableWorkerControls()
    {
        if (workforceFundingSource == null)
        {
            SetTargetStableWorkerButtonsInteractable(false, false);
            return;
        }

        var minTarget = CalculateNaturalStableWorkers(workforceFundingSource);
        var targetStableWorkers = Mathf.Clamp(
            workforceFundingSource.TargetStableWorkers,
            minTarget,
            workforceFundingSource.MaxWorkers);
        var canAdjust = !workforceFundingSource.AutoFullWorkerSubsidyEnabled;

        SetTargetStableWorkerButtonsInteractable(
            canAdjust && targetStableWorkers > minTarget,
            canAdjust && targetStableWorkers < workforceFundingSource.MaxWorkers);
    }

    private void RefreshWorkerCountText()
    {
        if (workforceFundingSource == null)
        {
            SetText(txt_当前工人, string.Empty);
            return;
        }

        SetText(
            txt_当前工人,
            $"工人：{workforceFundingSource.CurrentWorkers}/{workforceFundingSource.MaxWorkers}（稳定：{workforceFundingSource.StableWorkers}）");
    }

    private void SetTargetStableWorkerButtonsInteractable(bool canGoPrevious, bool canGoNext)
    {
        if (btn_目标稳定工人上一档 != null)
        {
            btn_目标稳定工人上一档.interactable = canGoPrevious;
        }

        if (btn_目标稳定工人下一档 != null)
        {
            btn_目标稳定工人下一档.interactable = canGoNext;
        }
    }

    private static int CalculateNaturalStableWorkers(IBuildingWorkforceFundingSource source)
    {
        return source == null
            ? 0
            : BuildingJobSystem.CalculateStableWorkers(source.MaxWorkers, source.JobAttractionWithoutSubsidy);
    }

    private void HandleRecruitWorkerClicked()
    {
        if (workforceFundingSource == null)
        {
            return;
        }

        if (!workforceFundingSource.CanRecruitToFull)
        {
            RefreshOwner();
            return;
        }

        workforceFundingSource.TryRecruitToFull();
        RefreshOwner();
    }

    private void BindWorkerDetailTrigger()
    {
        if (workerDetailTriggerBound)
        {
            return;
        }

        var triggerObject = go_工人详情触发区 == null ? gameObject : go_工人详情触发区;
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
        AddEventTrigger(eventTrigger, EventTriggerType.PointerDown, HandleWorkerDetailPointerDown);
        AddEventTrigger(eventTrigger, EventTriggerType.PointerUp, HandleWorkerDetailPointerUp);
        workerDetailTriggerBound = true;
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
        if (IsTouchPointer(eventData) || workforceFundingSource == null)
        {
            return;
        }

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
        HideWorkerDetailSidebar();
    }

    private void HandleWorkerDetailPointerDown(BaseEventData eventData)
    {
        if (!IsTouchPointer(eventData) || workforceFundingSource == null || !isActiveAndEnabled)
        {
            return;
        }

        ShowWorkerDetailSidebar();
    }

    private void HandleWorkerDetailPointerUp(BaseEventData eventData)
    {
        if (!IsTouchPointer(eventData))
        {
            return;
        }

        HideWorkerDetailSidebar();
    }

    private void ShowWorkerDetailSidebar()
    {
        if (workforceFundingSource == null || owner == null)
        {
            return;
        }

        workerDetailSidebarVisible = true;
        owner.ShowDetailSidebar(BuildWorkforceSidebarRows(workforceFundingSource));
    }

    private void HideWorkerDetailSidebar()
    {
        workerDetailSidebarVisible = false;
        if (owner != null)
        {
            owner.HideDetailSidebar();
        }
    }

    private static List<BuildingDetailsSidebarRow> BuildWorkforceSidebarRows(IBuildingWorkforceFundingSource source)
    {
        var rows = new List<BuildingDetailsSidebarRow>
        {
            new BuildingDetailsSidebarRow("满岗位需要的最少就业吸引力", FormatNumber(source.FullWorkerRequiredAttraction)),
            new BuildingDetailsSidebarRow("当前就业吸引力", FormatNumber(source.JobAttraction)),
            new BuildingDetailsSidebarRow("满岗位吸引力差值", FormatNumber(source.JobAttractionGapToFullWorkers)),
            new BuildingDetailsSidebarRow("就业吸引力影响因素", string.Empty)
        };

        IReadOnlyList<BuildingWorkforceAttractionFactor> factors = source.WorkforceAttractionFactors;
        if (factors == null || factors.Count == 0)
        {
            rows.Add(new BuildingDetailsSidebarRow("当前修正", "无"));
            return rows;
        }

        for (var i = 0; i < factors.Count; i++)
        {
            var factor = factors[i];
            if (!factor.IsValid)
            {
                continue;
            }

            rows.Add(new BuildingDetailsSidebarRow(factor.Label, FormatSigned(factor.Value), factor.Value, true));
        }

        return rows;
    }

    private void SetBlockVisible(bool visible)
    {
        SetActive(gameObject, visible);
    }

    private void RefreshOwner()
    {
        if (owner != null)
        {
            owner.RefreshCurrentBuildingDetails();
            return;
        }

        Refresh();
    }

    private static string FormatSigned(float value)
    {
        return value.ToString("+0.##;-0.##;0");
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.##");
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

}

public readonly struct BuildingDetailsSidebarRow
{
    public BuildingDetailsSidebarRow(string label, string value)
        : this(label, value, 0f, false)
    {
    }

    public BuildingDetailsSidebarRow(string label, string value, float signedValue, bool hasSignedValue)
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
