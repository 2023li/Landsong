using Landsong;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.UI;

public class GamePanel_Pause : MonoBehaviour
{
    [SerializeField] private Button btn_设置;

    //覆盖当前存档
    [SerializeField] private Button btn_快速保存;

    //打开存档面板
    [SerializeField] private Button btn_保存;

    [SerializeField] private Button btn_加载存档;
    [SerializeField] private Button btn_返回标题页;

    private UIPanel_Game panel_Game;
    //
    [SerializeField] private Button btn_返回游戏;

    public bool IsVisible => gameObject.activeSelf;



    private void Awake()
    {
        panel_Game = GetComponentInParent<UIPanel_Game>();

        RegisterButton(btn_设置, OpenSetting, nameof(btn_设置));
        RegisterButton(btn_快速保存, QuickSave, nameof(btn_快速保存));
        RegisterButton(btn_保存, OpenSaveConfirmation, nameof(btn_保存));
        RegisterButton(btn_加载存档, OpenLoadGame, nameof(btn_加载存档));
        RegisterButton(btn_返回标题页, ReturnToTitle, nameof(btn_返回标题页));
        RegisterButton(btn_返回游戏, ReturnToGame, nameof(btn_返回游戏));
        Hide();
    }

    private void OnDestroy()
    {
        UnregisterButton(btn_设置, OpenSetting);
        UnregisterButton(btn_快速保存, QuickSave);
        UnregisterButton(btn_保存, OpenSaveConfirmation);
        UnregisterButton(btn_加载存档, OpenLoadGame);
        UnregisterButton(btn_返回标题页, ReturnToTitle);
        UnregisterButton(btn_返回游戏, ReturnToGame);
    }

    private async void OpenSetting()
    {
        await UIManager.Instance.OpenAsync<UIPanel_Setting>();
    }

    private void QuickSave()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("快速保存失败：DataManager.Instance 为空。", this);
            return;
        }

        DataManager.Instance.QuickSaveGameData();
    }

    private async void OpenSaveConfirmation()
    {
        await UIManager.Instance.OpenAsync<UIPanel_SaveConfirmation>();
    }

    private async void OpenLoadGame()
    {
        await UIManager.Instance.OpenAsync<UIPanel_LoadGame>();
    }

    private void ReturnToTitle()
    {
        LoadScene_Start.Load();
    }

    private void ReturnToGame()
    {
        if (panel_Game == null)
        {
            panel_Game = GetComponentInParent<UIPanel_Game>();
        }

        panel_Game?.Hide_Pause();
    }

    private void RegisterButton(Button button, UnityEngine.Events.UnityAction action, string fieldName)
    {
        if (button == null)
        {
            Debug.LogError($"暂停面板配置错误：{fieldName} 未绑定。", this);
            return;
        }

        button.onClick.AddListener(action);
    }

    private static void UnregisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }


    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }
}
