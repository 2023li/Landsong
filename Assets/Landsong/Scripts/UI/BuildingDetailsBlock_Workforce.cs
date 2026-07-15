using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BuildingDetailsBlock_Workforce : BuildingDetailsBlockBase,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField, Required, LabelText("自动补贴满岗位开关")] private Toggle tgl_自动补贴满岗位;
    [SerializeField, Required, LabelText("目标稳定工人上一档按钮")] private Button btn_目标稳定工人上一档;
    [SerializeField, Required, LabelText("当前工人文本")] private TMP_Text txt_当前工人;
    [SerializeField, Required, LabelText("目标稳定工人下一档按钮")] private Button btn_目标稳定工人下一档;
    [SerializeField, Required, LabelText("当前补贴金币文本")] private TMP_Text txt_当前补贴金币;
    [SerializeField, Required, LabelText("招募工人按钮")] private Button btn_招募工人;
    [SerializeField, Required, LabelText("招募消耗文本")] private TMP_Text txt_招募消耗;

    private Popup_BuildingDetails owner;
    private IBuildingWorkforceFundingSource workforceFundingSource;
    private bool suppressWorkforceControlCallback;
    private bool listenersBound;

    public override bool ValidateConfiguration(out string error)
    {
        var missing = new List<string>();
        AddMissingReference(missing, tgl_自动补贴满岗位, nameof(tgl_自动补贴满岗位));
        AddMissingReference(missing, btn_目标稳定工人上一档, nameof(btn_目标稳定工人上一档));
        AddMissingReference(missing, txt_当前工人, nameof(txt_当前工人));
        AddMissingReference(missing, btn_目标稳定工人下一档, nameof(btn_目标稳定工人下一档));
        AddMissingReference(missing, txt_当前补贴金币, nameof(txt_当前补贴金币));
        AddMissingReference(missing, btn_招募工人, nameof(btn_招募工人));
        AddMissingReference(missing, txt_招募消耗, nameof(txt_招募消耗));
        return BuildValidationResult(missing, out error);
    }

    public override bool CanShow(BuildingBase targetBuilding)
    {
        return BuildingWorkforceUtility.TryGetSource(targetBuilding, out _);
    }

    public override void Initialize(
        Popup_BuildingDetails detailOwner)
    {
        owner = detailOwner;
        BindControlListeners();
    }

    public override void Bind(BuildingBase targetBuilding)
    {
        if (!BuildingWorkforceUtility.TryGetSource(targetBuilding, out workforceFundingSource))
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

        if (owner != null && owner.IsDetailSidebarOwner(this))
        {
            ShowWorkerDetailSidebar();
        }
    }

    public override void Unbind()
    {
        workforceFundingSource = null;
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

    private void HandleAutoSubsidyChanged(bool enabled)
    {
        if (suppressWorkforceControlCallback || workforceFundingSource == null)
        {
            return;
        }

        workforceFundingSource.SetAutoFullWorkerSubsidyEnabled(enabled);
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
            Refresh();
            return;
        }

        if (!workforceFundingSource.TryRecruitToFull())
        {
            Refresh();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
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

    public void OnPointerExit(PointerEventData eventData)
    {
        HideWorkerDetailSidebar();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsTouchPointer(eventData) || workforceFundingSource == null || !isActiveAndEnabled)
        {
            return;
        }

        ShowWorkerDetailSidebar();
    }

    public void OnPointerUp(PointerEventData eventData)
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

        owner.ShowDetailSidebar(this, BuildWorkforceSidebarRows(workforceFundingSource));
    }

    private void HideWorkerDetailSidebar()
    {
        if (owner != null)
        {
            owner.HideDetailSidebar(this);
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
