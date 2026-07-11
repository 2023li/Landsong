using System.Threading.Tasks;
using Moyo.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanel_SaveConfirmation : UIPanelBase
{

    //需要一个默认名称 王朝名称-回合数
    [SerializeField] private TMP_InputField ipt_存档名称;

    //点击确认存档 需要校验存档名不能为空
    [SerializeField] private Button btn_确认;

    [SerializeField] private Button btn_取消;

    private bool initialized;

    public override Task OnCreateAsync()
    {
        Initialize();
        return base.OnCreateAsync();
    }

    public override Task OnOpenAsync(object args)
    {
        Initialize();
        RefreshDefaultSaveName();
        return base.OnOpenAsync(args);
    }

    public override Task OnReleaseAsync()
    {
        UnregisterListeners();
        initialized = false;
        return base.OnReleaseAsync();
    }

    private void OnDestroy()
    {
        UnregisterListeners();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        if (btn_确认 != null)
        {
            btn_确认.onClick.AddListener(ConfirmSave);
        }
        else
        {
            Debug.LogError("保存确认面板配置错误：btn_确认 未绑定。", this);
        }

        if (btn_取消 != null)
        {
            btn_取消.onClick.AddListener(CancelSave);
        }
        else
        {
            Debug.LogError("保存确认面板配置错误：btn_取消 未绑定。", this);
        }

        if (ipt_存档名称 == null)
        {
            Debug.LogError("保存确认面板配置错误：ipt_存档名称 未绑定。", this);
        }
    }

    private void UnregisterListeners()
    {
        if (btn_确认 != null)
        {
            btn_确认.onClick.RemoveListener(ConfirmSave);
        }

        if (btn_取消 != null)
        {
            btn_取消.onClick.RemoveListener(CancelSave);
        }
    }

    private void RefreshDefaultSaveName()
    {
        if (ipt_存档名称 == null)
        {
            return;
        }

        ipt_存档名称.text = DataManager.Instance == null
            ? string.Empty
            : DataManager.Instance.GetDefaultCurrentGameSaveName();
    }

    private async void ConfirmSave()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("保存失败：DataManager.Instance 为空。", this);
            return;
        }

        string saveName = ipt_存档名称 == null ? string.Empty : ipt_存档名称.text.Trim();
        if (string.IsNullOrEmpty(saveName))
        {
            Debug.LogWarning("保存失败：存档名称不能为空。", this);
            ipt_存档名称?.Select();
            ipt_存档名称?.ActivateInputField();
            return;
        }

        if (!DataManager.Instance.SetCurrentGameSaveName(saveName))
        {
            return;
        }

        DataManager.Instance.SaveCurrentGame(GameDataSaveMode.NewSave);
        await UIManager.Instance.BackAsync();
    }

    private async void CancelSave()
    {
        await UIManager.Instance.BackAsync();
    }

}
